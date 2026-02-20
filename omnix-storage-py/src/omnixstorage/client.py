"""
OmnixClient - S3-compatible client for OmnixStorage
"""

import json
import logging
import warnings
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta, UTC
from typing import Optional, Dict, BinaryIO, List
from urllib.parse import urljoin, urlparse, urlunparse, quote

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
        public_endpoint: Optional[str] = None,
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
            public_endpoint: Public endpoint used for browser-accessible presigned URLs
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
        self.public_endpoint = public_endpoint
        self.public_base_url = self._to_base_url(public_endpoint) if public_endpoint else None
        
        self._http = HttpClient(timeout=request_timeout, max_retries=max_retries)
        self._signer = AwsSignatureV4Signer(access_key, secret_key)
        self._jwt_token: Optional[str] = None
        self._token_expires_at: Optional[datetime] = None
        self._logger = logging.getLogger(__name__)

    def _to_base_url(self, endpoint: str) -> str:
        """Normalize endpoint value to base URL form."""
        if endpoint.startswith("http://") or endpoint.startswith("https://"):
            return endpoint.rstrip("/")
        scheme = "https" if self.use_ssl else "http"
        return f"{scheme}://{endpoint}".rstrip("/")

    def _rewrite_presigned_url_to_public_endpoint(self, url: str) -> str:
        """Rewrite a presigned URL host/scheme to configured public endpoint."""
        if not self.public_base_url:
            return url

        parsed_url = urlparse(url)
        parsed_public = urlparse(self.public_base_url)

        rewritten = parsed_url._replace(
            scheme=parsed_public.scheme,
            netloc=parsed_public.netloc,
        )
        return urlunparse(rewritten)

    @staticmethod
    def _is_internal_host(host: str) -> bool:
        normalized = host.strip().lower()
        if normalized in {"localhost", "127.0.0.1", "::1"}:
            return True
        if normalized.endswith(".local") or normalized.endswith(".internal"):
            return True
        if normalized.startswith("10.") or normalized.startswith("192.168."):
            return True
        if normalized.startswith("172."):
            parts = normalized.split(".")
            if len(parts) > 1:
                try:
                    second = int(parts[1])
                    if 16 <= second <= 31:
                        return True
                except ValueError:
                    pass
        return False

    def _validate_expiry(self, expires_in_seconds: int) -> None:
        if expires_in_seconds < 1 or expires_in_seconds > 604800:
            raise ValueError(
                "Expiry must be between 1 second and 604800 seconds (7 days) per AWS S3 specification."
            )

    def _effective_presigned_base_url(self, browser_accessible: bool) -> str:
        if browser_accessible and self.public_base_url:
            return self.public_base_url

        if browser_accessible and not self.public_base_url:
            warnings.warn(
                "browser_accessible=True but public_endpoint is not configured. "
                "Returned URL may use an internal endpoint and fail in browser access.",
                stacklevel=3,
            )

        return self.base_url

    def _validate_and_log_presigned_url(self, url: str, expires_in_seconds: int, bucket_name: str, object_name: str) -> str:
        parsed = urlparse(url)
        if not parsed.scheme or not parsed.netloc:
            raise ValueError("Generated presigned URL is not a valid absolute URL.")

        host = parsed.hostname or ""
        if self._is_internal_host(host):
            raise ValueError(
                f"Rejected presigned URL generation because host '{host}' appears internal. "
                "Configure public_endpoint for browser-accessible URLs."
            )

        if self.public_base_url:
            public_host = urlparse(self.public_base_url).hostname or ""
            if public_host and host.lower() != public_host.lower():
                raise ValueError(
                    f"Rejected presigned URL generation because host '{host}' does not match configured public host '{public_host}'."
                )
        else:
            public_host = ""

        self._logger.info(
            "[OmnixStorage][PresignedUrl] host=%s configuredPublicHost=%s expirySeconds=%s bucket=%s object=%s",
            host,
            public_host,
            expires_in_seconds,
            bucket_name,
            object_name,
        )

        return url
    
    async def _get_auth_token(self) -> str:
        """Get JWT authentication token with caching."""
        # Return cached token if still valid
        if self._jwt_token and self._token_expires_at:
            if datetime.now(UTC) < self._token_expires_at:
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
            self._token_expires_at = datetime.now(UTC)
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
    ):
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
        elif method == "POST":
            if data:
                response = await self._http.post(url, content=data.read(), headers=headers)
            elif json_data:
                response = await self._http.post(url, json=json_data, headers=headers)
            else:
                response = await self._http.post(url, content=content, headers=headers)
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

    async def copy_object(
        self,
        source_bucket: str,
        source_object: str,
        destination_bucket: str,
        destination_object: str,
        metadata: Optional[Dict[str, str]] = None,
    ) -> Dict[str, Optional[str]]:
        """Copy an object to another location."""
        copy_source = f"/{source_bucket}/{source_object}"
        headers = {
            "x-amz-copy-source": copy_source,
            "Content-Length": "1",
        }

        if metadata:
            headers["x-amz-metadata-directive"] = "REPLACE"
            for key, value in metadata.items():
                headers[f"x-amz-meta-{key}"] = value

        path = f"/{destination_bucket}/{destination_object}"
        response = await self._make_request("PUT", path, headers=headers, content=b"\x00")

        etag = response.headers.get("ETag")
        if etag:
            return {"etag": etag.strip('"'), "last_modified": None}

        body = response.text or ""
        result = {"etag": None, "last_modified": None}
        if body.strip():
            try:
                doc = ET.fromstring(body)
                for node in doc.iter():
                    tag = node.tag.split("}")[-1]
                    if tag.lower() == "etag" and node.text:
                        result["etag"] = node.text.strip('"')
                    elif tag.lower() == "lastmodified" and node.text:
                        result["last_modified"] = node.text
            except ET.ParseError:
                pass

        return result

    async def remove_objects(self, bucket_name: str, object_names: List[str]) -> Dict[str, List[Dict[str, str]]]:
        """Remove multiple objects using S3 delete XML payload."""
        if not object_names:
            return {"deleted": [], "errors": []}

        delete_el = ET.Element("Delete")
        for name in object_names:
            obj_el = ET.SubElement(delete_el, "Object")
            key_el = ET.SubElement(obj_el, "Key")
            key_el.text = name

        xml_payload = ET.tostring(delete_el, encoding="utf-8", method="xml")
        path = f"/{bucket_name}?delete"
        headers = {"Content-Type": "application/xml"}
        response = await self._make_request("POST", path, headers=headers, content=xml_payload)

        result = {"deleted": [], "errors": []}
        body = response.text or ""
        if not body.strip():
            result["deleted"] = [{"key": name} for name in object_names]
            return result

        try:
            doc = ET.fromstring(body)
            for node in doc.iter():
                tag = node.tag.split("}")[-1].lower()
                if tag == "deleted":
                    key = ""
                    for child in list(node):
                        child_tag = child.tag.split("}")[-1].lower()
                        if child_tag == "key" and child.text:
                            key = child.text
                    if key:
                        result["deleted"].append({"key": key})
                elif tag == "error":
                    item = {"key": "", "code": "", "message": ""}
                    for child in list(node):
                        child_tag = child.tag.split("}")[-1].lower()
                        if child_tag == "key" and child.text:
                            item["key"] = child.text
                        elif child_tag == "code" and child.text:
                            item["code"] = child.text
                        elif child_tag == "message" and child.text:
                            item["message"] = child.text
                    result["errors"].append(item)
        except ET.ParseError:
            result["deleted"] = [{"key": name} for name in object_names]

        return result

    async def initiate_multipart_upload(
        self,
        bucket_name: str,
        object_name: str,
        content_type: str = "application/octet-stream",
        metadata: Optional[Dict[str, str]] = None,
    ) -> Dict[str, str]:
        """Initiate multipart upload and return upload_id."""
        path = f"/{bucket_name}/{object_name}?uploads"
        headers = {
            "Content-Type": content_type,
            "Content-Length": "1",
        }
        if metadata:
            for key, value in metadata.items():
                headers[f"x-amz-meta-{key}"] = value

        response = await self._make_request("POST", path, headers=headers, content=b"\x00")
        body = response.text or ""
        if not body.strip():
            raise ServerException("Multipart upload initiation did not return an upload ID.", response.status_code)

        try:
            doc = ET.fromstring(body)
            upload_id = ""
            for node in doc.iter():
                if node.tag.split("}")[-1].lower() == "uploadid" and node.text:
                    upload_id = node.text
                    break
            if not upload_id:
                raise ServerException("Multipart upload initiation did not return an upload ID.", response.status_code)
            return {"upload_id": upload_id}
        except ET.ParseError as ex:
            raise ServerException(f"Failed to parse multipart upload initiation response. {str(ex)}", response.status_code)

    async def upload_part(
        self,
        bucket_name: str,
        object_name: str,
        upload_id: str,
        part_number: int,
        data: BinaryIO,
    ) -> Dict[str, Optional[str]]:
        """Upload one multipart part."""
        path = f"/{bucket_name}/{object_name}?partNumber={part_number}&uploadId={quote(upload_id, safe='')}"
        response = await self._make_request("PUT", path, data=data)
        etag = response.headers.get("ETag", "").strip('"')
        return {"part_number": str(part_number), "etag": etag or None}

    async def complete_multipart_upload(
        self,
        bucket_name: str,
        object_name: str,
        upload_id: str,
        parts: List[Dict[str, str]],
    ) -> Dict[str, Optional[str]]:
        """Complete multipart upload with part list."""
        path = f"/{bucket_name}/{object_name}?uploadId={quote(upload_id, safe='')}"

        root = ET.Element("CompleteMultipartUpload")
        for part in parts:
            part_el = ET.SubElement(root, "Part")
            num_el = ET.SubElement(part_el, "PartNumber")
            num_el.text = str(part["part_number"])
            etag_el = ET.SubElement(part_el, "ETag")
            etag_value = part["etag"]
            if etag_value and not etag_value.startswith('"'):
                etag_value = f'"{etag_value}"'
            etag_el.text = etag_value

        payload = ET.tostring(root, encoding="utf-8", method="xml")
        response = await self._make_request("POST", path, headers={"Content-Type": "application/xml"}, content=payload)
        body = response.text or ""

        result = {"etag": None, "location": None}
        if body.strip():
            try:
                doc = ET.fromstring(body)
                for node in doc.iter():
                    tag = node.tag.split("}")[-1].lower()
                    if tag == "etag" and node.text:
                        result["etag"] = node.text.strip('"')
                    elif tag == "location" and node.text:
                        result["location"] = node.text
            except ET.ParseError:
                pass

        if not result["etag"]:
            result["etag"] = response.headers.get("ETag", "").strip('"') or None

        return result

    async def abort_multipart_upload(
        self,
        bucket_name: str,
        object_name: str,
        upload_id: str,
    ) -> None:
        """Abort multipart upload."""
        path = f"/{bucket_name}/{object_name}?uploadId={quote(upload_id, safe='')}"
        await self._make_request("DELETE", path)
    
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
        browser_accessible: bool = True,
    ) -> PresignedUrlResult:
        """Generate a presigned GET URL using local AWS SigV4 signing."""
        self._validate_expiry(expires_in_seconds)

        base_url = self._effective_presigned_base_url(browser_accessible)
        parsed_base = urlparse(base_url)
        host = parsed_base.netloc
        if not host:
            raise ValueError("Invalid endpoint configuration for presigned URL generation.")

        encoded_object = quote(object_name, safe="")
        path = f"/{bucket_name}/{encoded_object}"

        url = self._signer.generate_presigned_url(
            method="GET",
            host=host,
            path=path,
            expires_in_seconds=expires_in_seconds,
            use_https=(parsed_base.scheme.lower() == "https"),
        )

        if browser_accessible:
            url = self._validate_and_log_presigned_url(url, expires_in_seconds, bucket_name, object_name)

        return PresignedUrlResult(
            url=url,
            expires_at=datetime.now(UTC) + timedelta(seconds=int(expires_in_seconds)),
        )

    async def presigned_put_object(
        self,
        bucket_name: str,
        object_name: str,
        expires_in_seconds: int = 3600,
        content_type: Optional[str] = None,
        browser_accessible: bool = True,
    ) -> PresignedUrlResult:
        """Generate a presigned PUT URL using local AWS SigV4 signing."""
        self._validate_expiry(expires_in_seconds)

        base_url = self._effective_presigned_base_url(browser_accessible)
        parsed_base = urlparse(base_url)
        host = parsed_base.netloc
        if not host:
            raise ValueError("Invalid endpoint configuration for presigned URL generation.")

        encoded_object = quote(object_name, safe="")
        path = f"/{bucket_name}/{encoded_object}"

        query_params: Dict[str, str] = {}
        if content_type:
            query_params["content-type"] = content_type

        url = self._signer.generate_presigned_url(
            method="PUT",
            host=host,
            path=path,
            query_params=query_params,
            expires_in_seconds=expires_in_seconds,
            use_https=(parsed_base.scheme.lower() == "https"),
        )

        if browser_accessible:
            url = self._validate_and_log_presigned_url(url, expires_in_seconds, bucket_name, object_name)
        
        return PresignedUrlResult(
            url=url,
            expires_at=datetime.now(UTC) + timedelta(seconds=int(expires_in_seconds)),
        )

    async def ensure_bucket_exists(self, bucket_name: str, max_attempts: int = 3, delay_seconds: int = 2) -> None:
        """Ensure a bucket exists with retries (parity helper with .NET extensions)."""
        import asyncio

        last_error: Optional[Exception] = None
        for attempt in range(1, max_attempts + 1):
            try:
                if await self.bucket_exists(bucket_name):
                    return
                await self.make_bucket(bucket_name)
                if await self.bucket_exists(bucket_name):
                    return
            except Exception as ex:
                last_error = ex

            if attempt < max_attempts:
                await asyncio.sleep(delay_seconds)

        if last_error is not None:
            raise last_error
        raise RuntimeError(f"Failed to ensure bucket '{bucket_name}' exists after {max_attempts} attempts.")

    async def health_check_buckets(self) -> bool:
        """Check bucket-listing health (parity helper with .NET extensions)."""
        try:
            await self.list_buckets()
            return True
        except Exception:
            return False
    
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
