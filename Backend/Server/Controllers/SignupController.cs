using Server.Models;
using Server.Operators;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Server.Controllers
{
    [Route("signup")]
    [ApiController]
    public class SignupController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly IConfiguration _configuration;

        public SignupController(
            UserManager<UserModel> userManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Signup([FromBody] SignupModel model)
        {
            var userExists = await _userManager.FindByNameAsync(model.Email);
            if (userExists != null)
                return StatusCode(StatusCodes.Status409Conflict, new { Status = "Error", Message = "User already exists" });

            EmailAddressAttribute emailValidator = new();
            if (!emailValidator.IsValid(model.Email))
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Invalid email" });

            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Surname))
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Name and surname can't be empty" });

            UserModel user = UserOperator.CreateUser(model);

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = result.ToString() });

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
