namespace OmnixStorage;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using OmnixStorage.Args;
using OmnixStorage.DataModel;

/// <summary>
/// Builder for creating OmnixStorageClient instances with fluent configuration.
/// Provides a clean, type-safe way to configure the S3-compatible client.
/// </summary>
/// <example>
/// <code>
/// var client = new OmnixStorageClientBuilder()
///     .WithEndpoint("s3.example.com:9000")
///     .WithCredentials("ACCESS_KEY", "SECRET_KEY")
///     .WithSSL(true)
///     .WithRegion("us-east-1")
///     .Build();
/// </code>
/// </example>
public class OmnixStorageClientBuilder
{
    private string _endpoint = "localhost:9000";
    private string _accessKey = string.Empty;
    private string _secretKey = string.Empty;
    private string? _adminUsername = null;
    private string? _adminPassword = null;
    private string? _adminToken = null;
    private bool _useSSL = false;
    private string _region = "us-east-1";
    private string? _sessionToken = null;
    private int _requestTimeoutMs = 30000;
    private int _maxRetries = 3;

    /// <summary>
    /// Sets the endpoint (server address:port).
    /// </summary>
    public OmnixStorageClientBuilder WithEndpoint(string endpoint)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        return this;
    }

    /// <summary>
    /// Sets the credentials.
    /// </summary>
    public OmnixStorageClientBuilder WithCredentials(string accessKey, string secretKey)
    {
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        return this;
    }

    /// <summary>
    /// Sets admin credentials used for admin endpoints (JWT login).
    /// These are NOT S3 access keys and should be console/admin credentials.
    /// </summary>
    public OmnixStorageClientBuilder WithAdminCredentials(string username, string password)
    {
        _adminUsername = username ?? throw new ArgumentNullException(nameof(username));
        _adminPassword = password ?? throw new ArgumentNullException(nameof(password));
        return this;
    }

    /// <summary>
    /// Sets an admin JWT token to use for admin endpoints.
    /// If provided, the client will not call the login endpoint.
    /// </summary>
    public OmnixStorageClientBuilder WithAdminToken(string token)
    {
        _adminToken = token ?? throw new ArgumentNullException(nameof(token));
        return this;
    }

    /// <summary>
    /// Enables SSL/TLS (default: false).
    /// </summary>
    public OmnixStorageClientBuilder WithSSL(bool useSSL = true)
    {
        _useSSL = useSSL;
        return this;
    }

    /// <summary>
    /// Sets the session token for temporary credentials (IAM roles, STS).
    /// Required when using temporary security credentials.
    /// See: https://docs.aws.amazon.com/AmazonS3/latest/userguide/RESTAuthentication.html
    /// </summary>
    /// <param name="sessionToken">AWS session token (X-Amz-Security-Token)</param>
    public OmnixStorageClientBuilder WithSessionToken(string sessionToken)
    {
        _sessionToken = sessionToken;
        return this;
    }

    /// <summary>
    /// Sets the region for request signing (default: us-east-1).
    /// </summary>
    public OmnixStorageClientBuilder WithRegion(string region)
    {
        _region = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region.Trim();
        return this;
    }

    /// <summary>
    /// Sets the request timeout.
    /// </summary>
    public OmnixStorageClientBuilder WithRequestTimeout(TimeSpan timeout)
    {
        _requestTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retries for failed requests.
    /// </summary>
    public OmnixStorageClientBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Builds and returns the OmnixStorageClient.
    /// </summary>
    public IOmnixStorageClient Build()
    {
        if (string.IsNullOrEmpty(_accessKey))
            throw new InvalidOperationException("Access key is required. Use WithCredentials() to set it.");
        if (string.IsNullOrEmpty(_secretKey))
            throw new InvalidOperationException("Secret key is required. Use WithCredentials() to set it.");

        return new OmnixStorageClient(
            _endpoint,
            _accessKey,
            _secretKey,
            _adminUsername,
            _adminPassword,
            _adminToken,
            _useSSL,
            _region,
            _sessionToken,
            _requestTimeoutMs,
            _maxRetries);
    }
}

/// <summary>
/// S3-compatible client for OmnixStorage distributed object storage.
/// Uses AWS Signature Version 4 for authentication, compatible with all S3-compatible storage systems.
/// 
/// Key Features:
/// - Full S3 API compatibility (buckets, objects, presigned URLs)
/// - AWS SigV4 authentication (industry standard)
/// - Async/await support throughout
/// - Automatic request retry with exponential backoff
/// - Presigned URL generation for GET/PUT operations
/// - Multi-tenant support with admin operations
/// 
/// Architecture:
/// This client uses direct AWS SigV4 signing for S3 operations (stateless, no JWT required).
/// Admin operations use separate authentication for multi-tenant management.
/// Based on analysis of Minio.NET SDK for optimal S3 compatibility.
/// </summary>
/// <example>
/// <code>
/// // Initialize client
/// var client = new OmnixStorageClientBuilder()
///     .WithEndpoint("storage.example.com:443")
///     .WithCredentials("ACCESS_KEY", "SECRET_KEY")
///     .WithSSL(true)
///     .Build();
/// 
/// // Upload an object
/// using var stream = File.OpenRead("photo.jpg");
/// await client.PutObjectAsync("my-bucket", "photo.jpg", stream);
/// 
/// // Generate presigned download URL
/// var url = await client.PresignedGetObjectAsync(
///     new PresignedGetObjectArgs()
///         .WithBucket("my-bucket")
///         .WithObject("photo.jpg")
///         .WithExpiry(3600)); // 1 hour
/// </code>
/// </example>
public class OmnixStorageClient : IOmnixStorageClient
{
    private readonly string _endpoint;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string? _adminUsername;
    private readonly string? _adminPassword;
    private string? _adminToken;
    private readonly bool _useSSL;
    private readonly string _region;
    private readonly string? _sessionToken;
    private readonly int _requestTimeoutMs;
    private readonly int _maxRetries;
    private readonly HttpClient _httpClient;
    private readonly AwsSignatureV4Signer _signer;

    /// <summary>
    /// Initializes a new instance of the OmnixStorageClient.
    /// </summary>
    internal OmnixStorageClient(
        string endpoint,
        string accessKey,
        string secretKey,
        string? adminUsername,
        string? adminPassword,
        string? adminToken,
        bool useSSL,
        string region,
        string? sessionToken,
        int requestTimeoutMs,
        int maxRetries)
    {
        _endpoint = endpoint;
        _accessKey = accessKey;
        _secretKey = secretKey;
        _adminUsername = adminUsername;
        _adminPassword = adminPassword;
        _adminToken = adminToken;
        _useSSL = useSSL;
        _region = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region.Trim();
        _sessionToken = sessionToken;
        _requestTimeoutMs = requestTimeoutMs;
        _maxRetries = maxRetries;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(requestTimeoutMs)
        };

        _signer = new AwsSignatureV4Signer(_accessKey, _secretKey, _region, _useSSL, _sessionToken);
    }

    /// <summary>
    /// Gets the base URL for API requests.
    /// </summary>
    private string GetBaseUrl() => $"{(_useSSL ? "https" : "http")}://{_endpoint}";

    /// <summary>
    /// Makes an HTTP request with AWS SigV4 authentication and retry logic.
    /// Implements exponential backoff for transient failures.
    /// </summary>
    /// <param name="method">HTTP method (GET, PUT, POST, DELETE, HEAD)</param>
    /// <param name="url">Full URL to request</param>
    /// <param name="content">Optional request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="payloadHash">Optional pre-computed SHA256 hash of payload</param>
    /// <param name="extraHeaders">Optional additional headers to include (e.g., x-amz-copy-source)</param>
    /// <returns>HTTP response message</returns>
    /// <exception cref="ServerException">Thrown when request fails after all retries</exception>
    private async Task<HttpResponseMessage> MakeRequestAsync(
        HttpMethod method,
        string url,
        HttpContent? content = null,
        CancellationToken cancellationToken = default,
        string? payloadHash = null,
        Dictionary<string, string>? extraHeaders = null)
    {
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(method, url)
                {
                    Content = content
                };

                if (extraHeaders != null)
                {
                    foreach (var header in extraHeaders)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                // Compute payload hash if content is provided and hash not specified
                string contentHash;
                if (payloadHash != null)
                {
                    contentHash = payloadHash;
                }
                else if (content != null)
                {
                    // For stream content, we need to compute the hash
                    if (content is StreamContent streamContent)
                    {
                        var stream = await streamContent.ReadAsStreamAsync(cancellationToken);
                        var originalPosition = stream.CanSeek ? stream.Position : 0;
                        contentHash = await AwsSignatureV4Signer.ComputePayloadHashAsync(stream);
                        if (stream.CanSeek)
                        {
                            stream.Position = originalPosition;
                        }
                    }
                    else
                    {
                        // For other content types, read as stream
                        var stream = await content.ReadAsStreamAsync(cancellationToken);
                        var originalPosition = stream.CanSeek ? stream.Position : 0;
                        contentHash = await AwsSignatureV4Signer.ComputePayloadHashAsync(stream);
                        if (stream.CanSeek)
                        {
                            stream.Position = originalPosition;
                        }
                    }
                }
                else
                {
                    // For empty payload, use SHA256 hash of empty string
                    contentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
                }

                // Sign the request with AWS SigV4
                _signer.SignRequest(request, contentHash);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                return response;
            }
            catch (HttpRequestException ex) when (attempt < _maxRetries - 1)
            {
                lastException = ex;
                // Exponential backoff: 1s, 2s, 4s, max 10s
                var delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt), 10000);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < _maxRetries - 1)
            {
                // Timeout, not user cancellation - retry
                lastException = ex;
                var delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt), 10000);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        var errorMessage = $"Request to {url} failed after {_maxRetries} attempts. " +
                          $"Last error: {lastException?.Message ?? "Unknown error"}";
        
        throw lastException != null 
            ? new ServerException(errorMessage, 0, lastException)
            : new ServerException(errorMessage, 0);
    }

    private async Task<HttpResponseMessage> MakeAdminRequestAsync(
        HttpMethod method,
        string url,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                var token = await EnsureAdminTokenAsync(cancellationToken);

                var request = new HttpRequestMessage(method, url)
                {
                    Content = content
                };

                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                return response;
            }
            catch (HttpRequestException ex) when (attempt < _maxRetries - 1)
            {
                lastException = ex;
                var delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt), 10000);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < _maxRetries - 1)
            {
                lastException = ex;
                var delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt), 10000);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        var errorMessage = $"Admin request to {url} failed after {_maxRetries} attempts. " +
                          $"Last error: {lastException?.Message ?? "Unknown error"}";

        throw lastException != null
            ? new ServerException(errorMessage, 0, lastException)
            : new ServerException(errorMessage, 0);
    }

    private async Task<string> EnsureAdminTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_adminToken))
        {
            return _adminToken!;
        }

        if (string.IsNullOrWhiteSpace(_adminUsername) || string.IsNullOrWhiteSpace(_adminPassword))
        {
            throw new InvalidOperationException("Admin credentials or token are required for admin endpoints. Use WithAdminToken() or WithAdminCredentials().");
        }

        var loginUrl = $"{GetBaseUrl()}/api/admin/auth/login";
        var payload = JsonSerializer.Serialize(new
        {
            username = _adminUsername,
            password = _adminPassword
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(loginUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to authenticate admin user. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        string? token = null;
        if (root.TryGetProperty("token", out var tokenProp))
            token = tokenProp.GetString();
        else if (root.TryGetProperty("accessToken", out var accessTokenProp))
            token = accessTokenProp.GetString();
        else if (root.TryGetProperty("jwt", out var jwtProp))
            token = jwtProp.GetString();
        else if (root.TryGetProperty("access_token", out var accessTokenSnakeProp))
            token = accessTokenSnakeProp.GetString();

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ServerException("Admin login response did not contain a token.", (int)response.StatusCode);
        }

        _adminToken = token;
        return token;
    }

    #region Bucket Operations

    /// <inheritdoc/>
    public async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        var url = $"{GetBaseUrl()}/{bucketName}";
        var response = await MakeRequestAsync(HttpMethod.Head, url, cancellationToken: cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc/>
    public async Task MakeBucketAsync(MakeBucketArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}";
        var response = await MakeRequestAsync(HttpMethod.Put, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine($"Failed to create bucket '{args.BucketName}'.");
            errorMessage.AppendLine($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            if (!string.IsNullOrEmpty(errorBody))
            {
                errorMessage.AppendLine($"Server Response: {errorBody}");
            }
            throw new ServerException(
                errorMessage.ToString(),
                (int)response.StatusCode);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveBucketAsync(RemoveBucketArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}";
        var response = await MakeRequestAsync(HttpMethod.Delete, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ServerException(
                $"Failed to remove bucket '{args.BucketName}'.",
                (int)response.StatusCode);
        }
    }

    /// <inheritdoc/>
    public async Task<List<Bucket>> ListBucketsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{GetBaseUrl()}/";
        var response = await MakeRequestAsync(HttpMethod.Get, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ServerException("Failed to list buckets.", (int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var buckets = new List<Bucket>();

        try
        {
            var doc = XDocument.Parse(content);
            var bucketElements = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("Bucket", StringComparison.OrdinalIgnoreCase));

            foreach (var bucketElement in bucketElements)
            {
                var name = bucketElement.Elements()
                    .FirstOrDefault(e => e.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? string.Empty;

                var createdAtValue = bucketElement.Elements()
                    .FirstOrDefault(e => e.Name.LocalName.Equals("CreationDate", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                var createdAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(createdAtValue) &&
                    DateTime.TryParse(createdAtValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    createdAt = parsed;
                }

                buckets.Add(new Bucket
                {
                    Name = name,
                    CreationDate = createdAt
                });
            }
        }
        catch (Exception ex)
        {
            throw new ServerException($"Failed to parse bucket list. {ex.Message}", (int)response.StatusCode);
        }

        return buckets;
    }

    #endregion

    #region Object Operations

    /// <inheritdoc/>
    public async Task<PutObjectResult> PutObjectAsync(PutObjectArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}";
        var content = new StreamContent(args.Data!);

        if (args.Data!.CanSeek)
        {
            var remaining = args.Data.Length - args.Data.Position;
            if (remaining >= 0)
            {
                content.Headers.ContentLength = remaining;
            }
        }

        if (!string.IsNullOrEmpty(args.ContentType))
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(args.ContentType);
        }

        // Add metadata as custom headers
        foreach (var metadata in args.Metadata)
        {
            content.Headers.Add($"x-amz-meta-{metadata.Key}", metadata.Value);
        }

        var response = await MakeRequestAsync(HttpMethod.Put, url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorDetails = string.IsNullOrWhiteSpace(errorBody) ? "<empty response>" : errorBody;
            throw new ServerException(
                $"Failed to upload object '{args.ObjectName}' to bucket '{args.BucketName}'. " +
                $"Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorDetails}",
                (int)response.StatusCode);
        }

        var etag = response.Headers.ETag?.Tag ?? "unknown";

        return new PutObjectResult
        {
            BucketName = args.BucketName,
            ObjectName = args.ObjectName,
            ETag = etag
        };
    }

    /// <inheritdoc/>
    public async Task<ObjectMetadata> GetObjectAsync(GetObjectArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}";
        var response = await MakeRequestAsync(HttpMethod.Get, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new ObjectNotFoundException(args.BucketName, args.ObjectName);

            throw new ServerException(
                $"Failed to download object '{args.ObjectName}' from bucket '{args.BucketName}'.",
                (int)response.StatusCode);
        }

        if (args.OutputStream != null)
        {
            await response.Content.CopyToAsync(args.OutputStream, cancellationToken);
        }

        return new ObjectMetadata
        {
            Name = args.ObjectName,
            BucketName = args.BucketName,
            Size = response.Content.Headers.ContentLength ?? 0,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            ETag = response.Headers.ETag?.Tag ?? "unknown"
        };
    }

    /// <inheritdoc/>
    public async Task<ObjectMetadata> StatObjectAsync(StatObjectArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}";
        var response = await MakeRequestAsync(HttpMethod.Head, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new ObjectNotFoundException(args.BucketName, args.ObjectName);

            throw new ServerException(
                $"Failed to stat object '{args.ObjectName}' in bucket '{args.BucketName}'.",
                (int)response.StatusCode);
        }

        return new ObjectMetadata
        {
            Name = args.ObjectName,
            BucketName = args.BucketName,
            Size = response.Content.Headers.ContentLength ?? 0,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            ETag = response.Headers.ETag?.Tag ?? "unknown"
        };
    }

    /// <inheritdoc/>
    public async Task RemoveObjectAsync(RemoveObjectArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}";
        var response = await MakeRequestAsync(HttpMethod.Delete, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new ServerException(
                $"Failed to remove object '{args.ObjectName}' from bucket '{args.BucketName}'.",
                (int)response.StatusCode);
        }
    }

    /// <inheritdoc/>
    public async Task<CopyObjectResult> CopyObjectAsync(CopyObjectArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.DestinationBucketName}/{args.DestinationObjectName}";
        var copySource = $"/{args.SourceBucketName}/{Uri.EscapeDataString(args.SourceObjectName)}";

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-amz-copy-source"] = copySource
        };

        if (args.Metadata.Count > 0)
        {
            headers["x-amz-metadata-directive"] = "REPLACE";
            foreach (var metadata in args.Metadata)
            {
                headers[$"x-amz-meta-{metadata.Key}"] = metadata.Value;
            }
        }
        else
        {
            headers["x-amz-metadata-directive"] = "COPY";
        }

        foreach (var condition in args.CopyConditions)
        {
            headers[condition.Key] = condition.Value;
        }

        var response = await MakeRequestAsync(HttpMethod.Put, url, cancellationToken: cancellationToken, extraHeaders: headers);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to copy object '{args.SourceObjectName}' to '{args.DestinationObjectName}'. " +
                $"Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = new CopyObjectResult();

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var doc = XDocument.Parse(body);
                var lastModified = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("LastModified", StringComparison.OrdinalIgnoreCase))?.Value;
                var etag = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("ETag", StringComparison.OrdinalIgnoreCase))?.Value;

                if (!string.IsNullOrWhiteSpace(lastModified) && DateTime.TryParse(lastModified, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    result.LastModified = parsed;
                }

                if (!string.IsNullOrWhiteSpace(etag))
                {
                    result.ETag = etag.Trim('"');
                }
            }
            catch
            {
                result.ETag = response.Headers.ETag?.Tag?.Trim('"');
            }
        }
        else
        {
            result.ETag = response.Headers.ETag?.Tag?.Trim('"');
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<RemoveObjectsResult> RemoveObjectsAsync(RemoveObjectsArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}?delete";

        var deleteXml = new XDocument(
            new XElement("Delete",
                args.ObjectNames.Select(name =>
                    new XElement("Object",
                        new XElement("Key", name)))))
            .ToString(SaveOptions.DisableFormatting);

        var content = new StringContent(deleteXml, Encoding.UTF8, "application/xml");
        var response = await MakeRequestAsync(HttpMethod.Post, url, content, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to remove objects from bucket '{args.BucketName}'. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = new RemoveObjectsResult();

        if (string.IsNullOrWhiteSpace(body))
        {
            result.Deleted.AddRange(args.ObjectNames);
            return result;
        }

        try
        {
            var doc = XDocument.Parse(body);
            var deletedElements = doc.Descendants().Where(e => e.Name.LocalName.Equals("Deleted", StringComparison.OrdinalIgnoreCase));
            foreach (var deleted in deletedElements)
            {
                var key = deleted.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Key", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    result.Deleted.Add(key);
                }
            }

            var errorElements = doc.Descendants().Where(e => e.Name.LocalName.Equals("Error", StringComparison.OrdinalIgnoreCase));
            foreach (var error in errorElements)
            {
                result.Errors.Add(new RemoveObjectError
                {
                    Key = error.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Key", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                    Code = error.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Code", StringComparison.OrdinalIgnoreCase))?.Value,
                    Message = error.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Message", StringComparison.OrdinalIgnoreCase))?.Value
                });
            }
        }
        catch
        {
            result.Deleted.AddRange(args.ObjectNames);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<InitiateMultipartUploadResult> InitiateMultipartUploadAsync(InitiateMultipartUploadArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}?uploads";
        var content = new ByteArrayContent(Array.Empty<byte>());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(args.ContentType);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in args.Metadata)
        {
            headers[$"x-amz-meta-{metadata.Key}"] = metadata.Value;
        }

        var response = await MakeRequestAsync(HttpMethod.Post, url, content, cancellationToken: cancellationToken, extraHeaders: headers);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to initiate multipart upload for '{args.ObjectName}'. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ServerException("Multipart upload initiation did not return an upload ID.", (int)response.StatusCode);
        }

        try
        {
            var doc = XDocument.Parse(body);
            var uploadId = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("UploadId", StringComparison.OrdinalIgnoreCase))?.Value;

            if (string.IsNullOrWhiteSpace(uploadId))
            {
                throw new ServerException("Multipart upload initiation did not return an upload ID.", (int)response.StatusCode);
            }

            return new InitiateMultipartUploadResult { UploadId = uploadId };
        }
        catch (Exception ex) when (ex is not ServerException)
        {
            throw new ServerException($"Failed to parse multipart upload initiation response. {ex.Message}", (int)response.StatusCode);
        }
    }

    /// <inheritdoc/>
    public async Task<UploadPartResult> UploadPartAsync(UploadPartArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var queryString = $"?partNumber={args.PartNumber}&uploadId={Uri.EscapeDataString(args.UploadId)}";
        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}{queryString}";
        var content = new StreamContent(args.Data!);

        if (args.Data!.CanSeek)
        {
            var remaining = args.Data.Length - args.Data.Position;
            if (remaining >= 0)
            {
                content.Headers.ContentLength = remaining;
            }
        }

        var response = await MakeRequestAsync(HttpMethod.Put, url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to upload part {args.PartNumber} for '{args.ObjectName}'. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var etag = response.Headers.ETag?.Tag ?? "unknown";

        return new UploadPartResult
        {
            PartNumber = args.PartNumber,
            ETag = etag.Trim('"')
        };
    }

    /// <inheritdoc/>
    public async Task<CompleteMultipartUploadResult> CompleteMultipartUploadAsync(CompleteMultipartUploadArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var queryString = $"?uploadId={Uri.EscapeDataString(args.UploadId)}";
        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}{queryString}";

        var xml = new XDocument(
            new XElement("CompleteMultipartUpload",
                args.Parts.Select(part =>
                    new XElement("Part",
                        new XElement("PartNumber", part.PartNumber),
                        new XElement("ETag", EnsureQuotedEtag(part.ETag))))))
            .ToString(SaveOptions.DisableFormatting);

        var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        var response = await MakeRequestAsync(HttpMethod.Post, url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to complete multipart upload for '{args.ObjectName}'. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = new CompleteMultipartUploadResult();

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var doc = XDocument.Parse(body);
                result.ETag = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("ETag", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('"');
                result.Location = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Location", StringComparison.OrdinalIgnoreCase))?.Value;
            }
            catch
            {
                result.ETag = response.Headers.ETag?.Tag?.Trim('"');
            }
        }
        else
        {
            result.ETag = response.Headers.ETag?.Tag?.Trim('"');
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task AbortMultipartUploadAsync(AbortMultipartUploadArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var queryString = $"?uploadId={Uri.EscapeDataString(args.UploadId)}";
        var url = $"{GetBaseUrl()}/{args.BucketName}/{args.ObjectName}{queryString}";
        var response = await MakeRequestAsync(HttpMethod.Delete, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to abort multipart upload for '{args.ObjectName}'. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }
    }

    /// <inheritdoc/>
    public async Task<ListObjectsResult> ListObjectsAsync(ListObjectsArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(args.Prefix))
            queryParams.Add($"prefix={Uri.EscapeDataString(args.Prefix)}");
        if (!string.IsNullOrEmpty(args.Delimiter))
            queryParams.Add($"delimiter={Uri.EscapeDataString(args.Delimiter)}");
        if (args.MaxKeys > 0)
            queryParams.Add($"max-keys={args.MaxKeys}");
        if (!string.IsNullOrEmpty(args.ContinuationToken))
            queryParams.Add($"continuation-token={Uri.EscapeDataString(args.ContinuationToken)}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        var url = $"{GetBaseUrl()}/{args.BucketName}{queryString}";

        var response = await MakeRequestAsync(HttpMethod.Get, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ServerException(
                $"Failed to list objects in bucket '{args.BucketName}'.",
                (int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var result = new ListObjectsResult { Objects = new List<ObjectMetadata>() };

        // Server returns PascalCase: Contents (not contents)
        if (doc.RootElement.TryGetProperty("Contents", out var contentsElement) ||
            doc.RootElement.TryGetProperty("contents", out contentsElement))
        {
            foreach (var item in contentsElement.EnumerateArray())
            {
                var obj = new ObjectMetadata
                {
                    // Try PascalCase first (server format), then camelCase
                    Name = (item.TryGetProperty("Key", out var keyProp) || 
                            item.TryGetProperty("key", out keyProp))
                        ? keyProp.GetString() ?? string.Empty
                        : string.Empty,
                    BucketName = args.BucketName,
                    Size = (item.TryGetProperty("Size", out var sizeProp) || 
                            item.TryGetProperty("size", out sizeProp))
                        ? sizeProp.GetInt64()
                        : 0L,
                    LastModified = (item.TryGetProperty("LastModified", out var modProp) || 
                                    item.TryGetProperty("lastModified", out modProp))
                        ? modProp.GetDateTime()
                        : DateTime.MinValue,
                    ETag = (item.TryGetProperty("ETag", out var etagProp) || 
                            item.TryGetProperty("etag", out etagProp))
                        ? etagProp.GetString() ?? "unknown"
                        : "unknown"
                };
                result.Objects.Add(obj);
            }
        }

        // Try both PascalCase and camelCase
        if (doc.RootElement.TryGetProperty("IsTruncated", out var truncated) ||
            doc.RootElement.TryGetProperty("isTruncated", out truncated))
            result.IsTruncated = truncated.GetBoolean();

        if (doc.RootElement.TryGetProperty("NextContinuationToken", out var token) ||
            doc.RootElement.TryGetProperty("nextContinuationToken", out token) ||
            doc.RootElement.TryGetProperty("continuationToken", out token))
            result.ContinuationToken = token.GetString();

        return result;
    }

    /// <inheritdoc/>
    public async Task<PresignedUrlResult> PresignedGetObjectAsync(PresignedGetObjectArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var presignedUrl = GeneratePresignedUrl("GET", args.BucketName, args.ObjectName, args.ExpiresInSeconds);

        return new PresignedUrlResult
        {
            Url = presignedUrl,
            ExpiresAt = DateTime.UtcNow.AddSeconds(args.ExpiresInSeconds)
        };
    }

    /// <inheritdoc/>
    public async Task<PresignedUrlResult> PresignedPutObjectAsync(PresignedPutObjectArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var presignedUrl = GeneratePresignedUrl("PUT", args.BucketName, args.ObjectName, args.ExpiresInSeconds, args.ContentType);

        return new PresignedUrlResult
        {
            Url = presignedUrl,
            ExpiresAt = DateTime.UtcNow.AddSeconds(args.ExpiresInSeconds)
        };
    }

    /// <inheritdoc/>
    public async Task<Tenant> CreateTenantAsync(CreateTenantArgs args, CancellationToken cancellationToken = default)
    {
        args.Validate();

        var url = $"{GetBaseUrl()}/api/admin/tenants";
        var content = new StringContent(
            JsonSerializer.Serialize(new
            {
                name = args.Name,
                displayName = args.DisplayName,
                storageQuotaBytes = args.StorageQuotaBytes,
                maxBuckets = args.MaxBuckets
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await MakeAdminRequestAsync(HttpMethod.Post, url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to create tenant '{args.Name}'. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        return new Tenant
        {
            Id = root.GetProperty("id").GetString() ?? string.Empty,
            Name = root.GetProperty("name").GetString() ?? string.Empty,
            DisplayName = root.GetProperty("displayName").GetString() ?? string.Empty,
            CreatedAt = root.GetProperty("createdAt").GetDateTimeOffset(),
            StorageQuotaBytes = root.TryGetProperty("storageQuotaBytes", out var quota) ? quota.GetInt64() : 0,
            MaxBuckets = root.TryGetProperty("maxBuckets", out var maxBuckets) ? maxBuckets.GetInt32() : 0
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Tenant>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{GetBaseUrl()}/api/admin/tenants";
        var response = await MakeAdminRequestAsync(HttpMethod.Get, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to list tenants. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);

        var tenants = new List<Tenant>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var tenant in doc.RootElement.EnumerateArray())
            {
                tenants.Add(new Tenant
                {
                    Id = tenant.GetProperty("id").GetString() ?? string.Empty,
                    Name = tenant.GetProperty("name").GetString() ?? string.Empty,
                    DisplayName = tenant.GetProperty("displayName").GetString() ?? string.Empty,
                    CreatedAt = tenant.GetProperty("createdAt").GetDateTimeOffset(),
                    Status = tenant.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty,
                    StorageQuotaBytes = tenant.TryGetProperty("storageQuotaBytes", out var quota) ? quota.GetInt64() : 0,
                    StorageUsedBytes = tenant.TryGetProperty("storageUsedBytes", out var used) ? used.GetInt64() : 0,
                    MaxBuckets = tenant.TryGetProperty("maxBuckets", out var maxBuckets) ? maxBuckets.GetInt32() : 0,
                    MaxObjectsPerBucket = tenant.TryGetProperty("maxObjectsPerBucket", out var maxObjects) ? maxObjects.GetInt32() : 0
                });
            }
        }

        return tenants;
    }

    /// <inheritdoc/>
    public async Task DeleteTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        var url = $"{GetBaseUrl()}/api/admin/tenants/{Uri.EscapeDataString(tenantId)}";
        var response = await MakeAdminRequestAsync(HttpMethod.Delete, url, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ServerException(
                $"Failed to delete tenant '{tenantId}'. Status={(int)response.StatusCode} {response.StatusCode}. Response: {errorBody}",
                (int)response.StatusCode);
        }
    }

    #endregion

    #region Health Check

    /// <inheritdoc/>
    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{GetBaseUrl()}/health";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    private static string EnsureQuotedEtag(string etag)
    {
        if (string.IsNullOrWhiteSpace(etag))
        {
            return "\"\"";
        }

        var trimmed = etag.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return $"\"{trimmed}\"";
    }

    /// <summary>
    /// Generates an AWS SigV4 presigned URL for direct object access.
    /// Supports GET and PUT methods with optional Content-Type enforcement for PUT.
    /// </summary>
    /// <param name="method">HTTP method ("GET" or "PUT")</param>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="objectName">Object key</param>
    /// <param name="expiresInSeconds">URL validity period</param>
    /// <param name="contentType">Optional Content-Type for PUT (enforces upload type)</param>
    /// <returns>Presigned URL string</returns>
    private string GeneratePresignedUrl(string method, string bucketName, string objectName, int expiresInSeconds, string? contentType = null)
    {
        if (method == "GET")
        {
            return _signer.GeneratePresignedGetUrl(_endpoint, _useSSL, bucketName, objectName, expiresInSeconds);
        }
        else if (method == "PUT")
        {
            return _signer.GeneratePresignedPutUrl(_endpoint, _useSSL, bucketName, objectName, expiresInSeconds, contentType);
        }
        else
        {
            throw new NotSupportedException($"Presigned URL generation not supported for method '{method}'");
        }
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        await Task.CompletedTask;
    }
}
