namespace SSISAnalyticsDashboard.Models
{
    public class CurrentExecution
    {
        public long ExecutionId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int DurationSeconds { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusDescription { get; set; } = string.Empty;
        public string ExecutedBy { get; set; } = string.Empty;
        public bool IsLongRunning { get; set; }
    }
}
