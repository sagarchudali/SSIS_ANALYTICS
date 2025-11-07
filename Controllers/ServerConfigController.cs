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
        public async Task<IActionResult> Index(ServerConfigViewModel model)
        {
            // Check if this is an AJAX request
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (!ModelState.IsValid)
            {
                if (isAjax)
                    return Json(new { success = false, message = "Validation failed" });
                return View(model);
            }

            // Validate SQL Auth credentials
            if (model.AuthenticationMode == "SQL" && (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password)))
            {
                string errorMsg = "Username and Password are required for SQL Server Authentication.";
                if (isAjax)
                    return Json(new { success = false, message = errorMsg });
                model.ErrorMessage = errorMsg;
                return View(model);
            }

            try
            {
                // Build connection string based on authentication mode
                string connectionString;
                if (model.AuthenticationMode == "Windows")
                {
                    connectionString = $"Server={model.ServerName};Database=SSISDB;Integrated Security=true;TrustServerCertificate=true;";
                }
                else
                {
                    connectionString = $"Server={model.ServerName};Database=SSISDB;User Id={model.Username};Password={model.Password};TrustServerCertificate=true;";
                }

                // Test connection if requested
                if (model.TestConnection)
                {
                    using var connection = new SqlConnection(connectionString);
                    await connection.OpenAsync();
                    _logger.LogInformation($"Successfully connected to {model.ServerName} using {model.AuthenticationMode} Authentication");
                }

                // Update BOTH appsettings.json files (root and bin/Debug)
                // 1. Update the root appsettings.json
                var rootAppSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
                await UpdateAppSettingsFile(rootAppSettingsPath, connectionString);
                _logger.LogInformation($"Updated root appsettings.json at: {rootAppSettingsPath}");
                
                // 2. Update the bin/Debug appsettings.json (the one actually used by the running app)
                var binAppSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                await UpdateAppSettingsFile(binAppSettingsPath, connectionString);
                _logger.LogInformation($"Updated bin appsettings.json at: {binAppSettingsPath}");

                // Reload configuration
                var configRoot = (IConfigurationRoot)_configuration;
                configRoot.Reload();

                // Set session flag to bypass middleware check on next request
                HttpContext.Session.SetString("ConfigJustSaved", "true");

                TempData["SuccessMessage"] = $"Successfully configured server: {model.ServerName} with {model.AuthenticationMode} Authentication";
                TempData["ShowSuccessAlert"] = "true";
                
                // Return JSON for AJAX requests, redirect for normal requests
                if (isAjax)
                {
                    return Json(new { 
                        success = true, 
                        message = $"Successfully configured server: {model.ServerName} with {model.AuthenticationMode} Authentication",
                        serverName = model.ServerName,
                        authMode = model.AuthenticationMode
                    });
                }
                
                // Use absolute URL to avoid middleware redirect loop
                return Redirect("/Dashboard/Index");
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database connection failed");
                string errorMsg = $"Failed to connect to server: {ex.Message}";
                if (isAjax)
                    return Json(new { success = false, message = errorMsg });
                model.ErrorMessage = errorMsg;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration failed");
                string errorMsg = $"Configuration error: {ex.Message}";
                if (isAjax)
                    return Json(new { success = false, message = errorMsg });
                model.ErrorMessage = errorMsg;
                return View(model);
            }
        }

        private async Task UpdateAppSettingsFile(string filePath, string connectionString)
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var jsonObj = JsonDocument.Parse(json);
            
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                
                foreach (var property in jsonObj.RootElement.EnumerateObject())
                {
                    if (property.Name == "ConnectionStrings")
                    {
                        writer.WriteStartObject("ConnectionStrings");
                        writer.WriteString("SSISDBConnection", connectionString);
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }
                
                writer.WriteEndObject();
            }

            await System.IO.File.WriteAllBytesAsync(filePath, stream.ToArray());
        }
    }
}

