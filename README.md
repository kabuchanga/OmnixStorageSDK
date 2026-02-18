# OmnixStorage SDK

S3-compatible .NET SDK for OmnixStorage with AWS SigV4 authentication and admin APIs.

## Features

### Core S3 Operations
- Bucket create, list, delete, and existence checks
- Object upload, download, stat (HEAD), delete, and list
- Presigned URL generation for GET and PUT with expiry validation

### Extended Object Operations
- Copy object (supports metadata replacement and copy conditions)
- Batch delete objects using S3-style XML payload
- Multipart upload lifecycle: initiate, upload part, complete, abort

### Security and Auth
- AWS Signature Version 4 (SigV4) request signing
- Presigned URLs with optional Content-Type enforcement
- Temporary credentials support (X-Amz-Security-Token)
- Separate JWT Bearer flow for admin endpoints

### Reliability and Compatibility
- Retry logic with exponential backoff
- URL encoding/decoding aligned with SigV4 canonicalization rules
- Large presigned URL support through header size tuning

### Admin and Tenant Management
- Tenant create, list, and delete
- Health check endpoint

## Quick Start

```csharp
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com")
    .WithCredentials("ACCESS_KEY", "SECRET_KEY")
    .WithSSL(true)
    .Build();
```

## Examples

### Presigned GET URL

```csharp
var presigned = await client.PresignedGetObjectAsync(
    "photo-test",
    "images/13-47-42-738.jpg",
    expiresIn: TimeSpan.FromHours(1));

Console.WriteLine(presigned.Url);
```

### Copy Object

```csharp
var copyResult = await client.CopyObjectAsync(
    sourceBucket: "photo-test",
    sourceObject: "images/13-47-42-738.jpg",
    destinationBucket: "photo-test",
    destinationObject: "images/13-47-42-738-copy.jpg");

Console.WriteLine(copyResult.ETag);
```

### Batch Delete

```csharp
var deleteResult = await client.RemoveObjectsAsync(
    "photo-test",
    new[] { "images/old-1.jpg", "images/old-2.jpg" });

Console.WriteLine($"Deleted: {deleteResult.Deleted.Count}");
```

### Multipart Upload

```csharp
var init = await client.InitiateMultipartUploadAsync(
    "photo-test",
    "videos/big-file.mp4",
    contentType: "video/mp4");

var parts = new List<CompleteMultipartUploadArgs.PartInfo>();
for (var partNumber = 1; partNumber <= 3; partNumber++)
{
    using var partStream = File.OpenRead($"part-{partNumber}.bin");
    var uploaded = await client.UploadPartAsync(
        "photo-test",
        "videos/big-file.mp4",
        init.UploadId,
        partNumber,
        partStream,
        partStream.Length);

    parts.Add(new CompleteMultipartUploadArgs.PartInfo
    {
        PartNumber = uploaded.PartNumber,
        ETag = uploaded.ETag ?? string.Empty
    });
}

var completed = await client.CompleteMultipartUploadAsync(
    "photo-test",
    "videos/big-file.mp4",
    init.UploadId,
    parts);

Console.WriteLine(completed.ETag);
```

## Project Structure

- omnix-storage-dotnet: .NET SDK
- omnix-storage-py: Python SDK

## Build

```bash
dotnet build omnix-storage-dotnet/OmnixStorage.sln
```
