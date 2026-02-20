# OmnixStorage SDK - Local Package Integration Guide

**Version:** 2.0.0  
**SDKs:** .NET 10.0 | Python 3.8+  
**Package Locations:**
- .NET: `E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-dotnet\OmnixStorage.2.0.0.nupkg`
- Python: `E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-py\dist\omnix_storage-2.0.0-py3-none-any.whl`

---

## Choose Your SDK

- **[.NET Quick Start](#net-quick-start)** - For C# / ASP.NET Core applications
- **[Python Quick Start](#python-quick-start)** - For Python applications

---

## .NET Quick Start

### Quick Start for EdgeSentience Team

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

### Team Verification (Must Pass)

```powershell
# Confirm the feed exists
dotnet nuget list source

# Confirm package version resolved in EdgeSentience project
dotnet list package | Select-String "OmnixStorage"
# Expected: OmnixStorage 2.0.0
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

### 2. Using EdgeSentienceStorageService (Recommended)

```csharp
using EdgeSentience.Storage;

var storageService = new EdgeSentienceStorageService(
    internalEndpoint: "storage.kegeosapps.com:443",
    publicEndpoint: "https://storage-public.kegeosapps.com",
    accessKey: "edge-app-key",
    secretKey: "edge-app-secret",
    region: "us-east-1",
    defaultBucket: "edge-sentience-data"
);

// Ensure bucket exists
await storageService.EnsureBucketExistsAsync();

// Get presigned upload URL (30-minute expiry)
var uploadUrl = await storageService.GetUploadUrlAsync(
    objectName: $"uploads/{tenantId}/{Guid.NewGuid()}.jpg",
    expiryInSeconds: 1800
);

// Get presigned download URL (1-hour expiry)
var downloadUrl = await storageService.GetDownloadUrlAsync(
    objectName: "reports/monthly-2026-02.pdf",
    expiryInSeconds: 3600
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

// Guardrail behavior:
// - URL generation is rejected if hostname resolves to an internal endpoint
// - Hostname + expiry are logged for support diagnostics
```

### 3. Multi-Tenant Pattern

```csharp
// EdgeSentience tenant-specific storage paths
public class TenantStorageService
{
    private readonly EdgeSentienceStorageService _storage;
    
    public TenantStorageService(EdgeSentienceStorageService storage)
    {
        _storage = storage;
    }
    
    public async Task<string> GetUserUploadUrlAsync(string tenantId, string userId, string fileName)
    {
        var objectPath = $"tenants/{tenantId}/users/{userId}/uploads/{fileName}";
        return await _storage.GetUploadUrlAsync(objectPath, expiryInSeconds: 1800);
    }
    
    public async Task<string> GetReportDownloadUrlAsync(string tenantId, string reportId)
    {
        var objectPath = $"tenants/{tenantId}/reports/{reportId}.pdf";
        return await _storage.GetDownloadUrlAsync(objectPath, expiryInSeconds: 7200);
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
    
    // Register EdgeSentience dual-client wrapper with API guardrails
    services.AddSingleton<EdgeSentienceStorageService>(sp =>
    {
        return new EdgeSentienceStorageService(
            internalEndpoint: storageConfig["InternalEndpoint"]!,
            publicEndpoint: storageConfig["PublicEndpoint"]!,
            accessKey: storageConfig["AccessKey"]!,
            secretKey: storageConfig["SecretKey"]!,
            region: storageConfig["Region"] ?? "us-east-1",
            defaultBucket: storageConfig["DefaultBucket"] ?? "edge-sentience-data"
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

### 1.1 Validate Config Contract (Internal + Public Endpoints)

```json
{
    "OmnixStorage": {
        "InternalEndpoint": "storage.kegeosapps.com:443",
        "PublicEndpoint": "https://storage-public.kegeosapps.com"
    }
}
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

## Python Quick Start

### Installation from Local Wheel

```bash
# Install directly from the .whl file
pip install E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-py\dist\omnix_storage-2.0.0-py3-none-any.whl

# Verify installation
pip show omnix-storage
# Expected: Name: omnix-storage, Version: 2.0.0
```

### Alternative: Install from Source

```bash
# Navigate to Python SDK directory
cd E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-py

# Install in development mode
pip install -e .
```

---

## Python Integration Code Examples

### 1. Basic Setup

```python
from omnixstorage import OmnixClient

# Initialize client with dual endpoints
client = OmnixClient(
    endpoint="https://storage.kegeosapps.com",
    public_endpoint="https://storage-public.kegeosapps.com",
    access_key="your-access-key",
    secret_key="your-secret-key",
    use_https=True
)
```

### 2. Dual-Client Pattern for EdgeSentience

```python
from omnixstorage import OmnixClient

class EdgeSentienceStorageClient:
    """Python equivalent of EdgeSentienceStorageService"""
    
    def __init__(self, internal_endpoint: str, public_endpoint: str,
                 access_key: str, secret_key: str, default_bucket: str):
        self.client = OmnixClient(
            endpoint=internal_endpoint,
            public_endpoint=public_endpoint,
            access_key=access_key,
            secret_key=secret_key,
            use_https=True
        )
        self.default_bucket = default_bucket
    
    def ensure_bucket_exists(self):
        """Idempotent bucket creation"""
        return self.client.ensure_bucket_exists(self.default_bucket)
    
    def get_upload_url(self, object_name: str, expiry_seconds: int = 1800) -> str:
        """Get presigned PUT URL (browser-safe, uses public_endpoint)"""
        return self.client.presigned_put_object(
            self.default_bucket, 
            object_name, 
            expiry_seconds=expiry_seconds
        )
    
    def get_download_url(self, object_name: str, expiry_seconds: int = 3600) -> str:
        """Get presigned GET URL (browser-safe, uses public_endpoint)"""
        return self.client.presigned_get_object(
            self.default_bucket, 
            object_name, 
            expiry_seconds=expiry_seconds
        )
    
    def upload_file(self, object_name: str, file_data: bytes, content_type: str = "application/octet-stream"):
        """Upload file directly"""
        self.client.put_object(self.default_bucket, object_name, file_data, content_type)
    
    def delete_file(self, object_name: str, ignore_not_found: bool = True):
        """Delete file with optional ignore-not-found"""
        try:
            self.client.remove_object(self.default_bucket, object_name)
        except Exception as e:
            if not ignore_not_found:
                raise
    
    def health_check(self) -> bool:
        """Check storage connectivity"""
        try:
            buckets = list(self.client.health_check_buckets([self.default_bucket]))
            return len(buckets) > 0 and buckets[0]["accessible"]
        except Exception:
            return False

# Usage
storage = EdgeSentienceStorageClient(
    internal_endpoint="https://storage.kegeosapps.com",
    public_endpoint="https://storage-public.kegeosapps.com",
    access_key="edge-app-key",
    secret_key="edge-app-secret",
    default_bucket="edge-sentience-data"
)

storage.ensure_bucket_exists()

# Get presigned URLs (browser-safe, uses public endpoint)
upload_url = storage.get_upload_url(f"uploads/{tenant_id}/file.jpg", expiry_seconds=1800)
download_url = storage.get_download_url("reports/monthly-2026-02.pdf", expiry_seconds=3600)
```

### 3. Multi-Tenant Pattern (Python)

```python
from datetime import datetime

class TenantStorageService:
    """Python equivalent of multi-tenant storage"""
    
    def __init__(self, storage_client: EdgeSentienceStorageClient):
        self.storage = storage_client
    
    def get_user_upload_url(self, tenant_id: str, user_id: str, file_name: str) -> str:
        object_path = f"tenants/{tenant_id}/users/{user_id}/uploads/{file_name}"
        return self.storage.get_upload_url(object_path, expiry_seconds=1800)
    
    def get_report_download_url(self, tenant_id: str, report_id: str) -> str:
        object_path = f"tenants/{tenant_id}/reports/{report_id}.pdf"
        return self.storage.get_download_url(object_path, expiry_seconds=7200)
    
    def archive_tenant_data(self, tenant_id: str):
        """Archive tenant data to time-stamped folder"""
        source_prefix = f"tenants/{tenant_id}/"
        archive_date = datetime.utcnow().strftime("%Y-%m-%d")
        target_prefix = f"archives/{tenant_id}/{archive_date}/"
        
        # List all objects for tenant
        objects = self.storage.client.list_objects(
            self.storage.default_bucket, 
            prefix=source_prefix,
            recursive=True
        )
        
        # Copy each object to archive
        for obj in objects:
            source_name = obj["name"]
            target_name = source_name.replace(source_prefix, target_prefix, 1)
            self.storage.client.copy_object(
                self.storage.default_bucket,
                target_name,
                self.storage.default_bucket,
                source_name
            )
```

### 4. Configuration Pattern (Python)

```python
# config.py
import os
from dataclasses import dataclass

@dataclass
class OmnixStorageConfig:
    internal_endpoint: str
    public_endpoint: str
    access_key: str
    secret_key: str
    default_bucket: str
    use_ssl: bool = True

def load_config_from_env() -> OmnixStorageConfig:
    """Load from environment variables"""
    return OmnixStorageConfig(
        internal_endpoint=os.environ["OMNIX_INTERNAL_ENDPOINT"],
        public_endpoint=os.environ["OMNIX_PUBLIC_ENDPOINT"],
        access_key=os.environ["OMNIX_ACCESS_KEY"],
        secret_key=os.environ["OMNIX_SECRET_KEY"],
        default_bucket=os.environ.get("OMNIX_DEFAULT_BUCKET", "edge-sentience-data"),
        use_ssl=os.environ.get("OMNIX_USE_SSL", "true").lower() == "true"
    )

# Usage in application
config = load_config_from_env()
storage = EdgeSentienceStorageClient(
    internal_endpoint=config.internal_endpoint,
    public_endpoint=config.public_endpoint,
    access_key=config.access_key,
    secret_key=config.secret_key,
    default_bucket=config.default_bucket
)
```

---

## Python Verification Steps

### 1. Verify Installation

```bash
pip show omnix-storage
# Expected output:
# Name: omnix-storage
# Version: 2.0.0
```

### 2. Test Connection

```python
from omnixstorage import OmnixClient

client = OmnixClient(
    endpoint="https://storage.kegeosapps.com",
    access_key="your-key",
    secret_key="your-secret",
    use_https=True
)

# Test bucket access
exists = client.bucket_exists("edge-sentience-data")
print(f"Bucket accessible: {exists}")
```

### 3. Test Presigned URL Generation

```python
client = OmnixClient(
    endpoint="https://storage.kegeosapps.com",
    public_endpoint="https://storage-public.kegeosapps.com",
    access_key="your-key",
    secret_key="your-secret",
    use_https=True
)

# Generate browser-safe presigned PUT URL
url = client.presigned_put_object(
    "edge-sentience-data",
    "test.txt",
    expiry_seconds=3600
)

print(f"Upload URL: {url}")
# Should start with: https://storage-public.kegeosapps.com/edge-sentience-data/test.txt?X-Amz-Algorithm=...
```

### 4. Run Comprehensive Tests

```bash
# Navigate to Python SDK directory
cd E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-py

# Run parity tests
pytest tests/test_presigned_parity.py -v

# Run integration smoke tests
pytest tests/test_integration_smoke.py -v

# Expected: 6 passed, 1 xfailed (known server 405 endpoint)
```

---

## Python Troubleshooting

### Issue: "No module named 'omnixstorage'"

**Solution:**
```bash
# Verify installation
pip list | grep omnix-storage

# If not found, reinstall
pip install E:\google_drive\code\donet\OmnixStorageSDK\omnix-storage-py\dist\omnix_storage-2.0.0-py3-none-any.whl
```

### Issue: Presigned URLs not accessible from browser

**Solution:** Ensure `public_endpoint` is configured for browser-safe URLs:

```python
# ❌ Wrong - no public endpoint
client = OmnixClient(
    endpoint="https://internal-storage.local",
    access_key="...",
    secret_key="..."
)

# ✅ Correct - public endpoint configured
client = OmnixClient(
    endpoint="https://internal-storage.local",
    public_endpoint="https://storage-public.example.com",  # Browser-accessible
    access_key="...",
    secret_key="..."
)
```

### Issue: "InvalidHostError: Internal host detected"

**Solution:** This is a guardrail preventing internal endpoint leaks. Use a public endpoint:

```python
# Fix: Configure public_endpoint parameter
client = OmnixClient(
    endpoint="https://storage.internal.local",
    public_endpoint="https://storage.example.com",  # Add this
    access_key="...",
    secret_key="..."
)
```

---

## Python API Reference

For comprehensive Python SDK documentation, samples, and API reference, see:
- **[omnix-storage-py/README.md](../omnix-storage-py/README.md)** - Full Python SDK documentation
- **[tests/test_presigned_parity.py](../omnix-storage-py/tests/test_presigned_parity.py)** - Presigned URL tests
- **[tests/test_integration_smoke.py](../omnix-storage-py/tests/test_integration_smoke.py)** - Integration examples

---

## What's New in v2.0.0

### .NET SDK Features
- ✅ **OmnixStorageClientFactory**: Helper for creating public endpoint clients
- ✅ **OmnixStorageIntegrationService**: Convenience wrapper with common operations
- ✅ **EnsureBucketExistsAsync**: Extension method with retry logic
- ✅ **HealthCheckBucketsAsync**: Extension method for connectivity testing
- ✅ **CopyObjectAsync**: Copy objects within or across buckets
- ✅ **Empty-body fix**: Server compatibility for copy/multipart operations
- ✅ **XML Documentation**: Full IntelliSense support

### Python SDK Features
- ✅ **Local AWS SigV4 Signing**: Browser-safe presigned URLs with public endpoint support
- ✅ **presigned_get_object / presigned_put_object**: Generate browser-accessible URLs
- ✅ **Extended Operations**: copy_object, remove_objects (batch delete), multipart lifecycle
- ✅ **Guardrails**: Host validation preventing internal endpoint leaks
- ✅ **ensure_bucket_exists**: Idempotent bucket creation
- ✅ **health_check_buckets**: Connectivity validation
- ✅ **Comprehensive Tests**: 6 passing tests with edge-case validation

### Breaking Changes
None - both SDKs fully backward compatible with v1.x

### Server Limitations (Known - Affects Both SDKs)
- Batch delete (`RemoveObjectsAsync` / `remove_objects`) returns 405 MethodNotAllowed on some endpoints
- Multipart upload operations return 405 MethodNotAllowed on some endpoints
- These are server-side limitations, not SDK issues
- Test suites use `xfail` markers to document expected behavior

---

## Support

### .NET SDK Resources
- **Documentation:** See [SDK-INTEGRATION-GUIDE.md](../SDK-INTEGRATION-GUIDE.md)
- **Examples:** Check [omnixstorage_v2.0.md](./omnixstorage_v2.0.md)
- **Test Code:** Review [tests/OmnixStorage.Tests/Program.cs](./tests/OmnixStorage.Tests/Program.cs)

### Python SDK Resources  
- **Full Documentation:** See [omnix-storage-py/README.md](../omnix-storage-py/README.md)
- **Presigned URL Tests:** Check [omnix-storage-py/tests/test_presigned_parity.py](../omnix-storage-py/tests/test_presigned_parity.py)
- **Integration Tests:** Review [omnix-storage-py/tests/test_integration_smoke.py](../omnix-storage-py/tests/test_integration_smoke.py)

### General Resources
- **Release Notes:** See [SDK-IMPROVEMENTS-SUMMARY.md](../SDK-IMPROVEMENTS-SUMMARY.md)
- **Browser Guide:** See [PRESIGNED-URL-BROWSER-GUIDE.md](./PRESIGNED-URL-BROWSER-GUIDE.md)

---

**Package Details:**

**.NET Package:**
- **File:** `OmnixStorage.2.0.0.nupkg`
- **Size:** ~50 KB
- **Framework:** .NET 10.0
- **Created:** February 18, 2026
- **Dependencies:** None (uses only standard .NET libraries)

**Python Package:**
- **Files:** `omnix_storage-2.0.0-py3-none-any.whl`, `omnix_storage-2.0.0.tar.gz`
- **Size:** ~30 KB
- **Python:** 3.8+
- **Created:** February 20, 2026
- **Dependencies:** httpx>=0.24.0, python-dateutil>=2.8.2
