using AeroSyncAPI.DataAccessLayer;
using AeroSyncAPI.Models;
using AeroSyncAPI.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AeroSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IntentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SmartIntentService _smartIntentService;
        private readonly SmartItineraryService _itineraryService;

        public IntentController(SmartIntentService smartIntentService, SmartItineraryService itineraryService, ApplicationDbContext context)
        {
            _smartIntentService = smartIntentService;
            _itineraryService = itineraryService;
            _context = context;
        }
        [HttpPost("intent-analyze")]
        public async Task<ActionResult<string>> AnalyzeMessage([FromBody] Intent intent)
        {
            try
            {
                // 1. Get the raw response from your SmartIntentService / Phi-3 engine
                string rawAiOutput = await _smartIntentService.ParseIntentAsync(intent.Message);

                // 🚀 THE CONTROLLER SANITIZER: Clean up the text BEFORE wrapping it in Ok()
                if (!string.IsNullOrWhiteSpace(rawAiOutput))
                {
                    // Strip out system tokens if they are hanging around the edges
                    if (rawAiOutput.Contains("<|end|>"))
                    {
                        rawAiOutput = rawAiOutput.Replace("<|end|>", "").Trim();
                    }

                    // Safe boundary slice to extract ONLY the exact JSON content structure
                    int startIndex = rawAiOutput.IndexOf('{');
                    int endIndex = rawAiOutput.LastIndexOf('}');

                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        rawAiOutput = rawAiOutput.Substring(startIndex, endIndex - startIndex + 1);
                    }
                }

                // 2. Return the completely pristine string safely packed inside your wrapper
                return Ok(new ApiResponse { Success = true, Data = rawAiOutput });
            }
            catch (Exception ex)
            {
                // If the backend fails, it returns a safe JSON error object instead of a messy HTML crash page!
                return StatusCode(500, new ApiResponse { Success = false, Data = "", Error = ex.Message });
            }
        }


        [HttpPost("extract-from-image")]
        // RequestSizeLimit prevents users from uploading massive 50MB files and crashing your server
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> ExtractFromImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No image file was provided.");

            // Basic validation to ensure it's actually an image
            if (!file.ContentType.StartsWith("image/"))
                return BadRequest("Invalid file type. Please upload an image.");

            try
            {
                using var stream = file.OpenReadStream();

                // Pass the image stream into our pipeline
                var flightData = await _itineraryService.ProcessScreenshotAsync(stream);

                if (flightData == null)
                    return NotFound("Could not extract flight details from the image.");

                return Ok(new { Success = true, Data = flightData });
            }
            catch (Exception ex)
            {
                // In production, log this error instead of returning the raw message!
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
        [HttpPost("extract-from-text")]
        public async Task<IActionResult> ExtractItineraryFromText([FromBody] TextUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new ApiResponse { Success = false, Error = "No text was provided." });
            }

            try
            {
                // Skip OCR entirely! Send the pasted text directly to your AI engine
                // (Assuming you have a ParseItineraryAsync or similar method in your service)
                string rawAiOutput = await _smartIntentService.ParseItineraryAsync(request.Text);

                // 🚀 Re-use our bulletproof JSON sanitizer
                if (rawAiOutput.Contains("<|assistant|>")) rawAiOutput = rawAiOutput.Split("<|assistant|>")[0];
                if (rawAiOutput.Contains("<|end|>")) rawAiOutput = rawAiOutput.Split("<|end|>")[0];

                int startIndex = rawAiOutput.IndexOf('{');
                int endIndex = rawAiOutput.LastIndexOf('}');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string cleanJson = rawAiOutput.Substring(startIndex, endIndex - startIndex + 1);

                    // Parse it to ensure it's valid, then return it!
                    var itinerary = JsonSerializer.Deserialize<FlightItinerary>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return Ok(new ApiResponse { Success = true, Data = itinerary });
                }

                throw new Exception("AI failed to extract valid flight details.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
            }
        }
        [HttpPost("save-itinerary")]
        public async Task<ActionResult<int>> SaveItinerary(FlightItinerary flightItinerary)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var parameters = new DynamicParameters();

                    parameters.Add("@EmailHash", GenerateEmailHash(flightItinerary.EmailHash));
                    parameters.Add("@DepartureTerminal", flightItinerary.DepartureTerminal);
                    parameters.Add("@Gate", flightItinerary.Gate);
                    parameters.Add("@DepartureTime", flightItinerary.DepartureTime);
                    parameters.Add("@Date", flightItinerary.Date);
                    var count = await connection.ExecuteAsync("sp_SaveAeroSyncIternary", parameters, commandType: System.Data.CommandType.StoredProcedure);

                    return Ok(count);

                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving post: {ex.Message}");
            }
        }
        [HttpGet("get-itinerary/{EmailHash}/{Date}")]
        public async Task<ActionResult<FlightItinerary>> GetItinerary(string EmailHash, string
            Date)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var parameters = new DynamicParameters();

                    parameters.Add("@EmailHash",GenerateEmailHash(EmailHash));
                    parameters.Add("@Date", Date);
                    var result = await connection.QueryFirstOrDefaultAsync<FlightItinerary>("sp_GetAeroSyncIternary", parameters, commandType: System.Data.CommandType.StoredProcedure);
                    if(result == null)
                    {
                        return NotFound();
                    }
                    return Ok(result);
                }

            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving post: {ex.Message}");
            }
        }
        public static string GenerateEmailHash(string rawEmail)
        {
            // 1. Normalize the input to prevent case-sensitivity lockouts
            string normalizedEmail = rawEmail.Trim().ToLowerInvariant();

            // 2. Generate the deterministic hash
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(normalizedEmail);
                byte[] hash = sha256.ComputeHash(bytes);

                // 3. Return as a safe string for SQL
                return Convert.ToBase64String(hash);
            }
        }
    }
}
