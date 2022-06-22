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
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

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
        public async Task<ActionResult> GetAvatar()
        {
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

            var bucketExists = await _s3Client.DoesS3BucketExistAsync(thisUser.S3bucket);
            if (!bucketExists)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket does not exist" });

            try
            {
                if (thisUser.S3bucket == null)
                    throw new Exception("Bucket not exist");
                IEnumerable<S3ObjectDtoModel> s3Object = await BucketOperator.GetObjectsFromBucket(thisUser.S3bucket, "avatar", _s3Client, _configuration);
                return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Url = s3Object });
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Avatar loading error" });
            }
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
                Key = "avatar",
                InputStream = picture.OpenReadStream()
            };
            request.Metadata.Add("Content-Type", picture.ContentType);
            await _s3Client.PutObjectAsync(request);
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "Avatar updated successfully" });
        }

        [Route("deleteuser")]
        [HttpDelete]
        public async Task<ActionResult> DeleteUser()
        {
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

            var bucketExists = await _s3Client.DoesS3BucketExistAsync(thisUser.S3bucket);
            if (bucketExists)
            {
                await _s3Client.DeleteObjectAsync(thisUser.S3bucket, "avatar");
                await _s3Client.DeleteBucketAsync(thisUser.S3bucket);
            }

            _db.Users.Remove(thisUser);
            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "User deleted" });
        }
    }
}