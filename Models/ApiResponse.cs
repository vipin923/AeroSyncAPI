namespace AeroSyncAPI.Models
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public object? Data { get; set; }
        public string Error { get; set; } = "";// The AI returns a JSON string inside this property
    }
}
