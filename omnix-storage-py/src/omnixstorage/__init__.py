"""
OmnixStorage Python SDK - S3-compatible client for OmnixStorage
"""

__version__ = "1.0.0"
__author__ = "Kegeo Solutions Ltd"

from .client import OmnixClient
from .models import (
    Bucket,
    ObjectMetadata,
    ListObjectsResult,
    PutObjectResult,
    PresignedUrlResult,
    BucketPolicyResult,
)
from .error import (
    OmnixStorageException,
    BucketNotFoundException,
    BucketAlreadyExistsException,
    ObjectNotFoundException,
    InvalidObjectNameException,
    AuthenticationException,
    ServerException,
    AccessDeniedException,
)

__all__ = [
    "OmnixClient",
    "Bucket",
    "ObjectMetadata",
    "ListObjectsResult",
    "PutObjectResult",
    "PresignedUrlResult",
    "BucketPolicyResult",
    "OmnixStorageException",
    "BucketNotFoundException",
    "BucketAlreadyExistsException",
    "ObjectNotFoundException",
    "InvalidObjectNameException",
    "AuthenticationException",
    "ServerException",
    "AccessDeniedException",
]
