namespace OmnixStorage;

/// <summary>
/// Factory helpers for creating OmnixStorage clients.
/// </summary>
public static class OmnixStorageClientFactory
{
    /// <summary>
    /// Creates a client configured for a public endpoint (browser-facing) to generate presigned URLs.
    /// Accepts endpoints with or without scheme (e.g., "storage.example.com" or "https://storage.example.com").
    /// </summary>
    public static IOmnixStorageClient CreatePublicEndpointClient(
        string publicEndpoint,
        string accessKey,
        string secretKey,
        string region = "us-east-1",
        string? sessionToken = null)
    {
        if (string.IsNullOrWhiteSpace(publicEndpoint))
        {
            throw new ArgumentException("Public endpoint is required.", nameof(publicEndpoint));
        }

        var endpoint = publicEndpoint.Trim();
        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = $"http://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Public endpoint is not a valid URI.", nameof(publicEndpoint));
        }

        var port = uri.IsDefaultPort
            ? (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : uri.Port;

        var builder = new OmnixStorageClientBuilder()
            .WithEndpoint($"{uri.Host}:{port}")
            .WithCredentials(accessKey, secretKey)
            .WithRegion(region)
            .WithSSL(uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            builder.WithSessionToken(sessionToken);
        }

        return builder.Build();
    }
}
