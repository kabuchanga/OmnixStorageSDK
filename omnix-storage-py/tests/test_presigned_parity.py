import pytest

from omnixstorage.client import OmnixClient
from omnixstorage._signer import AwsSignatureV4Signer


@pytest.mark.asyncio
async def test_presigned_get_uses_public_endpoint_and_sigv4_params():
    client = OmnixClient(
        endpoint="storage.kegeosapps.com:443",
        public_endpoint="https://storage-public.kegeosapps.com",
        access_key="AKIATESAFEKEY0000001",
        secret_key="wJalrXUtnFEMIaKkMDENGbPxRfIcxAmPlEkEyZaB",
        use_ssl=True,
    )

    result = await client.presigned_get_object(
        bucket_name="photo-test",
        object_name="images/13-47-42-738.jpg",
        expires_in_seconds=3600,
        browser_accessible=True,
    )

    assert result.url.startswith("https://storage-public.kegeosapps.com/")
    assert "X-Amz-Algorithm=AWS4-HMAC-SHA256" in result.url
    assert "X-Amz-Credential=" in result.url
    assert "X-Amz-Signature=" in result.url


@pytest.mark.asyncio
async def test_presigned_put_generated_and_browser_safe():
    client = OmnixClient(
        endpoint="storage.kegeosapps.com:443",
        public_endpoint="https://storage-public.kegeosapps.com",
        access_key="AKIATESAFEKEY0000001",
        secret_key="wJalrXUtnFEMIaKkMDENGbPxRfIcxAmPlEkEyZaB",
        use_ssl=True,
    )

    result = await client.presigned_put_object(
        bucket_name="photo-test",
        object_name="uploads/test.bin",
        expires_in_seconds=1800,
        browser_accessible=True,
    )

    assert result.url.startswith("https://storage-public.kegeosapps.com/")
    assert "X-Amz-Algorithm=AWS4-HMAC-SHA256" in result.url
    assert "X-Amz-Signature=" in result.url


@pytest.mark.asyncio
async def test_guardrail_rejects_internal_host_for_browser_urls():
    client = OmnixClient(
        endpoint="storage.kegeosapps.com:443",
        public_endpoint="http://127.0.0.1:9000",
        access_key="AKIATESAFEKEY0000001",
        secret_key="wJalrXUtnFEMIaKkMDENGbPxRfIcxAmPlEkEyZaB",
        use_ssl=True,
    )

    with pytest.raises(ValueError, match="internal"):
        await client.presigned_get_object(
            bucket_name="photo-test",
            object_name="images/test.jpg",
            expires_in_seconds=3600,
            browser_accessible=True,
        )


@pytest.mark.asyncio
async def test_expiry_validation_matches_dotnet_limits():
    client = OmnixClient(
        endpoint="storage.kegeosapps.com:443",
        public_endpoint="https://storage-public.kegeosapps.com",
        access_key="AKIATESAFEKEY0000001",
        secret_key="wJalrXUtnFEMIaKkMDENGbPxRfIcxAmPlEkEyZaB",
        use_ssl=True,
    )

    with pytest.raises(ValueError, match="604800"):
        await client.presigned_get_object(
            bucket_name="photo-test",
            object_name="images/test.jpg",
            expires_in_seconds=604801,
            browser_accessible=True,
        )


def test_signer_respects_https_and_expiry_limits():
    signer = AwsSignatureV4Signer("AKIAXXX", "SECRET")

    url = signer.generate_presigned_url(
        method="GET",
        host="storage-public.kegeosapps.com",
        path="/photo-test/images%2Ftest.jpg",
        expires_in_seconds=3600,
        use_https=True,
    )

    assert url.startswith("https://storage-public.kegeosapps.com/")

    with pytest.raises(ValueError, match="604800"):
        signer.generate_presigned_url(
            method="GET",
            host="storage-public.kegeosapps.com",
            path="/photo-test/images%2Ftest.jpg",
            expires_in_seconds=604801,
            use_https=True,
        )
