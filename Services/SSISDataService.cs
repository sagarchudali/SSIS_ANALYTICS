using Microsoft.Data.SqlClient;
using SSISAnalyticsDashboard.Models;
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

        public async Task<ExecutionMetrics> GetMetricsAsync()
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        COUNT(*) as TotalExecutions,
                        SUM(CASE WHEN status = 4 THEN 1 ELSE 0 END) as FailedExecutions,
                        SUM(CASE WHEN status = 7 THEN 1 ELSE 0 END) as SuccessfulExecutions,
                        AVG(DATEDIFF(SECOND, start_time, end_time)) as AvgDuration
                    FROM [SSISDB].[catalog].[executions]
                    WHERE start_time >= DATEADD(day, -30, GETDATE())";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var totalExecutions = reader.GetInt32(0);
                    var failedExecutions = reader.GetInt32(1);
                    var successfulExecutions = reader.GetInt32(2);
                    var avgDuration = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

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

        public async Task<List<ExecutionTrend>> GetTrendsAsync()
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        CAST(start_time AS DATE) as Date,
                        SUM(CASE WHEN status = 7 THEN 1 ELSE 0 END) as Success,
                        SUM(CASE WHEN status = 4 THEN 1 ELSE 0 END) as Failed,
                        AVG(DATEDIFF(SECOND, start_time, end_time)) as AvgDuration
                    FROM [SSISDB].[catalog].[executions]
                    WHERE start_time >= DATEADD(day, -30, GETDATE())
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
                        Success = reader.GetInt32(1),
                        Failed = reader.GetInt32(2),
                        AvgDuration = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
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

        public async Task<List<ErrorLog>> GetErrorsAsync()
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT TOP 50
                        e.execution_id,
                        e.package_name,
                        em.message_time,
                        em.message_source_type,
                        em.message
                    FROM [SSISDB].[catalog].[executions] e
                    INNER JOIN [SSISDB].[catalog].[event_messages] em 
                        ON e.execution_id = em.execution_id
                    WHERE em.message_type = 120
                        AND e.start_time >= DATEADD(day, -30, GETDATE())
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
                        ErrorCode = reader.GetInt16(3),
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

        public async Task<List<PackageExecution>> GetExecutionsAsync()
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = @"
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
                        Duration = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
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

        public async Task<List<PackageExecution>> GetLastExecutedPackagesAsync(int count = 10)
        {
            try
            {
                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

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
                        Duration = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
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
    }
}
