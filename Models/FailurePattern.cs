namespace SSISAnalyticsDashboard.Models
{
    public class FailurePattern
    {
        public string PackageName { get; set; } = string.Empty;
        public int FailureCount { get; set; }
        public string MostCommonError { get; set; } = string.Empty;
        public DateTime? LastFailureTime { get; set; }
        public List<string> ErrorSources { get; set; } = new List<string>();
        public decimal FailureRate { get; set; }
    }
}
