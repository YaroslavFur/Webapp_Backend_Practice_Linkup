using Server.Models;
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
        private readonly UserManager<IdentityUser> _userManager;

        public SignupController(
            UserManager<IdentityUser> userManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
        }

        [HttpPost]
        public async Task<IActionResult> Signup([FromBody] SignupModel model)
        {
            var userExists = await _userManager.FindByNameAsync(model.Email);
            if (userExists != null)
                return StatusCode(StatusCodes.Status409Conflict, new Response { Status = "Error", Message = "User already exists" });

            EmailAddressAttribute emailValidator = new();
            if (!emailValidator.IsValid(model.Email))
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new Response { Status = "Error", Message = "Invalid email" });

            IdentityUser user = createUser(model);
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new Response { Status = "Error", Message = result.ToString() });

            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Surname))
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new Response { Status = "Error", Message = "Name and surname can't be empty" });

            var claimsToAdd = new List<Claim>() {
                new Claim(ClaimTypes.Name, model.Name),
                new Claim(ClaimTypes.Surname, model.Surname)
            };

            var addClaimsResult = await _userManager.AddClaimsAsync(user, claimsToAdd);

            return StatusCode(StatusCodes.Status201Created, new Response { Status = "Success", Message = "User created successfully!" });
        }

        private IdentityUser createUser(SignupModel model)
        {
            return new()
            {
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Email,
            };
        }
    }
}
