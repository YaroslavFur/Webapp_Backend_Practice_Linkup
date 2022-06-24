using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Server.Data;
using Server.Models;
using Server.Operators;

namespace Server.Controllers
{
    [ApiController]
    [Route("bases")]
    public class BaseController : Controller
    {
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;

        public BaseController(AppDbContext db,
            IAmazonS3 s3Client, IConfiguration configuration)
        {
            _httpContextAccessor = new();
            _db = db;
            _s3Client = s3Client;
            _configuration = configuration;
        }

        [Route("createbase")]
        [HttpPost]
        public ActionResult CreateBase([FromBody] BaseModel model)
        {
            var baseExists = _db.Bases.FirstOrDefault(Base => Base.Name == model.Name);
            if (baseExists != null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Base {model.Name} already exists" });

            model.S3bucket = $"base{Guid.NewGuid().ToString()}";

            _db.Bases.Add(model);
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status201Created, new { Status = "Success" });
        }

        [Route("getbase/{id}")]
        [HttpGet]
        public async Task<ActionResult> GetBase(int id)
        {
            var baseExists = _db.Bases.FirstOrDefault(Base => Base.Id == id);
            if (baseExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Base with Id = {id} does not exist" });
            if (baseExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Base doesn't have bucket attached" });
            try
            {
                var s3Objects = await BucketOperator.GetObjectsFromBucket(baseExists.S3bucket, _s3Client, _configuration);
                return StatusCode(StatusCodes.Status200OK, new
                {
                    Status = "Success",
                    Base = new
                    {
                        Id = baseExists.Id,
                        Name = baseExists.Name,
                        Description = baseExists.Description,
                        Picture = s3Objects
                    }
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Can't load picture" });
            }
        }

        [Route("updatebase/{id}")]
        [HttpPut]
        public ActionResult UpdateBase([FromBody] BaseModel model, int id)
        {
            var baseExists = _db.Bases.FirstOrDefault(Base => Base.Id == id);
            if (baseExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Base with Id = {id} does not exist" });
            
            baseExists.Name = model.Name;
            baseExists.Description = model.Description;

            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
        }


        [Route("deletebase/{id}")]
        [HttpDelete]
        public async Task<ActionResult> DeleteBase(int id)
        {
            var baseExists = _db.Bases.FirstOrDefault(Base => Base.Id == id);
            if (baseExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Base with Id = {id} does not exist" });

            if (baseExists.S3bucket != null)
            {
                await _s3Client.DeleteObjectAsync(_configuration["AWS:BucketName"], baseExists.S3bucket);
            }

            _db.Bases.Remove(baseExists);
            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
        }

        [Route("getallbases")]
        [HttpGet]
        public async Task<ActionResult> GetAllBases()
        {
            var allBases = _db.Bases.ToArray();
            List<object> resultBases;
            try
            {
                resultBases = await BasesToJsonAsync(allBases, _s3Client, _configuration);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Bases = resultBases });
        }

        [Route("updatebasepicture/{id}")]
        [HttpPut]
        public async Task<ActionResult> UpdateBasePicture([FromForm] IFormFile picture, int id)
        {
            var baseExists = _db.Bases.FirstOrDefault(Base => Base.Id == id);
            if (baseExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Base with Id = {id} does not exist" });
            if (baseExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket not attached" });

            try
            {
                await BucketOperator.UpdateFileInBucket(baseExists.S3bucket, picture, _s3Client, _configuration);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "BasePicture updated successfully" });
        }

        public static async Task<List<object>> BasesToJsonAsync(IEnumerable<BaseModel> bases, IAmazonS3 s3Client, IConfiguration configuration)
        {
            List<object> resultBases = new();
            foreach (var Base in bases)
            {
                IEnumerable<S3ObjectDtoModel>? s3Objects;
                if (Base.S3bucket == null)
                    s3Objects = null;
                else
                {
                    try
                    {
                        s3Objects = await BucketOperator.GetObjectsFromBucket(Base.S3bucket, s3Client, configuration);
                    }
                    catch
                    {
                        throw new Exception($"Can't load picture in base with id = {Base.Id}");
                    }
                }
                resultBases.Add(new
                {
                    id = Base.Id,
                    name = Base.Name,
                    description = Base.Description,
                    picture = s3Objects
                });
            }
            return resultBases;
        }
    }
}
