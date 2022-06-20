using System.ComponentModel.DataAnnotations;

namespace Server.Models
{
    public class UpdateUserInfoModel : SignupModel
    {
        [Required(ErrorMessage = "Old password is required")]
        public string? OldPassword { get; set; }
    }
}
