using Server.Models;
using Server.Operators;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Amazon.S3;
using Server.Data;

namespace Server.Controllers
{
    [Route("auth/")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IAmazonS3 _s3Client;
        private readonly AppDbContext _db;

        public AuthController(
            UserManager<UserModel> userManager, IConfiguration configuration, IAmazonS3 s3Client, AppDbContext db)
        {
            _userManager = userManager;
            _configuration = configuration;
            _s3Client = s3Client;
            _db = db;
        }

        [Route("signup")]
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

            UserModel user;
            SessionModel session;
            try
            {
                user = UserOperator.CreateUser(model, _db, _configuration, out session);
                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    _db.Sessions.Remove(session);
                    throw new Exception(result.ToString());
                }
                user.S3bucket = $"user{user.Id}";
                _db.SaveChanges();
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }

            TokenModel tokens;
            try
            {
                tokens = TokenOperator.GenerateAccessRefreshTokens(session, _configuration, _db, user);
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

        [Route("login")]
        [HttpPost]
        public async Task<ActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Email);
            if (user == null)
                return StatusCode(StatusCodes.Status401Unauthorized, new { Status = "Error", Message = "User not found" });
            if (!await _userManager.CheckPasswordAsync(user, model.Password))
                return StatusCode(StatusCodes.Status401Unauthorized, new { Status = "Error", Message = "Wrong email or password" });
            var session = _db.Sessions.FirstOrDefault(session => session.Id == user.SessionId);
            if (session == null)
                return StatusCode(StatusCodes.Status401Unauthorized, new { Status = "Error", Message = "Session for this user not found" });

            TokenModel tokens;
            try
            {
                tokens = TokenOperator.GenerateAccessRefreshTokens(session, _configuration, _db, user);
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

        [Route("signupanonymous")]
        [HttpPost]
        public ActionResult SignUpAnonymous()
        {
            SessionModel session;

            session = new SessionModel();
            _db.Sessions.Add(session);
            _db.SaveChanges();

            TokenModel tokens;
            try
            {
                tokens = TokenOperator.GenerateAccessRefreshTokens(session, _configuration, _db);
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

    public class SignupModel
    {
        [Required(ErrorMessage = "Name is required")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        public string? Surname { get; set; }

        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; }

        public string? AccessToken { get; set; }
    }

    public struct LoginModel
    {
        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; }
    }
}
