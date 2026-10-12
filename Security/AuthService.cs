using Paseto;
using Paseto.Builder;
using System.Text;

namespace AeroSyncAPI.Security
{
    public class AuthService
    {
        private readonly string _secretKey;

            // Inject IConfiguration here
            public AuthService(IConfiguration config)
            {
                // Pull the value using the Section:Key syntax
                _secretKey = config["PasetoSettings:SecretKey"]
                             ?? throw new ArgumentNullException("Paseto Secret Key is missing in appsettings.json!");
            }

            public string GeneratePasetoToken(Guid userId, string role)
            {
                // Safely convert the Base64 string from appsettings into actual bytes
                var keyBytes = Convert.FromBase64String(_secretKey);

                var token = new PasetoBuilder()
                    .Use(ProtocolVersion.V4, Purpose.Local)
                    .WithKey(keyBytes, Encryption.SymmetricKey)
                    .AddClaim("sub", userId)
                    .AddClaim("role", role)
                    .IssuedAt(DateTime.UtcNow)
                    .Expiration(DateTime.UtcNow.AddHours(2))
                    .Encode();

                return token;
            }
        }
    }

