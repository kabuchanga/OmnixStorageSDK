# OmnixStorage SDK - Quick Reference Guide

**Version:** 2.0.0  
**Target:** .NET 10.0+  
**Authentication:** AWS Signature Version 4

---

## Installation

```bash
# NuGet Package Manager
Install-Package OmnixStorage

# .NET CLI
dotnet add package OmnixStorage

# Package Reference
<PackageReference Include="OmnixStorage" Version="2.0.0" />
```

---

## Quick Start (30 seconds)

```csharp
using OmnixStorage;
using OmnixStorage.Args;

// 1. Initialize client
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.example.com:443")
    .WithCredentials("YOUR_ACCESS_KEY", "YOUR_SECRET_KEY")
    .WithSSL(true)
    .Build();

// 2. Create bucket
await client.MakeBucketAsync("my-bucket");

// 3. Upload file
using var stream = File.OpenRead("photo.jpg");
await client.PutObjectAsync("my-bucket", "photo.jpg", stream);

// 4. Generate shareable URL
var url = await client.PresignedGetObjectAsync(
    new PresignedGetObjectArgs()
        .WithBucket("my-bucket")
        .WithObject("photo.jpg")
        .WithExpiry(3600)); // 1 hour

Console.WriteLine($"Share: {url.Url}");
```

---

## Client Configuration

### Basic Setup
```csharp
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("localhost:9000")              // Server endpoint
    .WithCredentials("accessKey", "secretKey")   // S3 credentials
    .WithSSL(false)                              // Use HTTP (default: false)
    .Build();
```

### Production Setup
```csharp
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.production.com:443")
    .WithCredentials(
        Environment.GetEnvironmentVariable("S3_ACCESS_KEY"),
        Environment.GetEnvironmentVariable("S3_SECRET_KEY"))
    .WithSSL(true)                               // Use HTTPS
    .WithRegion("us-east-1")                     // AWS region
    .WithRequestTimeout(TimeSpan.FromSeconds(60))
    .WithMaxRetries(5)                           // Retry failed requests
    .Build();
```

---

## Bucket Operations

### Create Bucket
```csharp
await client.MakeBucketAsync("my-bucket");

// With Args pattern
await client.MakeBucketAsync(
    new MakeBucketArgs().WithBucket("my-bucket"));
```

### Check if Bucket Exists
```csharp
bool exists = await client.BucketExistsAsync("my-bucket");
if (!exists)
{
    await client.MakeBucketAsync("my-bucket");
}
```

### List All Buckets
```csharp
var buckets = await client.ListBucketsAsync();
foreach (var bucket in buckets)
{
    Console.WriteLine($"{bucket.Name} - Created: {bucket.CreationDate}");
}
```

### Remove Bucket
```csharp
// Bucket must be empty
await client.RemoveBucketAsync("my-bucket");
```

---

## Object Operations

### Upload Object (Simple)
```csharp
using var stream = File.OpenRead("document.pdf");
await client.PutObjectAsync(
    "my-bucket", 
    "documents/report.pdf", 
    stream,
    "application/pdf");
```

### Upload Object (Full Control)
```csharp
using var stream = File.OpenRead("image.jpg");

var args = new PutObjectArgs()
    .WithBucket("my-bucket")
    .WithObject("images/photo.jpg")
    .WithStreamData(stream, stream.Length)
    .WithContentType("image/jpeg")
    .WithMetadata(new Dictionary<string, string>
    {
        ["user-id"] = "12345",
        ["upload-source"] = "web-app"
    });

var result = await client.PutObjectAsync(args);
Console.WriteLine($"ETag: {result.ETag}");
```

### Download Object
```csharp
using var output = File.OpenWrite("downloaded.pdf");

var args = new GetObjectArgs()
    .WithBucket("my-bucket")
    .WithObject("documents/report.pdf")
    .WithOutputStream(output);

var metadata = await client.GetObjectAsync(args);
Console.WriteLine($"Downloaded {metadata.Size} bytes");
```

### Get Object Metadata (Without Download)
```csharp
var args = new StatObjectArgs()
    .WithBucket("my-bucket")
    .WithObject("images/photo.jpg");

var metadata = await client.StatObjectAsync(args);
Console.WriteLine($"Size: {metadata.Size} bytes");
Console.WriteLine($"Content-Type: {metadata.ContentType}");
Console.WriteLine($"ETag: {metadata.ETag}");
Console.WriteLine($"Last Modified: {metadata.LastModified}");
```

### List Objects in Bucket
```csharp
var args = new ListObjectsArgs()
    .WithBucket("my-bucket")
    .WithPrefix("images/")        // Optional: filter by prefix
    .WithRecursive(true);         // Optional: list recursively

var result = await client.ListObjectsAsync(args);
foreach (var obj in result.Objects)
{
    Console.WriteLine($"{obj.Name} - {obj.Size} bytes");
}
```

### Remove Object
```csharp
var args = new RemoveObjectArgs()
    .WithBucket("my-bucket")
    .WithObject("documents/old-file.pdf");

await client.RemoveObjectAsync(args);
```

---

## Presigned URLs (Direct Browser Access)

### Generate Download URL (GET)
```csharp
// 1-hour expiry (default: 3600 seconds)
var args = new PresignedGetObjectArgs()
    .WithBucket("my-bucket")
    .WithObject("images/photo.jpg")
    .WithExpiry(3600);

var result = await client.PresignedGetObjectAsync(args);

// Share this URL - no authentication required
Console.WriteLine(result.Url);
// https://storage.example.com/my-bucket/images/photo.jpg?X-Amz-Algorithm=AWS4-HMAC-SHA256&...
```

### Generate Upload URL (PUT)
```csharp
// Generate URL for direct browser upload
var args = new PresignedPutObjectArgs()
    .WithBucket("my-bucket")
    .WithObject("uploads/user-file.jpg")
    .WithExpiry(1800); // 30 minutes

var result = await client.PresignedPutObjectAsync(args);

// Use in HTML form or fetch() API
Console.WriteLine(result.Url);
```

**Frontend Usage Example:**
```javascript
// JavaScript: Upload file using presigned URL
const presignedUrl = "https://storage.example.com/..."; // From backend

fetch(presignedUrl, {
    method: 'PUT',
    body: fileBlob,
    headers: {
        'Content-Type': 'image/jpeg'
    }
})
.then(response => console.log('Uploaded!'))
.catch(error => console.error('Upload failed:', error));
```

---

## Multi-Tenant Operations (Admin)

### Create Tenant
```csharp
var args = new CreateTenantArgs()
    .WithName("acme-corp")
    .WithDisplayName("Acme Corporation")
    .WithStorageQuota(10_737_418_240)  // 10 GB
    .WithMaxBuckets(5);

var tenant = await client.CreateTenantAsync(args);
Console.WriteLine($"Tenant ID: {tenant.Id}");
```

### List All Tenants
```csharp
var tenants = await client.ListTenantsAsync();
foreach (var tenant in tenants)
{
    Console.WriteLine($"{tenant.Name} - {tenant.StorageUsedBytes}/{tenant.StorageQuotaBytes} bytes");
}
```

### Delete Tenant
```csharp
// Tenant must have no buckets
await client.DeleteTenantAsync("tenant-id");
```

---

## Error Handling

### Basic Pattern
```csharp
try
{
    await client.PutObjectAsync("bucket", "key", stream);
}
catch (BucketNotFoundException ex)
{
    Console.WriteLine($"Bucket not found: {ex.Message}");
}
catch (ServerException ex)
{
    Console.WriteLine($"Server error: {ex.Message}");
    Console.WriteLine($"Status Code: {ex.StatusCode}");
}
catch (AuthenticationException ex)
{
    Console.WriteLine($"Auth failed: {ex.Message}");
}
catch (OmnixStorageException ex)
{
    Console.WriteLine($"Storage error: {ex.Message}");
}
```

### Advanced Pattern (With Retry Context)
```csharp
try
{
    await client.PutObjectAsync("bucket", "key", stream);
}
catch (ServerException ex)
{
    // New: Get detailed retry information
    Console.WriteLine($"Error: {ex.Message}");
    
    // Check inner exception for root cause
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Root cause: {ex.InnerException.Message}");
        
        // Example: Network timeout after 3 retries
        // "Request to https://storage.example.com/bucket/key failed after 3 attempts.
        //  Last error: A task was canceled."
    }
}
```

### Cancellation Support
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    await client.PutObjectAsync("bucket", "key", stream, cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Upload cancelled (timeout or user cancel)");
}
```

---

## Health Check

```csharp
bool healthy = await client.HealthAsync();
if (healthy)
{
    Console.WriteLine("Storage server is healthy");
}
else
{
    Console.WriteLine("Storage server is down");
}
```

---

## Best Practices

### 1. Use Dependency Injection
```csharp
// Startup.cs or Program.cs
services.AddSingleton<IOmnixStorageClient>(sp =>
{
    return new OmnixStorageClientBuilder()
        .WithEndpoint(configuration["OmnixStorage:Endpoint"])
        .WithCredentials(
            configuration["OmnixStorage:AccessKey"],
            configuration["OmnixStorage:SecretKey"])
        .WithSSL(true)
        .Build();
});

// In your service
public class DocumentService
{
    private readonly IOmnixStorageClient _storage;
    
    public DocumentService(IOmnixStorageClient storage)
    {
        _storage = storage;
    }
    
    public async Task UploadDocumentAsync(Stream stream)
    {
        await _storage.PutObjectAsync("docs", "file.pdf", stream);
    }
}
```

### 2. Always Dispose Streams
```csharp
// Good: using statement ensures disposal
using var stream = File.OpenRead("file.txt");
await client.PutObjectAsync("bucket", "file.txt", stream);

// Bad: Stream not disposed
var stream = File.OpenRead("file.txt");
await client.PutObjectAsync("bucket", "file.txt", stream);
// Memory leak!
```

### 3. Use Presigned URLs for Frontend Uploads
```csharp
// Backend: Generate presigned URL
[HttpGet("get-upload-url")]
public async Task<IActionResult> GetUploadUrl(string fileName)
{
    var args = new PresignedPutObjectArgs()
        .WithBucket("user-uploads")
        .WithObject($"uploads/{Guid.NewGuid()}/{fileName}")
        .WithExpiry(1800); // 30 minutes
    
    var result = await _storage.PresignedPutObjectAsync(args);
    return Ok(new { url = result.Url });
}

// Frontend: Upload directly to storage
async function uploadFile(file) {
    const response = await fetch('/api/get-upload-url?fileName=' + file.name);
    const { url } = await response.json();
    
    await fetch(url, {
        method: 'PUT',
        body: file,
        headers: { 'Content-Type': file.type }
    });
}
```

### 4. Check Bucket Exists Before Upload
```csharp
public async Task UploadAsync(string bucket, string key, Stream stream)
{
    if (!await _storage.BucketExistsAsync(bucket))
    {
        await _storage.MakeBucketAsync(bucket);
    }
    
    await _storage.PutObjectAsync(bucket, key, stream);
}
```

### 5. Use Metadata for Searchability
```csharp
var metadata = new Dictionary<string, string>
{
    ["user-id"] = userId,
    ["department"] = "engineering",
    ["project"] = "project-alpha",
    ["original-filename"] = Path.GetFileName(filePath),
    ["upload-timestamp"] = DateTime.UtcNow.ToString("o")
};

await client.PutObjectAsync(
    "my-bucket",
    objectKey,
    stream,
    "application/pdf",
    metadata);
```

---

## Performance Tips

### 1. Reuse HttpClient (Built-in)
The SDK automatically reuses HttpClient internally. No action needed.

### 2. Use Async Throughout
```csharp
// Good: Fully async
await client.PutObjectAsync("bucket", "key", stream);

// Bad: Blocking async code
client.PutObjectAsync("bucket", "key", stream).Wait(); // DON'T DO THIS
```

### 3. Parallel Uploads
```csharp
var files = Directory.GetFiles("uploads");
var uploadTasks = files.Select(async file =>
{
    using var stream = File.OpenRead(file);
    await client.PutObjectAsync("bucket", Path.GetFileName(file), stream);
});

await Task.WhenAll(uploadTasks);
```

### 4. Configure Request Timeout
```csharp
// For large files or slow networks
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.example.com")
    .WithCredentials("key", "secret")
    .WithRequestTimeout(TimeSpan.FromMinutes(10)) // 10 minute timeout
    .WithMaxRetries(5)                             // Retry 5 times
    .Build();
```

---

## Common Patterns

### Pattern: Upload and Get Shareable Link
```csharp
public async Task<string> UploadAndShareAsync(Stream fileStream, string fileName)
{
    // 1. Upload file
    await _storage.PutObjectAsync("shared-files", fileName, fileStream);
    
    // 2. Generate shareable URL (24 hours)
    var args = new PresignedGetObjectArgs()
        .WithBucket("shared-files")
        .WithObject(fileName)
        .WithExpiry(86400);
    
    var result = await _storage.PresignedGetObjectAsync(args);
    return result.Url;
}
```

### Pattern: Copy Between Buckets
```csharp
public async Task CopyObjectAsync(string sourceBucket, string sourceKey,
    string destBucket, string destKey)
{
    // Download from source
    using var tempStream = new MemoryStream();
    var getArgs = new GetObjectArgs()
        .WithBucket(sourceBucket)
        .WithObject(sourceKey)
        .WithOutputStream(tempStream);
    
    var metadata = await _storage.GetObjectAsync(getArgs);
    
    // Upload to destination
    tempStream.Position = 0;
    await _storage.PutObjectAsync(
        destBucket,
        destKey,
        tempStream,
        metadata.ContentType);
}
```

### Pattern: Batch Delete
```csharp
public async Task DeleteObjectsBatchAsync(string bucket, IEnumerable<string> keys)
{
    var deleteTasks = keys.Select(key =>
        _storage.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)));
    
    await Task.WhenAll(deleteTasks);
}
```

---

## Support & Resources

- **GitHub:** [Repository Link]
- **Documentation:** [Full API Documentation]
- **Examples:** See `tests/OmnixStorage.Tests/Program.cs`
- **AWS SigV4 Spec:** https://docs.aws.amazon.com/general/latest/gr/signature-version-4.html

---

## Version History

- **2.0.0** (2026-02-18)
  - ✅ OmnixStorageClientFactory for public endpoint clients
  - ✅ OmnixStorageIntegrationService convenience wrapper
  - ✅ EnsureBucketExistsAsync with retry logic
  - ✅ HealthCheckBucketsAsync extension method
  - ✅ CopyObjectAsync implementation
  - ✅ Empty-body fix for copy/multipart operations
  - ✅ Full XML documentation for IntelliSense
  - ✅ Comprehensive test suite

- **1.0.2** (2026-02-18)
  - Performance optimization with ReadOnlySpan<byte>
  - Enhanced error handling with retry context
  - Comprehensive XML documentation
  - .NET Standard 2.0 compatibility
  - Removed debug logging

- **1.0.1** (Previous)
  - Initial AWS SigV4 implementation
  - Presigned URL support
  - Multi-tenant operations

