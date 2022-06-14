using Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using Server.Operators;

namespace Server.Controllers
{
    [Route("login")]
    [ApiController]
    public class LoginController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly IConfiguration _configuration;
        private readonly JwtSecurityTokenHandler _jwtSecurityTokenHandler;

        public LoginController(
            UserManager<UserModel> userManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
            _jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        }

        [HttpPost]
        public async Task<ActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Email);
            if (user == null)
                return StatusCode(StatusCodes.Status401Unauthorized, new { Status = "Error", Message = "User not found" });
            if (!(await _userManager.CheckPasswordAsync(user, model.Password)))
                return StatusCode(StatusCodes.Status401Unauthorized, new { Status = "Error", Message = "Wrong email or password" });
            
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            _ = long.TryParse(_configuration["JWT:AccessTokenValidityInSeconds"], out long tokenValidityInSeconds);
            _ = long.TryParse(_configuration["JWT:RefreshTokenValidityInSeconds"], out long refreshTokenValidityInSeconds);

            var token = TokenOperator.GenerateToken(authClaims, tokenValidityInSeconds, _configuration);
            var refreshToken = TokenOperator.GenerateToken(new List<Claim>(), refreshTokenValidityInSeconds, _configuration);

            user.RefreshToken = refreshToken;

            await _userManager.UpdateAsync(user);

            return StatusCode(StatusCodes.Status200OK, new 
            { 
                Status = "Success", 
                Token = token,
                RefreshToken = refreshToken 
            });
        }
    }
}
