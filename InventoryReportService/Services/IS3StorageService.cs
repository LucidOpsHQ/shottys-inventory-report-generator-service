namespace InventoryReportService.Services;

public interface IS3StorageService
{
    Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType);
}

