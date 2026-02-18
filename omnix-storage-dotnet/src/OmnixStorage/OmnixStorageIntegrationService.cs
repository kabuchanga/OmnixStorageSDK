namespace OmnixStorage;

using OmnixStorage.Args;

/// <summary>
/// Convenience wrapper for common OmnixStorage integration patterns.
/// </summary>
public class OmnixStorageIntegrationService
{
    private readonly IOmnixStorageClient _client;

    /// <summary>
    /// Initializes a new integration service wrapper.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    public OmnixStorageIntegrationService(IOmnixStorageClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Generates a presigned PUT URL for uploads.
    /// </summary>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectKey">Object key.</param>
    /// <param name="expirySeconds">URL expiry in seconds.</param>
    /// <param name="contentType">Optional content type enforcement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary>
    /// Generates a presigned GET URL for downloads.
    /// </summary>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectKey">Object key.</param>
    /// <param name="expirySeconds">URL expiry in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary>
    /// Uploads a file stream to object storage.
    /// </summary>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectKey">Object key.</param>
    /// <param name="fileContent">File content stream.</param>
    /// <param name="contentType">Optional content type header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary>
    /// Deletes an object, optionally ignoring not found errors.
    /// </summary>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectKey">Object key.</param>
    /// <param name="ignoreNotFound">Ignore 404 errors if true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary>
    /// Checks whether a bucket exists.
    /// </summary>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        return _client.BucketExistsAsync(bucketName, cancellationToken);
    }

    /// <summary>
    /// Ensures a bucket exists, creating it with retries if needed.
    /// </summary>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="maxAttempts">Maximum create attempts.</param>
    /// <param name="delaySeconds">Delay between attempts in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task EnsureBucketExistsAsync(
        string bucketName,
        int maxAttempts = 3,
        int delaySeconds = 2,
        CancellationToken cancellationToken = default)
    {
        return _client.EnsureBucketExistsAsync(bucketName, maxAttempts, delaySeconds, cancellationToken);
    }

    /// <summary>
    /// Performs a connectivity check using list buckets.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return _client.HealthCheckBucketsAsync(cancellationToken);
    }
}
