namespace TaskFirst.Licensing;

/// <summary>
/// Central place for product limits, the embedded public key, and store links.
/// Free vs Pro rules live here so gating is consistent across the app.
/// </summary>
public static class Entitlements
{
    public const string ProductId = "taskfirst";

    /// <summary>
    /// ECDSA P-256 public key (SubjectPublicKeyInfo, base64). Keys are verified against this.
    /// The matching PRIVATE key never ships — it lives only in the key-minting tool.
    /// Replace this after running: dotnet run --project tools/LicenseTool -- keygen
    /// </summary>
    public const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEMDsUp1Ij9wweWbvxigd9Mvh0yQw6NSwcT+yMA2TpoxX+I32nwAtsWJXOYEOfqjzFPNn8O39SsFjt/cWx7ftvqQ==";

    /// <summary>Where "Upgrade to Pro" sends the user. Set to your store checkout URL.</summary>
    public const string BuyUrl = "https://moshui-systems.lemonsqueezy.com/buy/REPLACE_ME";

    /// <summary>Free tier is capped to this many enabled rules; Pro is unlimited.</summary>
    public const int FreeMaxRules = 2;

    // Feature codes (Pro tier grants all of these).
    public const string FeatureUnlimitedRules = "unlimited_rules";
    public const string FeatureSchedules      = "schedules";
    public const string FeatureTamperLock     = "tamper_lock";
    public const string FeatureStats          = "stats";
}
