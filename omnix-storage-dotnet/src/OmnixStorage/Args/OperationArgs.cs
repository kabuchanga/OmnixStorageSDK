namespace OmnixStorage.Args;

/// <summary>
/// Arguments for the PutObject operation using fluent builder pattern.
/// </summary>
public class PutObjectArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name (key).
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the stream data to upload.
    /// </summary>
    public Stream? Data { get; private set; }

    /// <summary>
    /// Gets the size of the data in bytes.
    /// </summary>
    public long ObjectSize { get; private set; }

    /// <summary>
    /// Gets the content type.
    /// </summary>
    public string ContentType { get; private set; } = "application/octet-stream";

    /// <summary>
    /// Gets the custom metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; private set; } = new();

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public PutObjectArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public PutObjectArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the stream data and size.
    /// </summary>
    public PutObjectArgs WithStreamData(Stream data, long size)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        ObjectSize = size;
        return this;
    }

    /// <summary>
    /// Sets the stream data (size is determined from the stream if possible).
    /// </summary>
    public PutObjectArgs WithStreamData(Stream data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        ObjectSize = data.CanSeek ? data.Length : -1;
        return this;
    }

    /// <summary>
    /// Sets the content type.
    /// </summary>
    public PutObjectArgs WithContentType(string contentType)
    {
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        return this;
    }

    /// <summary>
    /// Adds metadata.
    /// </summary>
    public PutObjectArgs WithMetadata(Dictionary<string, string> metadata)
    {
        foreach (var kvp in metadata)
        {
            Metadata[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Adds a single metadata entry.
    /// </summary>
    public PutObjectArgs WithMetadata(string key, string value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
        if (Data == null)
            throw new ArgumentException("Data stream is required.");
    }
}

/// <summary>
/// Arguments for the GetObject operation.
/// </summary>
public class GetObjectArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the output stream to write to.
    /// </summary>
    public Stream? OutputStream { get; private set; }

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public GetObjectArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public GetObjectArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the output stream.
    /// </summary>
    public GetObjectArgs WithOutputStream(Stream outputStream)
    {
        OutputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
        if (OutputStream == null)
            throw new ArgumentException("Output stream is required.");
    }
}

/// <summary>
/// Arguments for the MakeBucket operation.
/// </summary>
public class MakeBucketArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public MakeBucketArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
    }
}

/// <summary>
/// Arguments for the RemoveBucket operation.
/// </summary>
public class RemoveBucketArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public RemoveBucketArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
    }
}

/// <summary>
/// Arguments for the ListObjects operation.
/// </summary>
public class ListObjectsArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the prefix filter.
    /// </summary>
    public string Prefix { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the delimiter for grouping objects.
    /// </summary>
    public string Delimiter { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the maximum number of objects to return.
    /// </summary>
    public int MaxKeys { get; private set; } = 1000;

    /// <summary>
    /// Gets the continuation token for pagination.
    /// </summary>
    public string? ContinuationToken { get; private set; }

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public ListObjectsArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the prefix filter.
    /// </summary>
    public ListObjectsArgs WithPrefix(string prefix)
    {
        Prefix = prefix ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets the delimiter.
    /// </summary>
    public ListObjectsArgs WithDelimiter(string delimiter)
    {
        Delimiter = delimiter ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of keys.
    /// </summary>
    public ListObjectsArgs WithMaxKeys(int maxKeys)
    {
        MaxKeys = maxKeys;
        return this;
    }

    /// <summary>
    /// Sets the continuation token.
    /// </summary>
    public ListObjectsArgs WithContinuationToken(string? token)
    {
        ContinuationToken = token;
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
    }
}

/// <summary>
/// Arguments for the RemoveObject operation.
/// </summary>
public class RemoveObjectArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public RemoveObjectArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public RemoveObjectArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
    }
}

/// <summary>
/// Arguments for the StatObject operation.
/// </summary>
public class StatObjectArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public StatObjectArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public StatObjectArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
    }
}

/// <summary>
/// Arguments for the PresignedGetObject operation.
/// </summary>
public class PresignedGetObjectArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the expiration time in seconds.
    /// </summary>
    public int ExpiresInSeconds { get; private set; } = 3600;

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public PresignedGetObjectArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public PresignedGetObjectArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the expiration time in seconds (default: 3600).
    /// </summary>
    public PresignedGetObjectArgs WithExpiresInSeconds(int seconds)
    {
        ExpiresInSeconds = seconds;
        return this;
    }

    /// <summary>
    /// Sets the expiration time in seconds (alias for WithExpiresInSeconds).
    /// </summary>
    public PresignedGetObjectArgs WithExpiry(int seconds)
    {
        return WithExpiresInSeconds(seconds);
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
    }
}

/// <summary>
/// Arguments for the PresignedPutObject operation.
/// </summary>
public class PresignedPutObjectArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the expiration time in seconds.
    /// </summary>
    public int ExpiresInSeconds { get; private set; } = 3600;

    /// <summary>
    /// Gets the optional Content-Type to enforce for uploads.
    /// When specified, clients must use this exact Content-Type when uploading.
    /// Recommended for security: prevents uploading unexpected file types.
    /// </summary>
    public string? ContentType { get; private set; }

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public PresignedPutObjectArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public PresignedPutObjectArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the expiration time in seconds (default: 3600).
    /// </summary>
    public PresignedPutObjectArgs WithExpiresInSeconds(int seconds)
    {
        ExpiresInSeconds = seconds;
        return this;
    }

    /// <summary>
    /// Sets the expiration time in seconds (alias for WithExpiresInSeconds).
    /// </summary>
    public PresignedPutObjectArgs WithExpiry(int seconds)
    {
        return WithExpiresInSeconds(seconds);
    }

    /// <summary>
    /// Sets the Content-Type to enforce for uploads.
    /// When set, the client MUST use this exact Content-Type header when uploading via the presigned URL.
    /// This adds security by restricting what file types can be uploaded.
    /// Example: "image/jpeg", "application/pdf", "text/plain"
    /// </summary>
    /// <param name="contentType">MIME content type (e.g., "image/jpeg")</param>
    public PresignedPutObjectArgs WithContentType(string contentType)
    {
        ContentType = contentType;
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
    }
}

/// <summary>
/// Arguments for the CopyObject operation.
/// Copies an object from one location to another (bucket/key or within same bucket).
/// </summary>
public class CopyObjectArgs
{
    /// <summary>
    /// Gets the source bucket name.
    /// </summary>
    public string SourceBucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the source object name (key).
    /// </summary>
    public string SourceObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the destination bucket name.
    /// </summary>
    public string DestinationBucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the destination object name (key).
    /// </summary>
    public string DestinationObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the metadata to apply to the destination object.
    /// </summary>
    public Dictionary<string, string> Metadata { get; private set; } = new();

    /// <summary>
    /// Gets the copy condition (e.g., CopyConditionIfMatch, CopyConditionIfModifiedSince).
    /// </summary>
    public Dictionary<string, string> CopyConditions { get; private set; } = new();

    /// <summary>
    /// Sets the source bucket and object.
    /// </summary>
    public CopyObjectArgs WithSourceBucket(string bucketName)
    {
        SourceBucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the source object name.
    /// </summary>
    public CopyObjectArgs WithSourceObject(string objectName)
    {
        SourceObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the destination bucket and object.
    /// </summary>
    public CopyObjectArgs WithDestinationBucket(string bucketName)
    {
        DestinationBucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the destination object name.
    /// </summary>
    public CopyObjectArgs WithDestinationObject(string objectName)
    {
        DestinationObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Adds metadata for the destination object.
    /// </summary>
    public CopyObjectArgs WithMetadata(string key, string value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Adds metadata dictionary.
    /// </summary>
    public CopyObjectArgs WithMetadata(Dictionary<string, string> metadata)
    {
        foreach (var kvp in metadata)
        {
            Metadata[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Adds a copy condition header (e.g., "x-amz-copy-source-if-match").
    /// </summary>
    public CopyObjectArgs WithCopyCondition(string headerName, string value)
    {
        CopyConditions[headerName] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple copy condition headers.
    /// </summary>
    public CopyObjectArgs WithCopyConditions(Dictionary<string, string> conditions)
    {
        foreach (var kvp in conditions)
        {
            CopyConditions[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(SourceBucketName))
            throw new ArgumentException("Source bucket name is required.");
        if (string.IsNullOrEmpty(SourceObjectName))
            throw new ArgumentException("Source object name is required.");
        if (string.IsNullOrEmpty(DestinationBucketName))
            throw new ArgumentException("Destination bucket name is required.");
        if (string.IsNullOrEmpty(DestinationObjectName))
            throw new ArgumentException("Destination object name is required.");
    }
}

/// <summary>
/// Arguments for removing multiple objects in a single operation.
/// </summary>
public class RemoveObjectsArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the list of object names to delete.
    /// </summary>
    public List<string> ObjectNames { get; private set; } = new();

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public RemoveObjectsArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Adds an object name to delete.
    /// </summary>
    public RemoveObjectsArgs WithObject(string objectName)
    {
        ObjectNames.Add(objectName ?? throw new ArgumentNullException(nameof(objectName)));
        return this;
    }

    /// <summary>
    /// Adds multiple object names to delete.
    /// </summary>
    public RemoveObjectsArgs WithObjects(params string[] objectNames)
    {
        foreach (var name in objectNames)
        {
            ObjectNames.Add(name ?? throw new ArgumentNullException(nameof(objectNames)));
        }
        return this;
    }

    /// <summary>
    /// Adds multiple object names to delete from a list.
    /// </summary>
    public RemoveObjectsArgs WithObjects(List<string> objectNames)
    {
        ObjectNames.AddRange(objectNames ?? throw new ArgumentNullException(nameof(objectNames)));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (ObjectNames.Count == 0)
            throw new ArgumentException("At least one object name is required.");
    }
}

/// <summary>
/// Arguments for initiating a multipart upload.
/// </summary>
public class InitiateMultipartUploadArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the content type.
    /// </summary>
    public string ContentType { get; private set; } = "application/octet-stream";

    /// <summary>
    /// Gets the metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; private set; } = new();

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public InitiateMultipartUploadArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public InitiateMultipartUploadArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the content type.
    /// </summary>
    public InitiateMultipartUploadArgs WithContentType(string contentType)
    {
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        return this;
    }

    /// <summary>
    /// Adds metadata.
    /// </summary>
    public InitiateMultipartUploadArgs WithMetadata(string key, string value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
    }
}

/// <summary>
/// Arguments for uploading a part in a multipart upload.
/// </summary>
public class UploadPartArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the upload ID.
    /// </summary>
    public string UploadId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the part number (1-10000).
    /// </summary>
    public int PartNumber { get; private set; }

    /// <summary>
    /// Gets the data stream for this part.
    /// </summary>
    public Stream? Data { get; private set; }

    /// <summary>
    /// Gets the size of the part in bytes.
    /// </summary>
    public long PartSize { get; private set; }

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public UploadPartArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public UploadPartArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the upload ID.
    /// </summary>
    public UploadPartArgs WithUploadId(string uploadId)
    {
        UploadId = uploadId ?? throw new ArgumentNullException(nameof(uploadId));
        return this;
    }

    /// <summary>
    /// Sets the part number.
    /// </summary>
    public UploadPartArgs WithPartNumber(int partNumber)
    {
        PartNumber = partNumber;
        return this;
    }

    /// <summary>
    /// Sets the part data and size.
    /// </summary>
    public UploadPartArgs WithData(Stream data, long size)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        PartSize = size;
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
        if (string.IsNullOrEmpty(UploadId))
            throw new ArgumentException("Upload ID is required.");
        if (PartNumber < 1 || PartNumber > 10000)
            throw new ArgumentException("Part number must be between 1 and 10000.");
        if (Data == null)
            throw new ArgumentException("Data stream is required.");
    }
}

/// <summary>
/// Arguments for completing a multipart upload.
/// </summary>
public class CompleteMultipartUploadArgs
{
    /// <summary>
    /// Represents an uploaded part with its ETag.
    /// </summary>
    public class PartInfo
    {
        /// <summary>
        /// Part number (1-10000).
        /// </summary>
        public int PartNumber { get; set; }

        /// <summary>
        /// ETag returned by the server for this part.
        /// </summary>
        public string ETag { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the upload ID.
    /// </summary>
    public string UploadId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the list of uploaded parts.
    /// </summary>
    public List<PartInfo> Parts { get; private set; } = new();

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public CompleteMultipartUploadArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public CompleteMultipartUploadArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the upload ID.
    /// </summary>
    public CompleteMultipartUploadArgs WithUploadId(string uploadId)
    {
        UploadId = uploadId ?? throw new ArgumentNullException(nameof(uploadId));
        return this;
    }

    /// <summary>
    /// Adds a completed part.
    /// </summary>
    public CompleteMultipartUploadArgs WithPart(int partNumber, string eTag)
    {
        Parts.Add(new PartInfo { PartNumber = partNumber, ETag = eTag });
        return this;
    }

    /// <summary>
    /// Adds multiple completed parts.
    /// </summary>
    public CompleteMultipartUploadArgs WithParts(IEnumerable<PartInfo> parts)
    {
        Parts.AddRange(parts ?? throw new ArgumentNullException(nameof(parts)));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
        if (string.IsNullOrEmpty(UploadId))
            throw new ArgumentException("Upload ID is required.");
        if (Parts.Count == 0)
            throw new ArgumentException("At least one part is required.");
    }
}

/// <summary>
/// Arguments for aborting a multipart upload.
/// </summary>
public class AbortMultipartUploadArgs
{
    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the upload ID.
    /// </summary>
    public string UploadId { get; private set; } = string.Empty;

    /// <summary>
    /// Sets the bucket name.
    /// </summary>
    public AbortMultipartUploadArgs WithBucket(string bucketName)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        return this;
    }

    /// <summary>
    /// Sets the object name.
    /// </summary>
    public AbortMultipartUploadArgs WithObject(string objectName)
    {
        ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
        return this;
    }

    /// <summary>
    /// Sets the upload ID.
    /// </summary>
    public AbortMultipartUploadArgs WithUploadId(string uploadId)
    {
        UploadId = uploadId ?? throw new ArgumentNullException(nameof(uploadId));
        return this;
    }

    /// <summary>
    /// Validates the arguments.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName))
            throw new ArgumentException("Bucket name is required.");
        if (string.IsNullOrEmpty(ObjectName))
            throw new ArgumentException("Object name is required.");
        if (string.IsNullOrEmpty(UploadId))
            throw new ArgumentException("Upload ID is required.");
    }
}
