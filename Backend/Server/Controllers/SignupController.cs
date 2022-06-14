using Server.Models;
using Server.Operators;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Server.Controllers
{
    [Route("signup")]
    [ApiController]
    public class SignupController : Controller
    {
        private readonly UserManager<UserModel> _userManager;

        public SignupController(
            UserManager<UserModel> userManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
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

            UserModel user = UserOperator.CreateUser(model);

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = result.ToString() });

            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Surname))
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Name and surname can't be empty" });

            return StatusCode(StatusCodes.Status201Created, new { Status = "Success", Message = "User created successfully!" });
        }
    }
}
