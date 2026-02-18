using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OmnixStorage;

/// <summary>
/// AWS Signature Version 4 authentication implementation for S3-compatible requests.
/// Implements AWS SigV4 specification with presigned URL support.
/// Based on Minio.NET V4Authenticator architecture for compatibility with all S3-compatible storage systems.
/// See: https://docs.aws.amazon.com/general/latest/gr/signature-version-4.html
/// </summary>
internal sealed class AwsSignatureV4Signer
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Service = "s3";
    private const string TerminationString = "aws4_request";
    private const string UnsignedPayload = "UNSIGNED-PAYLOAD";
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static readonly HashSet<string> IgnoredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "user-agent"
    };

    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _region;
    private readonly bool _isSecure;
    private readonly string? _sessionToken;

    public AwsSignatureV4Signer(string accessKey, string secretKey, string region, bool isSecure, string? sessionToken = null)
    {
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        _region = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region.Trim();
        _isSecure = isSecure;
        _sessionToken = sessionToken;
    }

    /// <summary>
    /// Signs an HTTP request with AWS Signature Version 4.
    /// Called for each HTTP request to add the Authorization header with computed signature.
    /// Implementation follows AWS SigV4 signing process:
    /// 1. Compute content hash (for request body)
    /// 2. Set required headers (Host, x-amz-date, x-amz-content-sha256)
    /// 3. Build canonical request (method + URI + query + headers + payload hash)
    /// 4. Hash canonical request
    /// 5. Build string to sign (algorithm + date + scope + canonical request hash)
    /// 6. Generate signing key (multi-level HMAC)
    /// 7. Compute final signature (HMAC of string to sign)
    /// 8. Add Authorization header to request
    /// Based on Minio.NET V4Authenticator implementation.
    /// </summary>
    /// <param name="request">The HttpRequestMessage to sign</param>
    /// <param name="payloadHash">Optional pre-computed SHA256 hash of payload. If null, will use appropriate default.</param>
    public void SignRequest(HttpRequestMessage request, string? payloadHash = null)
    {
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        // Set Host header (must NOT include standard ports 80/443, must include others)
        if (request.RequestUri != null)
        {
            request.Headers.Host = GetCanonicalHost(request.RequestUri);
        }

        // Add x-amz-date header
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        
        // For HTTPS (secure), use UNSIGNED-PAYLOAD per Minio implementation
        // For HTTP, use provided payload hash or the empty payload hash
        var contentHash = _isSecure ? UnsignedPayload : (payloadHash ?? EmptyPayloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", contentHash);

        // Build headers-to-sign from all request headers except ignored headers
        var headersToSign = GetHeadersToSign(request);
        var signedHeaders = string.Join(";", headersToSign.Keys);

        // Build canonical request with proper formatting
        var canonicalRequest = BuildCanonicalRequest(request, headersToSign);

        // Build string to sign
        var credentialScope = $"{dateStamp}/{_region}/{Service}/{TerminationString}";
        var stringToSign = BuildStringToSign(amzDate, credentialScope, canonicalRequest);

        // Calculate signature
        var signingKey = DeriveSigningKey(_secretKey, dateStamp, _region, Service);
        var signature = CalculateSignature(signingKey, stringToSign);

        // Add Authorization header
        var authorization = $"{Algorithm} Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
    }

    /// <summary>
    /// Generates a presigned GET URL for unauthenticated download access.
    /// Presigned URLs embed the SigV4 signature in the query string, allowing
    /// unauthenticated clients (browsers, curl, etc.) to access objects without credentials.
    /// The URL is time-limited and automatically expires after the specified duration.
    /// 
    /// AWS S3 Specification: https://docs.aws.amazon.com/AmazonS3/latest/userguide/ShareObjectPreSignedURL.html
    /// - Expiry range: 1 second to 604800 seconds (7 days)
    /// - Supports temporary credentials (session tokens)
    /// - Uses UNSIGNED-PAYLOAD (no body hash required)
    /// </summary>
    /// <param name="endpoint">Server endpoint (e.g., "s3.amazonaws.com" or "localhost:9000")</param>
    /// <param name="useSSL">Whether to use HTTPS (true) or HTTP (false)</param>
    /// <param name="bucketName">Name of the bucket containing the object</param>
    /// <param name="objectKey">Object key (file path within bucket)</param>
    /// <param name="expiresInSeconds">URL validity period in seconds (1-604800, default: 3600 = 1 hour)</param>
    /// <returns>Presigned URL with embedded signature</returns>
    /// <exception cref="ArgumentOutOfRangeException">When expiresInSeconds is less than 1 or greater than 604800</exception>
    public string GeneratePresignedGetUrl(string endpoint, bool useSSL, string bucketName, string objectKey, int expiresInSeconds = 3600)
    {
        // Validate expiry time per AWS S3 specification (1 second to 7 days)
        if (expiresInSeconds < 1 || expiresInSeconds > 604800)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresInSeconds), 
                "Expiry must be between 1 second and 604800 seconds (7 days) per AWS S3 specification.");
        }

        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");
        var credentialScope = $"{dateStamp}/{_region}/{Service}/{TerminationString}";
        var credential = $"{_accessKey}/{credentialScope}";
        var signedHeaders = "host";

        // Build URL
        var scheme = useSSL ? "https" : "http";
        // CRITICAL: For presigned URLs, the canonical request uses the DECODED path
        // but the final URL uses the ENCODED path. This prevents double-encoding by HTTP clients.
        var decodedPath = $"/{bucketName}/{objectKey}"; // Decoded path for canonical request
        var encodedPath = $"/{bucketName}/{Uri.EscapeDataString(objectKey)}"; // Encoded path for final URL
        var endpointUri = new Uri($"{scheme}://{endpoint}");
        var host = GetCanonicalHost(endpointUri);

        // Build canonical query string (sorted)
        // Per AWS spec, parameters must be in alphabetical order
        var queryParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            { "X-Amz-Algorithm", Algorithm },
            { "X-Amz-Credential", credential },
            { "X-Amz-Date", amzDate },
            { "X-Amz-Expires", expiresInSeconds.ToString() },
            { "X-Amz-SignedHeaders", signedHeaders }
        };

        // Add session token if present (for temporary credentials/STS)
        // Per AWS: https://docs.aws.amazon.com/AmazonS3/latest/userguide/ShareObjectPreSignedURL.html
        if (!string.IsNullOrEmpty(_sessionToken))
        {
            queryParams["X-Amz-Security-Token"] = _sessionToken;
        }

        var canonicalQueryString = string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        // Build canonical request for presigned URL using DECODED path
        var canonicalRequest = BuildCanonicalPresignedRequest("GET", decodedPath, canonicalQueryString, host);

        // Build string to sign
        var stringToSign = BuildStringToSign(amzDate, credentialScope, canonicalRequest);

        // Calculate signature
        var signingKey = DeriveSigningKey(_secretKey, dateStamp, _region, Service);
        var signature = CalculateSignature(signingKey, stringToSign);

        // Build final URL using ENCODED path (for HTTP transmission)
        var authority = endpointUri.IsDefaultPort ? endpointUri.Host : $"{endpointUri.Host}:{endpointUri.Port}";
        return $"{scheme}://{authority}{encodedPath}?{canonicalQueryString}&X-Amz-Signature={signature}";
    }

    /// <summary>
    /// Generates a presigned PUT URL for unauthenticated upload access.
    /// Presigned URLs embed the SigV4 signature in the query string, allowing
    /// unauthenticated clients (web forms, mobile apps) to upload objects without exposing credentials.
    /// The URL is time-limited and automatically expires after the specified duration.
    /// 
    /// AWS S3 Specification: https://docs.aws.amazon.com/AmazonS3/latest/userguide/PresignedUrlUploadObject.html
    /// - Expiry range: 1 second to 604800 seconds (7 days)
    /// - Supports temporary credentials (session tokens)
    /// - Client MUST use PUT method when uploading
    /// - Optional: Specify Content-Type to enforce upload content type
    /// </summary>
    /// <param name="endpoint">Server endpoint (e.g., "s3.amazonaws.com" or "localhost:9000")</param>
    /// <param name="useSSL">Whether to use HTTPS (true) or HTTP (false)</param>
    /// <param name="bucketName">Name of the bucket to upload to</param>
    /// <param name="objectKey">Object key (destination file path within bucket)</param>
    /// <param name="expiresInSeconds">URL validity period in seconds (1-604800, default: 3600 = 1 hour)</param>
    /// <param name="contentType">Optional Content-Type to enforce. If specified, client must use this exact Content-Type when uploading.</param>
    /// <returns>Presigned URL with embedded signature</returns>
    /// <exception cref="ArgumentOutOfRangeException">When expiresInSeconds is less than 1 or greater than 604800</exception>
    public string GeneratePresignedPutUrl(string endpoint, bool useSSL, string bucketName, string objectKey, int expiresInSeconds = 3600, string? contentType = null)
    {
        // Validate expiry time per AWS S3 specification (1 second to 7 days)
        if (expiresInSeconds < 1 || expiresInSeconds > 604800)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresInSeconds), 
                "Expiry must be between 1 second and 604800 seconds (7 days) per AWS S3 specification.");
        }

        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");
        var credentialScope = $"{dateStamp}/{_region}/{Service}/{TerminationString}";
        var credential = $"{_accessKey}/{credentialScope}";
        
        // For PUT with Content-Type, include it in signed headers
        var signedHeaders = !string.IsNullOrEmpty(contentType) ? "content-type;host" : "host";

        // Build URL
        var scheme = useSSL ? "https" : "http";
        // CRITICAL: For presigned URLs, the canonical request uses the DECODED path
        // but the final URL uses the ENCODED path. This prevents double-encoding by HTTP clients.
        var decodedPath = $"/{bucketName}/{objectKey}"; // Decoded path for canonical request
        var encodedPath = $"/{bucketName}/{Uri.EscapeDataString(objectKey)}"; // Encoded path for final URL
        var endpointUri = new Uri($"{scheme}://{endpoint}");
        var host = GetCanonicalHost(endpointUri);

        // Build canonical query string (sorted)
        // Per AWS spec, parameters must be in alphabetical order
        var queryParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            { "X-Amz-Algorithm", Algorithm },
            { "X-Amz-Credential", credential },
            { "X-Amz-Date", amzDate },
            { "X-Amz-Expires", expiresInSeconds.ToString() },
            { "X-Amz-SignedHeaders", signedHeaders }
        };

        // Add session token if present (for temporary credentials/STS)
        // Per AWS: https://docs.aws.amazon.com/AmazonS3/latest/userguide/PresignedUrlUploadObject.html
        if (!string.IsNullOrEmpty(_sessionToken))
        {
            queryParams["X-Amz-Security-Token"] = _sessionToken;
        }

        var canonicalQueryString = string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        // Build canonical request for presigned URL using DECODED path
        // If Content-Type specified, include it in the canonical request
        var canonicalRequest = !string.IsNullOrEmpty(contentType)
            ? BuildCanonicalPresignedRequestWithContentType("PUT", decodedPath, canonicalQueryString, host, contentType)
            : BuildCanonicalPresignedRequest("PUT", decodedPath, canonicalQueryString, host);

        // Build string to sign
        var stringToSign = BuildStringToSign(amzDate, credentialScope, canonicalRequest);

        // Calculate signature
        var signingKey = DeriveSigningKey(_secretKey, dateStamp, _region, Service);
        var signature = CalculateSignature(signingKey, stringToSign);

        // Build final URL using ENCODED path (for HTTP transmission)
        var authority = endpointUri.IsDefaultPort ? endpointUri.Host : $"{endpointUri.Host}:{endpointUri.Port}";
        return $"{scheme}://{authority}{encodedPath}?{canonicalQueryString}&X-Amz-Signature={signature}";
    }

    private string BuildCanonicalRequest(HttpRequestMessage request, SortedDictionary<string, string> headersToSign)
    {
        var sb = new StringBuilder();

        // Line 1: HTTP Method (uppercase)
        sb.Append(request.Method.Method.ToUpperInvariant());
        sb.Append('\n');

        // Line 2: Canonical URI (AbsolutePath)
        var path = request.RequestUri?.AbsolutePath ?? "/";
        sb.Append(CanonicalizeUri(path));
        sb.Append('\n');

        // Line 3: Canonical Query String (empty for header-based auth)
        var query = request.RequestUri?.Query;
        sb.Append(CanonicalizeQueryString(query));
        sb.Append('\n');

        // Lines 4+: Canonical Headers (sorted, lowercase keys with colon-delimited values)
        foreach (var header in headersToSign)
        {
            sb.Append($"{header.Key}:{header.Value}");
            sb.Append('\n');
        }

        // Blank line
        sb.Append('\n');

        // Signed headers (semicolon-delimited, lowercase, sorted)
        sb.Append(string.Join(";", headersToSign.Keys));
        sb.Append('\n');

        // Payload Hash
        if (headersToSign.TryGetValue("x-amz-content-sha256", out var payloadHash))
        {
            sb.Append(payloadHash);
        }
        else
        {
            sb.Append(EmptyPayloadHash);
        }

        return sb.ToString();
    }

    private string BuildCanonicalPresignedRequest(string method, string path, string canonicalQueryString, string host)
    {
        var sb = new StringBuilder();

        // HTTP Method
        sb.Append(method);
        sb.Append('\n');

        // Canonical URI
        sb.Append(path);
        sb.Append('\n');

        // Canonical Query String (without signature)
        sb.Append(canonicalQueryString);
        sb.Append('\n');

        // Canonical Headers
        sb.Append($"host:{host}");
        sb.Append('\n');
        sb.Append('\n');

        // Signed Headers
        sb.Append("host");
        sb.Append('\n');

        // Payload Hash (UNSIGNED for presigned URLs)
        sb.Append(UnsignedPayload);

        return sb.ToString();
    }

    /// <summary>
    /// Builds canonical request for presigned URL with Content-Type header.
    /// Per AWS S3 spec, when Content-Type is specified in presigned URL, 
    /// it must be included in the canonical request and the client must use the same Content-Type.
    /// See: https://docs.aws.amazon.com/AmazonS3/latest/userguide/PresignedUrlUploadObject.html
    /// </summary>
    private string BuildCanonicalPresignedRequestWithContentType(string method, string path, 
        string canonicalQueryString, string host, string contentType)
    {
        var sb = new StringBuilder();

        // HTTP Method
        sb.Append(method);
        sb.Append('\n');

        // Canonical URI
        sb.Append(path);
        sb.Append('\n');

        // Canonical Query String (without signature)
        sb.Append(canonicalQueryString);
        sb.Append('\n');

        // Canonical Headers (sorted alphabetically: content-type, then host)
        sb.Append($"content-type:{contentType}");
        sb.Append('\n');
        sb.Append($"host:{host}");
        sb.Append('\n');
        sb.Append('\n');

        // Signed Headers (semicolon-delimited, sorted)
        sb.Append("content-type;host");
        sb.Append('\n');

        // Payload Hash (UNSIGNED for presigned URLs)
        sb.Append(UnsignedPayload);

        return sb.ToString();
    }

    private static string BuildStringToSign(string amzDate, string credentialScope, string canonicalRequest)
    {
        var sb = new StringBuilder();
        sb.Append(Algorithm);
        sb.Append('\n');
        sb.Append(amzDate);
        sb.Append('\n');
        sb.Append(credentialScope);
        sb.Append('\n');
        sb.Append(HashString(canonicalRequest));
        return sb.ToString();
    }

    /// <summary>
    /// Derives the signing key using AWS Signature V4 multi-level HMAC chain.
    /// Implements the 4-level HMAC derivation as per AWS specification:
    /// 1. HMAC-SHA256(AWS4+secretKey, date)
    /// 2. HMAC-SHA256(result, region)
    /// 3. HMAC-SHA256(result, service)
    /// 4. HMAC-SHA256(result, "aws4_request")
    /// Based on Minio.NET implementation for optimal performance.
    /// </summary>
    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region, string service)
    {
#if NETSTANDARD
        // .NET Standard compatibility path
        var kSecret = Encoding.UTF8.GetBytes($"AWS4{secretKey}");
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        var kSigning = HmacSha256(kService, TerminationString);
        return kSigning;
#else
        // Modern .NET path with ReadOnlySpan<byte> for zero-copy operations
        ReadOnlySpan<byte> kSecret = Encoding.UTF8.GetBytes($"AWS4{secretKey}");
        ReadOnlySpan<byte> dateByes = Encoding.UTF8.GetBytes(dateStamp);
        var kDate = SignHmac(kSecret, dateByes);
        
        ReadOnlySpan<byte> regionBytes = Encoding.UTF8.GetBytes(region);
        var kRegion = SignHmac(kDate, regionBytes);
        
        ReadOnlySpan<byte> serviceBytes = Encoding.UTF8.GetBytes(service);
        var kService = SignHmac(kRegion, serviceBytes);
        
        ReadOnlySpan<byte> requestBytes = Encoding.UTF8.GetBytes(TerminationString);
        return SignHmac(kService, requestBytes).ToArray();
#endif
    }

    private static string CalculateSignature(byte[] signingKey, string stringToSign)
    {
#if NETSTANDARD
        var signatureBytes = HmacSha256(signingKey, stringToSign);
        return Convert.ToHexString(signatureBytes).ToLowerInvariant();
#else
        ReadOnlySpan<byte> stringToSignBytes = Encoding.UTF8.GetBytes(stringToSign);
        var signatureBytes = SignHmac(signingKey, stringToSignBytes);
        return BytesToHex(signatureBytes);
#endif
    }

#if NETSTANDARD
    /// <summary>
    /// HMAC-SHA256 implementation for .NET Standard.
    /// </summary>
    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
#else
    /// <summary>
    /// HMAC-SHA256 implementation for modern .NET using ReadOnlySpan for efficiency.
    /// Based on Minio.NET V4Authenticator pattern.
    /// </summary>
    private static ReadOnlySpan<byte> SignHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> content)
    {
        return HMACSHA256.HashData(key, content);
    }
    
    /// <summary>
    /// Converts bytes to hexadecimal string (lowercase).
    /// AWS SigV4 requires hex encoding of hashes.
    /// </summary>
    private static string BytesToHex(ReadOnlySpan<byte> checkSum)
    {
        return Convert.ToHexString(checkSum).ToLowerInvariant();
    }
#endif

    private static string HashString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CanonicalizeUri(string path)
    {
        // Per AWS SigV4 spec: use the absolute path without modification
        // The path from URI is already properly encoded
        if (string.IsNullOrEmpty(path)) 
            return "/";
        
        // Return path as-is (already URL-encoded from HttpRequestUri.AbsolutePath)
        return path;
    }

    private static string CanonicalizeQueryString(string? query)
    {
        if (string.IsNullOrEmpty(query)) return "";
        
        query = query.TrimStart('?');
        if (string.IsNullOrEmpty(query)) return "";

        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p =>
            {
                var idx = p.IndexOf('=');
                if (idx < 0) return (Key: Uri.EscapeDataString(p), Value: "");
                return (Key: Uri.EscapeDataString(p.Substring(0, idx)), 
                        Value: Uri.EscapeDataString(p.Substring(idx + 1)));
            })
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ThenBy(x => x.Value, StringComparer.Ordinal);

        return string.Join("&", pairs.Select(p => $"{p.Key}={p.Value}"));
    }

    private static string GetCanonicalHost(Uri uri)
    {
        return uri.Port == 80 || uri.Port == 443
            ? uri.Host
            : $"{uri.Host}:{uri.Port}";
    }

    private static SortedDictionary<string, string> GetHeadersToSign(HttpRequestMessage request)
    {
        var headersToSign = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var header in request.Headers)
        {
            var headerName = header.Key.ToLowerInvariant();
            if (IgnoredHeaders.Contains(headerName))
                continue;

            var headerValue = string.Join(",", header.Value.Select(NormalizeHeaderValue));
            headersToSign[headerName] = headerValue;
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                var headerName = header.Key.ToLowerInvariant();
                if (IgnoredHeaders.Contains(headerName))
                    continue;

                var headerValue = string.Join(",", header.Value.Select(NormalizeHeaderValue));
                headersToSign[headerName] = headerValue;
            }
        }

        return headersToSign;
    }

    private static string NormalizeHeaderValue(string value)
    {
        var trimmed = value.Trim();
        return Regex.Replace(trimmed, "\\s+", " ");
    }

    /// <summary>
    /// Computes SHA256 hash of stream content for payload signing.
    /// Required for AWS SigV4 authentication to verify request integrity.
    /// Handles stream position reset to allow content reuse.
    /// </summary>
    /// <param name="content">Stream to hash (will be reset to original position if seekable)</param>
    /// <returns>Lowercase hexadecimal SHA256 hash</returns>
    public static async Task<string> ComputePayloadHashAsync(Stream content)
    {
        if (content == null || !content.CanRead)
            return HashString("");

        var position = content.CanSeek ? content.Position : 0;
        var hash = await SHA256.HashDataAsync(content);
        
        if (content.CanSeek)
            content.Position = position;

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash of string content for payload signing (synchronous).
    /// Required for AWS SigV4 authentication when payload is a string.
    /// </summary>
    /// <param name="content">String content to hash</param>
    /// <returns>Lowercase hexadecimal SHA256 hash</returns>
    public static string ComputePayloadHashSync(string content)
    {
        return HashString(content ?? "");
    }
}
