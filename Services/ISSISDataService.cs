using SSISAnalyticsDashboard.Models;

namespace SSISAnalyticsDashboard.Services
{
    public interface ISSISDataService
    {
        Task<ExecutionMetrics> GetMetricsAsync();
        Task<List<ExecutionTrend>> GetTrendsAsync();
        Task<List<ErrorLog>> GetErrorsAsync();
        Task<List<PackageExecution>> GetExecutionsAsync();
        Task<List<PackageExecution>> GetLastExecutedPackagesAsync(int count = 10);
        
        // Enhanced monitoring methods
        Task<List<CurrentExecution>> GetCurrentExecutionsAsync();
        Task<List<PackagePerformance>> GetPackagePerformanceStatsAsync(int days = 30);
        Task<List<FailurePattern>> GetFailurePatternsAsync(int days = 30);
        Task<List<ExecutionTimeline>> GetExecutionTimelineAsync(int hours = 24);
    }
}
