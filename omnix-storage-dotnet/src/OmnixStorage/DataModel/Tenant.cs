namespace OmnixStorage.DataModel;

/// <summary>
/// Represents a tenant in a multi-tenant OmnixStorage deployment.
/// </summary>
public sealed class Tenant
{
    /// <summary>Tenant ID.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Tenant name (unique).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Created timestamp (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Tenant status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Storage quota in bytes.</summary>
    public long StorageQuotaBytes { get; init; }

    /// <summary>Storage used in bytes.</summary>
    public long StorageUsedBytes { get; init; }

    /// <summary>Maximum buckets allowed.</summary>
    public int MaxBuckets { get; init; }

    /// <summary>Maximum objects per bucket.</summary>
    public int MaxObjectsPerBucket { get; init; }
}
