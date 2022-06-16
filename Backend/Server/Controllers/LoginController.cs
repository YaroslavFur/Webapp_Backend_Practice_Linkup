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

        public LoginController(
            UserManager<UserModel> userManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<ActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Email);
            if (user == null)
                return StatusCode(StatusCodes.Status401Unauthorized, new { Status = "Error", Message = "User not found" });
            if (!(await _userManager.CheckPasswordAsync(user, model.Password)))
                return StatusCode(StatusCodes.Status401Unauthorized, new { Status = "Error", Message = "Wrong email or password" });

            TokenModel tokens;
            try
            {
                tokens = await TokenOperator.GenerateAccessRefreshTokens(user, _configuration, _userManager);
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Failed creating tokens" });
            }

            return StatusCode(StatusCodes.Status201Created, new
            {
                Status = "Success",
                Token = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken
            });
        }
    }
}
