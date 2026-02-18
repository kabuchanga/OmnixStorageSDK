using OmnixStorage;
using OmnixStorage.Args;

namespace EdgeSentience.Storage;

/// <summary>
/// EdgeSentience-specific storage service with proper dual-client pattern for presigned URLs.
/// This class ensures that presigned URLs are generated using the PUBLIC endpoint, making them
/// accessible from browsers and external clients.
/// </summary>
public class EdgeSentienceStorageService
{
    private readonly IOmnixStorageClient _internalClient;
    private readonly IOmnixStorageClient _publicClient;
    private readonly string _defaultBucket;
    private readonly string _publicEndpoint;

    /// <summary>
    /// Initialize EdgeSentience storage service with dual-client pattern for secure presigned URLs.
    /// </summary>
    /// <param name="internalEndpoint">Internal endpoint (e.g., "storage.kegeosapps.com:443")</param>
    /// <param name="publicEndpoint">Public endpoint accessible from browsers (e.g., "https://storage-public.kegeosapps.com")</param>
    /// <param name="accessKey">S3-compatible access key</param>
    /// <param name="secretKey">S3-compatible secret key</param>
    /// <param name="region">AWS region (default: "us-east-1")</param>
    /// <param name="defaultBucket">Default bucket for operations (default: "edge-sentience-data")</param>
    public EdgeSentienceStorageService(
        string internalEndpoint,
        string publicEndpoint,
        string accessKey,
        string secretKey,
        string region = "us-east-1",
        string defaultBucket = "edge-sentience-data")
    {
        // Create internal client for S3 operations (service-to-service)
        var parts = internalEndpoint.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 443;

        _internalClient = new OmnixStorageClientBuilder()
            .WithEndpoint(host, port, useSSL: true)
            .WithCredentials(accessKey, secretKey)
            .WithRegion(region)
            .Build();

        // Create public client for browser-safe presigned URLs
        _publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
            publicEndpoint,
            accessKey,
            secretKey,
            region
        );

        _defaultBucket = defaultBucket;
        _publicEndpoint = publicEndpoint;
    }

    /// <summary>
    /// Upload file using internal client (backend-only operation).
    /// </summary>
    /// <param name="objectName">Storage object path (e.g., "tenants/tenant1/files/document.pdf")</param>
    /// <param name="filePath">Local file path to upload</param>
    /// <param name="contentType">MIME type (default: "application/octet-stream")</param>
    /// <returns>Storage object path</returns>
    public async Task<string> UploadFileAsync(string objectName, string filePath, string contentType = "application/octet-stream")
    {
        using var stream = File.OpenRead(filePath);
        var fileInfo = new FileInfo(filePath);
        
        await _internalClient.PutObjectAsync(
            _defaultBucket,
            objectName,
            stream,
            contentType
        );
        
        return objectName;
    }

    /// <summary>
    /// Upload file from stream using internal client.
    /// </summary>
    public async Task<string> UploadStreamAsync(string objectName, Stream fileStream, long fileSize, string contentType = "application/octet-stream")
    {
        await _internalClient.PutObjectAsync(
            _defaultBucket,
            objectName,
            fileStream,
            contentType
        );
        
        return objectName;
    }

    /// <summary>
    /// Get browser-accessible download URL using PUBLIC endpoint.
    /// This URL can be shared with external users, web browsers, and client apps.
    /// </summary>
    /// <param name="objectName">Storage object path</param>
    /// <param name="expiryInSeconds">URL validity period in seconds (max 604800 = 7 days)</param>
    /// <returns>Presigned GET URL accessible from browsers</returns>
    public async Task<string> GetDownloadUrlAsync(string objectName, int expiryInSeconds = 3600)
    {
        if (expiryInSeconds > 604800)
            throw new ArgumentException("Expiry cannot exceed 604800 seconds (7 days)", nameof(expiryInSeconds));

        // ✓ Use PUBLIC client for browser-accessible URLs
        var result = await _publicClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(_defaultBucket)
                .WithObject(objectName)
                .WithExpiry(expiryInSeconds)
        );

        return result.Url;
    }

    /// <summary>
    /// Get browser-accessible upload URL using PUBLIC endpoint.
    /// This allows external clients to upload files directly without credentials.
    /// </summary>
    /// <param name="objectName">Storage object path where file will be uploaded</param>
    /// <param name="expiryInSeconds">URL validity period in seconds (default: 1800 = 30 minutes)</param>
    /// <param name="contentType">Optional: Restrict upload to specific MIME type</param>
    /// <returns>Presigned PUT URL accessible from browsers</returns>
    public async Task<string> GetUploadUrlAsync(string objectName, int expiryInSeconds = 1800, string? contentType = null)
    {
        if (expiryInSeconds > 604800)
            throw new ArgumentException("Expiry cannot exceed 604800 seconds (7 days)", nameof(expiryInSeconds));

        // ✓ Use PUBLIC client for browser-accessible URLs
        var args = new PresignedPutObjectArgs()
            .WithBucket(_defaultBucket)
            .WithObject(objectName)
            .WithExpiry(expiryInSeconds);

        if (!string.IsNullOrEmpty(contentType))
        {
            args = args.WithContentType(contentType);
        }

        var result = await _publicClient.PresignedPutObjectAsync(args);
        return result.Url;
    }

    /// <summary>
    /// Delete file using internal client (backend-only operation).
    /// </summary>
    /// <param name="objectName">Storage object path to delete</param>
    /// <param name="ignoreNotFound">If true, silently succeeds even if object doesn't exist</param>
    public async Task DeleteFileAsync(string objectName, bool ignoreNotFound = false)
    {
        try
        {
            await _internalClient.RemoveObjectAsync(_defaultBucket, objectName);
        }
        catch (Exception ex) when (ignoreNotFound && ex.Message.Contains("NoSuchKey"))
        {
            // Object doesn't exist - that's OK
        }
    }

    /// <summary>
    /// Check if object exists using internal client.
    /// </summary>
    public async Task<bool> ObjectExistsAsync(string objectName)
    {
        try
        {
            await _internalClient.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(_defaultBucket)
                    .WithObject(objectName)
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get object metadata (size, ETag, last modified) using internal client.
    /// </summary>
    public async Task<(long Size, string? ETag, DateTime Modified)> GetObjectMetadataAsync(string objectName)
    {
        var stat = await _internalClient.StatObjectAsync(
            new StatObjectArgs()
                .WithBucket(_defaultBucket)
                .WithObject(objectName)
        );

        return (stat.Size, stat.ETag, stat.LastModified);
    }

    /// <summary>
    /// List objects in bucket with optional prefix filtering using internal client.
    /// </summary>
    public async Task<List<(string Name, long Size)>> ListObjectsAsync(string? prefix = null, int maxResults = 1000)
    {
        var result = await _internalClient.ListObjectsAsync(
            new ListObjectsArgs()
                .WithBucket(_defaultBucket)
                .WithPrefix(prefix)
                .WithRecursive(true)
        );

        return result.Objects
            .Take(maxResults)
            .Select(obj => (obj.Name, obj.Size))
            .ToList();
    }

    /// <summary>
    /// Ensure bucket exists using internal client, with retry logic.
    /// </summary>
    public async Task EnsureBucketExistsAsync(int maxAttempts = 3, int delaySeconds = 2)
    {
        await _internalClient.EnsureBucketExistsAsync(_defaultBucket, maxAttempts, delaySeconds);
    }

    /// <summary>
    /// Health check: Verify connectivity to storage backend.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            await _internalClient.ListBucketsAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Multi-tenant helper: Generate a tenant-scoped download URL.
    /// </summary>
    public async Task<string> GetTenantFileUrlAsync(string tenantId, string filePath, int expiryInSeconds = 3600)
    {
        var objectName = $"tenants/{tenantId}/{filePath}";
        return await GetDownloadUrlAsync(objectName, expiryInSeconds);
    }

    /// <summary>
    /// Multi-tenant helper: Generate a tenant-scoped upload URL.
    /// </summary>
    public async Task<string> GetTenantUploadUrlAsync(string tenantId, string filePath, int expiryInSeconds = 1800)
    {
        var objectName = $"tenants/{tenantId}/{filePath}";
        return await GetUploadUrlAsync(objectName, expiryInSeconds);
    }

    /// <summary>
    /// Copy file from one location to another (useful for archiving or migrations).
    /// </summary>
    public async Task<string> CopyFileAsync(string sourceObject, string destinationObject)
    {
        var result = await _internalClient.CopyObjectAsync(
            new CopyObjectArgs()
                .WithSourceBucket(_defaultBucket)
                .WithSourceObject(sourceObject)
                .WithDestinationBucket(_defaultBucket)
                .WithDestinationObject(destinationObject)
        );

        return destinationObject;
    }
}
