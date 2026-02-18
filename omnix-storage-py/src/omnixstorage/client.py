"""
OmnixClient - S3-compatible client for OmnixStorage
"""

import json
from datetime import datetime
from typing import Optional, Dict, BinaryIO
from urllib.parse import urljoin

from ._http import HttpClient
from ._signer import AwsSignatureV4Signer
from .models import (
    Bucket,
    ObjectMetadata,
    ListObjectsResult,
    PutObjectResult,
    PresignedUrlResult,
)
from .error import (
    OmnixStorageException,
    BucketNotFoundException,
    ObjectNotFoundException,
    AuthenticationException,
    ServerException,
)


class OmnixClient:
    """
    S3-compatible client for OmnixStorage.
    
    Example:
        client = OmnixClient(
            endpoint="omnix.production.local:9000",
            access_key="admin",
            secret_key="password"
        )
        
        # Upload a file
        with open("image.jpg", "rb") as f:
            result = await client.put_object(
                bucket_name="photos",
                object_name="archive/camera-001/image.jpg",
                data=f,
                content_type="image/jpeg"
            )
    """
    
    def __init__(
        self,
        endpoint: str = "localhost:9000",
        username: str = "admin",
        password: str = "omnix-console-2026",
        access_key: str = "admin",
        secret_key: str = "omnix-secret-2026",
        use_ssl: bool = False,
        request_timeout: int = 30,
        max_retries: int = 3,
    ):
        """
        Initialize OmnixClient.
        
        Args:
            endpoint: Server address and port (e.g., "omnix.local:9000")
            username: Admin username for JWT authentication
            password: Admin password for JWT authentication
            access_key: S3 access key for request signing
            secret_key: S3 secret key for request signing
            use_ssl: Use HTTPS instead of HTTP
            request_timeout: Request timeout in seconds
            max_retries: Maximum number of retries for failed requests
        """
        self.endpoint = endpoint
        self.username = username
        self.password = password
        self.access_key = access_key
        self.secret_key = secret_key
        self.use_ssl = use_ssl
        self.base_url = f"{'https' if use_ssl else 'http'}://{endpoint}"
        
        self._http = HttpClient(timeout=request_timeout, max_retries=max_retries)
        self._signer = AwsSignatureV4Signer(access_key, secret_key)
        self._jwt_token: Optional[str] = None
        self._token_expires_at: Optional[datetime] = None
    
    async def _get_auth_token(self) -> str:
        """Get JWT authentication token with caching."""
        # Return cached token if still valid
        if self._jwt_token and self._token_expires_at:
            if datetime.utcnow() < self._token_expires_at:
                return self._jwt_token
        
        try:
            url = urljoin(self.base_url, "/api/admin/auth/login")
            payload = {
                "Username": self.username,
                "Password": self.password,
            }
            
            response = await self._http.post(
                url,
                json=payload,
            )
            
            if response.status_code >= 400:
                raise AuthenticationException("Failed to authenticate with the server.")
            
            data = response.json()
            token = data.get("token")
            
            if not token:
                raise AuthenticationException("No token in authentication response.")
            
            self._jwt_token = token
            # Token expires in 8 hours, refresh after 7 hours
            import asyncio
            self._token_expires_at = datetime.utcnow()
            # Note: In production, parse the JWT to get actual expiration
            # For now, assume 8 hours expiration
            from datetime import timedelta
            self._token_expires_at += timedelta(hours=7)
            
            return token
        except Exception as ex:
            if isinstance(ex, AuthenticationException):
                raise
            raise AuthenticationException(f"Authentication failed: {str(ex)}")
    
    async def _make_request(
        self,
        method: str,
        path: str,
        headers: Optional[Dict[str, str]] = None,
        content: Optional[bytes] = None,
        data: Optional[BinaryIO] = None,
        json_data: Optional[Dict] = None,
    ) -> dict:
        """Make authenticated HTTP request."""
        token = await self._get_auth_token()
        
        if headers is None:
            headers = {}
        
        headers["Authorization"] = f"Bearer {token}"
        
        url = urljoin(self.base_url, path)
        
        if method == "GET":
            response = await self._http.get(url, headers=headers)
        elif method == "HEAD":
            response = await self._http.head(url, headers=headers)
        elif method == "PUT":
            if data:
                response = await self._http.put(url, content=data.read(), headers=headers)
            elif json_data:
                response = await self._http.put(url, json=json_data, headers=headers)
            else:
                response = await self._http.put(url, content=content, headers=headers)
        elif method == "DELETE":
            response = await self._http.delete(url, headers=headers)
        else:
            raise ValueError(f"Unsupported HTTP method: {method}")
        
        if response.status_code >= 400:
            error_msg = f"Request failed with status {response.status_code}"
            if response.status_code == 404:
                error_msg = "Resource not found"
            raise ServerException(error_msg, response.status_code)
        
        return response
    
    # Bucket operations
    
    async def bucket_exists(self, bucket_name: str) -> bool:
        """Check if a bucket exists."""
        try:
            path = f"/{bucket_name}"
            await self._make_request("HEAD", path)
            return True
        except ServerException as e:
            if e.status_code == 404:
                return False
            raise
    
    async def make_bucket(self, bucket_name: str) -> None:
        """Create a new bucket."""
        path = f"/{bucket_name}"
        await self._make_request("PUT", path)
    
    async def remove_bucket(self, bucket_name: str) -> None:
        """Remove a bucket (must be empty)."""
        path = f"/{bucket_name}"
        await self._make_request("DELETE", path)
    
    async def list_buckets(self) -> list[Bucket]:
        """List all buckets."""
        response = await self._make_request("GET", "/api/admin/buckets")
        data = response.json()
        
        buckets = []
        for bucket_data in data.get("buckets", []):
            buckets.append(
                Bucket(
                    name=bucket_data["name"],
                    creation_date=datetime.fromisoformat(
                        bucket_data["createdAt"]
                    ),
                )
            )
        return buckets
    
    # Object operations
    
    async def put_object(
        self,
        bucket_name: str,
        object_name: str,
        data: BinaryIO,
        length: Optional[int] = None,
        content_type: str = "application/octet-stream",
        metadata: Optional[Dict[str, str]] = None,
    ) -> PutObjectResult:
        """Upload an object to the bucket."""
        headers = {"Content-Type": content_type}
        
        if metadata:
            for key, value in metadata.items():
                headers[f"x-amz-meta-{key}"] = value
        
        if length:
            headers["Content-Length"] = str(length)
        
        path = f"/{bucket_name}/{object_name}"
        response = await self._make_request("PUT", path, headers=headers, data=data)
        
        etag = response.headers.get("ETag", "unknown")
        
        return PutObjectResult(
            bucket_name=bucket_name,
            object_name=object_name,
            etag=etag,
        )
    
    async def get_object(
        self,
        bucket_name: str,
        object_name: str,
        output: BinaryIO,
    ) -> ObjectMetadata:
        """Download an object from the bucket."""
        path = f"/{bucket_name}/{object_name}"
        
        try:
            response = await self._make_request("GET", path)
        except ServerException as e:
            if e.status_code == 404:
                raise ObjectNotFoundException(bucket_name, object_name)
            raise
        
        output.write(response.content)
        
        return ObjectMetadata(
            object_name=object_name,
            bucket_name=bucket_name,
            size=len(response.content),
            content_type=response.headers.get("Content-Type"),
            etag=response.headers.get("ETag", "unknown"),
        )
    
    async def stat_object(
        self,
        bucket_name: str,
        object_name: str,
    ) -> ObjectMetadata:
        """Get object metadata without downloading."""
        path = f"/{bucket_name}/{object_name}"
        
        try:
            response = await self._make_request("HEAD", path)
        except ServerException as e:
            if e.status_code == 404:
                raise ObjectNotFoundException(bucket_name, object_name)
            raise
        
        return ObjectMetadata(
            object_name=object_name,
            bucket_name=bucket_name,
            size=int(response.headers.get("Content-Length", 0)),
            content_type=response.headers.get("Content-Type"),
            etag=response.headers.get("ETag", "unknown"),
        )
    
    async def remove_object(
        self,
        bucket_name: str,
        object_name: str,
    ) -> None:
        """Remove an object from the bucket."""
        path = f"/{bucket_name}/{object_name}"
        
        try:
            await self._make_request("DELETE", path)
        except ServerException as e:
            # Don't fail if object doesn't exist
            if e.status_code != 404:
                raise
    
    async def list_objects(
        self,
        bucket_name: str,
        prefix: str = "",
        delimiter: str = "",
        max_keys: int = 1000,
        continuation_token: Optional[str] = None,
    ) -> ListObjectsResult:
        """List objects in a bucket."""
        query_params = {}
        if prefix:
            query_params["prefix"] = prefix
        if delimiter:
            query_params["delimiter"] = delimiter
        if max_keys:
            query_params["max-keys"] = str(max_keys)
        if continuation_token:
            query_params["continuation-token"] = continuation_token
        
        query_string = "&".join(
            f"{k}={v}" for k, v in query_params.items()
        )
        path = f"/{bucket_name}"
        if query_string:
            path += f"?{query_string}"
        
        response = await self._make_request("GET", path)
        data = response.json()
        
        objects = []
        for obj in data.get("contents", []):
            objects.append(
                ObjectMetadata(
                    object_name=obj["key"],
                    bucket_name=bucket_name,
                    size=obj["size"],
                    etag=obj.get("etag", "unknown"),
                    last_modified=datetime.fromisoformat(obj["lastModified"]),
                )
            )
        
        return ListObjectsResult(
            objects=objects,
            is_truncated=data.get("isTruncated", False),
            continuation_token=data.get("continuationToken"),
            common_prefixes=data.get("commonPrefixes", []),
        )
    
    async def presigned_get_object(
        self,
        bucket_name: str,
        object_name: str,
        expires_in_seconds: int = 3600,
    ) -> PresignedUrlResult:
        """Generate a presigned URL for downloading an object."""
        path = (
            f"/api/presigned-url?"
            f"bucket={bucket_name}&"
            f"object={object_name}&"
            f"expires-in={expires_in_seconds}"
        )
        
        response = await self._make_request("GET", path)
        data = response.json()
        
        return PresignedUrlResult(
            url=data["url"],
            expires_at=datetime.utcnow().replace(
                second=int(expires_in_seconds)
            ),
        )
    
    # Health check
    
    async def health(self) -> bool:
        """Check if the server is healthy."""
        try:
            await self._make_request("GET", "/api/health")
            return True
        except Exception:
            return False
    
    async def close(self) -> None:
        """Close the client and cleanup resources."""
        await self._http.close()
    
    async def __aenter__(self):
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.close()
