using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;

namespace Server.Controllers
{
    [ApiController]
    [Route("tags")]
    public class TagController : Controller
    {
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly IAmazonS3 _s3Client;

        public TagController(AppDbContext db,
            IAmazonS3 s3Client)
        {
            _httpContextAccessor = new();
            _db = db;
            _s3Client = s3Client;
        }

        [Route("createtag")]
        [HttpPost]
        public ActionResult CreateTag([FromBody] TagModel model)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Name == model.Name);
            if (tagExists != null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag {model.Name} already exists" });
            
            _db.Tags.Add(model);
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status201Created, new { Status = "Success" });
        }

        [Route("gettag/{id}")]
        [HttpGet]
        public ActionResult GetTag(int id)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Id == id);
            if (tagExists != null)
            {
                return StatusCode(StatusCodes.Status200OK, new
                {
                    Status = "Success",
                    Tag = tagExists
                });
            }
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag with Id = {id} does not exist" });
        }

        [Route("updatetag/{id}")]
        [HttpPut]
        public ActionResult UpdateTag([FromBody] TagModel model, int id)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Id == id);
            if (tagExists != null)
            {
                tagExists = model;

                // change picture of tag for exmpl

                _db.SaveChanges();
                return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
            }
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag with Id = {id} does not exist" });
        }


        [Route("deletetag/{id}")]
        [HttpDelete]
        public ActionResult DeleteTag(int id)
        {
            var tagExists = _db.Tags.FirstOrDefault(tag => tag.Id == id);
            if (tagExists != null)
            {
                _db.Tags.Remove(tagExists);
                _db.SaveChanges();
                return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
            }

            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Tag with Id = {id} does not exist" });
        }

        [Route("getalltags")]
        [HttpGet]
        public ActionResult GetAllTags()
        {
            var allTags = _db.Tags.ToArray();

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Tags = allTags });
        }
    }
}
