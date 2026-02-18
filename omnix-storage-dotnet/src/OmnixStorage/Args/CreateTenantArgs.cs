namespace OmnixStorage.Args;

/// <summary>
/// Arguments for creating a tenant.
/// </summary>
public sealed class CreateTenantArgs
{
    /// <summary>Unique tenant name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Storage quota in bytes (0 = unlimited).</summary>
    public long? StorageQuotaBytes { get; set; }

    /// <summary>Maximum number of buckets (0 = unlimited).</summary>
    public int? MaxBuckets { get; set; }

    /// <summary>Validate required fields.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Tenant name is required.");
        if (string.IsNullOrWhiteSpace(DisplayName))
            throw new ArgumentException("Tenant display name is required.");
    }
}
