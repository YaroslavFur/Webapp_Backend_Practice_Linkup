using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Server.Operators;
using Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Server.Data;

namespace Server.Controllers
{
    [ApiController]
    [Route("token")]
    public class TokenController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;

        public TokenController(
            UserManager<UserModel> userManager, IConfiguration configuration, AppDbContext db)
        {
            _userManager = userManager;
            _configuration = configuration;
            _db = db;
        }

        [HttpPost]
        [Route("refreshtoken")]
        public IActionResult RefreshToken([FromBody] TokenModel tokenModel)
        {
            if (tokenModel is null || tokenModel.AccessToken == null || tokenModel.RefreshToken == null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { Status = "Error", Message = "Invalid request" });
            }

            string accessToken = tokenModel.AccessToken;
            string refreshToken = tokenModel.RefreshToken;
            ClaimsPrincipal accessTokenPrincipal;
            SessionModel thisSession;
            UserModel? thisUser = null;

            try
            {
                if ((accessTokenPrincipal = TokenOperator.ValidateToken(accessToken, false, _configuration)) == null)
                    throw new Exception();
            }
            catch 
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Invalid access token" }); }

            try
            { thisSession = UserOperator.GetUserOrAnonymousUserByPrincipal(accessTokenPrincipal, _db, out thisUser); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }

            try
            {
                bool checkRefreshTokenExpiration = thisUser == null ? false : true;         // if user anonymous - refresh token can't be expired
                TokenOperator.ValidateToken(refreshToken, checkRefreshTokenExpiration, _configuration);
                if (refreshToken != thisSession.RefreshToken)
                    throw new Exception();
            }
            catch
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Invalid refresh token" }); }

            TokenModel tokens;
            try
            { tokens = TokenOperator.GenerateAccessRefreshTokens(thisSession, _configuration, _db, thisUser); }
            catch
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Failed creating tokens" }); }

            return StatusCode(StatusCodes.Status200OK, new
            {
                Status = "Success",
                Token = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken
            });
        }

        [Authorize]
        [HttpPost]
        [Route("revoke")]
        public IActionResult Revoke()
        {
            SessionModel thisSession;
            try
            { thisSession = UserOperator.GetUserOrAnonymousUserByPrincipal(this.User, _db, out UserModel? thisUser); }
            catch (Exception exception)
            { return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = exception.Message }); }                

            thisSession.RefreshToken = null;
            _db.SaveChanges();

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "Token revoked successfully" });
        }
    }
}
