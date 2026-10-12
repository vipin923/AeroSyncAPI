using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1.Ocsp;
using Paseto;
using Paseto.Builder;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace AeroSyncAPI.Security
{
    public class PasetoAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly string _secretKey;

        // .NET 8 Modernization
        public PasetoAuthenticationHandler(
            IConfiguration config,
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
            _secretKey = config["PasetoSettings:SecretKey"]
                         ?? throw new ArgumentNullException("Secret Key missing in appsettings.json!");
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string token = null;

            // 1. STANDARD HTTP: Check for Authorization header
            if (Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                }
            }
            // 2. SIGNALR WEBSOCKETS: Check for token in the Query String
            else if (Request.Query.ContainsKey("access_token"))
            {
                var path = Request.Path;
                if (path.StartsWithSegments("/chathub"))
                {
                    token = Request.Query["access_token"];
                }
            }

            // If we didn't find a token in either place, they are Anonymous
            if (string.IsNullOrEmpty(token))
                return AuthenticateResult.NoResult();

            try
            {
                // 3. Decode and Validate the PASETO Token (Your exact existing code!)
                var keyBytes = Convert.FromBase64String(_secretKey);

                var validationParams = new PasetoTokenValidationParameters
                {
                    ValidateLifetime = true,
                    ValidateAudience = false,
                    ValidateIssuer = false
                };

                var decoded = new PasetoBuilder()
                    .Use(ProtocolVersion.V4, Purpose.Local)
                    .WithKey(keyBytes, Encryption.SymmetricKey)
                    .Decode(token, validationParams);

                if (!decoded.IsValid)
                    return AuthenticateResult.Fail("PASETO token is invalid or expired.");

                var extractedUserId = decoded.Paseto.Payload["sub"].ToString();

                // 4. Extract claims and create the user identity
                var claims = new List<Claim>
                {
                    // Standard claims
                    new Claim(ClaimTypes.NameIdentifier, extractedUserId),
                    new Claim(ClaimTypes.Role, decoded.Paseto.Payload["role"].ToString()),
                    
                    // CRITICAL FIX FOR SIGNALR CHAT HUB:
                    // The ChatHub looks specifically for a claim named "UserId".
                    new Claim("Authority", extractedUserId)
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                return AuthenticateResult.Fail($"PASETO validation failed: {ex.Message}");
            }
        }
    }
}

