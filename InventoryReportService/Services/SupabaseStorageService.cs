using InventoryReportService.Models;
using Microsoft.Extensions.Options;
using Supabase;
using Supabase.Storage;

namespace InventoryReportService.Services;

public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly SupabaseSettings _settings;
    private readonly ILogger<SupabaseStorageService> _logger;
    private readonly Lazy<Supabase.Client> _supabaseClient;

    public SupabaseStorageService(
        IOptions<SupabaseSettings> settings,
        ILogger<SupabaseStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _supabaseClient = new Lazy<Supabase.Client>(() =>
        {
            var options = new SupabaseOptions
            {
                AutoConnectRealtime = false,
                AutoRefreshToken = false
            };

            return new Supabase.Client(_settings.Url, _settings.AnonKey, options);
        });
    }

    public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType)
    {
        try
        {
            _logger.LogInformation("Uploading file to Supabase: {FileName}", fileName);

            var client = _supabaseClient.Value;
            
            // Ensure the client is initialized
            await client.InitializeAsync();

            // Get the storage bucket
            var storage = client.Storage.From(_settings.BucketName);

            // Upload the file
            using var stream = new MemoryStream(fileBytes);
            var fileOptions = new Supabase.Storage.FileOptions
            {
                ContentType = contentType,
                Upsert = false // Don't overwrite existing files
            };

            await storage.Upload(fileBytes, fileName, fileOptions);

            // Get the public URL using Supabase's built-in method
            var publicUrl = storage.GetPublicUrl(fileName);

            _logger.LogInformation("File uploaded successfully: {FileName}, URL: {Url}", fileName, publicUrl);

            return publicUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Supabase: {FileName}", fileName);
            throw;
        }
    }
}
