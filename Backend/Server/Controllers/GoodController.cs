using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
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

        public GoodController(AppDbContext db,
            IAmazonS3 s3Client)
        {
            _httpContextAccessor = new();
            _db = db;
            _s3Client = s3Client;
        }

        [Route("creategood")]
        [HttpPost]
        public ActionResult CreateGood([FromBody] GoodModel model)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Name == model.Name);
            if (goodExists != null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good {model.Name} already exists" });

            // create bucket
            _db.Goods.Add(model);
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status201Created, new { Status = "Success" });
        }

        [Route("getgood/{id}")]
        [HttpGet]
        public ActionResult GetGood(int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists != null)
            {
                return StatusCode(StatusCodes.Status200OK, new
                {
                    Status = "Success",
                    Good = goodExists,
                    // get picture from bucket
                });
            }
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
        }

        [Route("updategood/{id}")]
        [HttpPut]
        public ActionResult UpdateGood([FromBody] GoodModel model, int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists != null)
            {
                goodExists.Name = model.Name;
                goodExists.Price = model.Price;

                // change picture in bucket

                _db.SaveChanges();
                return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
            }
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
        }


        [Route("deletegood/{id}")]
        [HttpDelete]
        public ActionResult DeleteGood(int id)
        {
            var goodExists = _db.Goods.FirstOrDefault(good => good.Id == id);
            if (goodExists != null)
            {
                // delete bucket
                _db.Goods.Remove(goodExists);
                _db.SaveChanges();
                return StatusCode(StatusCodes.Status200OK, new { Status = "Success" });
            }
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
        }

        [Route("setgoodtags/{id}")]
        [HttpPut]
        public ActionResult SetGoodTags([FromBody] TagNames tagsProvided, int id)
        {
            var goodExists = _db.Goods.Include(good => good.Tags).FirstOrDefault(good => good.Id == id);
            if (goodExists != null)
            {
                var tags = _db.Tags.Where(tag => tag.Name != null && tagsProvided.tagNames.Contains(tag.Name));
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
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Good with Id = {id} does not exist" });
        }

        [Route("getgoods")]
        [HttpGet]
        public ActionResult GetGoods(GetGoodsProperties properties)
        {
            IQueryable<GoodModel> query = _db.Goods;
            if (properties.idOfPreviousGood > 0)
            {
                query = query
                    .Where(good => good.Id > properties.idOfPreviousGood);                          // skip previous ids
            }
            if (properties.category != "")
            {
                query = query
                    .Where(good => good.Tags.Any(tag => tag.Name == properties.category));          // filter by category 
            }
            if (properties.nameFilter != "")
            {
                query = query
                    .Where(good => good.Name != null && good.Name.Contains(properties.nameFilter)); // filter by name
            }
            var goodsToGet = query
                .OrderBy(good => good.Id)                                                           // order by id
                .Take(properties.numOfGoodsToGet)                                                   // take exact num of goods
                .ToArray();

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Goods = goodsToGet });
        }
    }

    public struct TagNames
    {
        [Required(ErrorMessage = "TagNames is required")]
        public List<string> tagNames { get; set; }
    }

    public struct GetGoodsProperties
    {
        [Required(ErrorMessage = "NumOfGoodsToGet is required")]
        public int numOfGoodsToGet { get; set; }
        [Required(ErrorMessage = "IdOfPreviousGood is required")]
        public int idOfPreviousGood { get; set; }
        public string category { get; set; }
        public string nameFilter { get; set; }
    }
}
