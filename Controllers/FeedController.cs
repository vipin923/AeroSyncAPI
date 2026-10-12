using AeroSyncAPI.DataAccessLayer;
using AeroSyncAPI.Hubs;
using AeroSyncAPI.Models;
using AeroSyncAPI.Security;
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
    public class FeedController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AesEncryptionService _aesEncryptionService;
        private readonly IHubContext<TerminalHub> _hubContext;
        public FeedController(ApplicationDbContext context, AesEncryptionService aesEncryptionService, IHubContext<TerminalHub> hubContext)
        {
            _context = context;
            _aesEncryptionService = aesEncryptionService;
            _hubContext = hubContext;
        }
       

     
        [HttpPost("save-post")]
        public async Task<int> SavePost([FromBody] FeedModels feed)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var parameters = new DynamicParameters();
                    parameters.Add("@FeedId", Guid.NewGuid());
                    parameters.Add("@EmailHash", GenerateEmailHash(feed.EmailHash));
                    parameters.Add("@Location", feed.Location);
                    parameters.Add("@Content", feed.Content);
                    parameters.Add("@TimeStamp", feed.TimeStamp);
                    parameters.Add("@Tags", JsonSerializer.Serialize(feed.Tags));
                    var count = await connection.ExecuteAsync("sp_SaveAeroSyncPostFeed", parameters, commandType: System.Data.CommandType.StoredProcedure);
                    await _hubContext.Clients.All.SendAsync("ReceiveNewPost", feed);
                    return count;
                   
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving post: {ex.Message}");
            }
        }
        [HttpPost("save-comments")]
        public async Task<int> SaveComments([FromBody] CommentModel comment)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var parameters = new DynamicParameters();
                    parameters.Add("@CommentId", Guid.NewGuid());
                    parameters.Add("@FeedId", comment.FeedId);
                    parameters.Add("@EmailHash", GenerateEmailHash(comment.EmailHash));
                    parameters.Add("@Text", comment.Text);
                    parameters.Add("@TimeStamp", comment.TimeStamp);
                    var count = await connection.ExecuteAsync("sp_SaveAeroSyncComments", parameters, commandType: System.Data.CommandType.StoredProcedure);
                    await _hubContext.Clients.All.SendAsync("ReceiveNewComment", comment);
                    return count;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving comment: {ex.Message}");
            }
        }
        [HttpGet("get-feed-user-info/{EmailHash}")]
        public async Task<ActionResult<Users>> GetUserInfo(string EmailHash)
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                   
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var result = await connection.QueryFirstOrDefaultAsync<Users>("sp_GetFeedUserInfo", new { @EmailHash = EmailHash }, commandType: System.Data.CommandType.StoredProcedure);
                    if (result != null)
                    {

                        
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
        [HttpGet("get-posts")]
        public async Task<ActionResult<List<FeedModels>>> GetPosts()
        {
            try
            {
                using (DbConnection connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();
                    var posts = await connection.QueryAsync<FeedModels>("sp_GetAeroSyncPost", commandType: System.Data.CommandType.StoredProcedure);
                    return Ok(posts.ToList());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving posts: {ex.Message}");
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
