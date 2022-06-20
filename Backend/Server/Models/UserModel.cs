using Microsoft.AspNetCore.Identity;

namespace Server.Models
{
    public class UserModel : IdentityUser
    {
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? S3bucket { get; set; }
        public string? RefreshToken { get; set; }

    }
}
