using Server.Models;
using Server.Operators;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Amazon.S3;

namespace Server.Controllers
{
    [Route("signup")]
    [ApiController]
    public class SignupController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IAmazonS3 _s3Client;

        public SignupController(
            UserManager<UserModel> userManager,
            IConfiguration configuration,
            IAmazonS3 s3Client)
        { 
            _userManager = userManager;
            _configuration = configuration;
            _s3Client = s3Client;
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

            user.S3bucket = user.Id;
            if (!await BucketOperator.CreateBucketAsync(user.S3bucket, _s3Client))
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Failed creating S3 bucket" });


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
