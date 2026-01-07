namespace InventoryReportService.Models;

public class S3Settings
{
    public string EndpointUrl { get; set; } = string.Empty;
    public string Region { get; set; } = "auto";
    public string BucketName { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
}

