using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Data;
using Server.Models;
using Server.Operators;
using System.ComponentModel.DataAnnotations;

namespace Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("cart")]
    public class CartController : Controller
    { 
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;

        public CartController(
            AppDbContext db, IConfiguration configuration, IAmazonS3 s3Client)
        {
            _httpContextAccessor = new();
            _db = db;
            _s3Client = s3Client;
            _configuration = configuration;
        }

        [Route("setcart")]
        [HttpPut]
        public ActionResult SetCart([FromBody] SessionModel session)
        {
            SessionModel thisSession;
            try
            { thisSession = UserOperator.GetUserOrAnonymousUserByPrincipal(this.User, _db, out UserModel? thisUser); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }

            try
            {
                if (session.OrdersSaved < thisSession.OrdersSaved)
                    return StatusCode(StatusCodes.Status409Conflict,
                        new
                        {
                            Status = "Error",
                            Message = $"This saving timestamp: {session.OrdersSaved} was made before existing one: {thisSession.OrdersSaved}"
                        });
                thisSession.OrdersSaved = session.OrdersSaved;

                _db.Orders.RemoveRange(thisSession.Orders);
                if (session.Orders != null)
                {
                    var goodIds = session.Orders.Select(order => order.Id);
                    var goods = _db.Goods.Where(good => goodIds.Contains(good.Id));
                    foreach (var order in session.Orders)
                    {
                        OrderModel newOrder = new();
                        newOrder.Session = thisSession;
                        try
                        { newOrder.Good = goods.First(good => good.Id == order.Id); }
                        catch
                        { continue; }
                        newOrder.Amount = order.Amount;

                        thisSession.Orders.Add(newOrder);
                    }
                }

                _db.SaveChanges();
            }
            catch(Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"{exception.Message}" });
            }

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "Set was completed successfully" });
        }

        [Route("getcart")]
        [HttpGet]
        public async Task<ActionResult> GetCart()
        {
            SessionModel thisSession;
            try
            { thisSession = UserOperator.GetUserOrAnonymousUserByPrincipal(this.User, _db, out UserModel? thisUser); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }

            List<object> resultOrders;
            try
            {
                resultOrders = await OrdersToJsonAsync(thisSession.Orders, _db, _s3Client, _configuration);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message });
            }
            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Cart = resultOrders });
        }

        public static async Task<List<object>> OrdersToJsonAsync(IEnumerable<OrderModel> orders, AppDbContext db, IAmazonS3 s3Client, IConfiguration configuration)
        {
            List<object> resultOrders = new();
            var goodIds = orders.Select(order => order.GoodId);
            var goods = db.Goods.Where(good => goodIds.Contains(good.Id));
            foreach (var good in goods)
            {
                IEnumerable<S3ObjectDtoModel>? s3Objects;
                if (good.S3bucket == null)
                    s3Objects = null;
                else
                {
                    try
                    {
                        s3Objects = await BucketOperator.GetObjectsFromBucket(good.S3bucket, s3Client, configuration);
                    }
                    catch
                    {
                        throw new Exception($"Can't load picture in good with id = {good.Id}");
                    }
                }
                resultOrders.Add(new
                {
                    id = good.Id,
                    name = good.Name,
                    price = good.Price,
                    amount = orders.First(order => order.GoodId == good.Id).Amount,
                    picture = s3Objects
                });
            }
            return resultOrders;
        }
    }

    public struct ReturnOrder
    {
        public int? id;
        public string? name;
        public int? price;
        public int? amount;
        public string? picture;
    }

    public struct Cart
    {
        public List<OrderModel> Orders { get; set; }
    }
}
