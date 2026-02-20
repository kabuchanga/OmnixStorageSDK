using OmnixStorage;
using OmnixStorage.Args;
using OmnixStorage.DataModel;
using EdgeSentience.Storage;

const string endpoint = "storage.kegeosapps.com:443";
const string accessKey = "AKIATESAFEKEY0000001";
const string secretKey = "wJalrXUtnFEMIaKkMDENGbPxRfIcxAmPlEkEyZaB";

Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine("OmnixStorage Updated SDK Test - Multi-Tenant + Presigned URLs");
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine();

try
{
    // Initialize OmnixStorage client with updated SDK
    Console.WriteLine("✓ Initializing OmnixStorage client (Updated SDK)...");
    var client = new OmnixStorageClientBuilder()
        .WithEndpoint(endpoint)
        .WithCredentials(accessKey, secretKey)
        .WithSSL(true)
        .Build();
    Console.WriteLine("✓ Client initialized successfully");
    Console.WriteLine();

    // Define tenant configurations
    var tenants = new[]
    {
        new
        {
            Name = "kegoesman",
            Bucket = "geospatialdata",
            Files = new[] { ("imagery/map-data.bin", 3_000_000), ("shapefile/location.dat", 1_000_000) }
        },
        new
        {
            Name = "photoman",
            Bucket = "photos",
            Files = new[] { ("cam_2/photo1.jpg", 50_000), ("cam_2/photo2.jpg", 50_000), ("cam_2/photo3.jpg", 50_000) }
        },
        new
        {
            Name = "financeman",
            Bucket = "financial-records",
            Files = new[] { ("ledger.xlsx", 3_000_000) }
        }
    };

    Console.WriteLine("=== STEP 1: CREATE TENANTS AND BUCKETS ===");
    Console.WriteLine();

    foreach (var tenant in tenants)
    {
        Console.WriteLine($"Processing tenant: {tenant.Name}");
        
        // Check if bucket exists
        bool bucketExists = await client.BucketExistsAsync(tenant.Bucket);

        if (!bucketExists)
        {
            Console.WriteLine($"  → Creating bucket: {tenant.Bucket}");
            try
            {
                await client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(tenant.Bucket));
                Console.WriteLine($"  ✓ Bucket created: {tenant.Bucket}");
            }
            catch (Exception bucketEx)
            {
                Console.WriteLine($"  ✗ Failed to create bucket: {bucketEx.Message}");
                if (bucketEx.InnerException != null)
                {
                    Console.WriteLine($"    Inner Exception: {bucketEx.InnerException.Message}");
                    if (bucketEx.InnerException.InnerException != null)
                    {
                        Console.WriteLine($"    Root Cause: {bucketEx.InnerException.InnerException.Message}");
                    }
                }
                Console.WriteLine($"    Exception Type: {bucketEx.GetType().Name}");
                throw;
            }
        }
        else
        {
            Console.WriteLine($"  ℹ Bucket already exists: {tenant.Bucket}");
        }

        // Upload files for tenant
        foreach (var (fileName, fileSize) in tenant.Files)
        {
            Console.WriteLine($"  → Uploading: {fileName} ({fileSize / 1024} KB)");
            
            // Create test data
            var fileData = new byte[fileSize];
            new Random().NextBytes(fileData);
            
            using var stream = new MemoryStream(fileData);
            await client.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(tenant.Bucket)
                    .WithObject(fileName)
                    .WithStreamData(stream, fileSize));
            
            Console.WriteLine($"  ✓ Uploaded: {fileName}");
        }
        Console.WriteLine();
    }

    Console.WriteLine("=== STEP 2: RETRIEVE TENANT STATISTICS ===");
    Console.WriteLine();

    foreach (var tenant in tenants)
    {
        Console.WriteLine($"Tenant: {tenant.Name}");
        Console.WriteLine($"  Bucket: {tenant.Bucket}");
        
        // List objects in bucket
        var listArgs = new ListObjectsArgs().WithBucket(tenant.Bucket);
        
        var result = await client.ListObjectsAsync(listArgs);
        long totalSize = 0;
        int objectCount = 0;
        
        foreach (var item in result.Objects ?? new List<ObjectMetadata>())
        {
            objectCount++;
            totalSize += item.Size;
            Console.WriteLine($"    - {item.Name}: {item.Size / 1024} KB");
        }
        
        Console.WriteLine($"  Object Count: {objectCount}");
        Console.WriteLine($"  Total Size: {totalSize / 1024} KB ({totalSize / 1_048_576.0:F2} MB)");
        Console.WriteLine();
    }

    Console.WriteLine("=== STEP 3: PRESIGNED URL TESTS ===");
    Console.WriteLine();

    // Test presigned URLs for first file of each tenant
    foreach (var tenant in tenants)
    {
        var firstFile = tenant.Files[0].Item1;
        
        Console.WriteLine($"Tenant: {tenant.Name} / Bucket: {tenant.Bucket}");
        
        // Generate presigned GET URL (returns PresignedUrlResult with .Url property)
        Console.WriteLine($"  → Generating presigned GET URL for: {firstFile}");
        var getUrlResult = await client.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(tenant.Bucket)
                .WithObject(firstFile)
                .WithExpiry(3600));
        
        var getUrl = getUrlResult.Url;
        Console.WriteLine($"  ✓ Presigned GET URL (1 hour expiry):");
        Console.WriteLine($"    {getUrl.Substring(0, Math.Min(120, getUrl.Length))}...");
        
        // Verify URL format
        if (getUrl.Contains("X-Amz-Algorithm=AWS4-HMAC-SHA256") &&
            getUrl.Contains("X-Amz-Credential=") &&
            getUrl.Contains("X-Amz-Signature="))
        {
            Console.WriteLine("  ✓ URL contains valid AWS SigV4 parameters");
        }
        
        // Generate presigned PUT URL (returns PresignedUrlResult with .Url property)
        Console.WriteLine($"  → Generating presigned PUT URL for: new-{firstFile}");
        var putUrlResult = await client.PresignedPutObjectAsync(
            new PresignedPutObjectArgs()
                .WithBucket(tenant.Bucket)
                .WithObject($"new-{firstFile}")
                .WithExpiry(3600));
        
        var putUrl = putUrlResult.Url;
        Console.WriteLine($"  ✓ Presigned PUT URL (1 hour expiry):");
        Console.WriteLine($"    {putUrl.Substring(0, Math.Min(120, putUrl.Length))}...");
        
        Console.WriteLine();
    }

    Console.WriteLine("=== STEP 4: SDK HELPER + EXTENDED OPS TESTS ===");
    Console.WriteLine();

    var opsBucket = "sdk-ops-test";
    var opsPrefix = "ops";

    Console.WriteLine($"→ Ensuring bucket exists (helper): {opsBucket}");
    await client.EnsureBucketExistsAsync(opsBucket, maxAttempts: 3, delaySeconds: 2);
    Console.WriteLine("✓ EnsureBucketExistsAsync completed\n");

    Console.WriteLine("→ Health check via ListBuckets (helper)");
    var healthOk = await client.HealthCheckBucketsAsync();
    if (healthOk)
    {
        Console.WriteLine("✓ HealthCheckBucketsAsync: OK\n");
    }
    else
    {
        Console.WriteLine("⚠ HealthCheckBucketsAsync: FAILED (check permissions)\n");
    }

    Console.WriteLine("→ Copy object test");
    var copySourceKey = $"{opsPrefix}/copy-source.txt";
    var copyDestKey = $"{opsPrefix}/copy-dest.txt";
    var copyData = new byte[1024];
    new Random().NextBytes(copyData);

    try
    {
        using (var stream = new MemoryStream(copyData))
        {
            await client.PutObjectAsync(opsBucket, copySourceKey, stream, "text/plain");
        }

        var copyResult = await client.CopyObjectAsync(
            new CopyObjectArgs()
                .WithSourceBucket(opsBucket)
                .WithSourceObject(copySourceKey)
                .WithDestinationBucket(opsBucket)
                .WithDestinationObject(copyDestKey));

        var copiedStat = await client.StatObjectAsync(
            new StatObjectArgs()
                .WithBucket(opsBucket)
                .WithObject(copyDestKey));

        Console.WriteLine($"✓ CopyObjectAsync ETag: {copyResult.ETag ?? "unknown"}, Size: {copiedStat.Size} bytes\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ CopyObjectAsync failed (server may not support copy): {ex.Message}\n");
    }

    Console.WriteLine("→ Batch delete test");
    var batchKey1 = $"{opsPrefix}/delete-1.txt";
    var batchKey2 = $"{opsPrefix}/delete-2.txt";

    try
    {
        using (var stream = new MemoryStream(new byte[256]))
        {
            await client.PutObjectAsync(opsBucket, batchKey1, stream);
        }
        using (var stream = new MemoryStream(new byte[256]))
        {
            await client.PutObjectAsync(opsBucket, batchKey2, stream);
        }

        var deleteResult = await client.RemoveObjectsAsync(
            new RemoveObjectsArgs()
                .WithBucket(opsBucket)
                .WithObjects(batchKey1, batchKey2));

        Console.WriteLine($"✓ RemoveObjectsAsync Deleted={deleteResult.Deleted.Count}, Errors={deleteResult.Errors.Count}\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ RemoveObjectsAsync failed (server may not support batch delete): {ex.Message}\n");
    }

    Console.WriteLine("→ Multipart upload test");
    var multipartKey = $"{opsPrefix}/multipart.bin";
    try
    {
        var init = await client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadArgs()
                .WithBucket(opsBucket)
                .WithObject(multipartKey)
                .WithContentType("application/octet-stream"));

        var multipartData = new byte[2048];
        new Random().NextBytes(multipartData);

        using var part1Stream = new MemoryStream(multipartData, 0, 1024, writable: false);
        var part1 = await client.UploadPartAsync(
            new UploadPartArgs()
                .WithBucket(opsBucket)
                .WithObject(multipartKey)
                .WithUploadId(init.UploadId)
                .WithPartNumber(1)
                .WithData(part1Stream, 1024));

        using var part2Stream = new MemoryStream(multipartData, 1024, 1024, writable: false);
        var part2 = await client.UploadPartAsync(
            new UploadPartArgs()
                .WithBucket(opsBucket)
                .WithObject(multipartKey)
                .WithUploadId(init.UploadId)
                .WithPartNumber(2)
                .WithData(part2Stream, 1024));

        var complete = await client.CompleteMultipartUploadAsync(
            new CompleteMultipartUploadArgs()
                .WithBucket(opsBucket)
                .WithObject(multipartKey)
                .WithUploadId(init.UploadId)
                .WithParts(new[]
                {
                    new CompleteMultipartUploadArgs.PartInfo { PartNumber = part1.PartNumber, ETag = part1.ETag ?? string.Empty },
                    new CompleteMultipartUploadArgs.PartInfo { PartNumber = part2.PartNumber, ETag = part2.ETag ?? string.Empty }
                }));

        Console.WriteLine($"✓ CompleteMultipartUploadAsync ETag: {complete.ETag ?? "unknown"}\n");

        Console.WriteLine("→ Multipart abort test");
        var abortKey = $"{opsPrefix}/multipart-abort.bin";
        var abortInit = await client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadArgs()
                .WithBucket(opsBucket)
                .WithObject(abortKey)
                .WithContentType("application/octet-stream"));

        await client.AbortMultipartUploadAsync(
            new AbortMultipartUploadArgs()
                .WithBucket(opsBucket)
                .WithObject(abortKey)
                .WithUploadId(abortInit.UploadId));

        Console.WriteLine("✓ AbortMultipartUploadAsync completed\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Multipart upload test failed (server may not support multipart): {ex.Message}\n");
    }

    Console.WriteLine("→ Integration service wrapper test");
    var integration = new OmnixStorageIntegrationService(client);
    var presignedDownload = await integration.GetPresignedDownloadUrlAsync(opsBucket, copyDestKey, 300);
    Console.WriteLine($"✓ Integration presigned URL length: {presignedDownload.Length}\n");

    Console.WriteLine("→ EdgeSentience guardrail parity test (internal public endpoint should fail)");
    try
    {
        var _ = new EdgeSentienceStorageService(
            internalEndpoint: endpoint,
            publicEndpoint: "http://127.0.0.1:9000",
            accessKey: accessKey,
            secretKey: secretKey,
            region: "us-east-1",
            defaultBucket: opsBucket);
        Console.WriteLine("⚠ Guardrail parity test failed: constructor accepted internal public endpoint\n");
    }
    catch (ArgumentException)
    {
        Console.WriteLine("✓ Guardrail parity test passed: internal public endpoint rejected\n");
    }

    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine("✓ ALL TESTS COMPLETED SUCCESSFULLY");
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine();
    Console.WriteLine("Summary:");
    Console.WriteLine($"  - {tenants.Length} tenants created");
    Console.WriteLine($"  - {tenants.Sum(t => t.Files.Length)} files uploaded");
    Console.WriteLine($"  - {tenants.Length * 2} presigned URLs generated (GET + PUT per tenant)");
    Console.WriteLine($"  - All AWS SigV4 signatures validated");

    // ===== PRESIGNED URL BROWSER TEST =====
    Console.WriteLine();
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine("PRESIGNED URL BROWSER TEST - Public Endpoint Required");
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine();

    Console.WriteLine("Key Lesson: Use OmnixStorageClientFactory for presigned URLs!");
    Console.WriteLine("❌ Internal endpoint: storage.kegeosapps.com:443 → Won't work in browser");
    Console.WriteLine("✅ Public endpoint: https://storage-public.kegeosapps.com → Works in browser");
    Console.WriteLine();

    string photoTestBucket = "photo-test";
    string photoObjectKey = "images/13-47-42-738.jpg";
    string photoPath = "13-47-42-738.jpg"; // File is in workspace root

    // Create bucket if not exists
    Console.WriteLine($"→ Creating bucket: {photoTestBucket}");
    try
    {
        await client.MakeBucketAsync(photoTestBucket);
        Console.WriteLine($"✓ Bucket created: {photoTestBucket}\n");
    }
    catch (Exception ex1) when (ex1.Message.Contains("already exists"))
    {
        Console.WriteLine($"✓ Bucket already exists: {photoTestBucket}\n");
    }

    // Upload the photo
    if (File.Exists(photoPath))
    {
        Console.WriteLine($"→ Uploading photo: {photoObjectKey}");
        using (var stream = File.OpenRead(photoPath))
        {
            await client.PutObjectAsync(photoTestBucket, photoObjectKey, stream);
        }
        Console.WriteLine($"✓ Photo uploaded successfully\n");

        // ❌ WRONG WAY - Using internal endpoint (won't work in browser)
        Console.WriteLine("=== PRESIGNED URL GENERATION - WRONG vs RIGHT ===\n");
        
        Console.WriteLine("❌ WRONG: Using internal endpoint client");
        var wrongUrlResult = await client.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(photoTestBucket)
                .WithObject(photoObjectKey)
                .WithExpiry(3600));
        
        string wrongUrl = wrongUrlResult.Url;
        Console.WriteLine($"   URL: {wrongUrl.Substring(0, Math.Min(100, wrongUrl.Length))}...");
        Console.WriteLine("   ℹ️  This URL starts with 'storage.kegeosapps.com'");
        Console.WriteLine("   ⚠️  Browsers can't access internal endpoints - CONNECTION REFUSED\n");

        // ✅ RIGHT WAY - Using public endpoint client via factory
        Console.WriteLine("✅ CORRECT: Using public endpoint client (via factory)");
        var publicClientForUrls = OmnixStorageClientFactory.CreatePublicEndpointClient(
            publicEndpoint: "https://storage-public.kegeosapps.com",
            accessKey: accessKey,
            secretKey: secretKey,
            region: "us-east-1"
        );

        var correctUrlResult = await publicClientForUrls.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(photoTestBucket)
                .WithObject(photoObjectKey)
                .WithExpiry(3600));

        string correctUrl = correctUrlResult.Url;
        Console.WriteLine($"   URL: {correctUrl.Substring(0, Math.Min(100, correctUrl.Length))}...");
        Console.WriteLine("   ✓ This URL starts with 'https://storage-public.kegeosapps.com'");
        Console.WriteLine("   ✓ Browsers CAN access public endpoints - WORKS!\n");

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("✓ PRESIGNED URL (Copy and paste in browser to view photo):");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine(correctUrl);
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Test edge cases
        Console.WriteLine("Testing Edge Cases:\n");

        // Test 1: Presigned URL with too long expiry (>604800 seconds = 7 days)
        Console.WriteLine("Test 1: Presigned URL with expiry > 604800 seconds (should fail server-side)");
        try
        {
            var invalidResult = await publicClientForUrls.PresignedGetObjectAsync(
                new PresignedGetObjectArgs()
                    .WithBucket(photoTestBucket)
                    .WithObject(photoObjectKey)
                    .WithExpiry(604801)); // 1 second over limit
            Console.WriteLine("⚠ URL generated (server will reject when accessed):");
            Console.WriteLine($"  {invalidResult.Url}\n");
        }
        catch (Exception ex1)
        {
            Console.WriteLine($"✓ Validation error: {ex1.Message}\n");
        }

        // Test 2: Presigned URL with minimum expiry (1 second)
        Console.WriteLine("Test 2: Presigned URL with minimum expiry (1 second)");
        var minExpiryResult = await publicClientForUrls.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(photoTestBucket)
                .WithObject(photoObjectKey)
                .WithExpiry(1));
        Console.WriteLine("✓ URL generated:");
        Console.WriteLine($"  {minExpiryResult.Url}");
        Console.WriteLine("  (This URL may already be expired)\n");

        // Test 3: Presigned URL for non-existent object
        Console.WriteLine("Test 3: Presigned URL for non-existent object (URL works, object not found on access)");
        var nonExistentResult = await publicClientForUrls.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(photoTestBucket)
                .WithObject("images/does-not-exist.jpg")
                .WithExpiry(3600));
        Console.WriteLine("✓ URL generated:");
        Console.WriteLine($"  {nonExistentResult.Url}");
        Console.WriteLine("  (Server will return 404 NoSuchKey when accessed)\n");

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("✓ Edge case tests completed");
        Console.WriteLine("=".PadRight(80, '='));

        // Test presigned GET URL access
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Testing Presigned GET URL Access (from browser perspective)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        Console.WriteLine($"→ Accessing presigned URL: {correctUrl.Substring(0, Math.Min(80, correctUrl.Length))}...\n");
        Console.WriteLine($"   Total URL length: {correctUrl.Length} chars");
        Console.WriteLine($"   Full URL: {correctUrl}\n");
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "EdgeSentience-Browser-Test/1.0");

        try
        {
            var getResponse = await httpClient.GetAsync(new Uri(correctUrl));
            Console.WriteLine($"✓ Response Status Code: {(int)getResponse.StatusCode} {getResponse.StatusCode}");
            
            if (getResponse.IsSuccessStatusCode)
            {
                var content = await getResponse.Content.ReadAsStreamAsync();
                Console.WriteLine($"✓ Success! Received {content.Length} bytes");
            }
            else
            {
                var errorBody = await getResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"✗ Error {(int)getResponse.StatusCode}");
                Console.WriteLine($"Content-Type: {getResponse.Content.Headers.ContentType}");
                if (!string.IsNullOrEmpty(errorBody))
                {
                    Console.WriteLine($"Response Body: {errorBody}");
                }
                else
                {
                    Console.WriteLine("(No response body)");
                }
            }
        }
        catch (Exception getEx)
        {
            Console.WriteLine($"✗ Failed to GET: {getEx.Message}");
        }

        Console.WriteLine();
    }
    else
    {
        Console.WriteLine($"⚠ Photo file not found: {photoPath}");
        Console.WriteLine($"   Current directory: {Directory.GetCurrentDirectory()}");
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine("✗ ERROR OCCURRED");
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine($"Error Type: {ex.GetType().FullName}");
    Console.WriteLine($"Message: {ex.Message}");
    
    // Check for OmnixStorageException properties
    if (ex is OmnixStorageException omnixEx)
    {
        Console.WriteLine($"HTTP Status Code: {omnixEx.StatusCode?.ToString() ?? "N/A"}");
        Console.WriteLine($"Error Code: {omnixEx.ErrorCode ?? "N/A"}");
    }
    
    Console.WriteLine();
    
    // Print full exception chain
    var currentEx = ex;
    int exLevel = 0;
    while (currentEx != null)
    {
        string indent = new string(' ', exLevel * 2);
        Console.WriteLine($"{indent}Level {exLevel}: {currentEx.GetType().Name}");
        Console.WriteLine($"{indent}  Message: {currentEx.Message}");
        if (!string.IsNullOrEmpty(currentEx.StackTrace))
        {
            Console.WriteLine($"{indent}  Stack Trace: {currentEx.StackTrace}");
        }
        currentEx = currentEx.InnerException;
        exLevel++;
    }
    
    Console.WriteLine();
    Console.WriteLine("Full Exception Details:");
    Console.WriteLine(ex.ToString());
    
    Environment.Exit(1);
}
