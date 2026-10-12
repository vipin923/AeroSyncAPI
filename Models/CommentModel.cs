namespace AeroSyncAPI.Models
{
    public class CommentModel
    {
        public Guid CommentId { get; set; } = new Guid();
        public Guid FeedId { get; set; } = new Guid();
        public string UserName { get; set; } = string.Empty;
        public string EmailHash { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; } = DateTime.Now;
    }
}
