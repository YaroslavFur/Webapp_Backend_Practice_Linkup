using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class GoodModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Column(TypeName="money")]
        public int? Price { get; set; }

        public string? S3bucket { get; set; }

        public List<OrderModel> Orders { get; set; } = new List<OrderModel>();
        public List<TagModel> Tags { get; set; } = new List<TagModel>();
    }
}
