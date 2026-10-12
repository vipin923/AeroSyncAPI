using System.Linq.Expressions;

namespace AeroSyncAPI.Models
{
    public class Users
    {
        public Guid UserId { get; set; } = new Guid();
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string EmailHash { get; set; } = string.Empty;

    }
}
