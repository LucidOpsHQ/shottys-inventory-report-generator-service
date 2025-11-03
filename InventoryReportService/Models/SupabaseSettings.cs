namespace InventoryReportService.Models;

public class SupabaseSettings
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
    public string? ServiceRoleKey { get; set; }
    public string BucketName { get; set; } = string.Empty;
}
