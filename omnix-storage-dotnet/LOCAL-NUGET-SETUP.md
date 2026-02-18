# OmnixStorage SDK - Local NuGet Integration Guide

**Version:** 2.0.0  
**Target Framework:** .NET 10.0  
**Package Location:** `E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-dotnet\OmnixStorage.2.0.0.nupkg`

---

## Quick Start for EdgeSentience Team

### Option 1: Install from Local File (Recommended for Testing)

```powershell
# Navigate to your EdgeSentience project directory
cd C:\path\to\EdgeSentienceApp

# Install directly from the .nupkg file
dotnet add package OmnixStorage --source "E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-dotnet" --version 2.0.0
```

### Option 2: Add Local NuGet Source

```powershell
# Add local folder as a NuGet source (one-time setup)
dotnet nuget add source "E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-dotnet" --name "OmnixStorage-Local"

# Install the package from the local source
dotnet add package OmnixStorage --version 2.0.0
```

### Option 3: Manual Reference (For Development)

Add this to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="OmnixStorage" Version="2.0.0" />
</ItemGroup>
```

Then restore packages:

```powershell
dotnet restore --source "E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-dotnet"
```

---

## Integration Code Example

### 1. Basic Setup

```csharp
using OmnixStorage;

var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com", 443, useSSL: true)
    .WithCredentials("YOUR_ACCESS_KEY", "YOUR_SECRET_KEY")
    .WithRegion("us-east-1")
    .Build();
```

### 2. Using the Integration Service Wrapper

```csharp
using OmnixStorage;

// Create internal endpoint client
var internalClient = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com", 443, useSSL: true)
    .WithCredentials("edge-app-key", "edge-app-secret")
    .WithRegion("us-east-1")
    .Build();

// Create public endpoint client for browser-safe presigned URLs
var publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
    publicEndpoint: "https://storage-public.kegeosapps.com",
    accessKey: "edge-app-key",
    secretKey: "edge-app-secret",
    region: "us-east-1"
);

// Initialize integration service
var storageService = new OmnixStorageIntegrationService(
    internalClient,
    publicClient,
    defaultBucket: "edge-sentience-data"
);

// Ensure bucket exists
await storageService.EnsureBucketExistsAsync();

// Get presigned upload URL (30-minute expiry)
var uploadUrl = await storageService.GetPresignedUploadUrlAsync(
    objectName: $"uploads/{tenantId}/{Guid.NewGuid()}.jpg"
);

// Get presigned download URL (24-hour expiry)
var downloadUrl = await storageService.GetPresignedDownloadUrlAsync(
    objectName: "reports/monthly-2026-02.pdf",
    expiryInHours: 24
);

// Upload file directly
await storageService.UploadFileAsync(
    objectName: "config/settings.json",
    filePath: @"C:\config\app-settings.json",
    contentType: "application/json"
);

// Delete file (ignore if not found)
await storageService.DeleteFileAsync(
    objectName: "temp/old-file.dat",
    ignoreNotFound: true
);

// Health check
bool isHealthy = await storageService.HealthCheckAsync();
```

### 3. Multi-Tenant Pattern

```csharp
// EdgeSentience tenant-specific storage paths
public class TenantStorageService
{
    private readonly OmnixStorageIntegrationService _storage;
    
    public TenantStorageService(OmnixStorageIntegrationService storage)
    {
        _storage = storage;
    }
    
    public async Task<string> GetUserUploadUrlAsync(string tenantId, string userId, string fileName)
    {
        var objectPath = $"tenants/{tenantId}/users/{userId}/uploads/{fileName}";
        return await _storage.GetPresignedUploadUrlAsync(objectPath);
    }
    
    public async Task<string> GetReportDownloadUrlAsync(string tenantId, string reportId)
    {
        var objectPath = $"tenants/{tenantId}/reports/{reportId}.pdf";
        return await _storage.GetPresignedDownloadUrlAsync(objectPath, expiryInHours: 2);
    }
    
    public async Task ArchiveTenantDataAsync(string tenantId, string archivePath)
    {
        // Implementation for archiving tenant data
        var sourcePath = $"tenants/{tenantId}/";
        var targetPath = $"archives/{tenantId}/{DateTime.UtcNow:yyyy-MM-dd}/";
        
        // Use CopyObjectAsync for each file in tenant folder
        // (List objects, copy, then delete originals)
    }
}
```

---

## Configuration for EdgeSentience Platform

### appsettings.json

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

### Dependency Injection Setup

```csharp
// In Program.cs or Startup.cs
public static void ConfigureOmnixStorage(IServiceCollection services, IConfiguration config)
{
    var storageConfig = config.GetSection("OmnixStorage");
    
    // Register internal client
    services.AddSingleton<IOmnixStorageClient>(sp =>
    {
        var builder = new OmnixStorageClientBuilder()
            .WithEndpoint(
                storageConfig["InternalEndpoint"]!.Split(':')[0],
                int.Parse(storageConfig["InternalEndpoint"]!.Split(':')[1]),
                storageConfig.GetValue<bool>("UseSSL")
            )
            .WithCredentials(
                storageConfig["AccessKey"]!,
                storageConfig["SecretKey"]!
            )
            .WithRegion(storageConfig["Region"]!);
        
        return builder.Build();
    });
    
    // Register public endpoint client
    services.AddSingleton<IOmnixStorageClient>(sp =>
    {
        return OmnixStorageClientFactory.CreatePublicEndpointClient(
            storageConfig["PublicEndpoint"]!,
            storageConfig["AccessKey"]!,
            storageConfig["SecretKey"]!,
            storageConfig["Region"]!
        );
    });
    
    // Register integration service
    services.AddSingleton<OmnixStorageIntegrationService>(sp =>
    {
        var clients = sp.GetServices<IOmnixStorageClient>().ToArray();
        return new OmnixStorageIntegrationService(
            clients[0], // internal client
            clients[1], // public client
            storageConfig["DefaultBucket"]!
        );
    });
}
```

---

## Verification Steps

### 1. Verify Installation

```powershell
# Check installed packages
dotnet list package | Select-String "OmnixStorage"

# Should show:
# > OmnixStorage    2.0.0
```

### 2. Test Connection

```csharp
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com", 443, useSSL: true)
    .WithCredentials("your-key", "your-secret")
    .WithRegion("us-east-1")
    .Build();

// Test bucket access
bool exists = await client.BucketExistsAsync("edge-sentience-data");
Console.WriteLine($"Bucket accessible: {exists}");
```

### 3. Test Presigned URL Generation

```csharp
var publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
    "https://storage-public.kegeosapps.com",
    "your-key",
    "your-secret",
    "us-east-1"
);

var result = await publicClient.GetPresignedPutObjectUrlAsync(
    "edge-sentience-data",
    "test.txt",
    expiryInSeconds: 3600
);

Console.WriteLine($"Upload URL: {result.Url}");
// Should start with: https://storage-public.kegeosapps.com/edge-sentience-data/test.txt?...
```

---

## Troubleshooting

### Issue: "Package 'OmnixStorage 2.0.0' was not found"

**Solution:**
```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Re-add local source with full path
dotnet nuget add source "E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-dotnet" --name "OmnixStorage-Local"

# Verify source
dotnet nuget list source
```

### Issue: "Could not load file or assembly 'OmnixStorage'"

**Solution:** Ensure target framework compatibility. OmnixStorage 2.0.0 requires **.NET 10.0** or later.

```xml
<!-- In your project .csproj -->
<TargetFramework>net10.0</TargetFramework>
```

### Issue: Presigned URLs not accessible from browser

**Solution:** Use the public endpoint client for browser-safe URLs:

```csharp
// ❌ Wrong - internal endpoint
var internalClient = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com", 443, useSSL: true)
    ...

// ✅ Correct - public endpoint
var publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
    "https://storage-public.kegeosapps.com", ...
);
```

---

## What's New in v2.0.0

### New Features
- ✅ **OmnixStorageClientFactory**: Helper for creating public endpoint clients
- ✅ **OmnixStorageIntegrationService**: Convenience wrapper with common operations
- ✅ **EnsureBucketExistsAsync**: Extension method with retry logic
- ✅ **HealthCheckBucketsAsync**: Extension method for connectivity testing
- ✅ **CopyObjectAsync**: Copy objects within or across buckets
- ✅ **Empty-body fix**: Server compatibility for copy/multipart operations
- ✅ **XML Documentation**: Full IntelliSense support

### Breaking Changes
None - fully backward compatible with v1.x

### Server Limitations (Known)
- Batch delete (`RemoveObjectsAsync`) returns 405 MethodNotAllowed
- Multipart upload operations return 405 MethodNotAllowed
- These are server-side limitations, not SDK issues

---

## Support

For EdgeSentience integration questions:
- **Documentation:** See [SDK-INTEGRATION-GUIDE.md](../SDK-INTEGRATION-GUIDE.md)
- **Examples:** Check [omnixstorage_v2.0.md](./omnixstorage_v2.0.md)
- **Test Code:** Review [tests/OmnixStorage.Tests/Program.cs](./tests/OmnixStorage.Tests/Program.cs)

---

**Package Details:**
- **File:** `OmnixStorage.2.0.0.nupkg`
- **Size:** ~50 KB
- **Created:** February 18, 2026
- **Dependencies:** None (uses only standard .NET libraries)
