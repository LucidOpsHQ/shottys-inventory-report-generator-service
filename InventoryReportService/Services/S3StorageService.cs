using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Transform;
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
            // Railway Buckets require virtual-hosted-style URLs:
            // https://bucket-name.storage.railway.app/key
            // When using a custom endpoint (ServiceURL), we should NOT set RegionEndpoint
            // as it causes the SDK to try authenticating against AWS instead of the custom endpoint
            var s3Config = new AmazonS3Config
            {
                ServiceURL = _settings.EndpointUrl.TrimEnd('/'), // Remove trailing slash if present
                ForcePathStyle = false, // Use virtual-hosted-style URLs (Railway requirement)
                // Don't set RegionEndpoint for custom S3-compatible endpoints
                // This prevents the SDK from trying to authenticate against AWS
                UseHttp = false, // Use HTTPS
                DisableHostPrefixInjection = false, // Allow host prefix for virtual-hosted-style
            };

            _logger.LogInformation("S3 Client configured: Endpoint={Endpoint}, Bucket={Bucket}, VirtualHostedStyle={VirtualHostedStyle}, AccessKeyId={AccessKeyId}",
                s3Config.ServiceURL, _settings.BucketName, !s3Config.ForcePathStyle,
                string.IsNullOrEmpty(_settings.AccessKeyId) ? "NOT SET" : $"{_settings.AccessKeyId.Substring(0, Math.Min(8, _settings.AccessKeyId.Length))}...");

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
            // Railway doesn't support server-side encryption or storage classes
            // Don't set these properties at all (setting to null might still add headers)
            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileName,
                InputStream = new MemoryStream(fileBytes),
                ContentType = contentType
                // Intentionally not setting ServerSideEncryptionMethod, StorageClass, etc.
                // to avoid Railway sending "header implies functionality" errors
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
            if (ex.Message.Contains("InvalidAccessKeyId") ||
                ex.Message.Contains("SignatureDoesNotMatch") ||
                ex.Message.Contains("does not exist in our records"))
            {
                throw new InvalidOperationException(
                    $"Failed to upload to S3. Invalid credentials or endpoint configuration issue. " +
                    $"This error usually means: 1) AccessKeyId/SecretAccessKey are incorrect, " +
                    $"2) The SDK is trying to authenticate against AWS instead of Railway endpoint '{_settings.EndpointUrl}', " +
                    $"3) Check that your endpoint URL is correct and doesn't include the bucket name. " +
                    $"Current endpoint: {_settings.EndpointUrl}", ex);
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

            if (ex.Message.Contains("header you provided implies functionality") ||
                ex.Message.Contains("functionality that is not implemented"))
            {
                throw new InvalidOperationException(
                    $"Railway doesn't support a feature the AWS SDK is trying to use. " +
                    $"The SDK may be automatically adding headers for features Railway doesn't support. " +
                    $"Current configuration: Endpoint={_settings.EndpointUrl}, VirtualHostedStyle=true. " +
                    $"Check Railway documentation for supported S3 features.", ex);
            }

            throw;
        }
    }
}

