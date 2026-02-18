"""
Data models for OmnixStorage SDK
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional, List, Dict


@dataclass
class Bucket:
    """Represents a bucket in OmnixStorage."""
    name: str
    creation_date: datetime


@dataclass
class ObjectMetadata:
    """Represents object metadata."""
    object_name: str
    bucket_name: str
    size: int = 0
    etag: Optional[str] = None
    last_modified: Optional[datetime] = None
    content_type: Optional[str] = None
    metadata: Dict[str, str] = field(default_factory=dict)


@dataclass
class ListObjectsResult:
    """Represents the result of a list objects operation."""
    objects: List[ObjectMetadata] = field(default_factory=list)
    is_truncated: bool = False
    continuation_token: Optional[str] = None
    common_prefixes: List[str] = field(default_factory=list)


@dataclass
class PutObjectResult:
    """Represents the result of a put object operation."""
    bucket_name: str
    object_name: str
    etag: str
    version_id: Optional[str] = None


@dataclass
class PresignedUrlResult:
    """Represents a presigned URL response."""
    url: str
    expires_at: datetime


@dataclass
class BucketPolicyResult:
    """Represents bucket policy information."""
    bucket_name: str
    policy_json: Optional[str] = None
