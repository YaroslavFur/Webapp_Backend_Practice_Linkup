using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Server.Operators
{
    public class TokenOperator
    {
        public static string GenerateToken(List<Claim> authClaims, long expirationTimeInSeconds, IConfiguration configuration)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: configuration["JWT:ValidIssuer"],
                audience: configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddSeconds(expirationTimeInSeconds),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static ClaimsPrincipal ValidateToken(string token, bool validateLifetime, IConfiguration configuration)
        {

            TokenValidationParameters parameters = GetTokenValidationParameters(configuration);
            parameters.ValidateLifetime = validateLifetime;

            ClaimsPrincipal principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out SecurityToken validatedToken);
            return principal;
        }

        public static TokenValidationParameters GetTokenValidationParameters(IConfiguration configuration)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]));

            return new TokenValidationParameters()
            {
                ClockSkew = TimeSpan.Zero,
                ValidateLifetime = true,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidAudience = configuration["JWT:ValidAudience"],
                ValidIssuer = configuration["JWT:ValidIssuer"],
                IssuerSigningKey = authSigningKey // The same key as the one that generate the token
            };
        }
    }
}
