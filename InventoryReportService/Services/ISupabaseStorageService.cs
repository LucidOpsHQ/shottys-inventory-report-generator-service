namespace InventoryReportService.Services;

public interface ISupabaseStorageService
{
    Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType);
}
