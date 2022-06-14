using Microsoft.AspNetCore.Mvc;
using Server.Data;
using Server.Models;
using System.Security.Claims;

namespace Server.Operators
{
    public class UserOperator
    {
        public static UserModel CreateUser(SignupModel model)
        {
            return new()
            {
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Email,
                Name = model.Name,
                Surname = model.Surname
            };
        }

        public static UserModel? GetUserByPrincipal(ClaimsPrincipal principal, AppDbContext db)
        {
            string thisUserEmail;
            UserModel? thisUser;
            try
            {
                thisUserEmail = principal.Claims.Where(claim => claim.Type.EndsWith("emailaddress")).First().Value;
                if ((thisUser = db.Users.FirstOrDefault(usr => usr.UserName == thisUserEmail)) == null)
                    throw new Exception();
            }
            catch
            {
                return null;
            }
            return thisUser;
        }
    }
}
