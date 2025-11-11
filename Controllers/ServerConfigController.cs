using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SSISAnalyticsDashboard.Models;
using System.Text.Json;

namespace SSISAnalyticsDashboard.Controllers
{
    public class ServerConfigController : Controller
    {
        private readonly ILogger<ServerConfigController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ServerConfigController(
            ILogger<ServerConfigController> _logger,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            this._logger = _logger;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var connectionString = _configuration.GetConnectionString("SSISDBConnection");
            var model = new ServerConfigViewModel
            {
                IsConfigured = !string.IsNullOrEmpty(connectionString) && 
                              !connectionString.Contains("your-server-name"),
                AuthenticationMode = "Windows" // Default to Windows Auth
            };

            if (model.IsConfigured)
            {
                // Extract server name from connection string
                var builder = new SqlConnectionStringBuilder(connectionString);
                model.ServerName = builder.DataSource;
                model.AuthenticationMode = builder.IntegratedSecurity ? "Windows" : "SQL";
                if (!builder.IntegratedSecurity)
                {
                    model.Username = builder.UserID;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(ServerConfigViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Only handle Windows Authentication for now
                if (model.AuthenticationMode != "Windows")
                {
                    model.ErrorMessage = "Only Windows Authentication is supported at this time.";
                    return View(model);
                }

                // Build connection string for Windows Authentication
                string connectionString = $"Server={model.ServerName};Database=SSISDB;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;";

                // Store connection string in session instead of appsettings.json
                HttpContext.Session.SetString("SSISDBConnection", connectionString);
                HttpContext.Session.SetString("ServerName", model.ServerName);
                
                _logger.LogInformation($"Connection string stored in session for server: {model.ServerName}");
                
                // Redirect directly to Dashboard
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration failed");
                model.ErrorMessage = $"Configuration error: {ex.Message}";
                return View(model);
            }
        }
    }
}

