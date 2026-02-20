# OmnixStorage Python SDK

S3-compatible Python SDK for OmnixStorage with browser-safe presigned URL generation.

## Features

- **Dual-Endpoint Support**: Configure separate internal and public endpoints for optimal security
- **Browser-Safe Presigned URLs**: Generate presigned URLs from public endpoint with SigV4 signing
- **Full S3 Operations**: Bucket management, object CRUD, copy, multipart uploads, batch operations
- **Built-in Guardrails**: Validates presigned URL hosts and prevents internal endpoint leaks
- **Production Ready**: Comprehensive test coverage and parity with .NET SDK v2.0.0

## Installation

```bash
pip install omnix-storage
```

## Quick Start

```python
from omnixstorage import OmnixClient

# Initialize with dual endpoints
client = OmnixClient(
    endpoint="https://internal.omnix.local",
    public_endpoint="https://omnix.example.com",
    access_key="your-access-key",
    secret_key="your-secret-key",
    use_https=True
)

# Upload object
client.put_object("my-bucket", "file.txt", b"Hello World")

# Generate browser-safe presigned GET URL (uses public_endpoint)
url = client.presigned_get_object("my-bucket", "file.txt", expiry_seconds=3600)
print(f"Share this URL: {url}")

# Copy object
client.copy_object("my-bucket", "copy.txt", "my-bucket", "file.txt")

# List objects
objects = client.list_objects("my-bucket")
for obj in objects:
    print(f"{obj['name']} - {obj['size']} bytes")
```

## Configuration

### Dual-Endpoint Pattern

For production deployments requiring browser-accessible presigned URLs:

```python
client = OmnixClient(
    endpoint="https://internal-omnix.corp.local",      # Internal endpoint for backend ops
    public_endpoint="https://omnix.example.com",        # Public endpoint for browser URLs
    access_key="...",
    secret_key="...",
    use_https=True
)
```

### Single-Endpoint Pattern

For development or single-endpoint deployments:

```python
client = OmnixClient(
    endpoint="https://omnix.example.com",
    access_key="...",
    secret_key="...",
    use_https=True
)
```

## API Reference

### Bucket Operations
- `make_bucket(bucket_name)` - Create a bucket
- `bucket_exists(bucket_name)` - Check if bucket exists
- `list_buckets()` - List all buckets
- `remove_bucket(bucket_name)` - Delete a bucket
- `ensure_bucket_exists(bucket_name)` - Idempotent bucket creation

### Object Operations
- `put_object(bucket, object_name, data)` - Upload object
- `get_object(bucket, object_name)` - Download object
- `stat_object(bucket, object_name)` - Get object metadata
- `list_objects(bucket, prefix="", recursive=True)` - List objects
- `remove_object(bucket, object_name)` - Delete single object
- `remove_objects(bucket, object_names)` - Batch delete objects
- `copy_object(dest_bucket, dest_object, src_bucket, src_object)` - Copy object

### Presigned URL Operations
- `presigned_get_object(bucket, object_name, expiry_seconds)` - Generate presigned GET URL
- `presigned_put_object(bucket, object_name, expiry_seconds)` - Generate presigned PUT URL

### Multipart Upload Operations
- `initiate_multipart_upload(bucket, object_name)` - Start multipart upload
- `upload_part(bucket, object_name, upload_id, part_number, data)` - Upload part
- `complete_multipart_upload(bucket, object_name, upload_id, parts)` - Finalize upload
- `abort_multipart_upload(bucket, object_name, upload_id)` - Cancel upload

### Health & Diagnostics
- `health_check_buckets(bucket_names)` - Validate bucket accessibility

## Version History

### v2.0.0 (2026-02-20)
- **Browser-Safe Presigned URLs**: Local AWS Signature V4 signing with public endpoint support
- **Extended Operations**: Copy, batch delete, multipart upload lifecycle
- **Guardrails**: Host validation preventing internal endpoint leaks in presigned URLs
- **Comprehensive Tests**: 6 passing tests with edge-case validation
- **.NET Parity**: Full API compatibility with OmnixStorage .NET SDK v2.0.0

## License

Apache License 2.0

## Support

For issues and questions, please contact Kegeo Solutions Ltd.
