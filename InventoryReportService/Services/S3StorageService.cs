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

            // Configure the client to use Railway's S3-compatible endpoint
            // Using the exact same configuration that works in the playground
            var s3Config = new AmazonS3Config
            {
                ServiceURL = _settings.EndpointUrl.TrimEnd('/'),
                // ForcePathStyle = true is crucial for Railway's S3-compatible endpoint
                // This stops the SDK from rewriting the URL to bucket.endpoint
                ForcePathStyle = true,
                RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_SUPPORTED,
                ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_SUPPORTED
            };

            _logger.LogInformation("S3 Client configured: Endpoint={Endpoint}, Bucket={Bucket}, PathStyle={PathStyle}, AccessKeyId={AccessKeyId}",
                s3Config.ServiceURL, _settings.BucketName, s3Config.ForcePathStyle,
                string.IsNullOrEmpty(_settings.AccessKeyId) ? "NOT SET" : $"{_settings.AccessKeyId.Substring(0, Math.Min(8, _settings.AccessKeyId.Length))}...");

            // Create credentials and client
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

            // Create the Put Request - using minimal configuration like in the playground
            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = fileName,
                InputStream = new MemoryStream(fileBytes),
                ContentType = contentType
                // Intentionally NOT setting:
                // - ServerSideEncryptionMethod (Railway doesn't support it)
                // - StorageClass (Railway uses standard tier)
                // - Any other features Railway might not support
            };

            var response = await client.PutObjectAsync(putRequest);

            _logger.LogInformation("File uploaded successfully: {FileName}, RequestId={RequestId}",
                fileName, response.ResponseMetadata.RequestId);

            // Generate pre-signed URL (expires in 1 hour)
            var presignedRequest = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = fileName,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddHours(1)
            };

            var presignedUrl = client.GetPreSignedURL(presignedRequest);

            _logger.LogInformation("Generated pre-signed URL for {FileName}, expires in 1 hour", fileName);

            return presignedUrl;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 Error uploading file: {FileName}. ErrorCode={ErrorCode}, StatusCode={StatusCode}, Message={Message}",
                fileName, ex.ErrorCode, ex.StatusCode, ex.Message);

            // Provide more context in the error message
            if (ex.ErrorCode == "InvalidAccessKeyId" || ex.ErrorCode == "SignatureDoesNotMatch")
            {
                throw new InvalidOperationException(
                    $"Failed to upload to S3. Invalid credentials. Check your AccessKeyId and SecretAccessKey.", ex);
            }

            if (ex.ErrorCode == "NoSuchBucket")
            {
                throw new InvalidOperationException(
                    $"Bucket '{_settings.BucketName}' does not exist. Check your S3 configuration.", ex);
            }

            if (ex.ErrorCode == "AccessDenied")
            {
                throw new InvalidOperationException(
                    $"Access denied. Check your S3 credentials and bucket permissions. Bucket: '{_settings.BucketName}'", ex);
            }

            throw new InvalidOperationException(
                $"S3 error uploading file: {ex.ErrorCode} - {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to S3: {FileName}. Endpoint: {Endpoint}, Bucket: {Bucket}",
                fileName, _settings.EndpointUrl, _settings.BucketName);
            throw;
        }
    }
}
