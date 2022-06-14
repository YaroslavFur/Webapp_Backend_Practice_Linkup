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

namespace Server.Controllers
{
    public class TokenController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly IConfiguration _configuration;

        public TokenController(
            UserManager<UserModel> userManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("refreshtoken")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenModel tokenModel)
        {
            if (tokenModel is null || tokenModel.AccessToken == null || tokenModel.RefreshToken == null)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { Status = "Error", Message = "Invalid request" });
            }

            string accessToken = tokenModel.AccessToken;
            string refreshToken = tokenModel.RefreshToken;
            ClaimsPrincipal accessTokenPrincipal;
            string? email;
            UserModel? user;

            try
            {
                if ((accessTokenPrincipal = TokenOperator.ValidateToken(accessToken, false, _configuration)) == null)
                    throw new Exception();
                email = accessTokenPrincipal.Claims.Where(claim => claim.Type.EndsWith("emailaddress")).First().Value;
                user = await _userManager.FindByNameAsync(email);
                if (user == null)
                    throw new Exception();
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Invalid access token" });
            }

            try
            {
                TokenOperator.ValidateToken(refreshToken, true, _configuration);
                if (refreshToken != user.RefreshToken)
                    throw new Exception();
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Invalid refresh token" });
            }

            _ = long.TryParse(_configuration["JWT:AccessTokenValidityInSeconds"], out long tokenValidityInSeconds);
            _ = long.TryParse(_configuration["JWT:RefreshTokenValidityInSeconds"], out long refreshTokenValidityInSeconds);

            string newAccessToken = TokenOperator.GenerateToken(accessTokenPrincipal.Claims.ToList(), tokenValidityInSeconds, _configuration);
            string newRefreshToken = TokenOperator.GenerateToken(accessTokenPrincipal.Claims.ToList(), refreshTokenValidityInSeconds, _configuration);

            user.RefreshToken = newRefreshToken;
            await _userManager.UpdateAsync(user);

            return StatusCode(StatusCodes.Status200OK, new
            {
                Status = "Success",
                Token = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }

        [Authorize]
        [HttpPost]
        [Route("revoke")]
        public async Task<IActionResult> Revoke()
        {
            string thisUserEmail;
            try
            {
                thisUserEmail = this.User.Claims.Where(claim => claim.Type.EndsWith("emailaddress")).First().Value;
            }
            catch
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { Status = "Error", Message = "Current user not found" });
            }

            var userToRevoke = await _userManager.FindByNameAsync(thisUserEmail);
            if (userToRevoke == null)
                return StatusCode(StatusCodes.Status400BadRequest, new { Status = "Error", Message = "User " + thisUserEmail + " not found" });

            userToRevoke.RefreshToken = null;
            await _userManager.UpdateAsync(userToRevoke);

            return StatusCode(StatusCodes.Status200OK, new { Status = "Success", Message = "User " + userToRevoke.Email + " revoked successfully" });
        }
    }
}
