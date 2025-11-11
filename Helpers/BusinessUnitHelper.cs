namespace SSISAnalyticsDashboard.Helpers
{
    public static class BusinessUnitHelper
    {
        public static string GetBusinessUnit(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return "Uncategorized";

            if (packageName.StartsWith("CR_", StringComparison.OrdinalIgnoreCase))
                return "ClientRepo";
            
            if (packageName.StartsWith("CN_", StringComparison.OrdinalIgnoreCase))
                return "ChartNav";
            
            if (packageName.StartsWith("EDS_", StringComparison.OrdinalIgnoreCase))
                return "EDS";
            
            if (packageName.StartsWith("HIM_", StringComparison.OrdinalIgnoreCase))
                return "HIM";

            return "Uncategorized";
        }

        public static string GetBusinessUnitWhereClause(string? businessUnit)
        {
            if (string.IsNullOrEmpty(businessUnit))
                return "";

            return businessUnit.ToUpper() switch
            {
                "CLIENTREPO" => "AND e.package_name LIKE 'CR_%'",
                "CHARTNAV" => "AND e.package_name LIKE 'CN_%'",
                "EDS" => "AND e.package_name LIKE 'EDS_%'",
                "HIM" => "AND e.package_name LIKE 'HIM_%'",
                "UNCATEGORIZED" => "AND e.package_name NOT LIKE 'CR_%' AND e.package_name NOT LIKE 'CN_%' AND e.package_name NOT LIKE 'EDS_%' AND e.package_name NOT LIKE 'HIM_%'",
                _ => ""
            };
        }
    }
}
