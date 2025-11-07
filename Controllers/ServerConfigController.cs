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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validate SQL Auth credentials
            if (model.AuthenticationMode == "SQL" && (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password)))
            {
                model.ErrorMessage = "Username and Password are required for SQL Server Authentication.";
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

                // Update appsettings.json
                var appSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
                var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
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

                await System.IO.File.WriteAllBytesAsync(appSettingsPath, stream.ToArray());

                // Reload configuration
                var configRoot = (IConfigurationRoot)_configuration;
                configRoot.Reload();

                TempData["SuccessMessage"] = $"Successfully configured server: {model.ServerName} with {model.AuthenticationMode} Authentication";
                
                // Use absolute URL to avoid middleware redirect loop
                return Redirect("/Dashboard/Index");
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database connection failed");
                model.ErrorMessage = $"Failed to connect to server: {ex.Message}";
                return View(model);
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
