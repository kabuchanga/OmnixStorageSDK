# OmnixStorage Presigned URL Browser Access - Troubleshooting Guide

## The Problem: Internal vs Public Endpoints

When EdgeSentience team members generate presigned URLs using the **internal endpoint**, they fail in browsers because:

```csharp
// ❌ WRONG - Internal endpoint (won't work in browser)
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com", 443, useSSL: true)  // ← Internal only
    .WithCredentials("key", "secret")
    .Build();

var url = await client.PresignedGetObjectAsync(...);
// Result: "https://storage.kegeosapps.com/bucket/object?..." 
// Browser tries to access this → Connection refused or not accessible
```

The URL looks correct but the internal endpoint is unreachable from external networks (browsers, client apps, etc.).

---

## The Solution: Use OmnixStorageClientFactory

Create a **separate public endpoint client** for presigned URLs:

```csharp
// ✅ CORRECT - Public endpoint for browser-safe URLs
var publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
    publicEndpoint: "https://storage-public.kegeosapps.com",  // ← Public-facing
    accessKey: "key",
    secretKey: "secret",
    region: "us-east-1"
);

var url = await publicClient.PresignedGetObjectAsync(...);
// Result: "https://storage-public.kegeosapps.com/bucket/object?..."
// Browser accesses this → Works! ✓
```

---

## Step-by-Step Integration for EdgeSentience Team

### 1. Identify Your Endpoints

```csharp
// From EdgeSentience appsettings.json or configuration:
var internalEndpoint = "storage.kegeosapps.com:443";        // Service-to-service
var publicEndpoint = "https://storage-public.kegeosapps.com";  // Browser access
var accessKey = "your-access-key";
var secretKey = "your-secret-key";
var region = "us-east-1";
```

### 2. Create Dual-Client Pattern

```csharp
// Internal client for core operations (bucket mgmt, direct uploads, etc.)
var internalClient = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com", 443, useSSL: true)
    .WithCredentials(accessKey, secretKey)
    .WithRegion(region)
    .Build();

// Public endpoint client for browser-safe presigned URLs
var publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
    publicEndpoint,    // "https://storage-public.kegeosapps.com"
    accessKey,
    secretKey,
    region
);
```

### 3. Use Correct Client for Each Operation

```csharp
// ✓ Internal client for storage operations
await internalClient.PutObjectAsync(...);        // Upload file
await internalClient.DeleteObjectAsync(...);     // Delete file
await internalClient.StatObjectAsync(...);       // Check file info
await internalClient.ListObjectsAsync(...);      // List files

// ✓ Internal client for bucket management
await internalClient.MakeBucketAsync(...);       // Create bucket
await internalClient.BucketExistsAsync(...);     // Check bucket

// ✓ PUBLIC client for presigned URLs (browser access)
await publicClient.PresignedGetObjectAsync(...); // Download URL for browser
await publicClient.PresignedPutObjectAsync(...); // Upload URL for browser
```

---

## Complete Working Example

```csharp
using OmnixStorage;

public class EdgeSentienceStorageService
{
    private readonly IOmnixStorageClient _internalClient;
    private readonly IOmnixStorageClient _publicClient;
    private readonly string _defaultBucket;

    public EdgeSentienceStorageService(string internalEndpoint, string publicEndpoint, string accessKey, string secretKey)
    {
        var endpointParts = internalEndpoint.Split(':');
        var internalHost = endpointParts[0];
        var internalPort = endpointParts.Length > 1 ? int.Parse(endpointParts[1]) : 443;

        // Create internal client for S3 operations
        _internalClient = new OmnixStorageClientBuilder()
            .WithEndpoint(internalHost, internalPort, useSSL: true)
            .WithCredentials(accessKey, secretKey)
            .WithRegion("us-east-1")
            .Build();

        // Create public client for browser-safe presigned URLs
        _publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
            publicEndpoint,
            accessKey,
            secretKey,
            "us-east-1"
        );

        _defaultBucket = "edge-sentience-data";
    }

    /// <summary>
    /// Upload file using internal client (backend operation)
    /// </summary>
    public async Task<string> UploadFileAsync(string objectName, Stream fileStream, string contentType = "application/octet-stream")
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
    /// Get BROWSER-ACCESSIBLE download URL (uses public endpoint)
    /// </summary>
    public async Task<string> GetDownloadUrlAsync(string objectName, int expirySeconds = 3600)
    {
        // ✓ Use PUBLIC client for browser access
        var result = await _publicClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(_defaultBucket)
                .WithObject(objectName)
                .WithExpiry(expirySeconds)
        );

        return result.Url;  // This URL works in browsers!
    }

    /// <summary>
    /// Get BROWSER-ACCESSIBLE upload URL (uses public endpoint)
    /// </summary>
    public async Task<string> GetUploadUrlAsync(string objectName, int expirySeconds = 1800)
    {
        // ✓ Use PUBLIC client for browser access
        var result = await _publicClient.PresignedPutObjectAsync(
            new PresignedPutObjectArgs()
                .WithBucket(_defaultBucket)
                .WithObject(objectName)
                .WithExpiry(expirySeconds)
        );

        return result.Url;  // This URL works in browsers!
    }

    /// <summary>
    /// Delete file using internal client
    /// </summary>
    public async Task DeleteFileAsync(string objectName)
    {
        // ✓ Use INTERNAL client for backend operations
        await _internalClient.RemoveObjectAsync(_defaultBucket, objectName);
    }
}
```

### Usage in EdgeSentience Controller/Service

```csharp
[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly EdgeSentienceStorageService _storage;

    public MediaController(EdgeSentienceStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Upload file to browser
    /// </summary>
    [HttpPost("upload-url")]
    public async Task<IActionResult> GetUploadUrl([FromBody] GetUploadUrlRequest req)
    {
        try
        {
            // Get browser-accessible upload URL
            string uploadUrl = await _storage.GetUploadUrlAsync(
                $"tenants/{req.TenantId}/uploads/{Guid.NewGuid()}.jpg",
                expirySeconds: 1800  // 30 minutes
            );

            return Ok(new { uploadUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Download file from browser
    /// </summary>
    [HttpPost("download-url")]
    public async Task<IActionResult> GetDownloadUrl([FromBody] GetDownloadUrlRequest req)
    {
        try
        {
            // Get browser-accessible download URL
            string downloadUrl = await _storage.GetDownloadUrlAsync(
                $"tenants/{req.TenantId}/files/{req.FileId}.pdf",
                expirySeconds: 3600  // 1 hour
            );

            return Ok(new { downloadUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete file (backend only)
    /// </summary>
    [HttpDelete("files/{fileId}")]
    public async Task<IActionResult> DeleteFile(string fileId)
    {
        try
        {
            await _storage.DeleteFileAsync($"tenants/{HttpContext.Items["TenantId"]}/files/{fileId}.pdf");
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

---

## Testing Presigned URLs in Browser

### Test 1: Verify URL is Accessible

```powershell
# Copy the presigned URL and test with curl
curl -v "https://storage-public.kegeosapps.com/bucket/object?X-Amz-Algorithm=AWS4-HMAC-SHA256&..."

# Expected: HTTP 200 OK + file content
# If you get 403 Forbidden → Check AWS SigV4 signature
# If you get 404 NotFound → Check bucket/object names
# If timeout/connection refused → Check public endpoint URL
```

### Test 2: Verify in Browser

1. Copy the full presigned URL
2. Paste in new browser tab
3. File should download or display

If it doesn't work:
- ❌ Check you're using **public endpoint client**, not internal
- ❌ Verify URL starts with `https://storage-public.kegeosapps.com` (not `storage.kegeosapps.com`)
- ❌ Check expiry hasn't passed
- ❌ Verify bucket/object exists

---

## Common Issues & Fixes

| Issue | Cause | Fix |
|-------|-------|-----|
| 403 Forbidden | Wrong credentials or invalid signature | Verify access key/secret key; use public endpoint client |
| 404 NotFound | Object doesn't exist | Upload file first using internal client |
| Connection refused | Using internal endpoint in browser | Use public endpoint with `OmnixStorageClientFactory` |
| 400 Bad Request | Malformed presigned URL | Check URL is complete and not truncated |
| URL expires too quickly | Expiry time too short | Increase expiry seconds (max 604800 = 7 days) |

---

## Implementation Checklist for EdgeSentience Team

- [ ] Identify your public endpoint URL (e.g., `https://storage-public.kegeosapps.com`)
- [ ] Update appsettings.json with both internal AND public endpoints
- [ ] Create `EdgeSentienceStorageService` with dual-client pattern
- [ ] Register in DI container (see example below)
- [ ] Use internal client for backend operations
- [ ] Use public client for presigned URLs
- [ ] Test URLs in browser
- [ ] Document in your API endpoints

### Dependency Injection Setup

```csharp
// In Program.cs
var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
var storageConfig = config.GetSection("OmnixStorage");

// Register the storage service
builder.Services.AddSingleton(sp => new EdgeSentienceStorageService(
    internalEndpoint: storageConfig["InternalEndpoint"]!,
    publicEndpoint: storageConfig["PublicEndpoint"]!,
    accessKey: storageConfig["AccessKey"]!,
    secretKey: storageConfig["SecretKey"]!
));

var app = builder.Build();
// ... rest of config
```

### Required appsettings.json Contract

```json
{
    "OmnixStorage": {
        "InternalEndpoint": "storage.kegeosapps.com:443",
        "PublicEndpoint": "https://storage-public.kegeosapps.com",
        "AccessKey": "edge-app-access-key",
        "SecretKey": "edge-app-secret-key",
        "Region": "us-east-1",
        "DefaultBucket": "edge-sentience-data",
        "UseSSL": true
    }
}
```

### Smoke Test Runbook (EdgeSentience Environment)

1. Upload one file with backend/internal client path.
2. Generate one presigned GET URL through `EdgeSentienceStorageService.GetDownloadUrlAsync(...)`.
3. Open the URL in browser and confirm HTTP 200 + file render/download.
4. Confirm logs include hostname and expiry for support debugging.

Expected outcomes:
- URL host equals the configured `PublicEndpoint` host.
- Any internal host is rejected by API guardrail with clear error.

---

## Key Takeaway

**Always use `OmnixStorageClientFactory.CreatePublicEndpointClient()` for any presigned URLs that will be accessed from browsers or external clients.**

The internal endpoint is for service-to-service communication only. Trying to access it from a browser will fail.
