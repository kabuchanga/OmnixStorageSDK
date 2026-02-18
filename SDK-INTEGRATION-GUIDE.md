# OmnixStorage SDK Integration Guide

This guide shows how to integrate the OmnixStorage .NET SDK into EdgeSentience services and generate browser-safe presigned URLs.

## 1. Install and Configure

```csharp
var client = new OmnixStorageClientBuilder()
    .WithEndpoint("storage.kegeosapps.com:443")
    .WithCredentials("YOUR_ACCESS_KEY", "YOUR_SECRET_KEY")
    .WithRegion("us-east-1")
    .WithSSL(true)
    .Build();
```

## 2. Generate Presigned URLs (Public Endpoint)

Use a public endpoint client so URLs work from browsers without replacement.

```csharp
var publicClient = OmnixStorageClientFactory.CreatePublicEndpointClient(
    publicEndpoint: "https://storage.kegeosapps.com",
    accessKey: "YOUR_ACCESS_KEY",
    secretKey: "YOUR_SECRET_KEY",
    region: "us-east-1");

var presigned = await publicClient.PresignedGetObjectAsync(
    new PresignedGetObjectArgs()
        .WithBucket("photos")
        .WithObject("cam_2/photo1.jpg")
        .WithExpiry(3600));

var url = presigned.Url;
```

## 3. Common S3 Operations

```csharp
// Upload
await client.PutObjectAsync(
    new PutObjectArgs()
        .WithBucket("photos")
        .WithObject("cam_2/photo1.jpg")
        .WithStreamData(stream, stream.Length)
        .WithContentType("image/jpeg"));

// Download
await client.GetObjectAsync(
    new GetObjectArgs()
        .WithBucket("photos")
        .WithObject("cam_2/photo1.jpg")
        .WithOutputStream(outputStream));

// Exists (bucket)
var exists = await client.BucketExistsAsync("photos");
```

## 4. Copy and Batch Delete

```csharp
await client.CopyObjectAsync(
    new CopyObjectArgs()
        .WithSourceBucket("photos")
        .WithSourceObject("cam_2/photo1.jpg")
        .WithDestinationBucket("photos")
        .WithDestinationObject("cam_2/photo1-copy.jpg"));

var deleteResult = await client.RemoveObjectsAsync(
    new RemoveObjectsArgs()
        .WithBucket("photos")
        .WithObjects("cam_2/old1.jpg", "cam_2/old2.jpg"));
```

## 5. Multipart Upload

```csharp
var init = await client.InitiateMultipartUploadAsync(
    new InitiateMultipartUploadArgs()
        .WithBucket("videos")
        .WithObject("big-file.mp4")
        .WithContentType("video/mp4"));

var parts = new List<CompleteMultipartUploadArgs.PartInfo>();
for (var partNumber = 1; partNumber <= 3; partNumber++)
{
    using var partStream = File.OpenRead($"part-{partNumber}.bin");
    var uploaded = await client.UploadPartAsync(
        new UploadPartArgs()
            .WithBucket("videos")
            .WithObject("big-file.mp4")
            .WithUploadId(init.UploadId)
            .WithPartNumber(partNumber)
            .WithData(partStream, partStream.Length));

    parts.Add(new CompleteMultipartUploadArgs.PartInfo
    {
        PartNumber = uploaded.PartNumber,
        ETag = uploaded.ETag ?? string.Empty
    });
}

await client.CompleteMultipartUploadAsync(
    new CompleteMultipartUploadArgs()
        .WithBucket("videos")
        .WithObject("big-file.mp4")
        .WithUploadId(init.UploadId)
        .WithParts(parts));
```

## 6. Recommended Integration Pattern

- Use one internal client for service-to-service operations (private endpoint).
- Use a second public client for browser presigned URLs.
- Keep credentials in environment variables; avoid hard-coded secrets.

## 7. Troubleshooting

- If presigned URLs fail, verify system time sync and region.
- For long presigned URLs, ensure proxies allow large headers.
