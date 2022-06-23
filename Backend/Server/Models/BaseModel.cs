namespace Server.Models
{
    public class BaseModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? S3bucket { get; set; }   
    }
}
