namespace SSISAnalyticsDashboard.Models
{
    public class DashboardViewModel
    {
        public ExecutionMetrics Metrics { get; set; } = new();
        public List<ExecutionTrend> Trends { get; set; } = new();
        public List<ErrorLog> RecentErrors { get; set; } = new();
        public List<PackageExecution> RecentExecutions { get; set; } = new();
        public List<PackageExecution> LastExecutedPackages { get; set; } = new();
        
        // Enhanced monitoring components
        public List<CurrentExecution> CurrentExecutions { get; set; } = new();
        public List<PackagePerformance> PackagePerformanceStats { get; set; } = new();
        public List<FailurePattern> FailurePatterns { get; set; } = new();
        public List<ExecutionTimeline> ExecutionTimeline { get; set; } = new();
    }
}
