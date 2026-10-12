using AeroSyncAPI.DataAccessLayer;
using AeroSyncAPI.Models;
using AeroSyncAPI.Security;
using AeroSyncAPI.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace AeroSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LayoverController : Controller
    {
        private readonly SmartMatcherService _matcherService;
        private readonly ApplicationDbContext _context;
        private readonly AesEncryptionService _aesEncryptionService;
        public LayoverController(SmartMatcherService matcherService, ApplicationDbContext context, AesEncryptionService aesEncryptionService)
        {
            _matcherService = matcherService;
            _context = context;
            _aesEncryptionService = aesEncryptionService;
        }

        [HttpPost("find-matches")]
        public async Task<IActionResult> FindMatches([FromBody] PassengerContext currentUser)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var parameters = new DynamicParameters();

                    parameters.Add("@Terminal", currentUser.Terminal);
                    parameters.Add("@Date", DateTime.Now.ToString("MMM dd, yyyy"));
                    var result = await connection.QueryAsync<PassengerContext>("sp_GetAeroSyncMatchData", parameters, commandType: System.Data.CommandType.StoredProcedure);
                    var activeAirportUsers = new List<PassengerContext>();
                    foreach (var flight in result)
                    {
                        // 🚀 DUMMY DATA: Simulating other people currently in the airport database
                        activeAirportUsers = new List<PassengerContext>
            {
                new PassengerContext {  UserName =  _aesEncryptionService.DecryptName(flight.UserName), MinutesAvailable = CalculateMinutesAvailable(flight.DepartureTime), InterestsText = flight.Tags },
            };
                    }
                    currentUser.MinutesAvailable = CalculateMinutesAvailable(currentUser.DepartureTime);
                    var matches = _matcherService.FindBestLayoverBuddies(currentUser, activeAirportUsers);
                    
                    return Ok(new ApiResponse { Success = true, Data = matches });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse { Success = false, Error = ex.Message });
            }
        }
        [HttpGet("get-user-match-data/{Email}")]
        public async Task<ActionResult<PassengerContext>> GetMatchUserData(string Email)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {

                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var result = await connection.QueryFirstOrDefaultAsync<PassengerContext>("sp_GetAeroSyncUserMatchData", new { @EmailHash = GenerateEmailHash(Email),@Date = DateTime.Now.ToString("MMM dd, yyyy") }, commandType: System.Data.CommandType.StoredProcedure);
                    if (result != null)
                    {


                        result.UserName = _aesEncryptionService.DecryptName(result.UserName);
                        return Ok(result);
                    }
                }
                return NotFound("Data not found.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving user info: {ex.Message}");
            }
        }
        public int CalculateMinutesAvailable(string timeString)
        {
            // 1. Try to parse the string ("08:30 AM") into a real DateTime object
            if (DateTime.TryParse(timeString, out DateTime departureTime))
            {
                DateTime currentTime = DateTime.Now;

                // 2. OVERNIGHT LAYOVER CHECK: 
                // If the parsed departure time is in the past, it means the flight is tomorrow morning!
                if (departureTime < currentTime)
                {
                    departureTime = departureTime.AddDays(1);
                }

                // 3. Subtract the current time from the departure time
                TimeSpan timeRemaining = departureTime - currentTime;

                // 4. Return the total minutes as a whole number
                return (int)timeRemaining.TotalMinutes;
            }

            // Fallback if the OCR gave us garbage text instead of a real time
            return 0;
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
