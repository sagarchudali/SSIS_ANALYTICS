using Microsoft.AspNetCore.Mvc;
using SSISAnalyticsDashboard.Models;
using SSISAnalyticsDashboard.Services;

namespace SSISAnalyticsDashboard.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ISSISDataService _dataService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ISSISDataService dataService, ILogger<DashboardController> logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Check if connection string exists in session
            var connectionString = HttpContext.Session.GetString("SSISDBConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                // Redirect to ServerConfig if not configured
                return RedirectToAction("Index", "ServerConfig");
            }

            try
            {
                var viewModel = new DashboardViewModel
                {
                    Metrics = await _dataService.GetMetricsAsync(),
                    Trends = await _dataService.GetTrendsAsync(),
                    RecentErrors = await _dataService.GetErrorsAsync(),
                    RecentExecutions = await _dataService.GetExecutionsAsync(),
                    LastExecutedPackages = await _dataService.GetLastExecutedPackagesAsync(10),
                    CurrentExecutions = await _dataService.GetCurrentExecutionsAsync(),
                    PackagePerformanceStats = await _dataService.GetPackagePerformanceStatsAsync(30),
                    FailurePatterns = await _dataService.GetFailurePatternsAsync(30),
                    ExecutionTimeline = await _dataService.GetExecutionTimelineAsync(24)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                ViewBag.ErrorMessage = "Failed to load dashboard data. Please check your database connection.";
                return View(new DashboardViewModel());
            }
        }

        // API endpoints for AJAX refresh
        [HttpGet]
        public async Task<IActionResult> GetMetrics()
        {
            // Check if connection string exists in session
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SSISDBConnection")))
            {
                return Unauthorized(new { error = "Not configured" });
            }

            try
            {
                var metrics = await _dataService.GetMetricsAsync();
                return Json(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metrics");
                return StatusCode(500, new { error = "Failed to fetch metrics" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTrends()
        {
            // Check if connection string exists in session
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SSISDBConnection")))
            {
                return Unauthorized(new { error = "Not configured" });
            }

            try
            {
                var trends = await _dataService.GetTrendsAsync();
                return Json(trends);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching trends");
                return StatusCode(500, new { error = "Failed to fetch trends" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLastExecutedPackages()
        {
            // Check if connection string exists in session
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SSISDBConnection")))
            {
                return Unauthorized(new { error = "Not configured" });
            }

            try
            {
                var packages = await _dataService.GetLastExecutedPackagesAsync(10);
                return Json(packages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching last executed packages");
                return StatusCode(500, new { error = "Failed to fetch last executed packages" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentExecutions()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SSISDBConnection")))
            {
                return Unauthorized(new { error = "Not configured" });
            }

            try
            {
                var currentExecutions = await _dataService.GetCurrentExecutionsAsync();
                return Json(currentExecutions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current executions");
                return StatusCode(500, new { error = "Failed to fetch current executions" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPackagePerformance()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SSISDBConnection")))
            {
                return Unauthorized(new { error = "Not configured" });
            }

            try
            {
                var performance = await _dataService.GetPackagePerformanceStatsAsync(30);
                return Json(performance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching package performance");
                return StatusCode(500, new { error = "Failed to fetch package performance" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFailurePatterns()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SSISDBConnection")))
            {
                return Unauthorized(new { error = "Not configured" });
            }

            try
            {
                var patterns = await _dataService.GetFailurePatternsAsync(30);
                return Json(patterns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching failure patterns");
                return StatusCode(500, new { error = "Failed to fetch failure patterns" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetExecutionTimeline()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SSISDBConnection")))
            {
                return Unauthorized(new { error = "Not configured" });
            }

            try
            {
                var timeline = await _dataService.GetExecutionTimelineAsync(24);
                return Json(timeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching execution timeline");
                return StatusCode(500, new { error = "Failed to fetch execution timeline" });
            }
        }
    }
}
