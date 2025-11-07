using System.ComponentModel.DataAnnotations;

namespace SSISAnalyticsDashboard.Models
{
    public class ServerConfigViewModel
    {
        [Required(ErrorMessage = "Server name is required")]
        [Display(Name = "SQL Server Name")]
        public string ServerName { get; set; } = string.Empty;

        [Display(Name = "Test Connection")]
        public bool TestConnection { get; set; } = true;

        public string? ErrorMessage { get; set; }
        public bool IsConfigured { get; set; }
    }
}
