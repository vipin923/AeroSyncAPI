namespace AeroSyncAPI.Models
{
    public class PassengerContext
    {
        // 🚀 Swapped UserId for EmailHash
        public string EmailHash { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Terminal { get; set; } = "";
        public int MinutesAvailable { get; set; }
        public string DepartureTime { get; set; } = string.Empty;
        public string Tags {  get; set; } = string.Empty;
        public string InterestsText { get; set; } = "";
       
    }

    public class MatchScore
    {
        // 🚀 Added this so the frontend knows who to start a chat with!
        public string MatchedEmailHash { get; set; } = "";
        public string MatchedUserName { get; set; } = "";
        public string MatchedUserInterests { get; set; } = "";
        public float SimilarityScore { get; set; }
    }
}
