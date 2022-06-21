using System.ComponentModel.DataAnnotations;

namespace Server.Models
{
    public class TagModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        public string? Name { get; set; }

        public List<GoodModel> Goods { get; set; } = new List<GoodModel>();
    }
}
