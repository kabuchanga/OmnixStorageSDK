namespace OmnixStorage.DataModel;

/// <summary>
/// Represents a bucket in OmnixStorage.
/// </summary>
public class Bucket
{
    /// <summary>
    /// Gets or sets the bucket name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTime CreationDate { get; set; }
}

/// <summary>
/// Represents an object (file) in OmnixStorage.
/// </summary>
public class ObjectMetadata
{
    /// <summary>
    /// Gets or sets the object name (key).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the bucket name.
    /// </summary>
    public required string BucketName { get; set; }

    /// <summary>
    /// Gets or sets the object size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the ETag (entity tag).
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Gets or sets the last modified date.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents the result of a list objects operation.
/// </summary>
public class ListObjectsResult
{
    /// <summary>
    /// Gets or sets the list of objects.
    /// </summary>
    public required List<ObjectMetadata> Objects { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are more results.
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Gets or sets the continuation token for pagination.
    /// </summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the list of common prefixes (for directory-like browsing).
    /// </summary>
    public List<string> CommonPrefixes { get; set; } = new();
}

/// <summary>
/// Represents the result of a put object operation.
/// </summary>
public class PutObjectResult
{
    /// <summary>
    /// Gets or sets the bucket name.
    /// </summary>
    public required string BucketName { get; set; }

    /// <summary>
    /// Gets or sets the object name.
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the ETag of the uploaded object.
    /// </summary>
    public required string ETag { get; set; }

    /// <summary>
    /// Gets or sets the version ID if versioning is enabled.
    /// </summary>
    public string? VersionId { get; set; }
}

/// <summary>
/// Represents a presigned URL response.
/// </summary>
public class PresignedUrlResult
{
    /// <summary>
    /// Gets or sets the presigned URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the expiration time of the presigned URL.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Represents bucket policy information.
/// </summary>
public class BucketPolicyResult
{
    /// <summary>
    /// Gets or sets the bucket name.
    /// </summary>
    public required string BucketName { get; set; }

    /// <summary>
    /// Gets or sets the policy JSON.
    /// </summary>
    public string? PolicyJson { get; set; }
}
