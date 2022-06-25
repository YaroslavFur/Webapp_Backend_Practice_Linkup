using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Controllers;
using Server.Data;
using Server.Models;
using System.Security.Claims;
using static Server.Controllers.AuthController;

namespace Server.Operators
{
    public class UserOperator
    {
        public static UserModel CreateUser(SignupModel model, AppDbContext db, IConfiguration configuration, out SessionModel sessionOut)
        {
            SessionModel? session;

            if (model.AccessToken == null)
                session = new SessionModel();                                                       // if need new session - create new
            else
            {
                session = GetAnonymousUserByPrincipal(TokenOperator.ValidateToken(model.AccessToken, true, configuration), db); 
                if (session.User != null)                                                           // if session found and has user - error
                    throw new Exception($"Session with Id = {session.Id} already has registered user attached");
            }

            UserModel newUser = new()
            {
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Email,
                Name = model.Name,
                Surname = model.Surname,
                Session = session
            };

            sessionOut = session;
            return newUser;
        }

        public static UserModel GetUserByPrincipal(ClaimsPrincipal principal, AppDbContext db)
        {
            UserModel? thisUser;
            string userEmailFromToken;

            try
            {
                userEmailFromToken = principal.Claims.Where(claim => claim.Type.EndsWith("emailaddress")).First().Value;
            }
            catch
            {
                throw new Exception($"Wrong token. Can't parse email from field 'emailaddress'");
            }
            if ((thisUser = db.Users.FirstOrDefault(user => user.UserName == userEmailFromToken)) == null)
                throw new Exception($"User with Email = {userEmailFromToken} not found");

            return thisUser;
        }

        public static SessionModel GetAnonymousUserByPrincipal(ClaimsPrincipal principal, AppDbContext db)
        {
            SessionModel? thisSession;
            if (!int.TryParse(principal.Claims.Where(claim => claim.Type.EndsWith("anonymous")).First().Value, out int sessionIdFromToken))
                throw new Exception($"Wrong token. Can't parse id from field 'anonymous'");
            if ((thisSession = db.Sessions.Include(ses => ses.Orders).FirstOrDefault(session => session.Id == sessionIdFromToken)) == null)
                throw new Exception($"Session with Id = {sessionIdFromToken} not found");

            return thisSession;
        }

        public static SessionModel GetUserOrAnonymousUserByPrincipal(ClaimsPrincipal principal, AppDbContext db, out UserModel? userOut)
        {
            UserModel? thisUser;
            SessionModel? thisSession;
            string userEmailFromToken;

            try
            {
                userEmailFromToken = principal.Claims.Where(claim => claim.Type.EndsWith("emailaddress")).First().Value;
            }
            catch
            {
                if (int.TryParse(principal.Claims.Where(claim => claim.Type.EndsWith("anonymous")).First().Value, out int sessionIdFromToken))
                {
                    if ((thisSession = db.Sessions.Include(ses => ses.Orders).FirstOrDefault(session => session.Id == sessionIdFromToken)) == null)
                        throw new Exception($"Session with Id = {sessionIdFromToken} not found");
                    userOut = null;
                    return thisSession;
                }
                else
                {
                    throw new Exception("Wrong token, no fields 'emailadress' or 'anonymous' found");
                }
            }
            if ((thisUser = db.Users.FirstOrDefault(user => user.UserName == userEmailFromToken)) == null)
                throw new Exception($"User with Email = {userEmailFromToken} not found");
            if ((thisSession = db.Sessions.Include(ses => ses.Orders).FirstOrDefault(session => session.Id == thisUser.SessionId)) == null)
                throw new Exception($"No session for user with Email = {userEmailFromToken} found");

            userOut = thisUser;
            return thisSession;
        }
    }
}
