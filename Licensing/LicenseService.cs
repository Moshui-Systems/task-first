using System.IO;
using TaskFirst.Services;

namespace TaskFirst.Licensing;

public enum LicenseState { Free, Pro, Expired, Invalid }

/// <summary>
/// App-side license manager. Loads a stored key from %AppData%\TaskFirst\license.key,
/// verifies it offline against the embedded public key, and exposes entitlement checks.
/// </summary>
public sealed class LicenseService
{
    private static string KeyPath => Path.Combine(ConfigStore.Dir, "license.key");

    public LicenseState State { get; private set; } = LicenseState.Free;
    public LicensePayload? Payload { get; private set; }
    public string LastMessage { get; private set; } = "";

    public event Action? Changed;

    public bool IsPro => State == LicenseState.Pro;

    public LicenseService() => Load();

    public void Load()
    {
        if (!File.Exists(KeyPath))
        {
            Set(LicenseState.Free, null, "No license — Free tier.");
            return;
        }
        try
        {
            Apply(File.ReadAllText(KeyPath), persist: false);
        }
        catch
        {
            Set(LicenseState.Free, null, "Could not read stored license.");
        }
    }

    /// <summary>Validate and (if valid) store a pasted key. Returns a user-facing message.</summary>
    public (bool ok, string message) Activate(string key)
    {
        var result = Apply(key, persist: true);
        return (result == LicenseState.Pro, LastMessage);
    }

    public void Deactivate()
    {
        try { if (File.Exists(KeyPath)) File.Delete(KeyPath); } catch { /* ignore */ }
        Set(LicenseState.Free, null, "License removed — back to Free tier.");
    }

    private LicenseState Apply(string key, bool persist)
    {
        var v = LicenseToken.Verify(key.Trim(), Entitlements.PublicKeyBase64);
        if (!v.Ok || v.Payload is null)
        {
            Set(LicenseState.Invalid, null, v.Error);
            return State;
        }

        var p = v.Payload;

        if (!string.Equals(p.Product, Entitlements.ProductId, StringComparison.OrdinalIgnoreCase))
        {
            Set(LicenseState.Invalid, null, "This key is for a different product.");
            return State;
        }

        if (p.IsExpired)
        {
            Set(LicenseState.Expired, p, $"License expired on {p.ExpiresUtc:yyyy-MM-dd}.");
            return State;
        }

        if (!p.IsPro)
        {
            Set(LicenseState.Free, p, "Valid key, Free tier.");
            return State;
        }

        if (persist)
        {
            Directory.CreateDirectory(ConfigStore.Dir);
            File.WriteAllText(KeyPath, key.Trim());
        }

        string until = p.ExpiresUtc is { } e ? $"until {e:yyyy-MM-dd}" : "(perpetual)";
        Set(LicenseState.Pro, p, $"Pro activated for {p.Email} {until}. Thank you!");
        return State;
    }

    /// <summary>Whole-tier check today; per-feature flags reserved for future granularity.</summary>
    public bool Has(string featureCode) => IsPro;

    private void Set(LicenseState state, LicensePayload? payload, string message)
    {
        State = state;
        Payload = payload;
        LastMessage = message;
        Changed?.Invoke();
    }
}
