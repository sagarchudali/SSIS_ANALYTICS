using SSISAnalyticsDashboard.Models;

namespace SSISAnalyticsDashboard.Services
{
    public interface ISSISDataService
    {
        Task<ExecutionMetrics> GetMetricsAsync(string? businessUnit = null);
        Task<List<ExecutionTrend>> GetTrendsAsync(string? businessUnit = null);
        Task<List<ErrorLog>> GetErrorsAsync(string? businessUnit = null);
        Task<List<PackageExecution>> GetExecutionsAsync(string? businessUnit = null);
        Task<List<PackageExecution>> GetLastExecutedPackagesAsync(int count = 10, string? businessUnit = null);
        
        // Enhanced monitoring methods
        Task<List<CurrentExecution>> GetCurrentExecutionsAsync(string? businessUnit = null);
        Task<List<PackagePerformance>> GetPackagePerformanceStatsAsync(int days = 30, string? businessUnit = null);
        Task<List<FailurePattern>> GetFailurePatternsAsync(int days = 30, string? businessUnit = null);
        Task<List<ExecutionTimeline>> GetExecutionTimelineAsync(int hours = 24, string? businessUnit = null);
    }
}
