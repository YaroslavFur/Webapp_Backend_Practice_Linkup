using Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Server.Data;
using Server.Operators;

namespace Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class ApiController : Controller
    {
        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;

        public ApiController(AppDbContext db)
        {
            _httpContextAccessor = new();
            _db = db;
        }

        [HttpGet]
        public ActionResult Get()
        {
            UserModel? thisUser;
            if ((thisUser = UserOperator.GetUserByPrincipal(this.User, _db)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });

            string secretData = "*some private data of " + thisUser.Name + " " + thisUser.Surname + "* Email: " + thisUser.UserName;

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = secretData });
        }
    }
}