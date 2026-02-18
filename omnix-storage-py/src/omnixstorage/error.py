"""
Exception classes for OmnixStorage SDK
"""


class OmnixStorageException(Exception):
    """
    Base exception for all OmnixStorage SDK errors.
    """
    
    def __init__(self, message: str, status_code: int = None, error_code: str = None):
        super().__init__(message)
        self.status_code = status_code
        self.error_code = error_code


class BucketNotFoundException(OmnixStorageException):
    """Thrown when a bucket is not found."""
    
    def __init__(self, bucket_name: str):
        super().__init__(
            f"Bucket '{bucket_name}' not found.",
            status_code=404,
            error_code="NoSuchBucket"
        )


class BucketAlreadyExistsException(OmnixStorageException):
    """Thrown when trying to create a bucket that already exists."""
    
    def __init__(self, bucket_name: str):
        super().__init__(
            f"Bucket '{bucket_name}' already exists.",
            status_code=409,
            error_code="BucketAlreadyExists"
        )


class ObjectNotFoundException(OmnixStorageException):
    """Thrown when an object is not found."""
    
    def __init__(self, bucket_name: str, object_name: str):
        super().__init__(
            f"Object '{object_name}' not found in bucket '{bucket_name}'.",
            status_code=404,
            error_code="NoSuchKey"
        )


class InvalidObjectNameException(OmnixStorageException):
    """Thrown when an object name is invalid."""
    
    def __init__(self, object_name: str):
        super().__init__(f"Object name '{object_name}' is invalid.")


class AuthenticationException(OmnixStorageException):
    """Thrown when authentication fails."""
    
    def __init__(self, message: str):
        super().__init__(
            message,
            status_code=401,
            error_code="InvalidCredentials"
        )


class ServerException(OmnixStorageException):
    """Thrown when the server returns an error."""
    
    def __init__(self, message: str, status_code: int, error_code: str = None):
        super().__init__(message, status_code, error_code)


class AccessDeniedException(OmnixStorageException):
    """Thrown when access is denied."""
    
    def __init__(self, message: str):
        super().__init__(
            message,
            status_code=403,
            error_code="AccessDenied"
        )
