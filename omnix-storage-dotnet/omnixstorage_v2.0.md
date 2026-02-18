# OmnixStorage Implementation Documentation v2.0

## Overview

OmnixStorage is a wrapper SDK around MinIO (S3-compatible object storage) used throughout the EdgeSentience platform for managing frame images, media assets, and object data. The implementation uses the `OmnixStorageClient` and `OmnixStorageClientBuilder` classes to provide S3 operations across three main service areas: MediaIngest, DashboardApi, and AlertsEngine.

---

## 1. Core Configuration Settings

### MinioSettings Class (AlertsEngine)

Location: `src/Workers/AlertsEngine/Configuration/Settings.cs`

```csharp
/// <summary>
/// OmnixStorage object storage settings for accessing frame images
/// </summary>
public class MinioSettings
{
    public const string SectionName = "OmnixStorage";
    
    /// <summary>
    /// Internal MinIO endpoint (for generating presigned URLs)
    /// </summary>
    public string Endpoint { get; set; } = "localhost:9000";
    
    /// <summary>
    /// Public MinIO endpoint (used in presigned URLs for external access)
    /// </summary>
    public string PublicEndpoint { get; set; } = "https://storage.edgesentience.kegeosapps.com";
    
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool Secure { get; set; } = false;
    public string Region { get; set; } = "us-east-1";
    public string DefaultBucket { get; set; } = "cameras";
    
    /// <summary>
    /// Presigned URL expiration time in seconds
    /// </summary>
    public int PresignedUrlExpirationSeconds { get; set; } = 3600; // 1 hour
}
```

**Purpose**: Stores configuration for OmnixStorage/MinIO connection. The `Endpoint` is for internal service-to-service communication, while `PublicEndpoint` is used for generating presigned URLs that work from external networks (e.g., web browsers).

---

### MinioSettings Class (MediaIngest)

Location: `src/Services/MediaIngest/MediaIngest.Infrastructure/Services/MinioStorageService.cs`

```csharp
/// <summary>
/// Configuration settings for OmnixStorage.
/// </summary>
public class MinioSettings
{
    public const string SectionName = "OmnixStorage";
    
    public string Endpoint { get; set; } = "localhost:9000";
    public string? PublicEndpoint { get; set; }
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin123";
    public bool UseSsl { get; set; } = false;
    public string Region { get; set; } = "us-east-1";
    public bool AutoCreateBucket { get; set; } = true;
    public int BucketCreateMaxAttempts { get; set; } = 2;
    public int BucketCreateRetryDelaySeconds { get; set; } = 2;
}
```

**Purpose**: MediaIngest-specific configuration for frame storage operations. Includes auto-bucket creation settings and retry logic for bucket creation.

---

## 2. OmnixStorageClient Builder Pattern

### MediaIngest Dependency Injection

Location: `src/Services/MediaIngest/MediaIngest.Infrastructure/DependencyInjection.cs`

```csharp
services.AddSingleton<IOmnixStorageClient>(sp =>
{
    var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("OmnixStorage");
    
    // DEPLOYMENT TEST LOG - If you see this, new code is deployed! (Feb 16, 2026 - Commit: b6ee937)
    logger?.LogWarning("🚀 DEPLOYMENT VERIFICATION: MediaIngest.Infrastructure loaded with latest fixes (Trim + Endpoint + Logging)");
    
    // CRITICAL: System time sync diagnostic - SigV4 signatures require matching timestamps
    var utcNow = DateTime.UtcNow;
    var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    logger?.LogWarning(
        "⏰ TIME SYNC CHECK: ClientUTC=[{ClientUtcTime}], UnixTimestamp=[{UnixTimestamp}]. IMPORTANT: If server rejects uploads, check if server clock is synchronized!",
        utcNow,
        unixTimestamp);
    
    // ENHANCED CREDENTIAL DEBUGGING - Shows exactly what credentials are being used
    logger?.LogWarning(
        "🔐 CREDENTIAL DEBUG: AccessKey=[{AccessKeyFull}] (len={AccessKeyLen}), SecretKey=[{SecretKeyMasked}] (len={SecretKeyLen}), Region=[{Region}]",
        MaskKeyEnhanced(minioSettings.AccessKey),
        minioSettings.AccessKey?.Length ?? 0,
        MaskKeyEnhanced(minioSettings.SecretKey),
        minioSettings.SecretKey?.Length ?? 0,
        minioSettings.Region);
    
    logger?.LogInformation(
        "OmnixStorage client resolved: Endpoint={Endpoint}, PublicEndpoint={PublicEndpoint}, Region={Region}, UseSsl={UseSsl}, AccessKey={AccessKey}, EnvOverrides=[S3_ENDPOINT:{S3Endpoint}, S3_REGION:{RegionSet}]",
        minioSettings.Endpoint,
        minioSettings.PublicEndpoint ?? "(null)",
        minioSettings.Region,
        minioSettings.UseSsl,
        MaskKey(minioSettings.AccessKey),
        s3EndpointProvided,
        s3RegionProvided);

    var client = new OmnixStorageClientBuilder()
        .WithEndpoint(minioSettings.Endpoint)
        .WithCredentials(minioSettings.AccessKey, minioSettings.SecretKey)
        .WithRegion(minioSettings.Region)
        .WithSSL(minioSettings.UseSsl);
    
    return client.Build();
});
```

**Purpose**: Creates a singleton `IOmnixStorageClient` instance for MediaIngest using the builder pattern. Includes credential masking for secure logging and environment variable overrides for S3_ENDPOINT and S3_REGION.

---

### DashboardApi Dependency Injection

Location: `src/Services/DashboardApi/DashboardApi.Infrastructure/DependencyInjection.cs`

```csharp
services.AddSingleton<IOmnixStorageClient>(_ =>
    new OmnixStorageClientBuilder()
        .WithEndpoint(minioEndpoint)
        .WithCredentials(minioAccessKey, minioSecretKey)
        .WithRegion(s3Region)
        .WithSSL(minioUseSSL)
        .Build());
```

**Purpose**: Creates a singleton `IOmnixStorageClient` for DashboardApi. Includes environment variable processing for S3_ENDPOINT and S3_REGION.

---

### AlertsEngine Dependency Injection (DataCleanupWorker)

Location: `src/Workers/AlertsEngine/Workers/DataCleanupWorker.cs`

```csharp
// Create OmnixStorage client for cleanup operations
_minioClient = new OmnixStorageClientBuilder()
    .WithEndpoint(_minioSettings.Endpoint)
    .WithCredentials(_minioSettings.AccessKey, _minioSettings.SecretKey)
    .WithRegion(_minioSettings.Region)
    .WithSSL(_minioSettings.Secure)
    .Build();
```

**Purpose**: Creates an OmnixStorageClient instance for data cleanup operations (deleting old frame files from MinIO).

---

## 3. MediaIngest Frame Storage Service

### MinioStorageService Class

Location: `src/Services/MediaIngest/MediaIngest.Infrastructure/Services/MinioStorageService.cs`

#### Overview

Implements `IStorageService` interface and provides methods for uploading, retrieving, and deleting frames with multiple resolutions. Uses OmnixStorage for all S3 operations.

#### Constructor

```csharp
public class MinioStorageService : IStorageService
{
    private readonly IOmnixStorageClient _storageClient;
    private readonly MinioSettings _settings;
    private readonly ILogger<MinioStorageService> _logger;
    
    public MinioStorageService(
        IOmnixStorageClient storageClient, 
        IOptions<MinioSettings> settings,
        ILogger<MinioStorageService> logger)
    {
        _storageClient = storageClient;
        _settings = settings.Value;
        _logger = logger;

        // DEPLOYMENT TEST LOG - Verifies service is using latest code with all fixes applied
        _logger.LogWarning("🔧 DEPLOYMENT VERIFICATION: MinioStorageService initialized with LATEST CODE (Feb 16, 2026)");
        
        _logger.LogInformation(
            "OmnixStorage config: Endpoint={Endpoint}, PublicEndpoint={PublicEndpoint}, Region={Region}, UseSsl={UseSsl}, AutoCreateBucket={AutoCreate}, AccessKey={AccessKey}",
            _settings.Endpoint,
            _settings.PublicEndpoint ?? "(null)",
            _settings.Region,
            _settings.UseSsl,
            _settings.AutoCreateBucket,
            MaskKey(_settings.AccessKey));
    }
```

**Purpose**: Initializes the storage service with a pre-configured OmnixStorageClient. Logs configuration for debugging deployment issues.

#### SaveLatestAsync Method

```csharp
public async Task<FrameStorageLocation> SaveLatestAsync(
    string deviceId,
    byte[] frameData,
    string contentType,
    CancellationToken cancellationToken = default)
{
    var location = FrameStorageLocation.ForLatest(deviceId);
    
    await EnsureBucketExistsAsync(location.Bucket, cancellationToken);
    
    using var stream = new MemoryStream(frameData);
    
    try
    {
        await _storageClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(location.Bucket)
                .WithObject(location.Key)
                .WithStreamData(stream, frameData.Length)
                .WithContentType(contentType),
            cancellationToken);
        
        _logger.LogDebug("Uploaded latest frame to {Bucket}/{Key} ({Size} bytes)",
            location.Bucket, location.Key, frameData.Length);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to upload latest frame to {Bucket}/{Key}. Endpoint={Endpoint}, Region={Region}, AccessKey={AccessKey}",
            location.Bucket, location.Key, _settings.Endpoint, _settings.Region, MaskKey(_settings.AccessKey));
        throw;
    }
    
    return location;
}
```

**Purpose**: Uploads the most recent frame for a device to the "latest" path (e.g., `latest/{deviceId}.jpg`). This allows live viewing of the current camera state. Uses `PutObjectArgs` with a memory stream.

#### ArchiveAsync Method

```csharp
public async Task<FrameStorageLocation> ArchiveAsync(
    string deviceId,
    byte[] frameData,
    string contentType,
    DateTimeOffset capturedAt,
    CancellationToken cancellationToken = default)
{
    var location = FrameStorageLocation.ForArchive(deviceId, capturedAt);
    
    await EnsureBucketExistsAsync(location.Bucket, cancellationToken);
    
    using var stream = new MemoryStream(frameData);
    
    try
    {
        await _storageClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(location.Bucket)
                .WithObject(location.Key)
                .WithStreamData(stream, frameData.Length)
                .WithContentType(contentType),
            cancellationToken);
        
        _logger.LogInformation("Archived frame to {Bucket}/{Key} ({Size} bytes)",
            location.Bucket, location.Key, frameData.Length);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to archive frame to {Bucket}/{Key}",
           location.Bucket, location.Key);
        throw;
    }
    
    return location;
}
```

**Purpose**: Uploads a frame to the archive path with timestamp information (e.g., `archive/{deviceId}/{yyyy-MM-dd}/{time}.jpg`). Enables historical frame retrieval by capture time.

#### GetAsync Methods

```csharp
public Task<byte[]> GetAsync(
    FrameStorageLocation location,
    CancellationToken cancellationToken = default)
{
    return GetAsync(location.Bucket, location.Key, cancellationToken);
}

public async Task<byte[]> GetAsync(
    string bucket,
    string key,
    CancellationToken cancellationToken = default)
{
    using var memoryStream = new MemoryStream();

    await _storageClient.GetObjectAsync(
        new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithOutputStream(memoryStream),
        cancellationToken);
    
    return memoryStream.ToArray();
}
```

**Purpose**: Retrieves frame data from MinIO as a byte array. Uses `GetObjectArgs` to stream data into memory.

#### GetPresignedUrlAsync Method

```csharp
public async Task<string> GetPresignedUrlAsync(
    FrameStorageLocation location,
    TimeSpan expiry,
    CancellationToken cancellationToken = default)
{
    var result = await _storageClient.PresignedGetObjectAsync(
        new PresignedGetObjectArgs()
            .WithBucket(location.Bucket)
            .WithObject(location.Key)
            .WithExpiry((int)expiry.TotalSeconds));
    
    return result.Url;
}
```

**Purpose**: Generates a presigned URL for downloading a frame without authentication. The URL expires after the specified time period.

#### DeleteAsync Method

```csharp
public async Task DeleteAsync(
    FrameStorageLocation location,
    CancellationToken cancellationToken = default)
{
    try
    {
        await _storageClient.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(location.Bucket)
                .WithObject(location.Key),
            cancellationToken);
    }
    catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("NoSuchKey"))
    {
        // Object doesn't exist, nothing to delete
        _logger.LogDebug("Object not found during delete: {Bucket}/{Key}", location.Bucket, location.Key);
    }
}
```

**Purpose**: Deletes a frame from MinIO. Silently handles "object not found" errors.

#### ArchiveMultiResolutionAsync Method

```csharp
public async Task<MultiResolutionArchiveResult> ArchiveMultiResolutionAsync(
    string deviceId,
    OptimizedImages optimizedImages,
    string contentType,
    DateTimeOffset timestamp,
    CancellationToken cancellationToken = default)
{
    var fullLocation = FrameStorageLocation.ForArchive(deviceId, timestamp, ImageSize.Full);
    var mediumLocation = FrameStorageLocation.ForArchive(deviceId, timestamp, ImageSize.Medium);
    var thumbnailLocation = FrameStorageLocation.ForArchive(deviceId, timestamp, ImageSize.Thumbnail);
    
    await EnsureBucketExistsAsync(fullLocation.Bucket, cancellationToken);
    
    // Upload all three sizes in parallel
    var uploadTasks = new[]
    {
        UploadBytesAsync(fullLocation, optimizedImages.Full, contentType, cancellationToken),
        UploadBytesAsync(mediumLocation, optimizedImages.Medium, contentType, cancellationToken),
        UploadBytesAsync(thumbnailLocation, optimizedImages.Thumbnail, contentType, cancellationToken)
    };
    
    await Task.WhenAll(uploadTasks);
    
    _logger.LogInformation(
        "Archived multi-resolution frame for {DeviceId}: Full={FullSize}, Medium={MediumSize}, Thumb={ThumbSize} bytes",
        deviceId, optimizedImages.FullSize, optimizedImages.MediumSize, optimizedImages.ThumbnailSize);
    
    return new MultiResolutionArchiveResult
    {
        Full = fullLocation,
        Medium = mediumLocation,
        Thumbnail = thumbnailLocation,
        TotalBytes = optimizedImages.TotalSize
    };
}
```

**Purpose**: Uploads three different resolution versions of a frame (full, medium, thumbnail) in parallel. Returns storage locations for all three variants.

#### UploadBytesAsync Helper

```csharp
private async Task UploadBytesAsync(
    FrameStorageLocation location,
    byte[] data,
    string contentType,
    CancellationToken cancellationToken)
{
    using var stream = new MemoryStream(data);
    await _storageClient.PutObjectAsync(
        new PutObjectArgs()
            .WithBucket(location.Bucket)
            .WithObject(location.Key)
            .WithStreamData(stream, data.Length)
            .WithContentType(contentType),
        cancellationToken);
}
```

**Purpose**: Helper method to upload a single frame resolution variant with proper content type.

#### ExistsAsync Method

```csharp
public async Task<bool> ExistsAsync(
    FrameStorageLocation location,
    CancellationToken cancellationToken = default)
{
    try
    {
        await _storageClient.StatObjectAsync(
            new StatObjectArgs()
                .WithBucket(location.Bucket)
                .WithObject(location.Key),
            cancellationToken);
        return true;
    }
    catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("NoSuchKey"))
    {
        return false;
    }
}
```

**Purpose**: Checks if an object exists in MinIO without downloading it. Uses `StatObjectAsync` for efficient metadata-only query.

#### EnsureBucketExistsAsync Method

```csharp
private async Task EnsureBucketExistsAsync(string bucket, CancellationToken cancellationToken)
{
    _logger.LogDebug("Ensuring bucket exists: {Bucket}. Endpoint={Endpoint}, Region={Region}, AccessKey={AccessKey}",
        bucket, _settings.Endpoint, _settings.Region, MaskKey(_settings.AccessKey));

    var exists = await _storageClient.BucketExistsAsync(bucket);
    if (exists)
    {
        return;
    }

    if (!_settings.AutoCreateBucket)
    {
        _logger.LogWarning("Auto bucket creation is disabled. Ensure bucket exists: {Bucket}", bucket);
        return;
    }

    var maxAttempts = Math.Max(1, _settings.BucketCreateMaxAttempts);
    var delay = TimeSpan.FromSeconds(Math.Max(1, _settings.BucketCreateRetryDelaySeconds));

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            _logger.LogInformation("Creating bucket {Bucket} (attempt {Attempt}/{MaxAttempts})...", bucket, attempt, maxAttempts);
            await _storageClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucket),
                cancellationToken);
            _logger.LogInformation("Bucket created successfully: {Bucket}", bucket);
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            _logger.LogWarning(ex, "Bucket creation failed for {Bucket}. Retrying in {DelaySeconds}s...", bucket, delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bucket creation failed for {Bucket}. Re-checking existence...", bucket);
            var existsAfter = await _storageClient.BucketExistsAsync(bucket);
            if (!existsAfter)
            {
                throw;
            }

            _logger.LogInformation("Bucket exists after failed create attempt: {Bucket}", bucket);
            return;
        }
    }
}
```

**Purpose**: Ensures a bucket exists before uploading. Implements automatic bucket creation with retry logic and exponential backoff if enabled.

#### HealthCheckAsync Method

```csharp
public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
{
    try
    {
        // List buckets as a health check - verifies connectivity
        await _storageClient.ListBucketsAsync(cancellationToken);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "OmnixStorage health check failed. Endpoint={Endpoint}, Region={Region}, AccessKey={AccessKey}",
            _settings.Endpoint, _settings.Region, MaskKey(_settings.AccessKey));
        return false;
    }
}
```

**Purpose**: Performs a health check by listing all buckets. Returns `true` if connected, `false` if OmnixStorage is unavailable.

---

## 4. DashboardApi OmnixStorage Service

### OmnixStorageService Class

Location: `src/Services/DashboardApi/OmnixStorageService.cs`

#### Interface Definition

```csharp
public interface IOmnixStorageService
{
    Task<string> GetPresignedUploadUrlAsync(string bucketName, string objectKey, int expirySeconds = 3600);
    Task<string> GetPresignedDownloadUrlAsync(string bucketName, string objectKey, int expirySeconds = 3600);
    Task<string> UploadFileAsync(string bucketName, string objectKey, Stream fileContent);
    Task DeleteFileAsync(string bucketName, string objectKey);
    Task<bool> BucketExistsAsync(string bucketName);
    Task CreateBucketAsync(string bucketName);
}
```

**Purpose**: Defines operations for file management and presigned URL generation.

#### Class Implementation

```csharp
public class OmnixStorageService : IOmnixStorageService
{
    private readonly IOmnixStorageClient _storageClient;
    private readonly ILogger<OmnixStorageService> _logger;

    public OmnixStorageService(IOmnixStorageClient storageClient, ILogger<OmnixStorageService> logger)
    {
        _storageClient = storageClient;
        _logger = logger;
    }

    public async Task<string> GetPresignedUploadUrlAsync(string bucketName, string objectKey, int expirySeconds = 3600)
    {
        try
        {
            _logger.LogInformation($"Generating presigned PUT URL for {bucketName}/{objectKey}");
            
            var result = await _storageClient.PresignedPutObjectAsync(
                new PresignedPutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithExpiry(expirySeconds));

            _logger.LogInformation($"✓ Presigned PUT URL generated (expires in {expirySeconds}s)");
            return result.Url;
        }
        catch (Exception ex)
        {
            _logger.LogError($"✗ Failed to generate presigned upload URL: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetPresignedDownloadUrlAsync(string bucketName, string objectKey, int expirySeconds = 3600)
    {
        try
        {
            _logger.LogInformation($"Generating presigned GET URL for {bucketName}/{objectKey}");
            
            var result = await _storageClient.PresignedGetObjectAsync(
                new PresignedGetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithExpiry(expirySeconds));

            _logger.LogInformation($"✓ Presigned GET URL generated (expires in {expirySeconds}s)");
            return result.Url;
        }
        catch (Exception ex)
        {
            _logger.LogError($"✗ Failed to generate presigned download URL: {ex.Message}");
            throw;
        }
    }

    public async Task<string> UploadFileAsync(string bucketName, string objectKey, Stream fileContent)
    {
        try
        {
            _logger.LogInformation($"Uploading file to {bucketName}/{objectKey}");
            
            await _storageClient.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithStreamData(fileContent));

            _logger.LogInformation($"✓ File uploaded successfully");
            return objectKey;
        }
        catch (Exception ex)
        {
            _logger.LogError($"✗ Failed to upload file: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteFileAsync(string bucketName, string objectKey)
    {
        try
        {
            _logger.LogInformation($"Deleting file {bucketName}/{objectKey}");
            
            await _storageClient.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey));

            _logger.LogInformation($"✓ File deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"✗ Failed to delete file: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> BucketExistsAsync(string bucketName)
    {
        try
        {
            return await _storageClient.BucketExistsAsync(bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"✗ Error checking bucket existence: {ex.Message}");
            throw;
        }
    }

    public async Task CreateBucketAsync(string bucketName)
    {
        try
        {
            _logger.LogInformation($"Creating bucket: {bucketName}");
            
            await _storageClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName));

            _logger.LogInformation($"✓ Bucket created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"✗ Failed to create bucket: {ex.Message}");
            throw;
        }
    }
}
```

**Purpose**: Provides wrapper methods for common OmnixStorage operations with consistent error handling and logging.

#### Usage in Programs.cs

```csharp
using OmnixStorage;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Add OmnixStorage configuration
var omnixEndpoint = builder.Configuration["OmnixStorage:Endpoint"] ?? "37.60.228.216:9000";
var omnixAccessKey = builder.Configuration["OmnixStorage:AccessKey"] ?? "admin";
var omnixSecretKey = builder.Configuration["OmnixStorage:SecretKey"] ?? throw new InvalidOperationException("OmnixStorage:SecretKey not configured");
var omnixUseSSL = builder.Configuration.GetValue<bool>("OmnixStorage:UseSSL", false);

var storageClient = new OmnixStorageClientBuilder()
    .WithEndpoint(omnixEndpoint)
    .WithCredentials(omnixAccessKey, omnixSecretKey)
    .WithRegion("us-east-1")
    .WithSSL(omnixUseSSL)
    .Build();

builder.Services.AddSingleton<IOmnixStorageClient>(storageClient);
builder.Services.AddScoped<IOmnixStorageService, OmnixStorageService>();

var app = builder.Build();
app.Run();
```

**Purpose**: Dependency injection setup for DashboardApi services.

#### Controller Example Usage

```csharp
[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IOmnixStorageService _storageService;
    private readonly ILogger<FileController> _logger;

    public FileController(IOmnixStorageService storageService, ILogger<FileController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    [HttpPost("upload-url")]
    public async Task<IActionResult> GetUploadUrl([FromQuery] string fileName)
    {
        try
        {
            var url = await _storageService.GetPresignedUploadUrlAsync(
                bucketName: "edgesentience-media",
                objectKey: $"user-uploads/{Guid.NewGuid()}-{fileName}",
                expirySeconds: 3600);

            return Ok(new { uploadUrl = url });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            return StatusCode(500, new { error = "Failed to generate upload URL" });
        }
    }

    [HttpPost("download-url")]
    public async Task<IActionResult> GetDownloadUrl([FromQuery] string objectKey)
    {
        try
        {
            var url = await _storageService.GetPresignedDownloadUrlAsync(
                bucketName: "edgesentience-media",
                objectKey: objectKey,
                expirySeconds: 3600);

            return Ok(new { downloadUrl = url });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            return StatusCode(500, new { error = "Failed to generate download URL" });
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is required");

            var fileName = $"uploads/{Guid.NewGuid()}-{file.FileName}";
            
            using (var stream = file.OpenReadStream())
            {
                await _storageService.UploadFileAsync(
                    bucketName: "edgesentience-media",
                    objectKey: fileName,
                    fileContent: stream);
            }

            return Ok(new { 
                message = "File uploaded successfully",
                objectKey = fileName 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            return StatusCode(500, new { error = "Failed to upload file" });
        }
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> DeleteFile([FromQuery] string objectKey)
    {
        try
        {
            await _storageService.DeleteFileAsync(
                bucketName: "edgesentience-media",
                objectKey: objectKey);

            return Ok(new { message = "File deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            return StatusCode(500, new { error = "Failed to delete file" });
        }
    }
}
```

**Purpose**: Demonstrates how controllers use `IOmnixStorageService` for file operations.

---

## 5. DashboardApi FrameQueryService

### FrameQueryService Class

Location: `src/Services/DashboardApi/DashboardApi.Infrastructure/Services/FrameQueryService.cs`

#### Overview

Handles frame retrieval and presigned URL generation for the Dashboard API. Uses separate OmnixStorageClient instances for internal operations and public endpoint presigned URL generation.

#### Constructor and Initialization

```csharp
public class FrameQueryService : IFrameQueryService
{
    private readonly DashboardMediaDbContext _context;
    private readonly IOmnixStorageClient _storageClient;
    private readonly IOmnixStorageClient _publicStorageClient;
    private readonly ILogger<FrameQueryService> _logger;
    private readonly int _defaultExpirationSeconds;
    private readonly string _publicEndpoint;

    public FrameQueryService(
        DashboardMediaDbContext context, 
        IOmnixStorageClient storageClient,
        IConfiguration configuration,
        ILogger<FrameQueryService> logger)
    {
        _context = context;
        _storageClient = storageClient;
        _logger = logger;
        _defaultExpirationSeconds = configuration.GetValue("OmnixStorage:PresignedUrlExpiration", 300);
        var s3Endpoint = configuration["S3_ENDPOINT"]?.Trim();
        var s3Region = configuration["S3_REGION"] ?? "us-east-1";
        var minioEndpoint = configuration["OmnixStorage:Endpoint"]?.Trim();
        _publicEndpoint = configuration.GetValue<string>("OmnixStorage:PublicEndpoint")
            ?? (!string.IsNullOrWhiteSpace(s3Endpoint)
                ? EnsurePublicEndpoint(s3Endpoint)
                : EnsurePublicEndpoint(minioEndpoint ?? "localhost:9000"));
        
        // Create a separate OmnixStorage client configured with the PUBLIC endpoint for presigned URL generation
        // This ensures presigned URLs work from the browser without endpoint replacement
        var publicEndpointUri = new Uri(_publicEndpoint);
        var accessKey = configuration["OmnixStorage:AccessKey"]?.Trim();
        var secretKey = configuration["OmnixStorage:SecretKey"]?.Trim();

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("OmnixStorage credentials are not configured. Set OmnixStorage__AccessKey and OmnixStorage__SecretKey environment variables.");
        }
        
        // Determine the correct port - use explicit port from URI, or default for scheme
        var port = publicEndpointUri.Port == -1 
            ? (publicEndpointUri.Scheme == "https" ? 443 : 80) 
            : publicEndpointUri.Port;
        
        _publicStorageClient = new OmnixStorageClientBuilder()
            .WithEndpoint($"{publicEndpointUri.Host}:{port}")
            .WithCredentials(accessKey, secretKey)
            .WithRegion(s3Region)
            .WithSSL(publicEndpointUri.Scheme == "https")
            .Build();
        
        _logger.LogInformation(
            "FrameQueryService initialized with public endpoint: {Endpoint}, SSL: {SSL}, Region={Region}, AccessKey={AccessKey}",
            publicEndpointUri.Host,
            publicEndpointUri.Scheme == "https",
            s3Region,
            MaskKey(accessKey));
    }

    private static string EnsurePublicEndpoint(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out _))
        {
            return endpoint;
        }

        return $"http://{endpoint}";
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "(null)";
        }

        if (key.Length <= 6)
        {
            return "***";
        }

        return $"{key.Substring(0, 3)}***{key.Substring(key.Length - 3)}";
    }
```

**Purpose**: Initializes two separate OmnixStorageClient instances:
- `_storageClient`: For internal operations using the private endpoint
- `_publicStorageClient`: For presigned URL generation using the public endpoint

This dual-client approach ensures that presigned URLs generated for browsers use the public-facing endpoint.

#### GetLatestFrameUrlAsync Method

```csharp
public async Task<FrameUrlDto?> GetLatestFrameUrlAsync(string deviceId, CancellationToken cancellationToken = default)
{
    // For live view, generate a presigned URL to the "latest" file
    // This file is always overwritten with the most recent frame
    
    try
    {
        // Generate presigned URL for the latest frame
        var latestUrl = await GetPresignedUrlAsync("cameras", $"latest/{deviceId}.jpg", _defaultExpirationSeconds);
        
        // Get the timestamp from the most recent frame in the database (for display purposes)
        var frame = await _context.Frames
            .Where(f => f.DeviceId == deviceId)
            .OrderByDescending(f => f.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Return the presigned URL with frame timestamp (or now if no frames yet)
        var capturedAt = frame?.CapturedAt ?? DateTimeOffset.UtcNow;
        _logger.LogDebug("GetLatestFrameUrlAsync: Generated presigned URL for latest frame of device {DeviceId}", deviceId);
        return new FrameUrlDto(latestUrl, capturedAt, _defaultExpirationSeconds);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get latest frame URL for device {DeviceId}", deviceId);
        return null;
    }
}
```

**Purpose**: Generates a presigned GET URL for the latest frame of a device. Used in live view dashboards.

#### GetFramesForPlaybackAsync Method

```csharp
public async Task<IReadOnlyList<FrameMetadataDto>> GetFramesForPlaybackAsync(
    string deviceId, 
    DateTimeOffset start, 
    DateTimeOffset end, 
    int maxFrames = 1000,
    CancellationToken cancellationToken = default)
{
    _logger.LogInformation("GetFramesForPlaybackAsync: DeviceId={DeviceId}, Start={Start}, End={End}, MaxFrames={MaxFrames}",
        deviceId, start, end, maxFrames);
    
    // Order descending to get the MOST RECENT frames when limited by maxFrames
    // Then reverse to return in chronological order for playback (oldest → newest)
    var frames = await _context.Frames
        .Where(f => f.DeviceId == deviceId && f.CapturedAt >= start && f.CapturedAt <= end)
        .OrderByDescending(f => f.CapturedAt)
        .Take(maxFrames)
        .ToListAsync(cancellationToken);
    
    // Reverse to chronological order for playback timeline
    frames.Reverse();

    _logger.LogInformation("GetFramesForPlaybackAsync: Found {Count} frames (most recent, chronological). First={First}, Last={Last}",
        frames.Count,
        frames.FirstOrDefault()?.CapturedAt.ToString("o") ?? "none",
        frames.LastOrDefault()?.CapturedAt.ToString("o") ?? "none");

    return frames.Select(f => new FrameMetadataDto(
        f.Id,
        f.DeviceId,
        f.CapturedAt,
        f.HasMotion,
        f.IsProcessed,
        f.SizeBytes)).ToList();
}
```

**Purpose**: Retrieves frame metadata from the database for a time range and device, ordered chronologically for playback.

#### GetFrameUrlAsync Methods

```csharp
public async Task<string?> GetFrameUrlAsync(Guid frameId, int expirationSeconds = 300, CancellationToken cancellationToken = default)
{
    return await GetFrameUrlAsync(frameId, "full", expirationSeconds, cancellationToken);
}

public async Task<string?> GetFrameUrlAsync(Guid frameId, FrameSize size, int expirationSeconds = 300, CancellationToken cancellationToken = default)
{
    return await GetFrameUrlAsync(frameId, size.ToString().ToLowerInvariant(), expirationSeconds, cancellationToken);
}

/// <summary>
/// Gets a presigned URL for a specific frame with optional size variant.
/// </summary>
private async Task<string?> GetFrameUrlAsync(Guid frameId, string size, int expirationSeconds = 300, CancellationToken cancellationToken = default)
{
    var frame = await _context.Frames
        .FirstOrDefaultAsync(f => f.Id == frameId, cancellationToken);

    if (frame is null)
    {
        _logger.LogWarning("Frame {FrameId} not found in database", frameId);
        return null;
    }

    _logger.LogDebug("Getting URL for frame {FrameId}, ObjectKey: {ObjectKey}, Size: {Size}", 
        frameId, frame.ObjectKey, size);

    // For all frames (including latest/), generate presigned URLs
    // Determine the object key based on size
    // New format: archive/{deviceId}/{date}/{size}/{time}.jpg
    // Legacy format: archive/{deviceId}/{date}/{time}.jpg (treat as full)
    // Latest format: latest/{deviceId}.jpg
    var objectKey = frame.ObjectKey;
    
    // Check if it's the new multi-resolution archive format
    var parts = objectKey.Split('/');
    if (!objectKey.StartsWith("latest/") && parts.Length == 5 && (parts[3] == "full" || parts[3] == "medium" || parts[3] == "thumbnail"))
    {
        // New format - replace size segment
        parts[3] = size.ToLowerInvariant();
        objectKey = string.Join('/', parts);
    }
    else if (!objectKey.StartsWith("latest/") && parts.Length == 4 && size != "full")
    {
        // Legacy archive format but requesting non-full size - try to construct new path
        // archive/{deviceId}/{date}/{time}.jpg -> archive/{deviceId}/{date}/{size}/{time}.jpg
        objectKey = $"{parts[0]}/{parts[1]}/{parts[2]}/{size.ToLowerInvariant()}/{parts[3]}";
    }
    // else: latest/ format or legacy format requesting full - use original path or latest/

    // For all frames, generate presigned URL using public endpoint client
    return await GetPresignedUrlAsync(frame.BucketName, objectKey, expirationSeconds);
}

private async Task<string> GetPresignedUrlAsync(string bucketName, string objectKey, int expirationSeconds)
{
    try
    {
        // Use the public OmnixStorage client to generate URLs that work from browsers
        var result = await _publicStorageClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithExpiry(expirationSeconds));
        
        _logger.LogDebug("Generated presigned URL for {Bucket}/{Key}: {Url}", 
            bucketName, objectKey, result.Url);
        
        return result.Url;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to generate presigned URL for {Bucket}/{Key}", bucketName, objectKey);
        throw;
    }
}
```

**Purpose**: Generates presigned GET URLs for specific frames, with support for multiple image resolutions (full, medium, thumbnail). Handles both new multi-resolution format and legacy single-resolution format.

---

## 6. AlertsEngine MinioService

### MinioService Class

Location: `src/Workers/AlertsEngine/Services/MinioService.cs`

#### Overview

Provides frame URL generation and health checking for the AlertsEngine worker.

#### Interface and Implementation

```csharp
/// <summary>
/// OmnixStorage client for generating presigned URLs to frame images
/// </summary>
public interface IMinioService
{
    Task<string?> GetFrameUrlAsync(string frameId, CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

public class MinioService : IMinioService
{
    private readonly MinioSettings _settings;
    private readonly IOmnixStorageClient _storageClient;
    private readonly IOmnixStorageClient _publicStorageClient;
    private readonly DatabaseSettings _dbSettings;
    private readonly ILogger<MinioService> _logger;

    public MinioService(
        IOptions<MinioSettings> settings,
        IOptions<DatabaseSettings> dbSettings,
        ILogger<MinioService> logger)
    {
        _settings = settings.Value;
        _dbSettings = dbSettings.Value;
        _logger = logger;

        _logger.LogInformation(
            "AlertsEngine OmnixStorage config: Endpoint={Endpoint}, PublicEndpoint={PublicEndpoint}, Region={Region}, UseSsl={UseSsl}, AccessKey={AccessKey}",
            _settings.Endpoint,
            _settings.PublicEndpoint,
            _settings.Region,
            _settings.Secure,
            MaskKey(_settings.AccessKey));

        // Create OmnixStorage client for internal operations
        _storageClient = new OmnixStorageClientBuilder()
            .WithEndpoint(_settings.Endpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .WithRegion(_settings.Region)
            .WithSSL(_settings.Secure)
            .Build();

        // Create a separate client that uses public endpoint for presigned URLs
        // This ensures the generated URLs work from external networks
        var publicUri = new Uri(_settings.PublicEndpoint);
        _publicStorageClient = new OmnixStorageClientBuilder()
            .WithEndpoint($"{publicUri.Host}:{(publicUri.Port == -1 ? (publicUri.Scheme == "https" ? 443 : 80) : publicUri.Port)}")
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .WithRegion(_settings.Region)
            .WithSSL(publicUri.Scheme == "https")
            .Build();
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "(null)";
        }

        if (key.Length <= 6)
        {
            return "***";
        }

        return $"{key.Substring(0, 3)}***{key.Substring(key.Length - 3)}";
    }
```

**Purpose**: Initializes dual OmnixStorageClient instances (internal and public) for URL generation.

#### GetFrameUrlAsync Method

```csharp
public async Task<string?> GetFrameUrlAsync(string frameId, CancellationToken cancellationToken = default)
{
    if (string.IsNullOrEmpty(frameId))
    {
        _logger.LogDebug("No frame ID provided, skipping URL generation");
        return null;
    }

    try
    {
        // Query the database to get the frame's storage location
        var frameInfo = await GetFrameStorageInfoAsync(frameId, cancellationToken);
        
        if (frameInfo is null)
        {
            _logger.LogWarning("Frame {FrameId} not found in database", frameId);
            return null;
        }

        // For latest/ prefix, use public URL directly (no presigned needed)
        if (frameInfo.ObjectKey.StartsWith("latest/"))
        {
            return $"{_settings.PublicEndpoint.TrimEnd('/')}/{frameInfo.Bucket}/{frameInfo.ObjectKey}";
        }

        // For archive/ prefix, generate presigned URL
        var presignedUrlResult = await _publicStorageClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(frameInfo.Bucket)
                .WithObject(frameInfo.ObjectKey)
                .WithExpiry(_settings.PresignedUrlExpirationSeconds));
        
        _logger.LogDebug("Generated presigned URL for frame {FrameId}: {Bucket}/{Key}", 
            frameId, frameInfo.Bucket, frameInfo.ObjectKey);
        
        return presignedUrlResult.Url;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to generate frame URL for {FrameId}", frameId);
        return null;
    }
}

private async Task<FrameStorageInfo?> GetFrameStorageInfoAsync(string frameId, CancellationToken cancellationToken)
{
    // Parse the frame ID as GUID
    if (!Guid.TryParse(frameId, out var frameGuid))
    {
        _logger.LogWarning("Invalid frame ID format: {FrameId}", frameId);
        return null;
    }

    try
    {
        await using var connection = new Npgsql.NpgsqlConnection(_dbSettings.MediaConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Note: Table name is "Frames" (capital F) due to EF Core naming conventions
        var query = """
            SELECT "StorageBucket", "StorageKey" 
            FROM media."Frames" 
            WHERE "Id" = @Id
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("Id", frameGuid);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return new FrameStorageInfo(
                reader.GetString(0),
                reader.GetString(1)
            );
        }

        return null;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error querying frame storage info for {FrameId}", frameId);
        return null;
    }
}
```

**Purpose**: Generates presigned URLs for frames referenced in alerts. Uses direct database queries to fetch storage locations.

#### HealthCheckAsync Method

```csharp
public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
{
    try
    {
        return await _storageClient.BucketExistsAsync(
            _settings.DefaultBucket);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "OmnixStorage health check failed");
        return false;
    }
}
```

**Purpose**: Tests OmnixStorage connectivity by checking if the default bucket exists.

#### Helper Record

```csharp
private record FrameStorageInfo(string Bucket, string ObjectKey);
```

**Purpose**: Simple data structure to hold bucket and object key information.

---

## 7. AlertsEngine DataCleanupWorker

### DataCleanupWorker Class

Location: `src/Workers/AlertsEngine/Workers/DataCleanupWorker.cs`

#### Overview

Background service that periodically cleans up expired data from PostgreSQL, MinIO, and Redis. Uses OmnixStorage to delete old frame files from MinIO.

#### Constructor

```csharp
public sealed class DataCleanupWorker : BackgroundService, IDataCleanupService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataCleanupSettings _settings;
    private readonly MinioSettings _minioSettings;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DataCleanupWorker> _logger;
    private readonly IOmnixStorageClient _minioClient;
    
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    
    public DataCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DataCleanupSettings> settings,
        IOptions<MinioSettings> minioSettings,
        IConnectionMultiplexer redis,
        ILogger<DataCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _minioSettings = minioSettings.Value;
        _redis = redis;
        _logger = logger;
        _interval = TimeSpan.FromHours(_settings.IntervalHours);

        _logger.LogInformation(
            "AlertsEngine OmnixStorage config: Endpoint={Endpoint}, PublicEndpoint={PublicEndpoint}, Region={Region}, UseSsl={UseSsl}, AccessKey={AccessKey}",
            _minioSettings.Endpoint,
            _minioSettings.PublicEndpoint,
            _minioSettings.Region,
            _minioSettings.Secure,
            MaskKey(_minioSettings.AccessKey));
        
        // Create OmnixStorage client for cleanup operations
        _minioClient = new OmnixStorageClientBuilder()
            .WithEndpoint(_minioSettings.Endpoint)
            .WithCredentials(_minioSettings.AccessKey, _minioSettings.SecretKey)
            .WithRegion(_minioSettings.Region)
            .WithSSL(_minioSettings.Secure)
            .Build();
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "(null)";
        }

        if (key.Length <= 6)
        {
            return "***";
        }

        return $"{key.Substring(0, 3)}***{key.Substring(key.Length - 3)}";
    }
```

**Purpose**: Initializes the background worker with OmnixStorageClient for MinIO cleanup operations.

#### CleanupMinioAsync Method

```csharp
private async Task CleanupMinioAsync(CleanupStats stats, CancellationToken ct)
{
    try
    {
        _logger.LogInformation("Starting MinIO cleanup for bucket: {Bucket}", _minioSettings.DefaultBucket);
        
        // Check if bucket exists
        var bucketExists = await _minioClient.BucketExistsAsync(
            _minioSettings.DefaultBucket);
        
        if (!bucketExists)
        {
            _logger.LogWarning("MinIO bucket {Bucket} does not exist, skipping cleanup", _minioSettings.DefaultBucket);
            return;
        }
        
        // Calculate cutoff date based on default retention
        var cutoffDate = DateTime.UtcNow.AddDays(-_settings.DefaultRetentionDays);
        
        // MinIO archive path format: archive/{deviceId}/{yyyy-MM-dd}/{size}/{time}.jpg
        // We need to delete all objects in date folders older than cutoff
        
        // List all objects under archive/ prefix
        var objectsToDelete = new List<string>();
        var listArgs = new ListObjectsArgs()
            .WithBucket(_minioSettings.DefaultBucket)
            .WithPrefix("archive/");
        
        var listResult = await _minioClient.ListObjectsAsync(listArgs, ct);
        
        foreach (var item in listResult.Objects)
        {
            // Parse the date from path: archive/{deviceId}/{yyyy-MM-dd}/{size}/{time}.jpg
            var pathParts = item.Name.Split('/');
            if (pathParts.Length >= 3)
            {
                // pathParts[2] should be the date folder (yyyy-MM-dd)
                if (DateTime.TryParse(pathParts[2], out var objectDate))
                {
                    if (objectDate < cutoffDate)
                    {
                        objectsToDelete.Add(item.Name);
                    }
                }
            }
        }
        
        _logger.LogInformation("Found {Count} MinIO objects to delete (older than {Cutoff})", 
            objectsToDelete.Count, cutoffDate.ToString("yyyy-MM-dd"));
        
        // Delete objects one by one (more reliable than bulk delete)
        var deleted = 0;
        var errors = 0;
        foreach (var objectKey in objectsToDelete)
        {
            if (ct.IsCancellationRequested) break;
            
            try
            {
                await _minioClient.RemoveObjectAsync(
                    new RemoveObjectArgs()
                        .WithBucket(_minioSettings.DefaultBucket)
                        .WithObject(objectKey), 
                    ct);
                deleted++;
                
                if (deleted % 100 == 0)
                {
                    _logger.LogDebug("Deleted {Count} MinIO objects so far...", deleted);
                }
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning("Failed to delete MinIO object {Key}: {Error}", 
                    objectKey, ex.Message);
            }
        }
        
        stats.MinioObjectsDeleted = deleted;
        _logger.LogInformation("MinIO cleanup completed: {Count} objects deleted", deleted);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "MinIO cleanup failed");
    }
}
```

**Purpose**: Deletes expired frame files from MinIO. Uses `ListObjectsArgs` to list objects with an "archive/" prefix, parses the date from the path, and deletes files older than the retention cutoff date. Implements one-by-one deletion for reliability and progress logging.

#### Main Cleanup Cycle

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    if (!_settings.Enabled)
    {
        _logger.LogInformation("Data cleanup worker is disabled");
        return;
    }
    
    _logger.LogInformation(
        "Data cleanup worker started. Interval: {Hours}h, Default retention: {Days} days",
        _settings.IntervalHours, _settings.DefaultRetentionDays);
    
    // Wait a bit for other services to be ready before first cleanup
    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    
    // Run immediately on startup, then at interval
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await RunCleanupCycleWithRetryAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup cycle");
        }
        
        await Task.Delay(_interval, stoppingToken);
    }
    
    _logger.LogInformation("Data cleanup worker stopped");
}
```

**Purpose**: Main execution loop for the cleanup worker. Runs cleanup periodically with retry logic for transient failures.

---

## 8. Configuration Examples

### appsettings.json Configuration

```json
{
  "OmnixStorage": {
    "Endpoint": "37.60.228.216:9000",
    "PublicEndpoint": "https://storage.edgesentience.kegeosapps.com",
    "AccessKey": "admin",
    "SecretKey": "YOUR_SECRET_KEY",
    "UseSSL": false,
    "Region": "us-east-1",
    "DefaultBucket": "cameras",
    "PresignedUrlExpirationSeconds": 3600,
    "AutoCreateBucket": true,
    "BucketCreateMaxAttempts": 2,
    "BucketCreateRetryDelaySeconds": 2
  }
}
```

**Purpose**: Configuration for OmnixStorage settings used by dependency injection.

### Environment Variables

- `OmnixStorage__Endpoint`: Internal MinIO endpoint (e.g., `minio:9000`)
- `OmnixStorage__PublicEndpoint`: External-facing MinIO endpoint (e.g., `https://storage.example.com`)
- `OmnixStorage__AccessKey`: MinIO access key
- `OmnixStorage__SecretKey`: MinIO secret key
- `OmnixStorage__UseSSL`: Whether to use HTTPS/SSL
- `OmnixStorage__Region`: AWS region (default: `us-east-1`)
- `S3_ENDPOINT`: Override for Endpoint (takes precedence)
- `S3_REGION`: Override for Region (takes precedence)

**Purpose**: Environment-based configuration for containerized deployments.

---

## 9. Storage Path Formats

### Latest Frame Path

```
latest/{deviceId}.jpg
```

**Purpose**: Always overwritten with the most recent frame for a device. Used for live view.

**Example**: `latest/camera-001.jpg`

### Archive Path (Legacy)

```
archive/{deviceId}/{yyyy-MM-dd}/{HH-mm-ss}.jpg
```

**Purpose**: Historical frames stored by date and time.

**Example**: `archive/camera-001/2026-02-17/14-30-45.jpg`

### Archive Path (Multi-Resolution)

```
archive/{deviceId}/{yyyy-MM-dd}/{size}/{HH-mm-ss}.jpg
```

**Purpose**: Historical frames stored with multiple resolutions (full, medium, thumbnail).

**Sizes**: `full`, `medium`, `thumbnail`

**Example**: 
- `archive/camera-001/2026-02-17/full/14-30-45.jpg`
- `archive/camera-001/2026-02-17/medium/14-30-45.jpg`
- `archive/camera-001/2026-02-17/thumbnail/14-30-45.jpg`

---

## 10. OmnixStorage Operations Summary

| Operation | Method | Args Class | Purpose |
|-----------|--------|-----------|---------|
| Upload Object | `PutObjectAsync` | `PutObjectArgs` | Upload frames or files to MinIO |
| Download Object | `GetObjectAsync` | `GetObjectArgs` | Download frames or files from MinIO |
| Delete Object | `RemoveObjectAsync` | `RemoveObjectArgs` | Delete frames or files from MinIO |
| Copy Object | `CopyObjectAsync` | `CopyObjectArgs` | Copy objects within/between buckets |
| Batch Delete Objects | `RemoveObjectsAsync` | `RemoveObjectsArgs` | Delete multiple objects in one request |
| Initiate Multipart Upload | `InitiateMultipartUploadAsync` | `InitiateMultipartUploadArgs` | Start multipart upload and get UploadId |
| Upload Part | `UploadPartAsync` | `UploadPartArgs` | Upload a part for multipart upload |
| Complete Multipart Upload | `CompleteMultipartUploadAsync` | `CompleteMultipartUploadArgs` | Finalize multipart upload |
| Abort Multipart Upload | `AbortMultipartUploadAsync` | `AbortMultipartUploadArgs` | Cancel multipart upload |
| List Objects | `ListObjectsAsync` | `ListObjectsArgs` | List objects in a bucket with filters |
| Check Object Exists | `StatObjectAsync` | `StatObjectArgs` | Get metadata without downloading |
| Presigned GET URL | `PresignedGetObjectAsync` | `PresignedGetObjectArgs` | Generate time-limited download URLs |
| Presigned PUT URL | `PresignedPutObjectAsync` | `PresignedPutObjectArgs` | Generate time-limited upload URLs |
| Check Bucket Exists | `BucketExistsAsync` | (string bucketName) | Verify bucket existence |
| Create Bucket | `MakeBucketAsync` | `MakeBucketArgs` | Create a new bucket |
| List Buckets | `ListBucketsAsync` | (none) | List all buckets in MinIO |

---

## 11. Error Handling Patterns

### Silent Failure for Missing Objects

```csharp
catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("NoSuchKey"))
{
    // Object doesn't exist, nothing to delete
    _logger.LogDebug("Object not found...");
}
```

**Purpose**: Handles "object not found" errors gracefully, as deleting non-existent objects is idempotent.

### Retry Logic with Exponential Backoff

```csharp
var delay = TimeSpan.FromSeconds(10);
for (var attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        // Operation
        return;
    }
    catch (NpgsqlException ex) when (attempt < maxRetries)
    {
        _logger.LogWarning("Error (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
            attempt, maxRetries, delay.TotalSeconds);
        await Task.Delay(delay);
        delay *= 2;
    }
}
```

**Purpose**: Implements exponential backoff for transient database and network errors.

### Credential Masking

```csharp
private static string MaskKey(string? key)
{
    if (string.IsNullOrWhiteSpace(key))
        return "(empty)";

    if (key.Length <= 6)
        return $"{key[0]}***{key[^1]}";

    return $"{key[..3]}***{key[^3..]}";
}
```

**Purpose**: Prevents sensitive credentials from appearing in logs while maintaining debuggability.

---

## 12. Key Design Patterns

### Dual Client Pattern (Internal + Public Endpoints)

Multiple services maintain two separate OmnixStorageClient instances:
- One configured with the internal endpoint for service-to-service communication
- One configured with the public endpoint for generating presigned URLs

This ensures presigned URLs work from external networks without requiring endpoint replacement.

**SDK Tip**: Use `OmnixStorageClientFactory.CreatePublicEndpointClient(...)` to build the public endpoint client and avoid manual URI/port handling.

### Lazy Bucket Creation

MediaIngest implements automatic bucket creation with retry logic, reducing deployment complexity when buckets don't pre-exist.

### Health Checks

Services implement health check methods that verify OmnixStorage connectivity by performing basic operations (bucket listing, bucket existence check).

### Configuration Overrides

S3_ENDPOINT and S3_REGION environment variables override configuration file values, enabling flexible deployment scenarios without code changes.

---

## 13. Dependency Injection Registration Summary

### MediaIngest Services

- `IOmnixStorageClient`: Singleton instance
- `IStorageService`: Scoped implementation (`MinioStorageService`)

### DashboardApi Services

- `IOmnixStorageClient`: Singleton instance
- `IOmnixStorageService`: Scoped implementation (`OmnixStorageService`)
- `IFrameQueryService`: Scoped implementation (`FrameQueryService`)

### AlertsEngine Services

- `IOmnixStorageClient`: Instance in `DataCleanupWorker` constructor
- `IMinioService`: Scoped/transient as needed
- `DataCleanupWorker`: Hosted service for periodic cleanup

---

## 14. Docker Compose OmnixStorage Configuration

### MediaIngest Service Configuration

Location: `docker-compose.yaml` (mediaingest service)

```yaml
mediaingest:
  environment:
    OmnixStorage__Endpoint: storage.kegeosapps.com:443
    OmnixStorage__AccessKey: YOUR_ACCESS_KEY
    OmnixStorage__SecretKey: YOUR_SECRET_KEY
    OmnixStorage__UseSsl: "true"
    OmnixStorage__PublicEndpoint: https://storage.kegeosapps.com
    S3_ENDPOINT: storage.kegeosapps.com
    S3_REGION: us-east-1
```

**Purpose**: Configures MediaIngest to connect to OmnixStorage (MinIO) for frame ingestion and storage.

**Environment Variables**:
- `OmnixStorage__Endpoint`: Internal endpoint for service-to-service communication (requires port 443 for HTTPS)
- `OmnixStorage__AccessKey`: AWS access key for authentication
- `OmnixStorage__SecretKey`: AWS secret key for authentication
- `OmnixStorage__UseSsl`: Enables SSL/TLS for secure connections
- `OmnixStorage__PublicEndpoint`: External endpoint used for public presigned URLs (`https://storage.kegeosapps.com`)
- `S3_ENDPOINT`: Environment override for endpoint (takes precedence over `OmnixStorage__Endpoint`)
- `S3_REGION`: Environment override for region (takes precedence over configuration)

**Important Notes**:
- MediaIngest includes volume mounts for timezone synchronization (`/etc/timezone` and `/etc/localtime`) - critical for S3 SigV4 signature validation
- Uses `SYS_TIME` capability for time synchronization
- Healthcheck verifies connectivity via HTTP endpoint

---

### DashboardApi Service Configuration

Location: `docker-compose.yaml` (dashboardapi service)

```yaml
dashboardapi:
  environment:
    OmnixStorage__Endpoint: storage.kegeosapps.com:443
    OmnixStorage__AccessKey: YOUR_ACCESS_KEY
    OmnixStorage__SecretKey: YOUR_SECRET_KEY
    OmnixStorage__UseSsl: "true"
    OmnixStorage__PublicEndpoint: https://storage.kegeosapps.com
```

**Purpose**: Configures DashboardApi to generate presigned URLs and retrieve frames for the web dashboard.

**Key Characteristics**:
- Uses the same OmnixStorage credentials as MediaIngest
- `PublicEndpoint` ensures presigned URLs are browser-accessible
- No S3 environment overrides (uses direct OmnixStorage config)
- Volume mounts for timezone synchronization (`/etc/timezone` and `/etc/localtime`)

---

### AlertsEngine Service Configuration

Location: `docker-compose.yaml` (alertsengine service)

```yaml
alertsengine:
  environment:
    OmnixStorage__Endpoint: storage.kegeosapps.com:443
    OmnixStorage__PublicEndpoint: https://storage.kegeosapps.com
    OmnixStorage__AccessKey: YOUR_ACCESS_KEY
    OmnixStorage__SecretKey: YOUR_SECRET_KEY
    OmnixStorage__Secure: "true"
    OmnixStorage__DefaultBucket: cameras
    OmnixStorage__PresignedUrlExpirationSeconds: 3600
```

**Purpose**: Configures AlertsEngine for:
- Generating presigned frame URLs for Telegram/email alerts
- Deleting expired frames during cleanup cycles

**Additional Configuration**:
- `OmnixStorage__DefaultBucket`: Default bucket name (`cameras`)
- `OmnixStorage__PresignedUrlExpirationSeconds`: Presigned URL lifetime (3600 seconds = 1 hour)
- `OmnixStorage__Secure`: Boolean flag for SSL/TLS (name differs from `UseSsl` in code)

**Important Notes**:
- Volume mounts for timezone synchronization (critical for data cleanup operations and S3 signatures)
- `SYS_TIME` capability enabled for time synchronization

---

### AIWorker Service Configuration (Python)

Location: `docker-compose.yaml` (aiworker service)

```yaml
aiworker:
  environment:
    # MinIO Configuration (uses different naming convention than .NET services)
    MINIO_ENDPOINT: storage.kegeosapps.com:443
    MINIO_ACCESS_KEY: YOUR_ACCESS_KEY
    MINIO_SECRET_KEY: YOUR_SECRET_KEY
    MINIO_BUCKET: cameras
    MINIO_SECURE: "true"
```

**Purpose**: Configures the Python-based AI Worker for object detection and frame analysis.

**Environment Variable Naming Convention**:
- Python services use simplified naming: `MINIO_*` instead of `OmnixStorage__*`
- `MINIO_ACCESS_KEY` instead of `OmnixStorage__AccessKey`
- `MINIO_SECRET_KEY` instead of `OmnixStorage__SecretKey`
- `MINIO_ENDPOINT` instead of `OmnixStorage__Endpoint`
- `MINIO_SECURE` instead of `OmnixStorage__UseSsl`

**Key Differences**:
- Python environment uses flat naming (underscores) rather than .NET's double-underscore convention
- No public endpoint configuration (AIWorker doesn't generate presigned URLs)
- Direct bucket specification in environment

---

### Docker Compose Network Configuration

All services using OmnixStorage are connected to two networks:

```yaml
networks:
  - edgesentience-network  # Internal service-to-service communication
  - coolify                # External Coolify/Traefik network (for TLS termination)
```

**Impact on OmnixStorage**:
- Services communicate via `edgesentience-network` using internal endpoint
- Traefik on `coolify` network handles HTTPS/TLS termination for public endpoints
- Presigned URLs reference the public endpoint (`https://storage.kegeosapps.com`) for browser access

---

### Traefik Labels for OmnixStorage-Dependent Services

Services that use OmnixStorage include Traefik labels for routing:

#### MediaIngest Router

```yaml
labels:
  - "traefik.enable=true"
  - "traefik.http.routers.mediaingest.rule=Host(`ingest.edgesentience.kegeosapps.com`)"
  - "traefik.http.routers.mediaingest.entrypoints=http,https"
  - "traefik.http.routers.mediaingest.tls=true"
  - "traefik.http.routers.mediaingest.tls.certresolver=letsencrypt"
  - "traefik.http.services.mediaingest.loadbalancer.server.port=8080"
  # Increase body size limit for frame uploads (10 MB)
  - "traefik.http.middlewares.mediaingest-body.buffering.maxRequestBodyBytes=10485760"
  - "traefik.http.routers.mediaingest.middlewares=mediaingest-body"
```

**Purpose**: Ensures MediaIngest can receive large frame uploads via Traefik.

#### DashboardApi Router

```yaml
labels:
  - "traefik.enable=true"
  - "traefik.http.routers.dashboardapi.rule=Host(`api.edgesentience.kegeosapps.com`)"
  - "traefik.http.routers.dashboardapi.entrypoints=http,https"
  - "traefik.http.routers.dashboardapi.tls=true"
  - "traefik.http.routers.dashboardapi.tls.certresolver=letsencrypt"
  - "traefik.http.services.dashboardapi.loadbalancer.server.port=8080"
  # CORS headers for browser-based presigned URL access
  - "traefik.http.middlewares.dashboardapi-cors.headers.accesscontrolallowmethods=GET,OPTIONS,PUT,PATCH,POST,DELETE"
  - "traefik.http.middlewares.dashboardapi-cors.headers.accesscontrolallowheaders=*"
  - "traefik.http.middlewares.dashboardapi-cors.headers.accesscontrolalloworiginlist=*"
  - "traefik.http.routers.dashboardapi.middlewares=dashboardapi-cors"
```

**Purpose**: Routes frame requests to DashboardApi, which generates presigned URLs for OmnixStorage access.

---

### Environment Variable Precedence and Overrides

The docker-compose configuration demonstrates the following precedence:

1. **Explicit S3 Environment Variables** (if defined)
   - `S3_ENDPOINT` → overrides `OmnixStorage__Endpoint`
   - `S3_REGION` → overrides `OmnixStorage__Region`

2. **OmnixStorage Configuration Section**
   - `OmnixStorage__Endpoint`
   - `OmnixStorage__PublicEndpoint`
   - `OmnixStorage__AccessKey`
   - `OmnixStorage__SecretKey`
   - `OmnixStorage__UseSsl` / `OmnixStorage__Secure`
   - `OmnixStorage__Region`
   - `OmnixStorage__DefaultBucket`

3. **Fallback Defaults**
   - Defined in code (e.g., `localhost:9000` for endpoint)

**Example from MediaIngest DependencyInjection**:
```csharp
var s3Endpoint = configuration["S3_ENDPOINT"]?.Trim();
if (s3EndpointProvided)
{
    minioSettings.Endpoint = NormalizeEndpoint(s3Endpoint!);
}
```

This allows flexible deployment where S3_ENDPOINT environment variable overrides configuration file settings.

---

### Health Checks for OmnixStorage-Dependent Services

Services implement health checks that verify OmnixStorage connectivity:

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
  interval: 10s
  timeout: 10s
  retries: 10
  start_period: 120s
```

**Implementation in Code**:
- MediaIngest: `MinioStorageService.HealthCheckAsync()` - calls `ListBucketsAsync()`
- DashboardApi: Health endpoint checks database connectivity
- AlertsEngine: Health endpoint verifies service is running

---

### Credential Management in Docker Compose

**Current Implementation** (Development/Staging):
```yaml
OmnixStorage__AccessKey: YOUR_ACCESS_KEY
OmnixStorage__SecretKey: YOUR_SECRET_KEY
```

**Best Practice for Production**:
Replace hardcoded credentials with environment variable references:
```yaml
OmnixStorage__AccessKey: ${OMNIX_ACCESS_KEY:?OMNIX_ACCESS_KEY required}
OmnixStorage__SecretKey: ${OMNIX_SECRET_KEY:?OMNIX_SECRET_KEY required}
```

This requires passing credentials via:
- `.env` file (Coolify auto-detects)
- Docker secrets (for swarm mode)
- External secret management (e.g., HashiCorp Vault)

---

### Timezone Synchronization (Critical for S3 SigV4)

Services that perform OmnixStorage operations include timezone mounts:

```yaml
volumes:
  - /etc/timezone:/etc/timezone:ro
  - /etc/localtime:/etc/localtime:ro
```

**Reason**: S3 SigV4 signature validation requires matching client and server timestamps. Without timezone synchronization:
- Authentication failures
- Upload rejections
- "Authorization header is invalid" errors

**Affected Services**:
- MediaIngest
- DashboardApi
- AlertsEngine

---

## Summary

OmnixStorage is used throughout EdgeSentience for S3-compatible object storage operations:

- **MediaIngest**: Receives frames from devices, stores them to MinIO with automatic bucket creation
- **DashboardApi**: Retrieves frames, generates presigned URLs for browser access
- **AlertsEngine**: Generates URLs for alert frame references, cleans up expired frame files

All implementations follow consistent patterns for configuration, error handling, credential masking, and dual-endpoint URL generation.
