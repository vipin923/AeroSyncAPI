namespace AeroSyncAPI.Models
{
    public class FeedModels
    {
        public Guid FeedId { get; set; } = new Guid();
        public string EmailHash { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; } = DateTime.Now;
        public List<string> Tags { get; set; } = new();
        public List<CommentModel> Comments { get; set; } = new();
      
    }
}
