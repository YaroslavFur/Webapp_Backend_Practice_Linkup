using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Data;
using Server.Models;
using System.ComponentModel.DataAnnotations;

namespace Server.Controllers
{
    [Authorize(Roles = UserRoles.Admin)]
    [Route("roles")]
    [ApiController]
    public class RoleController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;

        public RoleController(
            UserManager<UserModel> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _db = db;
        }

        [Route("getadmins")]
        [HttpGet]
        public IActionResult GetAdmins()
        {
            IdentityRole? adminRole;
            try
            {
                if ((adminRole = _roleManager.Roles.FirstOrDefault(role => role.Name == UserRoles.Admin)) == null)
                    throw new Exception();
            }
            catch
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"Role 'Admin' does not exist" }); }

            var adminIds = _db.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId);
            var admins = _db.Users.Where(user => adminIds.Contains(user.Id));

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Admins = admins });
        }

        [Route("createadmin")]
        [HttpPost]
        public async Task<IActionResult> CreateAdmin([FromBody] EmailModel email)
        {
            UserModel? user;
            if ((user = _db.Users.FirstOrDefault(user => user.UserName != null && user.UserName == email.Email)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"User with Email = {email.Email} not found" });

            if (!await _roleManager.RoleExistsAsync(UserRoles.Admin))
                await _roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));

            await _userManager.AddToRoleAsync(user, UserRoles.Admin);

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = $"Admin role added to user {user.UserName} successfully" });
        }

        [Route("deleteadmin")]
        [HttpDelete]
        public async Task<IActionResult> DeleteAdmin([FromBody] EmailModel email)
        {
            UserModel? user;
            if ((user = _db.Users.FirstOrDefault(user => user.UserName != null && user.UserName == email.Email)) == null)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = $"User with Email = {email.Email} not found" });

            if (!await _roleManager.RoleExistsAsync(UserRoles.Admin))
                await _roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));

            await _userManager.RemoveFromRoleAsync(user, UserRoles.Admin);

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = $"Admin role removed from user {user.UserName} successfully" });
        }
    }

    public static class UserRoles
    {
        public const string Admin = "Admin";
    }

    public class EmailModel
    {
        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }
    }
}
