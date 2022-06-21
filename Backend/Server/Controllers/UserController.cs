using Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Server.Data;
using Server.Operators;
using Microsoft.AspNetCore.Identity;
using Amazon.S3;
using Amazon.S3.Model;

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

        public UserController(AppDbContext db, 
            UserManager<UserModel> userManager, 
            IAmazonS3 s3Client)
        {
            _httpContextAccessor = new();
            _db = db;
            _userManager = userManager;
            _s3Client = s3Client;
        }

        [Route("getinfo")]
        [HttpGet]
        public ActionResult GetInfo()
        {
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

            return StatusCode(StatusCodes.Status200OK, new {
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
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

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
        public async Task<ActionResult> GetAvatarAsync()
        {
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

            var bucketExists = await _s3Client.DoesS3BucketExistAsync(thisUser.S3bucket);
            if (!bucketExists)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket does not exist" });
            GetObjectResponse s3Object;
            try
            {
                s3Object = await _s3Client.GetObjectAsync(thisUser.S3bucket, "avatar.png");
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Avatar not found" });
            }
            return File(s3Object.ResponseStream, s3Object.Headers.ContentType);
        }

        [Route("updateavatar")]
        [HttpPut]
        public async Task<ActionResult> UpdateAvatar([FromForm] IFormFile picture)
        {
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

            var bucketExists = await _s3Client.DoesS3BucketExistAsync(thisUser.S3bucket);
            if (!bucketExists)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket does not exist" });

            var request = new PutObjectRequest()
            {
                BucketName = thisUser.S3bucket,
                Key = "avatar.png",
                InputStream = picture.OpenReadStream()
            };
            request.Metadata.Add("Content-Type", picture.ContentType);
            await _s3Client.PutObjectAsync(request);
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "Avatar updated successfully" });
        }
    }
}