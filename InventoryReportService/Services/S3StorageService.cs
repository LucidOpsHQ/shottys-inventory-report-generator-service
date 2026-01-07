using System.Security.Cryptography;
using System.Text;
using System.Web;
using InventoryReportService.Models;
using Microsoft.Extensions.Options;

namespace InventoryReportService.Services;

public class S3StorageService : IS3StorageService
{
    private readonly S3Settings _settings;
    private readonly ILogger<S3StorageService> _logger;
    private readonly HttpClient _httpClient;

    public S3StorageService(
        IOptions<S3Settings> settings,
        ILogger<S3StorageService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;

        // Validate configuration
        if (string.IsNullOrWhiteSpace(_settings.EndpointUrl))
            throw new InvalidOperationException("S3 EndpointUrl is not configured");
        if (string.IsNullOrWhiteSpace(_settings.AccessKeyId))
            throw new InvalidOperationException("S3 AccessKeyId is not configured");
        if (string.IsNullOrWhiteSpace(_settings.SecretAccessKey))
            throw new InvalidOperationException("S3 SecretAccessKey is not configured");
        if (string.IsNullOrWhiteSpace(_settings.BucketName))
            throw new InvalidOperationException("S3 BucketName is not configured");

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        _logger.LogInformation("S3 Storage Service configured: Endpoint={Endpoint}, Bucket={Bucket}",
            _settings.EndpointUrl, _settings.BucketName);
    }

    public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType)
    {
        try
        {
            _logger.LogInformation("Uploading file to S3: {FileName} to bucket {BucketName}", fileName, _settings.BucketName);

            // Railway uses virtual-hosted-style URLs: https://bucket-name.storage.railway.app/key
            var baseUrl = _settings.EndpointUrl.TrimEnd('/');
            var bucketHost = $"{_settings.BucketName}.{baseUrl.Replace("https://", "").Replace("http://", "")}";
            var url = $"https://{bucketHost}/{Uri.EscapeDataString(fileName)}";

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new ByteArrayContent(fileBytes)
            };

            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            request.Content.Headers.ContentLength = fileBytes.Length;

            // Sign the request using AWS Signature Version 4
            SignRequest(request, "PUT", fileName, fileBytes, contentType);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("S3 upload failed: Status={Status}, Response={Response}",
                    response.StatusCode, errorContent);

                throw new InvalidOperationException(
                    $"Failed to upload file to S3. Status: {response.StatusCode}, Response: {errorContent}");
            }

            _logger.LogInformation("File uploaded successfully: {FileName}", fileName);

            // Generate pre-signed URL (expires in 1 hour)
            var presignedUrl = GeneratePresignedUrl(fileName, TimeSpan.FromHours(1));

            _logger.LogInformation("Generated pre-signed URL for {FileName}, expires in 1 hour", fileName);

            return presignedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to S3: {FileName}. Endpoint: {Endpoint}, Bucket: {Bucket}",
                fileName, _settings.EndpointUrl, _settings.BucketName);
            throw;
        }
    }

    private void SignRequest(HttpRequestMessage request, string method, string key, byte[]? body, string? contentType)
    {
        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd");
        var timeStamp = now.ToString("yyyyMMddTHHmmssZ");
        var region = _settings.Region == "auto" ? "us-east-1" : _settings.Region;
        var service = "s3";

        // Parse the URL to get host
        var uri = request.RequestUri!;
        var host = uri.Host;

        // Set required headers
        request.Headers.Add("Host", host);
        request.Headers.Add("x-amz-date", timeStamp);
        if (contentType != null)
        {
            request.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        // Create canonical request
        var canonicalHeaders = $"host:{host}\nx-amz-date:{timeStamp}\n";
        var signedHeaders = "host;x-amz-date";
        var payloadHash = body != null ? ComputeSha256Hash(body) : ComputeSha256Hash(Array.Empty<byte>());

        var canonicalRequest = $"{method}\n" +
                              $"/{Uri.EscapeDataString(key)}\n" +
                              $"\n" + // Query string (empty for PUT)
                              $"{canonicalHeaders}\n" +
                              $"{signedHeaders}\n" +
                              $"{payloadHash}";

        // Create string to sign
        var algorithm = "AWS4-HMAC-SHA256";
        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = $"{algorithm}\n{timeStamp}\n{credentialScope}\n{ComputeSha256Hash(Encoding.UTF8.GetBytes(canonicalRequest))}";

        // Calculate signature
        var kSecret = Encoding.UTF8.GetBytes($"AWS4{_settings.SecretAccessKey}");
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        var kSigning = HmacSha256(kService, "aws4_request");
        var signature = BitConverter.ToString(HmacSha256(kSigning, stringToSign)).Replace("-", "").ToLowerInvariant();

        // Create authorization header
        var authHeader = $"{algorithm} Credential={_settings.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.Add("Authorization", authHeader);
    }

    private string GeneratePresignedUrl(string key, TimeSpan expiration)
    {
        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd");
        var timeStamp = now.ToString("yyyyMMddTHHmmssZ");
        var region = _settings.Region == "auto" ? "us-east-1" : _settings.Region;
        var service = "s3";

        // Railway uses virtual-hosted-style URLs
        var baseUrl = _settings.EndpointUrl.TrimEnd('/');
        var bucketHost = $"{_settings.BucketName}.{baseUrl.Replace("https://", "").Replace("http://", "")}";
        var url = $"https://{bucketHost}/{Uri.EscapeDataString(key)}";

        // Create query parameters for pre-signed URL
        var queryParams = new Dictionary<string, string>
        {
            { "X-Amz-Algorithm", "AWS4-HMAC-SHA256" },
            { "X-Amz-Credential", $"{_settings.AccessKeyId}/{dateStamp}/{region}/{service}/aws4_request" },
            { "X-Amz-Date", timeStamp },
            { "X-Amz-Expires", ((int)expiration.TotalSeconds).ToString() },
            { "X-Amz-SignedHeaders", "host" }
        };

        // Build canonical query string
        var sortedParams = queryParams.OrderBy(p => p.Key).ToList();
        var queryString = string.Join("&", sortedParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        // Create canonical request for signing
        var host = bucketHost;
        var canonicalHeaders = $"host:{host}\n";
        var signedHeaders = "host";
        var payloadHash = "UNSIGNED-PAYLOAD";

        var canonicalRequest = $"GET\n" +
                              $"/{Uri.EscapeDataString(key)}\n" +
                              $"{queryString}\n" +
                              $"{canonicalHeaders}\n" +
                              $"{signedHeaders}\n" +
                              $"{payloadHash}";

        // Create string to sign
        var algorithm = "AWS4-HMAC-SHA256";
        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = $"{algorithm}\n{timeStamp}\n{credentialScope}\n{ComputeSha256Hash(Encoding.UTF8.GetBytes(canonicalRequest))}";

        // Calculate signature
        var kSecret = Encoding.UTF8.GetBytes($"AWS4{_settings.SecretAccessKey}");
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        var kSigning = HmacSha256(kService, "aws4_request");
        var signature = BitConverter.ToString(HmacSha256(kSigning, stringToSign)).Replace("-", "").ToLowerInvariant();

        // Add signature to query string
        queryParams["X-Amz-Signature"] = signature;
        sortedParams = queryParams.OrderBy(p => p.Key).ToList();
        queryString = string.Join("&", sortedParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{url}?{queryString}";
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ComputeSha256Hash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
