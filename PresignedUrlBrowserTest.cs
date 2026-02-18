using System;
using System.IO;
using System.Threading.Tasks;
using OmnixStorage;

class PresignedUrlBrowserTest
{
    static async Task Main(string[] args)
    {
        var endpoint = Environment.GetEnvironmentVariable("OmnixStorage__Endpoint") ?? "storage.kegeosapps.com";
        var publicEndpoint = Environment.GetEnvironmentVariable("OmnixStorage__PublicEndpoint") ?? "https://storage.kegeosapps.com";
        var accessKey = Environment.GetEnvironmentVariable("OmnixStorage__AccessKey") ?? "AKIATESAFEKEY0000001";
        var secretKey = Environment.GetEnvironmentVariable("OmnixStorage__SecretKey") ?? "wJalrXUtnFEMIaKkMDENGbPxRfIcxAmPlEkEyZaB";
        var secure = bool.Parse(Environment.GetEnvironmentVariable("OmnixStorage__Secure") ?? "true");

        try
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("Presigned URL Browser Test - Image Access");
            Console.WriteLine("================================================================================\n");

            // Initialize client
            var client = new OmnixStorageClient()
                .WithEndpoint(endpoint)
                .WithPublicEndpoint(publicEndpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSecure(secure);

            Console.WriteLine("✓ Client initialized\n");

            // Test bucket and object names
            string bucketName = "photo-test";
            string objectKey = "images/13-47-42-738.jpg";
            string photoPath = "13-47-42-738.jpg";

            // Check if bucket exists, create if not
            Console.WriteLine($"→ Creating bucket: {bucketName}");
            try
            {
                await client.MakeBucketAsync(bucketName);
                Console.WriteLine($"✓ Bucket created: {bucketName}\n");
            }
            catch (Exception ex) when (ex.Message.Contains("already exists"))
            {
                Console.WriteLine($"✓ Bucket already exists: {bucketName}\n");
            }

            // Upload the photo
            if (File.Exists(photoPath))
            {
                Console.WriteLine($"→ Uploading photo: {objectKey}");
                using (var stream = File.OpenRead(photoPath))
                {
                    await client.PutObjectAsync(bucketName, objectKey, stream);
                }
                Console.WriteLine($"✓ Photo uploaded successfully\n");
            }
            else
            {
                Console.WriteLine($"❌ Photo file not found: {photoPath}");
                return;
            }

            // Generate presigned GET URL
            Console.WriteLine("→ Generating presigned GET URL (valid for 1 hour)...\n");
            
            // Generate with 1 hour expiry (3600 seconds)
            string presignedUrl = client.GetPresignedGetObjectUrl(
                bucketName, 
                objectKey, 
                expiresIn: 3600
            );

            Console.WriteLine("================================================================================");
            Console.WriteLine("✓ PRESIGNED URL (Copy and paste in browser to view photo):");
            Console.WriteLine("================================================================================\n");
            Console.WriteLine(presignedUrl);
            Console.WriteLine("\n================================================================================\n");

            // Test edge cases
            Console.WriteLine("Testing Edge Cases:\n");

            // Test 1: Presigned URL with too long expiry (>604800 seconds = 7 days)
            Console.WriteLine("Test 1: Presigned URL with expiry > 604800 seconds (should fail server-side)");
            try
            {
                string invalidUrl = client.GetPresignedGetObjectUrl(
                    bucketName, 
                    objectKey, 
                    expiresIn: 604801  // 1 second over limit
                );
                Console.WriteLine("⚠ URL generated (server will reject when accessed):");
                Console.WriteLine($"  {invalidUrl}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✓ SDK validation caught: {ex.Message}\n");
            }

            // Test 2: Presigned URL with 1 second expiry (should work but expire immediately)
            Console.WriteLine("Test 2: Presigned URL with minimum expiry (1 second)");
            string minExpiryUrl = client.GetPresignedGetObjectUrl(
                bucketName, 
                objectKey, 
                expiresIn: 1
            );
            Console.WriteLine("✓ URL generated:");
            Console.WriteLine($"  {minExpiryUrl}");
            Console.WriteLine("  (This URL may already be expired)\n");

            // Test 3: Presigned URL with standard expiry (1 hour)
            Console.WriteLine("Test 3: Presigned URL with standard expiry (3600 seconds)");
            string standardUrl = client.GetPresignedGetObjectUrl(
                bucketName, 
                objectKey, 
                expiresIn: 3600
            );
            Console.WriteLine("✓ URL generated (valid for 1 hour):");
            Console.WriteLine($"  {standardUrl}\n");

            // Test 4: Presigned URL for non-existent object
            Console.WriteLine("Test 4: Presigned URL for non-existent object (URL works, object not found on access)");
            string nonExistentUrl = client.GetPresignedGetObjectUrl(
                bucketName, 
                "images/does-not-exist.jpg", 
                expiresIn: 3600
            );
            Console.WriteLine("✓ URL generated:");
            Console.WriteLine($"  {nonExistentUrl}");
            Console.WriteLine("  (Server will return 404 NoSuchKey when accessed)\n");

            Console.WriteLine("================================================================================");
            Console.WriteLine("✓ Edge case tests completed");
            Console.WriteLine("================================================================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
    }
}
