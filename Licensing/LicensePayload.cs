using System.Text.Json.Serialization;

namespace TaskFirst.Licensing;

/// <summary>
/// The signed contents of a license key. Kept deliberately small and pure (no WPF / no I/O)
/// so the same type compiles into both the app (verify) and the key-minting tool (sign).
/// </summary>
public sealed class LicensePayload
{
    /// <summary>Unique id for this license (for revocation lists / support).</summary>
    public string LicenseId { get; set; } = "";

    /// <summary>Buyer email — shown in the app and bound to the key.</summary>
    public string Email { get; set; } = "";

    /// <summary>Product this key is valid for. Guards against cross-product reuse.</summary>
    public string Product { get; set; } = "taskfirst";

    /// <summary>"free" or "pro".</summary>
    public string Tier { get; set; } = "pro";

    /// <summary>Optional fine-grained feature codes (future-proofing; empty = whole tier).</summary>
    public string[] Features { get; set; } = Array.Empty<string>();

    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null = perpetual license. Otherwise the key stops granting Pro after this date.</summary>
    public DateTime? ExpiresUtc { get; set; }

    [JsonIgnore]
    public bool IsExpired => ExpiresUtc is { } e && DateTime.UtcNow > e;

    [JsonIgnore]
    public bool IsPro => string.Equals(Tier, "pro", StringComparison.OrdinalIgnoreCase);
}
