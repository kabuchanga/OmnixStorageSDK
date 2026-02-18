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

/// <summary>
/// Represents the result of a copy object operation.
/// </summary>
public class CopyObjectResult
{
    /// <summary>
    /// Gets or sets the ETag of the copied object.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp of the copied object.
    /// </summary>
    public DateTime? LastModified { get; set; }
}

/// <summary>
/// Represents an error for a batch delete operation.
/// </summary>
public class RemoveObjectError
{
    /// <summary>
    /// Gets or sets the object key that failed to delete.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Represents the result of a batch delete operation.
/// </summary>
public class RemoveObjectsResult
{
    /// <summary>
    /// Gets or sets the list of deleted object keys.
    /// </summary>
    public List<string> Deleted { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of errors for objects that failed to delete.
    /// </summary>
    public List<RemoveObjectError> Errors { get; set; } = new();
}

/// <summary>
/// Represents the result of initiating a multipart upload.
/// </summary>
public class InitiateMultipartUploadResult
{
    /// <summary>
    /// Gets or sets the upload ID.
    /// </summary>
    public string UploadId { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of uploading a part.
/// </summary>
public class UploadPartResult
{
    /// <summary>
    /// Gets or sets the part number.
    /// </summary>
    public int PartNumber { get; set; }

    /// <summary>
    /// Gets or sets the ETag returned by the server for this part.
    /// </summary>
    public string? ETag { get; set; }
}

/// <summary>
/// Represents the result of completing a multipart upload.
/// </summary>
public class CompleteMultipartUploadResult
{
    /// <summary>
    /// Gets or sets the ETag of the completed object.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Gets or sets the location returned by the server, if any.
    /// </summary>
    public string? Location { get; set; }
}
