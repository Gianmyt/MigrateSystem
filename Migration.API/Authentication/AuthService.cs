using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Migration.API.Authentication
{
    public interface IAuthService
    {
        Task<string?> AuthenticateAsync(string username, string password);
    }

    public class AuthService : IAuthService
    {
        private readonly JwtOptions _jwtOptions;

        // Simuliamo utenti in memoria (per demo)
        private readonly Dictionary<string, string> _users = new()
    {
        { "admin", "admin" },
        { "user", "password" }
    };

        public AuthService(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        public Task<string?> AuthenticateAsync(string username, string password)
        {
            if (!_users.TryGetValue(username, out var pwd) || pwd != password)
                return Task.FromResult<string?>(null);

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtOptions.Key);

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, username == "admin" ? "Administrator" : "User")
        };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
                Issuer = _jwtOptions.Issuer,
                Audience = _jwtOptions.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return Task.FromResult(tokenHandler.WriteToken(token));
        }
    }
}
