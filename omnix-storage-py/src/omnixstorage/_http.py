"""
HTTP client utilities for OmnixStorage SDK
"""

import httpx
from typing import Optional, Dict, Any
import asyncio


class HttpClient:
    """
    HTTP client wrapper with connection pooling and retry logic.
    """
    
    def __init__(self, timeout: int = 30, max_retries: int = 3):
        self.timeout = timeout
        self.max_retries = max_retries
        self._client = httpx.AsyncClient(
            timeout=timeout,
            limits=httpx.Limits(max_connections=100, max_keepalive_connections=100)
        )
    
    async def get(
        self,
        url: str,
        headers: Optional[Dict[str, str]] = None,
        **kwargs
    ) -> httpx.Response:
        """Make a GET request with retry logic."""
        return await self._request("GET", url, headers=headers, **kwargs)
    
    async def put(
        self,
        url: str,
        content: Optional[bytes] = None,
        data: Optional[Any] = None,
        headers: Optional[Dict[str, str]] = None,
        **kwargs
    ) -> httpx.Response:
        """Make a PUT request with retry logic."""
        return await self._request(
            "PUT",
            url,
            content=content,
            data=data,
            headers=headers,
            **kwargs
        )
    
    async def delete(
        self,
        url: str,
        headers: Optional[Dict[str, str]] = None,
        **kwargs
    ) -> httpx.Response:
        """Make a DELETE request with retry logic."""
        return await self._request("DELETE", url, headers=headers, **kwargs)
    
    async def post(
        self,
        url: str,
        json: Optional[Dict] = None,
        data: Optional[Any] = None,
        headers: Optional[Dict[str, str]] = None,
        **kwargs
    ) -> httpx.Response:
        """Make a POST request with retry logic."""
        return await self._request(
            "POST",
            url,
            json=json,
            data=data,
            headers=headers,
            **kwargs
        )
    
    async def head(
        self,
        url: str,
        headers: Optional[Dict[str, str]] = None,
        **kwargs
    ) -> httpx.Response:
        """Make a HEAD request with retry logic."""
        return await self._request("HEAD", url, headers=headers, **kwargs)
    
    async def _request(
        self,
        method: str,
        url: str,
        headers: Optional[Dict[str, str]] = None,
        **kwargs
    ) -> httpx.Response:
        """Make HTTP request with exponential backoff retry logic."""
        for attempt in range(self.max_retries):
            try:
                response = await self._client.request(method, url, headers=headers, **kwargs)
                return response
            except httpx.RequestError:
                if attempt < self.max_retries - 1:
                    wait_time = min(1000 * (2 ** attempt), 10000) / 1000
                    await asyncio.sleep(wait_time)
                else:
                    raise
    
    async def close(self):
        """Close the HTTP client."""
        await self._client.aclose()
    
    async def __aenter__(self):
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.close()
