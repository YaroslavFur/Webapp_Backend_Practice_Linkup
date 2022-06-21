namespace Server.Models
{
    public class OrderModel
    {
        public int Id { get; set; }

        public int? Amount { get; set; }

        public int? GoodId { get; set; }

        public GoodModel? Good { get; set; }

        public string? UserId { get; set; }

        public UserModel? User { get; set; }
    }
}
