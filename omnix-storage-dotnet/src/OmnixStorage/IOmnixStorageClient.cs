namespace OmnixStorage;

using OmnixStorage.Args;
using OmnixStorage.DataModel;

/// <summary>
/// Interface for OmnixStorage client operations.
/// </summary>
public interface IOmnixStorageClient : IAsyncDisposable
{
    // Bucket operations

    /// <summary>
    /// Checks if a bucket exists.
    /// </summary>
    Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new bucket.
    /// </summary>
    Task MakeBucketAsync(MakeBucketArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a bucket (must be empty).
    /// </summary>
    Task RemoveBucketAsync(RemoveBucketArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all buckets.
    /// </summary>
    Task<List<Bucket>> ListBucketsAsync(CancellationToken cancellationToken = default);

    // Object operations

    /// <summary>
    /// Uploads an object to the bucket.
    /// </summary>
    Task<PutObjectResult> PutObjectAsync(PutObjectArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an object from the bucket.
    /// </summary>
    Task<ObjectMetadata> GetObjectAsync(GetObjectArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets object metadata without downloading the object.
    /// </summary>
    Task<ObjectMetadata> StatObjectAsync(StatObjectArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an object from the bucket.
    /// </summary>
    Task RemoveObjectAsync(RemoveObjectArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies an object to a new location.
    /// </summary>
    Task<CopyObjectResult> CopyObjectAsync(CopyObjectArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple objects from a bucket.
    /// </summary>
    Task<RemoveObjectsResult> RemoveObjectsAsync(RemoveObjectsArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a multipart upload.
    /// </summary>
    Task<InitiateMultipartUploadResult> InitiateMultipartUploadAsync(InitiateMultipartUploadArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a part for a multipart upload.
    /// </summary>
    Task<UploadPartResult> UploadPartAsync(UploadPartArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a multipart upload.
    /// </summary>
    Task<CompleteMultipartUploadResult> CompleteMultipartUploadAsync(CompleteMultipartUploadArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts a multipart upload.
    /// </summary>
    Task AbortMultipartUploadAsync(AbortMultipartUploadArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists objects in a bucket.
    /// </summary>
    Task<ListObjectsResult> ListObjectsAsync(ListObjectsArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a presigned URL for downloading an object.
    /// </summary>
    Task<PresignedUrlResult> PresignedGetObjectAsync(PresignedGetObjectArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a presigned URL for uploading an object.
    /// </summary>
    Task<PresignedUrlResult> PresignedPutObjectAsync(PresignedPutObjectArgs args, CancellationToken cancellationToken = default);

    // Tenant operations

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    Task<Tenant> CreateTenantAsync(CreateTenantArgs args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all tenants.
    /// </summary>
    Task<IReadOnlyList<Tenant>> ListTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tenant (must have no buckets).
    /// </summary>
    Task DeleteTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    // Health check

    /// <summary>
    /// Checks if the server is healthy.
    /// </summary>
    Task<bool> HealthAsync(CancellationToken cancellationToken = default);
}
