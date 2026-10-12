using AeroSyncAPI.DataAccessLayer;
using AeroSyncAPI.Models;
using AeroSyncAPI.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace AeroSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly AuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly AesEncryptionService _aesEncryptionService;
        public AuthController(AuthService authService, ApplicationDbContext context, AesEncryptionService aesEncryptionService)
        {
            _authService = authService;
            _context = context;
            _aesEncryptionService = aesEncryptionService;
        }
        [HttpPost("register")]
        public async Task<ActionResult<int>> Registeration(Users user)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var parameters = new DynamicParameters();
                    parameters.Add("@UserId", Guid.NewGuid());
                    parameters.Add("@PassengerName", _aesEncryptionService.EncryptName(user.PassengerName));
                    parameters.Add("@Email", _aesEncryptionService.EncryptName(user.Email));
                    parameters.Add("@Password", BCrypt.Net.BCrypt.HashPassword(user.Password));
                    parameters.Add("@UserName", _aesEncryptionService.EncryptName(user.UserName));
                    parameters.Add("@EmailHash", GenerateEmailHash(user.Email));


                    var count = await connection.ExecuteAsync("sp_SaveAeroSyncUser", parameters, commandType: System.Data.CommandType.StoredProcedure);
                    return Ok(count);

                }
            }
            catch (SqlException ex)
            {
                // Check if this is our custom "Email Exists" error from the Stored Procedure
                if (ex.Number == 50001)
                {
                    // Return the exact string we defined in the SQL THROW statement
                    return BadRequest(ex.Message);
                }
                return StatusCode(500, new { success = false, message = $"SQL Error: {ex.Message}" });

                // If it is a different SQL error (like a timeout or connection drop), log it and return a generic message

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving merchant: {ex.Message}");

            }
        }
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(Users user)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    
                    var result = await connection.QueryFirstOrDefaultAsync<Users>("sp_LoginAeroSyncUser", new { @EmailHash =  GenerateEmailHash(user.Email) }, commandType: System.Data.CommandType.StoredProcedure);
                    if (result != null)
                    {

                        if (BCrypt.Net.BCrypt.Verify(user.Password, result.Password))
                        {
                            var token = _authService.GeneratePasetoToken(result.UserId, "UserId");
                            return Ok(token);
                        }
                    }
                }
                return Unauthorized("Invalid email or password.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error during login: {ex.Message}");
                
            }
        }
        [Authorize]
        [HttpGet("get-user-info/{Email}")]
        public async Task<ActionResult<Users>> GetUserInfo(string Email)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    var EmailHash = GenerateEmailHash(Email);
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var result = await connection.QueryFirstOrDefaultAsync<Users>("sp_GetAeroSyncUserInfo", new { @EmailHash = EmailHash }, commandType: System.Data.CommandType.StoredProcedure);
                    if (result != null)
                    {
                       
                        result.PassengerName = _aesEncryptionService.DecryptName(result.PassengerName);
                        result.UserName = _aesEncryptionService.DecryptName(result.UserName);
                        return Ok(result);
                    }
                }
                return NotFound("User not found.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving user info: {ex.Message}");
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
