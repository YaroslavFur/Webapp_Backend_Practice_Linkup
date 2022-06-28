using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Server.Data;
using Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Server.Operators
{
    public class TokenOperator
    {
        public static async Task<TokenModel> GenerateAccessRefreshTokens(
            SessionModel session, IConfiguration configuration, AppDbContext db, UserManager<UserModel> userManager, UserModel? user = null)
        {
            _ = long.TryParse(configuration["JWT:AccessTokenValidityInSeconds"], out long tokenValidityInSeconds);
            _ = long.TryParse(configuration["JWT:RefreshTokenValidityInSeconds"], out long refreshTokenValidityInSeconds);

            var authClaims = new List<Claim>();
            if (user == null)
                authClaims.Add(new Claim(ClaimTypes.Anonymous, session.Id.ToString()));
            else
            {

                authClaims.Add(new Claim(ClaimTypes.Email, user.UserName));
                var userRoles = await userManager.GetRolesAsync(user);
                foreach (var role in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
            authClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            
            var token = GenerateToken(authClaims, tokenValidityInSeconds, configuration);
            var refreshToken = GenerateToken(new List<Claim>(), refreshTokenValidityInSeconds, configuration);

            session.RefreshToken = refreshToken;
            db.SaveChanges();

            return new TokenModel { AccessToken = token, RefreshToken = refreshToken };
        }

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
