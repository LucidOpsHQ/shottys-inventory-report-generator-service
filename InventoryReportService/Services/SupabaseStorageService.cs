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

            // Prefer service_role key for backend operations (bypasses RLS)
            // Otherwise use anon key
            var apiKey = !string.IsNullOrWhiteSpace(_settings.ServiceRoleKey) 
                ? _settings.ServiceRoleKey 
                : _settings.AnonKey;

            return new Supabase.Client(_settings.Url, apiKey, options);
        });
    }

    public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType)
    {
        try
        {
            // Validate configuration
            if (string.IsNullOrWhiteSpace(_settings.Url))
                throw new InvalidOperationException("Supabase URL is not configured");
            if (string.IsNullOrWhiteSpace(_settings.AnonKey) && string.IsNullOrWhiteSpace(_settings.ServiceRoleKey))
                throw new InvalidOperationException("Either Supabase AnonKey or ServiceRoleKey must be configured");
            if (string.IsNullOrWhiteSpace(_settings.BucketName))
                throw new InvalidOperationException("Supabase BucketName is not configured");

            _logger.LogInformation("Uploading file to Supabase: {FileName} to bucket {BucketName}", fileName, _settings.BucketName);

            var client = _supabaseClient.Value;
            
            // Ensure the client is initialized
            await client.InitializeAsync();

            // Get the storage bucket
            var storage = client.Storage.From(_settings.BucketName);

            // Upload the file - using byte array directly
            var fileOptions = new Supabase.Storage.FileOptions
            {
                ContentType = contentType,
                Upsert = true // Allow overwriting to avoid conflicts
            };

            _logger.LogInformation("Calling Supabase Upload with file size: {Size} bytes", fileBytes.Length);
            
            await storage.Upload(fileBytes, fileName, fileOptions);

            // Get the public URL using Supabase's built-in method
            var publicUrl = storage.GetPublicUrl(fileName);

            _logger.LogInformation("File uploaded successfully: {FileName}, URL: {Url}", fileName, publicUrl);

            return publicUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Supabase: {FileName}. URL: {Url}, Bucket: {Bucket}", 
                fileName, _settings.Url, _settings.BucketName);
            
            // Provide more context in the error message
            if (ex.Message.Contains("JsonReaderException") || ex.Message.Contains("Unexpected character"))
            {
                throw new InvalidOperationException(
                    $"Failed to upload to Supabase. This usually indicates: 1) Bucket '{_settings.BucketName}' doesn't exist, 2) Invalid API key, 3) Invalid URL, or 4) Bucket permissions issue. Check your Supabase configuration.", ex);
            }
            
            if (ex.Message.Contains("row-level security policy") || ex.Message.Contains("RLS"))
            {
                throw new InvalidOperationException(
                    $"Row Level Security (RLS) policy violation. For backend services, use ServiceRoleKey instead of AnonKey to bypass RLS. " +
                    $"Alternatively, configure your bucket's RLS policies to allow uploads. Bucket: '{_settings.BucketName}'", ex);
            }
            
            if (ex.Message.Contains("Invalid Compact JWS") || ex.Message.Contains("JWT") || ex.Message.Contains("JWS"))
            {
                throw new InvalidOperationException(
                    $"Invalid API key format. The ServiceRoleKey must be a JWT token (similar format to AnonKey with three parts separated by dots). " +
                    $"You may have copied an S3 secret key instead. " +
                    $"Get your ServiceRoleKey from Supabase Dashboard → Settings → API → service_role key. " +
                    $"The key should look like: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", ex);
            }
            
            throw;
        }
    }
}
