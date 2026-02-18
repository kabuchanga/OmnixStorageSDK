namespace OmnixStorage;

/// <summary>
/// Base exception for all OmnixStorage SDK errors.
/// </summary>
public class OmnixStorageException : Exception
{
    /// <summary>
    /// Gets the HTTP status code if available.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets the error code returned by the server.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnixStorageException"/> class.
    /// </summary>
    public OmnixStorageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnixStorageException"/> class with status code and error code.
    /// </summary>
    public OmnixStorageException(string message, int? statusCode = null, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnixStorageException"/> class with inner exception.
    /// </summary>
    public OmnixStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a bucket is not found.
/// </summary>
public class BucketNotFoundException : OmnixStorageException
{
    /// <summary>
    /// Initializes a new instance of the BucketNotFoundException class.
    /// </summary>
    /// <param name="bucketName">The name of the bucket that was not found.</param>
    public BucketNotFoundException(string bucketName)
        : base($"Bucket '{bucketName}' not found.", 404, "NoSuchBucket")
    {
    }
}

/// <summary>
/// Thrown when trying to create a bucket that already exists.
/// </summary>
public class BucketAlreadyExistsException : OmnixStorageException
{
    /// <summary>
    /// Initializes a new instance of the BucketAlreadyExistsException class.
    /// </summary>
    /// <param name="bucketName">The name of the bucket that already exists.</param>
    public BucketAlreadyExistsException(string bucketName)
        : base($"Bucket '{bucketName}' already exists.", 409, "BucketAlreadyExists")
    {
    }
}

/// <summary>
/// Thrown when an object is not found.
/// </summary>
public class ObjectNotFoundException : OmnixStorageException
{
    /// <summary>
    /// Initializes a new instance of the ObjectNotFoundException class.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="objectName">The name of the object that was not found.</param>
    public ObjectNotFoundException(string bucketName, string objectName)
        : base($"Object '{objectName}' not found in bucket '{bucketName}'.", 404, "NoSuchKey")
    {
    }
}

/// <summary>
/// Thrown when an object name is invalid.
/// </summary>
public class InvalidObjectNameException : OmnixStorageException
{
    /// <summary>
    /// Initializes a new instance of the InvalidObjectNameException class.
    /// </summary>
    /// <param name="objectName">The invalid object name.</param>
    public InvalidObjectNameException(string objectName)
        : base($"Object name '{objectName}' is invalid.")
    {
    }
}

/// <summary>
/// Thrown when authentication fails.
/// </summary>
public class AuthenticationException : OmnixStorageException
{
    /// <summary>
    /// Initializes a new instance of the AuthenticationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public AuthenticationException(string message)
        : base(message, 401, "InvalidCredentials")
    {
    }

    /// <summary>
    /// Initializes a new instance of the AuthenticationException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when the server returns an error.
/// </summary>
public class ServerException : OmnixStorageException
{
    /// <summary>
    /// Initializes a new instance of the ServerException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="errorCode">The server error code.</param>
    public ServerException(string message, int statusCode, string? errorCode = null)
        : base(message, statusCode, errorCode)
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the ServerException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="innerException">The inner exception.</param>
    public ServerException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when access is denied.
/// </summary>
public class AccessDeniedException : OmnixStorageException
{
    /// <summary>
    /// Initializes a new instance of the AccessDeniedException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public AccessDeniedException(string message)
        : base(message, 403, "AccessDenied")
    {
    }
}
