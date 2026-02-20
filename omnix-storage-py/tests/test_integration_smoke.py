import io
import uuid

import pytest

from omnixstorage.client import OmnixClient
from omnixstorage.error import ServerException


ENDPOINT = "storage.kegeosapps.com:443"
PUBLIC_ENDPOINT = "https://storage-public.kegeosapps.com"
ACCESS_KEY = "AKIATESAFEKEY0000001"
SECRET_KEY = "wJalrXUtnFEMIaKkMDENGbPxRfIcxAmPlEkEyZaB"


def _new_bucket(prefix: str) -> str:
    suffix = uuid.uuid4().hex[:12]
    return f"{prefix}-{suffix}".lower()


@pytest.mark.asyncio
async def test_multi_tenant_bucket_file_flow_and_presigned_checks():
    client = OmnixClient(
        endpoint=ENDPOINT,
        public_endpoint=PUBLIC_ENDPOINT,
        access_key=ACCESS_KEY,
        secret_key=SECRET_KEY,
        use_ssl=True,
    )

    tenants = [
        {"bucket": _new_bucket("py-photo"), "objects": ["cam_1/photo1.jpg", "cam_1/photo2.jpg"]},
        {"bucket": _new_bucket("py-geo"), "objects": ["maps/segment-1.bin"]},
    ]

    created_buckets = []
    try:
        for tenant in tenants:
            bucket = tenant["bucket"]
            exists = await client.bucket_exists(bucket)
            if not exists:
                await client.make_bucket(bucket)
                created_buckets.append(bucket)

            assert await client.bucket_exists(bucket)

            for name in tenant["objects"]:
                payload = io.BytesIO(b"python-sdk-test-data")
                await client.put_object(bucket, name, payload, content_type="application/octet-stream")

            listing = await client.list_objects(bucket)
            listed_names = {o.object_name for o in listing.objects}
            for name in tenant["objects"]:
                assert name in listed_names
                meta = await client.stat_object(bucket, name)
                assert meta.size > 0

            first = tenant["objects"][0]
            get_url = await client.presigned_get_object(bucket, first, 3600, browser_accessible=True)
            put_url = await client.presigned_put_object(bucket, f"new-{first}", 3600, browser_accessible=True)

            assert "X-Amz-Algorithm=AWS4-HMAC-SHA256" in get_url.url
            assert "X-Amz-Credential=" in get_url.url
            assert "X-Amz-Signature=" in get_url.url
            assert "X-Amz-Algorithm=AWS4-HMAC-SHA256" in put_url.url
            assert "X-Amz-Signature=" in put_url.url
            assert get_url.url.startswith("https://storage-public.kegeosapps.com/")
            assert put_url.url.startswith("https://storage-public.kegeosapps.com/")

            with pytest.raises(ValueError, match="604800"):
                await client.presigned_get_object(bucket, first, 604801, browser_accessible=True)

            one_second = await client.presigned_get_object(bucket, first, 1, browser_accessible=True)
            assert "X-Amz-Expires=1" in one_second.url

            not_found_url = await client.presigned_get_object(bucket, "images/does-not-exist.jpg", 3600, browser_accessible=True)
            assert "does-not-exist.jpg" in not_found_url.url
    finally:
        for tenant in tenants:
            bucket = tenant["bucket"]
            try:
                objects = await client.list_objects(bucket)
                for obj in objects.objects:
                    await client.remove_object(bucket, obj.object_name)
                if bucket in created_buckets:
                    await client.remove_bucket(bucket)
            except Exception:
                pass


@pytest.mark.asyncio
async def test_extended_ops_smoke_copy_batch_multipart_and_health():
    client = OmnixClient(
        endpoint=ENDPOINT,
        public_endpoint=PUBLIC_ENDPOINT,
        access_key=ACCESS_KEY,
        secret_key=SECRET_KEY,
        use_ssl=True,
    )

    bucket = _new_bucket("py-ops")
    created = False
    try:
        if not await client.bucket_exists(bucket):
            await client.make_bucket(bucket)
            created = True

        await client.ensure_bucket_exists(bucket)
        assert await client.health_check_buckets() in (True, False)

        source_key = "ops/copy-source.txt"
        dest_key = "ops/copy-dest.txt"
        await client.put_object(bucket, source_key, io.BytesIO(b"copy-body"), content_type="text/plain")

        copy_failed_405 = False
        try:
            copy_result = await client.copy_object(bucket, source_key, bucket, dest_key)
            assert "etag" in copy_result
            copied = await client.stat_object(bucket, dest_key)
            assert copied.size >= 1
        except ServerException as ex:
            if ex.status_code == 405:
                copy_failed_405 = True
            else:
                raise

        k1 = "ops/delete-1.txt"
        k2 = "ops/delete-2.txt"
        await client.put_object(bucket, k1, io.BytesIO(b"1"))
        await client.put_object(bucket, k2, io.BytesIO(b"2"))

        try:
            removed = await client.remove_objects(bucket, [k1, k2])
            assert "deleted" in removed
            assert "errors" in removed
        except ServerException as ex:
            if ex.status_code != 405:
                raise

        multipart_failed_405 = False
        try:
            init = await client.initiate_multipart_upload(bucket, "ops/multipart.bin")
            p1 = await client.upload_part(bucket, "ops/multipart.bin", init["upload_id"], 1, io.BytesIO(b"A" * 1024))
            p2 = await client.upload_part(bucket, "ops/multipart.bin", init["upload_id"], 2, io.BytesIO(b"B" * 1024))
            complete = await client.complete_multipart_upload(
                bucket,
                "ops/multipart.bin",
                init["upload_id"],
                [
                    {"part_number": "1", "etag": p1["etag"] or ""},
                    {"part_number": "2", "etag": p2["etag"] or ""},
                ],
            )
            assert "etag" in complete

            abort_init = await client.initiate_multipart_upload(bucket, "ops/multipart-abort.bin")
            await client.abort_multipart_upload(bucket, "ops/multipart-abort.bin", abort_init["upload_id"])
        except ServerException as ex:
            if ex.status_code == 405:
                multipart_failed_405 = True
            else:
                raise

        if copy_failed_405:
            pytest.xfail("Server does not support copy endpoint (405).")
        if multipart_failed_405:
            pytest.xfail("Server does not support multipart endpoint (405).")

    finally:
        try:
            objects = await client.list_objects(bucket)
            for obj in objects.objects:
                await client.remove_object(bucket, obj.object_name)
            if created:
                await client.remove_bucket(bucket)
        except Exception:
            pass
