using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Operators;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Server.Controllers
{
    [ApiController]
    [Route("goods")]
    public class GoodController : Controller
    {
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;

        public GoodController(AppDbContext db,
            IAmazonS3 s3Client, IConfiguration configuration)
        {
            _httpContextAccessor = new();
            _db = db;
            _s3Client = s3Client;
            _configuration = configuration;
        }

        [Authorize(Roles = UserRoles.Admin)]
        [Route("creategood")]
        [HttpPost]
        public ActionResult CreateGood([FromBody] GoodModel model)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Name == model.Name);
            if (goodExists != null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good {model.Name} already exists" });

            model.S3bucket = Guid.NewGuid().ToString();

            _db.Goods.Add(model);
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status201Created, new { Status = "Success" });
        }

        [Route("getgood/{id}")]
        [HttpGet]
        public async Task<ActionResult> GetGood(int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
            if (goodExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good doesn't have bucket attached" });
            try
            {
                var s3Objects = await BucketOperator.GetObjectsFromBucket($"good{goodExists.S3bucket}", _s3Client, _configuration);
                return StatusCode(StatusCodes.Status200OK, new
                {
                    Status = "Success",
                    Good = new
                    {
                        Id = goodExists.Id,
                        Name = goodExists.Name,
                        Price = goodExists.Price,
                        Sold = _db.Orders.Where(order => order.GoodId == goodExists.Id).Select(order => order.Amount).Sum(),
                        Picture = s3Objects
                    }
                }); ;
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Can't load picture" });
            }
        }

        [Authorize(Roles = UserRoles.Admin)]
        [Route("updategood/{id}")]
        [HttpPut]
        public ActionResult UpdateGood([FromBody] GoodModel model, int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists == null)
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
            
            goodExists.Name = model.Name;
            goodExists.Price = model.Price;

            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
        }

        [Authorize(Roles = UserRoles.Admin)]
        [Route("deletegood/{id}")]
        [HttpDelete]
        public async Task<ActionResult> DeleteGood(int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });

            if (goodExists.S3bucket != null)
            {
                await _s3Client.DeleteObjectAsync(_configuration["AWS:BucketName"], $"good{goodExists.S3bucket}");
            }

            _db.Goods.Remove(goodExists);
            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
        }

        [Authorize(Roles = UserRoles.Admin)]
        [Route("setgoodtags/{id}")]
        [HttpPut]
        public ActionResult SetGoodTags([FromBody] TagIds tagsProvided, int id)
        {
            var goodExists = _db.Goods.Include(good => good.Tags).FirstOrDefault(good => good.Id == id);
            if (goodExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
            
            var tags = _db.Tags.Where(tag => tagsProvided.tagIds.Contains(tag.Id));
            if (!tags.Any())
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"No tags with these names found" });

            if (goodExists.Tags != null)
                goodExists.Tags.Clear();
            else
                goodExists.Tags = new List<TagModel>();
                
            goodExists.Tags.AddRange(tags);

            _db.SaveChanges();
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
        }

        [Route("getgoods")]
        [HttpPost]
        public async Task<ActionResult> GetGoods([FromBody] GetGoodsProperties properties)
        {
            IQueryable<GoodModel> query = _db.Goods;
            if (properties.nameFilter != "")
            {
                query = query
                    .Include(good => good.Tags)                                                     // save tags so later we can take them
                    .Where(good => good.Name != null && good.Name.Contains(properties.nameFilter)); // filter by name
            }

            /*
             filteredTags are tags that contain goods that satisfy the search request (nameFilter)
             so other tags are useless since there are no goods in them that user is searching for
             so with this search request (nameFilter) these are only tags needed to be pickable
            */
            var filteredTags = query
                .SelectMany(good => good.Tags)                                                      // take tags from filtered by name goods
                .Distinct()
                .OrderBy(tag => tag.Id)
                .ToArray();

            if (properties.idOfPreviousGood > 0)
            {
                query = query
                    .Where(good => good.Id > properties.idOfPreviousGood);                          // skip previous ids
            }
            if (properties.category != 0)
            {
                query = query
                    .Where(good => good.Tags.Any(tag => tag.Id == properties.category));            // filter by category 
            }
            var filteredGoods = query
                .OrderBy(good => good.Id)                                                           // order by id
                .Take(properties.numOfGoodsToGet)                                                   // take exact num of goods
                .ToArray();

            List<object> resultGoods;
            List<object> resultTags;
            try
            {
                resultGoods = await GoodsToJsonAsync(filteredGoods, _db, _s3Client, _configuration);
                resultTags = await TagController.TagsToJsonAsync(filteredTags, _s3Client, _configuration);
            }
            catch(Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Goods = resultGoods, Tags = resultTags });
        }

        [Authorize(Roles = UserRoles.Admin)]
        [Route("updategoodpicture/{id}")]
        [HttpPut]
        public async Task<ActionResult> UpdateGoodPicture([FromForm] IFormFile picture, int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
            if (goodExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket not attached" });

            try
            {
                await BucketOperator.UpdateFileInBucket($"good{goodExists.S3bucket}", picture, _s3Client, _configuration);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "GoodPicture updated successfully" });
        }

        [Authorize(Roles = UserRoles.Admin)]
        [Route("updategooddetails/{id}")]
        [HttpPut]
        public async Task<ActionResult> UpdateGoodDetails(
            [FromForm] List<PictureModel> pictures, [FromForm] string shortDescription, [FromForm] string description, int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
            if (goodExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "S3 bucket not attached" });

            try
            {
                foreach (var picture in pictures)
                {
                    if (picture.file == null || picture.Id == null)
                        throw new Exception("Pictures and Ids can't be empty");
                    await BucketOperator.UpdateFileInBucket($"good{goodExists.S3bucket}{picture.Id}", picture.file, _s3Client, _configuration);
                }
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }
            goodExists.ShortDescription = shortDescription;
            goodExists.Description = description;
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "Details updated successfully" });
        }

        [Route("getgooddetails/{id}")]
        [HttpGet]
        public async Task<ActionResult> GetGoodDetails(int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
            if (goodExists.S3bucket == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good doesn't have bucket attached" });
            try
            {
                string key = $"good{goodExists.S3bucket}";
                var pictures = (await BucketOperator.GetObjectsFromBucket(key, _s3Client, _configuration)).ToList();
                List<S3ObjectDtoModel> picturesWithIds = new List<S3ObjectDtoModel>();
                foreach (var picture in pictures)
                {
                    if (picture.Name != null)
                    {
                        string strId = picture.Name.Replace(key, "");
                        if (strId != "")
                        {
                            picture.Id = int.Parse(strId);
                            picturesWithIds.Add(picture);
                        }
                    }   
                }

                return StatusCode(StatusCodes.Status200OK, new
                {
                    Status = "Success",
                    Details = new
                    {
                        Id = goodExists.Id,
                        ShortDescription = goodExists.ShortDescription,
                        Description = goodExists.Description,
                        Pictures = picturesWithIds
                    }
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Can't load picture" });
            }
        }

        public static async Task<List<object>> GoodsToJsonAsync(IEnumerable<GoodModel> goods, AppDbContext db, IAmazonS3 s3Client, IConfiguration configuration)
        {
            List<object> resultGoods = new();
            var allOrders = db.Orders.ToArray();
            foreach (var good in goods)
            {
                IEnumerable<S3ObjectDtoModel>? s3Objects;
                if (good.S3bucket == null)
                    s3Objects = null;
                else
                {
                    try
                    {
                        s3Objects = await BucketOperator.GetObjectsFromBucket($"good{good.S3bucket}", s3Client, configuration);
                    }
                    catch
                    {
                        throw new Exception($"Can't load picture in good with id = {good.Id}");
                    }
                }
                resultGoods.Add(new
                {
                    id = good.Id,
                    name = good.Name,
                    price = good.Price,
                    sold = allOrders.Where(order => order.GoodId == good.Id).Select(order => order.Amount).Sum(),
                    picture = s3Objects
                });
            }
            return resultGoods;
        }
    }

    public class PictureModel
    {
        public int? Id { get; set; }
        public IFormFile? file { get; set; }
    }

    public struct TagIds
    {
        [Required(ErrorMessage = "TagIds is required")]
        public List<int> tagIds { get; set; }
    }

    public struct GetGoodsProperties
    {
        [Required(ErrorMessage = "NumOfGoodsToGet is required")]
        public int numOfGoodsToGet { get; set; }
        [Required(ErrorMessage = "IdOfPreviousGood is required")]
        public int idOfPreviousGood { get; set; }
        public int category { get; set; }
        public string nameFilter { get; set; }
    }
}
