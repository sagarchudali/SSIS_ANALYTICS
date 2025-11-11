namespace SSISAnalyticsDashboard.Models
{
    public class PackagePerformance
    {
        public string PackageName { get; set; } = string.Empty;
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public decimal SuccessRate { get; set; }
        public int AvgDurationSeconds { get; set; }
        public int MinDurationSeconds { get; set; }
        public int MaxDurationSeconds { get; set; }
        public DateTime? LastExecutionTime { get; set; }
        public string LastExecutionStatus { get; set; } = string.Empty;
    }
}
