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
