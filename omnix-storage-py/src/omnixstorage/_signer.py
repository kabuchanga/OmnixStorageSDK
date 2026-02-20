"""
AWS Signature V4 signer for OmnixStorage SDK
"""

import hashlib
import hmac
import json
from datetime import datetime, UTC
from typing import Dict, Optional
from urllib.parse import quote


class AwsSignatureV4Signer:
    """
    Signs requests using AWS Signature Version 4.
    Used for presigned URL generation.
    """
    
    def __init__(self, access_key: str, secret_key: str):
        self.access_key = access_key
        self.secret_key = secret_key
    
    def sign_request(
        self,
        method: str,
        host: str,
        path: str,
        query_params: Optional[Dict[str, str]] = None,
        headers: Optional[Dict[str, str]] = None,
        body: Optional[bytes] = None,
        timestamp: Optional[datetime] = None,
    ) -> Dict[str, str]:
        """
        Sign a request and return headers with authorization.
        """
        if timestamp is None:
            timestamp = datetime.now(UTC)
        
        amz_date = timestamp.strftime("%Y%m%dT%H%M%SZ")
        datestamp = timestamp.strftime("%Y%m%d")
        
        # Canonical request
        canonical_headers_dict = self._build_canonical_headers(headers or {})
        canonical_headers_str = "\n".join(
            f"{k}:{v}" for k, v in sorted(canonical_headers_dict.items())
        ) + "\n"
        signed_headers = ";".join(sorted(canonical_headers_dict.keys()))
        
        canonical_querystring = self._build_canonical_querystring(query_params or {})
        payload_hash = self._hash_payload(body)
        
        canonical_request = "\n".join([
            method,
            path,
            canonical_querystring,
            canonical_headers_str.rstrip(),
            "",
            signed_headers,
            payload_hash,
        ])
        
        # String to sign
        credential_scope = f"{datestamp}/us-east-1/s3/aws4_request"
        canonical_request_hash = hashlib.sha256(
            canonical_request.encode()
        ).hexdigest()
        
        string_to_sign = "\n".join([
            "AWS4-HMAC-SHA256",
            amz_date,
            credential_scope,
            canonical_request_hash,
        ])
        
        # Signature
        signing_key = self._derive_signing_key(datestamp)
        signature = hmac.new(
            signing_key,
            string_to_sign.encode(),
            hashlib.sha256
        ).hexdigest()
        
        # Authorization header
        auth_header = (
            f"AWS4-HMAC-SHA256 Credential={self.access_key}/{credential_scope}, "
            f"SignedHeaders={signed_headers}, Signature={signature}"
        )
        
        return {
            "Authorization": auth_header,
            "X-Amz-Date": amz_date,
        }
    
    def _build_canonical_headers(self, headers: Dict[str, str]) -> Dict[str, str]:
        """Build canonical headers for signing."""
        canonical = {}
        for key, value in headers.items():
            canonical[key.lower()] = value.strip()
        return canonical
    
    def _build_canonical_querystring(self, params: Dict[str, str]) -> str:
        """Build canonical query string for signing."""
        if not params:
            return ""
        sorted_params = sorted(params.items())
        return "&".join(
            f"{quote(k, safe='')}={quote(str(v), safe='')}"
            for k, v in sorted_params
        )
    
    def _hash_payload(self, body: Optional[bytes]) -> str:
        """Hash the request payload."""
        if body is None:
            return hashlib.sha256(b"").hexdigest()
        return hashlib.sha256(body).hexdigest()
    
    def _derive_signing_key(self, datestamp: str) -> bytes:
        """Derive the signing key for AWS Signature V4."""
        k_date = hmac.new(
            f"AWS4{self.secret_key}".encode(),
            datestamp.encode(),
            hashlib.sha256
        ).digest()
        
        k_region = hmac.new(k_date, "us-east-1".encode(), hashlib.sha256).digest()
        k_service = hmac.new(k_region, "s3".encode(), hashlib.sha256).digest()
        k_signing = hmac.new(k_service, "aws4_request".encode(), hashlib.sha256).digest()
        
        return k_signing
    
    def generate_presigned_url(
        self,
        method: str,
        host: str,
        path: str,
        query_params: Optional[Dict[str, str]] = None,
        expires_in: int = 3600,
        expires_in_seconds: Optional[int] = None,
        use_https: bool = False,
        timestamp: Optional[datetime] = None,
    ) -> str:
        """
        Generate a presigned URL with AWS Signature V4.
        """
        # Support both expires_in and expires_in_seconds for backward compatibility
        expiry = expires_in_seconds if expires_in_seconds is not None else expires_in

        if expiry < 1 or expiry > 604800:
            raise ValueError(
                "Expiry must be between 1 second and 604800 seconds (7 days) per AWS S3 specification."
            )
        
        if timestamp is None:
            timestamp = datetime.now(UTC)
        
        amz_date = timestamp.strftime("%Y%m%dT%H%M%SZ")
        datestamp = timestamp.strftime("%Y%m%d")
        
        # Create credential scope
        credential_scope = f"{datestamp}/us-east-1/s3/aws4_request"
        
        # Prepare query parameters for presigned URL
        presigned_params = {
            "X-Amz-Algorithm": "AWS4-HMAC-SHA256",
            "X-Amz-Credential": f"{self.access_key}/{credential_scope}",
            "X-Amz-Date": amz_date,
            "X-Amz-Expires": str(expiry),
            "X-Amz-SignedHeaders": "host",
        }
        
        if query_params:
            presigned_params.update(query_params)
        
        # Build canonical request
        canonical_querystring = self._build_canonical_querystring(presigned_params)
        payload_hash = self._hash_payload(None)
        
        canonical_request = "\n".join([
            method,
            path,
            canonical_querystring,
            "host:" + host,
            "",
            "host",
            payload_hash,
        ])
        
        # String to sign
        canonical_request_hash = hashlib.sha256(
            canonical_request.encode()
        ).hexdigest()
        
        string_to_sign = "\n".join([
            "AWS4-HMAC-SHA256",
            amz_date,
            credential_scope,
            canonical_request_hash,
        ])
        
        # Signature
        signing_key = self._derive_signing_key(datestamp)
        signature = hmac.new(
            signing_key,
            string_to_sign.encode(),
            hashlib.sha256
        ).hexdigest()
        
        # Build presigned URL
        presigned_params["X-Amz-Signature"] = signature
        
        scheme = "https" if use_https else "http"
        url = f"{scheme}://{host}{path}"
        query_string = self._build_canonical_querystring(presigned_params)
        return f"{url}?{query_string}"
