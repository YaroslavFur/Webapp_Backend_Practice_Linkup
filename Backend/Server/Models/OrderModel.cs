namespace Server.Models
{
    public class OrderModel
    {
        public int Id { get; set; }

        public int? Amount { get; set; }

        public int? GoodId { get; set; }

        public GoodModel? Good { get; set; }

        public int? SessionId { get; set; }

        public SessionModel? Session { get; set; }
    }
}
