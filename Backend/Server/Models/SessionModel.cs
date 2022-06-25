namespace Server.Models
{
    public class SessionModel
    {
        public int Id { get; set; }
        public UserModel? User { get; set; }
        public List<OrderModel> Orders { get; set; } = new List<OrderModel>();
        public DateTime? OrdersSaved { get; set; }
        public string? RefreshToken { get; set; }
    }
}
