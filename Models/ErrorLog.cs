namespace SSISAnalyticsDashboard.Models
{
    public class ErrorLog
    {
        public long ExecutionId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public DateTime ErrorTime { get; set; }
        public long ErrorCode { get; set; }
        public string ErrorDescription { get; set; } = string.Empty;
    }
}
