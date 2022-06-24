using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Operators;

namespace Server.Controllers
{
    [ApiController]
    [Route("tags")]
    public class TagController : Controller
    {
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;

        public TagController(AppDbContext db,
            IAmazonS3 s3Client, IConfiguration configuration)
        {
            _httpContextAccessor = new();
            _db = db;
            _s3Client = s3Client;
            _configuration = configuration;
        }

        [Route("createtag")]
        [HttpPost]
        public ActionResult CreateTag([FromBody] TagModel model)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Name == model.Name);
            if (tagExists != null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag {model.Name} already exists" });

            model.S3bucket = $"tag{Guid.NewGuid().ToString()}";

            _db.Tags.Add(model);
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status201Created, new { Status = "Success" });
        }

        [Route("gettag/{id}")]
        [HttpGet]
        public async Task<ActionResult> GetTag(int id)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Id == id);
            if (tagExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag with Id = {id} does not exist" });
            if (tagExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag doesn't have bucket attached" });
            try
            {
                var s3Objects = await BucketOperator.GetObjectsFromBucket(tagExists.S3bucket, _s3Client, _configuration);
                return StatusCode(StatusCodes.Status200OK, new
                {
                    Status = "Success",
                    Tag = new
                    {
                        Id = tagExists.Id,
                        Name = tagExists.Name,
                        Picture = s3Objects
                    }
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Can't load picture" });
            }            
        }

        [Route("updatetag/{id}")]
        [HttpPut]
        public ActionResult UpdateTag([FromBody] TagModel model, int id)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Id == id);
            if (tagExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag with Id = {id} does not exist" });

            tagExists.Name = model.Name;

            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
        }


        [Route("deletetag/{id}")]
        [HttpDelete]
        public async Task<ActionResult> DeleteTag(int id)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Id == id);
            if (tagExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag with Id = {id} does not exist" });
            
            if (tagExists.S3bucket != null)
            {
                await _s3Client.DeleteObjectAsync(_configuration["AWS:BucketName"], tagExists.S3bucket);
            }

            _db.Tags.Remove(tagExists);
            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
        }

        [Route("getalltags")]
        [HttpGet]
        public async Task<ActionResult> GetAllTags()
        {
            var allTags = _db.Tags.ToArray();
            List<object> resultTags;
            try
            {
                resultTags = await TagsToJsonAsync(allTags, _s3Client, _configuration);
            }
            catch(Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message});
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Tags = resultTags });
        }

        [Route("updatetagpicture/{id}")]
        [HttpPut]
        public async Task<ActionResult> UpdateTagPicture([FromForm] IFormFile picture, int id)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Id == id);
            if (tagExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag with Id = {id} does not exist" });
            if (tagExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket not attached" });

            try
            {
                await BucketOperator.UpdateFileInBucket(tagExists.S3bucket, picture, _s3Client, _configuration);
            }
            catch(Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "TagPicture updated successfully" });
        }

        public static async Task<List<object>> TagsToJsonAsync(IEnumerable<TagModel> tags, IAmazonS3 s3Client, IConfiguration configuration)
        {
            List<object> resultTags = new();
            foreach (var tag in tags)
            {
                IEnumerable<S3ObjectDtoModel>? s3Objects;
                if (tag.S3bucket == null)
                    s3Objects = null;
                else
                {
                    try
                    {
                        s3Objects = await BucketOperator.GetObjectsFromBucket(tag.S3bucket, s3Client, configuration);
                    }
                    catch
                    {
                        throw new Exception($"Can't load picture in tag with id = {tag.Id}");
                    }
                }
                resultTags.Add(new
                {
                    id = tag.Id,
                    name = tag.Name,
                    picture = s3Objects
                });
            }
            return resultTags;
        }
    }
}
