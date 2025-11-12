using Microsoft.Data.SqlClient;
using SSISAnalyticsDashboard.Models;
using SSISAnalyticsDashboard.Helpers;
using System.Data;

namespace SSISAnalyticsDashboard.Services
{
    public class SSISDataService : ISSISDataService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SSISDataService> _logger;

        public SSISDataService(IHttpContextAccessor httpContextAccessor, ILogger<SSISDataService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private string GetConnectionString()
        {
            // Try to get connection string from session first
            var sessionConnectionString = _httpContextAccessor.HttpContext?.Session?.GetString("SSISDBConnection");
            if (!string.IsNullOrEmpty(sessionConnectionString))
            {
                return sessionConnectionString;
            }
            
            throw new InvalidOperationException("Connection string not found in session. Please configure the server first.");
        }

        public async Task<ExecutionMetrics> GetMetricsAsync(string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT 
                        COUNT(*) as TotalExecutions,
                        SUM(CASE WHEN status = 4 THEN 1 ELSE 0 END) as FailedExecutions,
                        SUM(CASE WHEN status = 7 THEN 1 ELSE 0 END) as SuccessfulExecutions,
                        AVG(DATEDIFF(SECOND, start_time, end_time)) as AvgDuration
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE start_time >= DATEADD(day, -30, GETDATE())
                    {businessUnitFilter}";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var totalExecutions = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                    var failedExecutions = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                    var successfulExecutions = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                    var avgDuration = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));

                    var successRate = totalExecutions > 0 
                        ? (decimal)successfulExecutions / totalExecutions * 100 
                        : 0;

                    return new ExecutionMetrics
                    {
                        TotalExecutions = totalExecutions,
                        SuccessfulExecutions = successfulExecutions,
                        FailedExecutions = failedExecutions,
                        SuccessRate = Math.Round(successRate, 2),
                        AvgDuration = avgDuration
                    };
                }

                return new ExecutionMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metrics");
                throw;
            }
        }

        public async Task<List<ExecutionTrend>> GetTrendsAsync(string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT 
                        CAST(start_time AS DATE) as Date,
                        SUM(CASE WHEN status = 7 THEN 1 ELSE 0 END) as Success,
                        SUM(CASE WHEN status = 4 THEN 1 ELSE 0 END) as Failed,
                        AVG(DATEDIFF(SECOND, start_time, end_time)) as AvgDuration
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE start_time >= DATEADD(day, -30, GETDATE())
                    {businessUnitFilter}
                    GROUP BY CAST(start_time AS DATE)
                    ORDER BY Date DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var trends = new List<ExecutionTrend>();
                while (await reader.ReadAsync())
                {
                    trends.Add(new ExecutionTrend
                    {
                        Date = reader.GetDateTime(0).ToString("yyyy-MM-dd"),
                        Success = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                        Failed = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                        AvgDuration = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3))
                    });
                }

                return trends;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching trends");
                throw;
            }
        }

        public async Task<List<ErrorLog>> GetErrorsAsync(string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT TOP 50
                        e.execution_id,
                        e.package_name,
                        em.message_time,
                        em.event_message_id,
                        em.message
                    FROM [SSISDB].[catalog].[executions] e
                    INNER JOIN [SSISDB].[catalog].[event_messages] em 
                        ON e.execution_id = em.operation_id
                    WHERE em.message_type = 120  -- Error messages
                        AND e.start_time >= DATEADD(day, -30, GETDATE())
                        AND e.status = 4  -- Failed executions
                        {businessUnitFilter}
                    ORDER BY em.message_time DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var errors = new List<ErrorLog>();
                while (await reader.ReadAsync())
                {
                    errors.Add(new ErrorLog
                    {
                        ExecutionId = reader.GetInt64(0),
                        PackageName = reader.GetString(1),
                        ErrorTime = reader.GetDateTimeOffset(2).DateTime,
                        ErrorCode = reader.GetInt64(3),
                        ErrorDescription = reader.GetString(4)
                    });
                }

                return errors;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching errors");
                throw;
            }
        }

        public async Task<List<PackageExecution>> GetExecutionsAsync(string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT TOP 50
                        e.execution_id,
                        e.package_name,
                        e.folder_name,
                        e.project_name,
                        CASE 
                            WHEN e.status = 1 THEN 'Created'
                            WHEN e.status = 2 THEN 'Running'
                            WHEN e.status = 3 THEN 'Canceled'
                            WHEN e.status = 4 THEN 'Failed'
                            WHEN e.status = 5 THEN 'Pending'
                            WHEN e.status = 6 THEN 'Ended Unexpectedly'
                            WHEN e.status = 7 THEN 'Succeeded'
                            WHEN e.status = 8 THEN 'Stopping'
                            WHEN e.status = 9 THEN 'Completed'
                            ELSE 'Unknown'
                        END as Status,
                        e.start_time,
                        e.end_time,
                        DATEDIFF(SECOND, e.start_time, e.end_time) as Duration
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE e.start_time >= DATEADD(day, -30, GETDATE())
                    {businessUnitFilter}
                    ORDER BY e.start_time DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var executions = new List<PackageExecution>();
                while (await reader.ReadAsync())
                {
                    executions.Add(new PackageExecution
                    {
                        ExecutionId = reader.GetInt64(0),
                        PackageName = reader.GetString(1),
                        FolderName = reader.GetString(2),
                        ProjectName = reader.GetString(3),
                        Status = reader.GetString(4),
                        StartTime = reader.GetDateTimeOffset(5).DateTime,
                        EndTime = reader.IsDBNull(6) ? null : reader.GetDateTimeOffset(6).DateTime,
                        Duration = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7))
                    });
                }

                return executions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching executions");
                throw;
            }
        }

        public async Task<List<PackageExecution>> GetLastExecutedPackagesAsync(int count = 10, string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT TOP {count}
                        e.execution_id,
                        e.package_name,
                        e.folder_name,
                        e.project_name,
                        CASE 
                            WHEN e.status = 1 THEN 'Created'
                            WHEN e.status = 2 THEN 'Running'
                            WHEN e.status = 3 THEN 'Canceled'
                            WHEN e.status = 4 THEN 'Failed'
                            WHEN e.status = 5 THEN 'Pending'
                            WHEN e.status = 6 THEN 'Ended Unexpectedly'
                            WHEN e.status = 7 THEN 'Succeeded'
                            WHEN e.status = 8 THEN 'Stopping'
                            WHEN e.status = 9 THEN 'Completed'
                            ELSE 'Unknown'
                        END as Status,
                        e.start_time,
                        e.end_time,
                        DATEDIFF(SECOND, e.start_time, e.end_time) as Duration
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE 1=1
                    {businessUnitFilter}
                    ORDER BY e.start_time DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var executions = new List<PackageExecution>();
                while (await reader.ReadAsync())
                {
                    executions.Add(new PackageExecution
                    {
                        ExecutionId = reader.GetInt64(0),
                        PackageName = reader.GetString(1),
                        FolderName = reader.GetString(2),
                        ProjectName = reader.GetString(3),
                        Status = reader.GetString(4),
                        StartTime = reader.GetDateTimeOffset(5).DateTime,
                        EndTime = reader.IsDBNull(6) ? null : reader.GetDateTimeOffset(6).DateTime,
                        Duration = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7))
                    });
                }

                return executions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching last executed packages");
                throw;
            }
        }

        public async Task<List<CurrentExecution>> GetCurrentExecutionsAsync(string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT 
                        e.execution_id,
                        e.package_name,
                        e.start_time,
                        DATEDIFF(SECOND, e.start_time, GETDATE()) as duration_seconds,
                        e.status,
                        CASE e.status
                            WHEN 1 THEN 'Created'
                            WHEN 2 THEN 'Running'
                            WHEN 3 THEN 'Canceled'
                            WHEN 4 THEN 'Failed'
                            WHEN 5 THEN 'Pending'
                            WHEN 6 THEN 'Ended Unexpectedly'
                            WHEN 7 THEN 'Succeeded'
                            WHEN 8 THEN 'Stopping'
                            WHEN 9 THEN 'Completed'
                            ELSE 'Unknown'
                        END as status_description,
                        e.executed_as_name
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE e.status IN (1, 2, 5, 8)  -- Created, Running, Pending, Stopping
                    {businessUnitFilter}
                    ORDER BY e.start_time DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var currentExecutions = new List<CurrentExecution>();
                while (await reader.ReadAsync())
                {
                    var durationSeconds = Convert.ToInt32(reader.GetValue(3));
                    currentExecutions.Add(new CurrentExecution
                    {
                        ExecutionId = reader.GetInt64(0),
                        PackageName = reader.GetString(1),
                        StartTime = reader.GetDateTime(2),
                        DurationSeconds = durationSeconds,
                        Status = reader.GetInt32(4).ToString(),
                        StatusDescription = reader.GetString(5),
                        ExecutedBy = reader.IsDBNull(6) ? "N/A" : reader.GetString(6),
                        IsLongRunning = durationSeconds > 1800 // 30 minutes
                    });
                }

                return currentExecutions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current executions");
                throw;
            }
        }

        public async Task<List<PackagePerformance>> GetPackagePerformanceStatsAsync(int days = 30, string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT 
                        e.package_name,
                        COUNT(*) as total_executions,
                        SUM(CASE WHEN e.status = 7 THEN 1 ELSE 0 END) as successful_executions,
                        SUM(CASE WHEN e.status = 4 THEN 1 ELSE 0 END) as failed_executions,
                        CAST(SUM(CASE WHEN e.status = 7 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS DECIMAL(5,2)) as success_rate,
                        AVG(DATEDIFF(SECOND, e.start_time, e.end_time)) as avg_duration,
                        MIN(DATEDIFF(SECOND, e.start_time, e.end_time)) as min_duration,
                        MAX(DATEDIFF(SECOND, e.start_time, e.end_time)) as max_duration,
                        MAX(e.start_time) as last_execution_time,
                        (SELECT TOP 1 status FROM [SSISDB].[catalog].[executions] 
                         WHERE package_name = e.package_name 
                         ORDER BY start_time DESC) as last_status
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE e.start_time >= DATEADD(day, -@Days, GETDATE())
                    {businessUnitFilter}
                    GROUP BY e.package_name
                    ORDER BY total_executions DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Days", days);
                using var reader = await command.ExecuteReaderAsync();

                var packagePerformance = new List<PackagePerformance>();
                while (await reader.ReadAsync())
                {
                    packagePerformance.Add(new PackagePerformance
                    {
                        PackageName = reader.GetString(0),
                        TotalExecutions = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                        SuccessfulExecutions = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                        FailedExecutions = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                        SuccessRate = reader.GetDecimal(4),
                        AvgDurationSeconds = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                        MinDurationSeconds = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)),
                        MaxDurationSeconds = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7)),
                        LastExecutionTime = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        LastExecutionStatus = reader.IsDBNull(9) ? "Unknown" : (Convert.ToInt32(reader.GetValue(9)) == 7 ? "Success" : "Failed")
                    });
                }

                return packagePerformance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching package performance stats");
                throw;
            }
        }

        public async Task<List<FailurePattern>> GetFailurePatternsAsync(int days = 30, string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT 
                        e.package_name,
                        COUNT(*) as failure_count,
                        (SELECT TOP 1 em.message 
                         FROM [SSISDB].[catalog].[event_messages] em
                         WHERE em.operation_id = e.execution_id 
                         AND em.message_type = 120
                         ORDER BY em.message_time DESC) as most_common_error,
                        MAX(e.end_time) as last_failure_time,
                        CAST(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM [SSISDB].[catalog].[executions] 
                                                   WHERE package_name = e.package_name 
                                                   AND start_time >= DATEADD(day, -@Days, GETDATE())) AS DECIMAL(5,2)) as failure_rate
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE e.status = 4  -- Failed
                    AND e.start_time >= DATEADD(day, -@Days, GETDATE())
                    {businessUnitFilter}
                    GROUP BY e.package_name
                    HAVING COUNT(*) > 0
                    ORDER BY failure_count DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Days", days);
                using var reader = await command.ExecuteReaderAsync();

                var failurePatterns = new List<FailurePattern>();
                while (await reader.ReadAsync())
                {
                    failurePatterns.Add(new FailurePattern
                    {
                        PackageName = reader.GetString(0),
                        FailureCount = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                        MostCommonError = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                        LastFailureTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        FailureRate = reader.GetDecimal(4)
                    });
                }

                return failurePatterns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching failure patterns");
                throw;
            }
        }

        public async Task<List<ExecutionTimeline>> GetExecutionTimelineAsync(int hours = 24, string? businessUnit = null)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var businessUnitFilter = BusinessUnitHelper.GetBusinessUnitWhereClause(businessUnit);

                var query = $@"
                    SELECT 
                        e.execution_id,
                        e.package_name,
                        e.start_time,
                        e.end_time,
                        DATEDIFF(MINUTE, e.start_time, ISNULL(e.end_time, GETDATE())) as duration_minutes,
                        e.status,
                        CASE e.status
                            WHEN 7 THEN 'success'
                            WHEN 4 THEN 'danger'
                            WHEN 2 THEN 'primary'
                            WHEN 3 THEN 'warning'
                            ELSE 'secondary'
                        END as status_color
                    FROM [SSISDB].[catalog].[executions] e
                    WHERE e.start_time >= DATEADD(hour, -@Hours, GETDATE())
                    {businessUnitFilter}
                    ORDER BY e.start_time DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Hours", hours);
                using var reader = await command.ExecuteReaderAsync();

                var timeline = new List<ExecutionTimeline>();
                while (await reader.ReadAsync())
                {
                    var status = reader.GetInt32(5);
                    var statusText = status switch
                    {
                        1 => "Created",
                        2 => "Running",
                        3 => "Canceled",
                        4 => "Failed",
                        5 => "Pending",
                        6 => "Ended Unexpectedly",
                        7 => "Succeeded",
                        8 => "Stopping",
                        9 => "Completed",
                        _ => "Unknown"
                    };

                    timeline.Add(new ExecutionTimeline
                    {
                        ExecutionId = reader.GetInt64(0),
                        PackageName = reader.GetString(1),
                        StartTime = reader.GetDateTime(2),
                        EndTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        DurationMinutes = Convert.ToInt32(reader.GetValue(4)),
                        Status = statusText,
                        StatusColor = reader.GetString(6)
                    });
                }

                return timeline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching execution timeline");
                throw;
            }
        }
    }
}
