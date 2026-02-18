namespace OmnixStorage;

using OmnixStorage.Args;

/// <summary>
/// Convenience wrapper for common OmnixStorage integration patterns.
/// </summary>
public class OmnixStorageIntegrationService
{
    private readonly IOmnixStorageClient _client;

    public OmnixStorageIntegrationService(IOmnixStorageClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<string> GetPresignedUploadUrlAsync(
        string bucketName,
        string objectKey,
        int expirySeconds = 3600,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry(expirySeconds);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            args.WithContentType(contentType);
        }

        var result = await _client.PresignedPutObjectAsync(args, cancellationToken);
        return result.Url;
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string bucketName,
        string objectKey,
        int expirySeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithExpiry(expirySeconds),
            cancellationToken);

        return result.Url;
    }

    public async Task<string> UploadFileAsync(
        string bucketName,
        string objectKey,
        Stream fileContent,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(fileContent);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            args.WithContentType(contentType);
        }

        await _client.PutObjectAsync(args, cancellationToken);
        return objectKey;
    }

    public async Task DeleteFileAsync(
        string bucketName,
        string objectKey,
        bool ignoreNotFound = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey),
                cancellationToken);
        }
        catch (OmnixStorageException ex) when (ignoreNotFound && ex.StatusCode == 404)
        {
            return;
        }
        catch (ObjectNotFoundException) when (ignoreNotFound)
        {
            return;
        }
    }

    public Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        return _client.BucketExistsAsync(bucketName, cancellationToken);
    }

    public Task EnsureBucketExistsAsync(
        string bucketName,
        int maxAttempts = 3,
        int delaySeconds = 2,
        CancellationToken cancellationToken = default)
    {
        return _client.EnsureBucketExistsAsync(bucketName, maxAttempts, delaySeconds, cancellationToken);
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return _client.HealthCheckBucketsAsync(cancellationToken);
    }
}
