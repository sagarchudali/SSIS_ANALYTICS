namespace SSISAnalyticsDashboard.Models
{
    public class ExecutionTimeline
    {
        public long ExecutionId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty; // success, danger, warning, primary
    }
}
