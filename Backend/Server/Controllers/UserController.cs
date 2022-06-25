using Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Server.Data;
using Server.Operators;
using Microsoft.AspNetCore.Identity;
using Amazon.S3;
using Amazon.S3.Model;
using System.ComponentModel.DataAnnotations;

namespace Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("user")]
    public class UserController : Controller
    {
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly UserManager<UserModel> _userManager;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;

        public UserController(AppDbContext db,
            UserManager<UserModel> userManager,
            IAmazonS3 s3Client, IConfiguration configuration)
        {
            _httpContextAccessor = new();
            _db = db;
            _userManager = userManager;
            _s3Client = s3Client;
            _configuration = configuration;
        }

        [Route("getinfo")]
        [HttpGet]
        public ActionResult GetInfo()
        {
            UserModel thisUser;
            try
            { thisUser = UserOperator.GetUserByPrincipal(this.User, _db); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }


            return StatusCode(StatusCodes.Status200OK, new
            {
                Status = "Success",
                Email = thisUser.UserName,
                Name = thisUser.Name,
                Surname = thisUser.Surname
            });
        }

        [Route("updateinfo")]
        [HttpPut]
        public async Task<ActionResult> UpdateInfo([FromBody] UpdateUserInfoModel model)
        {
            UserModel thisUser;
            try
            { thisUser = UserOperator.GetUserByPrincipal(this.User, _db); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }

            var result = await _userManager.ChangePasswordAsync(thisUser, model.OldPassword, model.Password);
            if (result.Succeeded)
            {
                thisUser.UserName = model.Email;
                thisUser.Surname = model.Surname;
                thisUser.Name = model.Name;

                _db.SaveChanges();
                return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
            }
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Failed verificating old password" });
        }

        [Route("getavatar")]
        [HttpGet]
        public async Task<ActionResult> GetAvatar()
        {
            UserModel thisUser;
            try
            { thisUser = UserOperator.GetUserByPrincipal(this.User, _db); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }

            if (thisUser.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"User doesn't have bucket attached" });
            try
            {
                var s3Objects = await BucketOperator.GetObjectsFromBucket(thisUser.S3bucket, _s3Client, _configuration);
                return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Picture = s3Objects });
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Can't load picture" });
            }
        }

        [Route("updateavatar")]
        [HttpPut]
        public async Task<ActionResult> UpdateAvatar([FromForm] IFormFile picture)
        {
            UserModel thisUser;
            try
            { thisUser = UserOperator.GetUserByPrincipal(this.User, _db); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }

            if (thisUser.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket not attached" });

            try
            {
                await BucketOperator.UpdateFileInBucket(thisUser.S3bucket, picture, _s3Client, _configuration);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "Avatar updated successfully" });
        }

        [Route("deleteuser")]
        [HttpDelete]
        public async Task<ActionResult> DeleteUser()
        {
            SessionModel thisSession;
            UserModel? thisUser;
            try
            { thisSession = UserOperator.GetUserOrAnonymousUserByPrincipal(this.User, _db, out thisUser); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }

            if (thisUser != null && thisUser.S3bucket != null)
            {
                await _s3Client.DeleteObjectAsync(_configuration["AWS:BucketName"], thisUser.S3bucket);
            }

            _db.Sessions.Remove(thisSession);
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "User deleted" });
        }
    }

    public class UpdateUserInfoModel : SignupModel
    {
        [Required(ErrorMessage = "Old password is required")]
        public string? OldPassword { get; set; }
    }
}