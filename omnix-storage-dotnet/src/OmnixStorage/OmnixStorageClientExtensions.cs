namespace OmnixStorage;

using System.Linq;
using OmnixStorage.Args;
using OmnixStorage.DataModel;

/// <summary>
/// Extension methods for IOmnixStorageClient providing simplified method signatures.
/// These methods wrap the Args-based API with more convenient overloads.
/// </summary>
public static class OmnixStorageClientExtensions
{
    #region Bucket Operations

    /// <summary>
    /// Creates a new bucket.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task MakeBucketAsync(
        this IOmnixStorageClient client,
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        var args = new MakeBucketArgs().WithBucket(bucketName);
        return client.MakeBucketAsync(args, cancellationToken);
    }

    /// <summary>
    /// Removes a bucket (must be empty).
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task RemoveBucketAsync(
        this IOmnixStorageClient client,
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        var args = new RemoveBucketArgs().WithBucket(bucketName);
        return client.RemoveBucketAsync(args, cancellationToken);
    }

    #endregion

    #region Object Operations

    /// <summary>
    /// Uploads an object to the bucket.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket.</param>
    /// <param name="objectName">Name of the object (key).</param>
    /// <param name="data">Stream containing the object data.</param>
    /// <param name="contentType">Content type (optional, defaults to application/octet-stream).</param>
    /// <param name="metadata">Custom metadata (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing ETag and other upload information.</returns>
    public static Task<PutObjectResult> PutObjectAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        Stream data,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(data);

        if (!string.IsNullOrEmpty(contentType))
        {
            args.WithContentType(contentType);
        }

        if (metadata != null && metadata.Count > 0)
        {
            args.WithMetadata(metadata);
        }

        return client.PutObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Downloads an object from the bucket.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket.</param>
    /// <param name="objectName">Name of the object.</param>
    /// <param name="output">Stream to write the object data to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Object metadata.</returns>
    public static Task<ObjectMetadata> GetObjectAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        var args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithOutputStream(output);

        return client.GetObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Gets object metadata without downloading the object.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket.</param>
    /// <param name="objectName">Name of the object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Object metadata.</returns>
    public static Task<ObjectMetadata> StatObjectAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var args = new StatObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        return client.StatObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Removes an object from the bucket.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket.</param>
    /// <param name="objectName">Name of the object to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task RemoveObjectAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        return client.RemoveObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Copies an object to a new location.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="sourceBucket">Source bucket name.</param>
    /// <param name="sourceObject">Source object name.</param>
    /// <param name="destinationBucket">Destination bucket name.</param>
    /// <param name="destinationObject">Destination object name.</param>
    /// <param name="metadata">Optional metadata to apply to the destination object.</param>
    /// <param name="copyConditions">Optional copy condition headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing ETag and last modified.</returns>
    public static Task<CopyObjectResult> CopyObjectAsync(
        this IOmnixStorageClient client,
        string sourceBucket,
        string sourceObject,
        string destinationBucket,
        string destinationObject,
        Dictionary<string, string>? metadata = null,
        Dictionary<string, string>? copyConditions = null,
        CancellationToken cancellationToken = default)
    {
        var args = new CopyObjectArgs()
            .WithSourceBucket(sourceBucket)
            .WithSourceObject(sourceObject)
            .WithDestinationBucket(destinationBucket)
            .WithDestinationObject(destinationObject);

        if (metadata != null && metadata.Count > 0)
        {
            args.WithMetadata(metadata);
        }

        if (copyConditions != null && copyConditions.Count > 0)
        {
            args.WithCopyConditions(copyConditions);
        }

        return client.CopyObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Removes multiple objects from a bucket in a single request.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectNames">Object names to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing deleted keys and errors.</returns>
    public static Task<RemoveObjectsResult> RemoveObjectsAsync(
        this IOmnixStorageClient client,
        string bucketName,
        IEnumerable<string> objectNames,
        CancellationToken cancellationToken = default)
    {
        var args = new RemoveObjectsArgs().WithBucket(bucketName);
        args.WithObjects(objectNames.ToList());
        return client.RemoveObjectsAsync(args, cancellationToken);
    }

    /// <summary>
    /// Initiates a multipart upload.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectName">Object name.</param>
    /// <param name="contentType">Content type for the final object.</param>
    /// <param name="metadata">Optional metadata to store with the object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Multipart upload ID.</returns>
    public static Task<InitiateMultipartUploadResult> InitiateMultipartUploadAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var args = new InitiateMultipartUploadArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            args.WithContentType(contentType);
        }

        if (metadata != null && metadata.Count > 0)
        {
            foreach (var kvp in metadata)
            {
                args.WithMetadata(kvp.Key, kvp.Value);
            }
        }

        return client.InitiateMultipartUploadAsync(args, cancellationToken);
    }

    /// <summary>
    /// Uploads a part for a multipart upload.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectName">Object name.</param>
    /// <param name="uploadId">Upload ID.</param>
    /// <param name="partNumber">Part number (1-10000).</param>
    /// <param name="data">Part data stream.</param>
    /// <param name="size">Part size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing ETag for the part.</returns>
    public static Task<UploadPartResult> UploadPartAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        string uploadId,
        int partNumber,
        Stream data,
        long size,
        CancellationToken cancellationToken = default)
    {
        var args = new UploadPartArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithUploadId(uploadId)
            .WithPartNumber(partNumber)
            .WithData(data, size);

        return client.UploadPartAsync(args, cancellationToken);
    }

    /// <summary>
    /// Completes a multipart upload.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectName">Object name.</param>
    /// <param name="uploadId">Upload ID.</param>
    /// <param name="parts">Completed parts with ETags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing ETag and location.</returns>
    public static Task<CompleteMultipartUploadResult> CompleteMultipartUploadAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        string uploadId,
        IEnumerable<CompleteMultipartUploadArgs.PartInfo> parts,
        CancellationToken cancellationToken = default)
    {
        var args = new CompleteMultipartUploadArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithUploadId(uploadId)
            .WithParts(parts);

        return client.CompleteMultipartUploadAsync(args, cancellationToken);
    }

    /// <summary>
    /// Aborts a multipart upload.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Bucket name.</param>
    /// <param name="objectName">Object name.</param>
    /// <param name="uploadId">Upload ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task AbortMultipartUploadAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        var args = new AbortMultipartUploadArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithUploadId(uploadId);

        return client.AbortMultipartUploadAsync(args, cancellationToken);
    }

    /// <summary>
    /// Lists objects in a bucket.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket.</param>
    /// <param name="prefix">Filter objects by prefix (optional).</param>
    /// <param name="maxKeys">Maximum number of objects to return (optional).</param>
    /// <param name="continuationToken">Token for pagination (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List result containing objects and pagination information.</returns>
    public static Task<ListObjectsResult> ListObjectsAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string? prefix = null,
        int? maxKeys = null,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        var args = new ListObjectsArgs().WithBucket(bucketName);

        if (!string.IsNullOrEmpty(prefix))
        {
            args.WithPrefix(prefix);
        }

        if (maxKeys.HasValue)
        {
            args.WithMaxKeys(maxKeys.Value);
        }

        if (!string.IsNullOrEmpty(continuationToken))
        {
            args.WithContinuationToken(continuationToken);
        }

        return client.ListObjectsAsync(args, cancellationToken);
    }

    /// <summary>
    /// Generates a presigned URL for downloading an object.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket.</param>
    /// <param name="objectName">Name of the object.</param>
    /// <param name="expiresIn">How long the URL should be valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Presigned URL result.</returns>
    public static Task<PresignedUrlResult> PresignedGetObjectAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry((int)expiresIn.TotalSeconds);

        return client.PresignedGetObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Generates a presigned URL for uploading an object.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="bucketName">Name of the bucket.</param>
    /// <param name="objectName">Name of the object.</param>
    /// <param name="expiresIn">How long the URL should be valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Presigned URL result.</returns>
    public static Task<PresignedUrlResult> PresignedPutObjectAsync(
        this IOmnixStorageClient client,
        string bucketName,
        string objectName,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry((int)expiresIn.TotalSeconds);

        return client.PresignedPutObjectAsync(args, cancellationToken);
    }

    /// <summary>
    /// Checks if the server is healthy.
    /// </summary>
    /// <param name="client">The OmnixStorage client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if server is healthy, false otherwise.</returns>
    public static Task<bool> HealthCheckAsync(
        this IOmnixStorageClient client,
        CancellationToken cancellationToken = default)
    {
        return client.HealthAsync(cancellationToken);
    }

    #endregion
}
