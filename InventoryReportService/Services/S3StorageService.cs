using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using InventoryReportService.Models;
using Microsoft.Extensions.Options;

namespace InventoryReportService.Services;

public class S3StorageService : IS3StorageService
{
    private readonly S3Settings _settings;
    private readonly ILogger<S3StorageService> _logger;
    private readonly Lazy<IAmazonS3> _s3Client;

    public S3StorageService(
        IOptions<S3Settings> settings,
        ILogger<S3StorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _s3Client = new Lazy<IAmazonS3>(() =>
        {
            // Validate configuration
            if (string.IsNullOrWhiteSpace(_settings.EndpointUrl))
                throw new InvalidOperationException("S3 EndpointUrl is not configured");
            if (string.IsNullOrWhiteSpace(_settings.AccessKeyId))
                throw new InvalidOperationException("S3 AccessKeyId is not configured");
            if (string.IsNullOrWhiteSpace(_settings.SecretAccessKey))
                throw new InvalidOperationException("S3 SecretAccessKey is not configured");
            if (string.IsNullOrWhiteSpace(_settings.BucketName))
                throw new InvalidOperationException("S3 BucketName is not configured");

            // Create S3 config for Railway's S3-compatible storage
            var s3Config = new AmazonS3Config
            {
                ServiceURL = _settings.EndpointUrl,
                ForcePathStyle = true, // Required for Railway/S3-compatible storage
                RegionEndpoint = RegionEndpoint.USEast1 // Default, Railway uses "auto" but SDK needs a valid region
            };

            // Parse region if it's not "auto"
            if (!string.IsNullOrWhiteSpace(_settings.Region) && _settings.Region != "auto")
            {
                try
                {
                    s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(_settings.Region);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid region '{Region}', using default", _settings.Region);
                }
            }

            // Create S3 client with credentials
            var credentials = new Amazon.Runtime.BasicAWSCredentials(
                _settings.AccessKeyId,
                _settings.SecretAccessKey);

            return new AmazonS3Client(credentials, s3Config);
        });
    }

    public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType)
    {
        try
        {
            _logger.LogInformation("Uploading file to S3: {FileName} to bucket {BucketName}", fileName, _settings.BucketName);

            var client = _s3Client.Value;

            // Upload the file
            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileName,
                InputStream = new MemoryStream(fileBytes),
                ContentType = contentType
            };

            await client.PutObjectAsync(putRequest);

            _logger.LogInformation("File uploaded successfully: {FileName}", fileName);

            // Generate pre-signed URL (expires in 1 hour)
            var preSignedUrlRequest = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = fileName,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddHours(1)
            };

            var publicUrl = client.GetPreSignedURL(preSignedUrlRequest);

            _logger.LogInformation("Generated pre-signed URL for {FileName}, expires in 1 hour", fileName);

            return publicUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to S3: {FileName}. Endpoint: {Endpoint}, Bucket: {Bucket}",
                fileName, _settings.EndpointUrl, _settings.BucketName);

            // Provide more context in the error message
            if (ex.Message.Contains("InvalidAccessKeyId") || ex.Message.Contains("SignatureDoesNotMatch"))
            {
                throw new InvalidOperationException(
                    $"Failed to upload to S3. Invalid credentials. Check your AccessKeyId and SecretAccessKey.", ex);
            }

            if (ex.Message.Contains("NoSuchBucket"))
            {
                throw new InvalidOperationException(
                    $"Bucket '{_settings.BucketName}' does not exist. Check your S3 configuration.", ex);
            }

            if (ex.Message.Contains("AccessDenied"))
            {
                throw new InvalidOperationException(
                    $"Access denied. Check your S3 credentials and bucket permissions. Bucket: '{_settings.BucketName}'", ex);
            }

            throw;
        }
    }
}

