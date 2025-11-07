using System.ComponentModel.DataAnnotations;

namespace SSISAnalyticsDashboard.Models
{
    public class ServerConfigViewModel
    {
        [Required(ErrorMessage = "Authentication mode is required")]
        [Display(Name = "Authentication Mode")]
        public string AuthenticationMode { get; set; } = "Windows";

        [Required(ErrorMessage = "Server name is required")]
        [Display(Name = "SQL Server Name")]
        public string ServerName { get; set; } = string.Empty;

        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Display(Name = "Password")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Display(Name = "Test Connection")]
        public bool TestConnection { get; set; } = true;

        public string? ErrorMessage { get; set; }
        public bool IsConfigured { get; set; }
    }
}
