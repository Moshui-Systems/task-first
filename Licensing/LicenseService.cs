using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TaskFirst.Security;
using TaskFirst.Services;

namespace TaskFirst.Licensing;

public enum LicenseState { Free, Pro, Expired, Invalid }

/// <summary>
/// App-side license manager. Verifies a signed key offline against the embedded public key,
/// and — when a licensing API is configured — enforces a per-machine activation limit and
/// honours a revocation list. Falls back to pure-offline trust if the API is unset/unreachable.
/// </summary>
public sealed class LicenseService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    private static string KeyPath => Path.Combine(ConfigStore.Dir, "license.key");

    public LicenseState State { get; private set; } = LicenseState.Free;
    public LicensePayload? Payload { get; private set; }
    public string LastMessage { get; private set; } = "";

    public event Action? Changed;

    public bool IsPro => State == LicenseState.Pro;

    public LicenseService() => Load();

    // ---------- load / local verify ----------

    public void Load()
    {
        if (!File.Exists(KeyPath))
        {
            Set(LicenseState.Free, null, "No license — Free tier.");
            return;
        }
        try
        {
            var local = VerifyLocal(File.ReadAllText(KeyPath));
            Set(local.state, local.payload, local.message);
        }
        catch
        {
            Set(LicenseState.Free, null, "Could not read stored license.");
        }
    }

    private (LicenseState state, LicensePayload? payload, string message) VerifyLocal(string key)
    {
        var v = LicenseToken.Verify(key.Trim(), Entitlements.PublicKeyBase64);
        if (!v.Ok || v.Payload is null)
            return (LicenseState.Invalid, null, v.Error);

        var p = v.Payload;
        if (!string.Equals(p.Product, Entitlements.ProductId, StringComparison.OrdinalIgnoreCase))
            return (LicenseState.Invalid, null, "This key is for a different product.");
        if (p.IsExpired)
            return (LicenseState.Expired, p, $"License expired on {p.ExpiresUtc:yyyy-MM-dd}.");
        if (!p.IsPro)
            return (LicenseState.Free, p, "Valid key, Free tier.");

        string until = p.ExpiresUtc is { } e ? $"until {e:yyyy-MM-dd}" : "(perpetual)";
        return (LicenseState.Pro, p, $"Pro for {p.Email} {until}.");
    }

    // ---------- activation ----------

    /// <summary>
    /// Validate a pasted key, register this machine with the licensing API (if configured),
    /// and store the key on success. Returns a user-facing message.
    /// </summary>
    public async Task<(bool ok, string message)> ActivateAsync(string key)
    {
        var local = VerifyLocal(key);
        if (local.state != LicenseState.Pro)
        {
            Set(local.state, local.payload, local.message);
            return (false, local.message);
        }

        var payload = local.payload!;

        if (Entitlements.ApiConfigured)
        {
            var (status, detail) = await CallActivateAsync(payload.LicenseId, key.Trim()).ConfigureAwait(true);
            switch (status)
            {
                case "ok":
                    Persist(key);
                    Set(LicenseState.Pro, payload, $"Pro activated for {payload.Email} on this device. {detail}".Trim());
                    return (true, LastMessage);

                case "limit_reached":
                    Set(LicenseState.Invalid, payload,
                        $"Activation limit reached ({detail}). Deactivate another device first.");
                    return (false, LastMessage);

                case "revoked":
                    Set(LicenseState.Invalid, payload, "This license has been revoked. Contact support.");
                    return (false, LastMessage);

                case "invalid":
                    Set(LicenseState.Invalid, payload, "The server rejected this key.");
                    return (false, LastMessage);

                default: // network/other error — don't punish a paying customer
                    Persist(key);
                    Set(LicenseState.Pro, payload,
                        $"Pro activated for {payload.Email} (offline — device will register later).");
                    return (true, LastMessage);
            }
        }

        // Pure offline mode.
        Persist(key);
        Set(LicenseState.Pro, payload, $"Pro activated for {payload.Email}. Thank you!");
        return (true, LastMessage);
    }

    /// <summary>Re-check a stored Pro license against the revocation list. Safe to call in the background.</summary>
    public async Task RefreshAsync()
    {
        if (State != LicenseState.Pro || Payload is null || !Entitlements.ApiConfigured) return;

        var revoked = await IsRevokedAsync(Payload.LicenseId).ConfigureAwait(true);
        if (revoked)
        {
            try { if (File.Exists(KeyPath)) File.Delete(KeyPath); } catch { /* ignore */ }
            Set(LicenseState.Invalid, Payload, "This license has been revoked.");
        }
    }

    public void Deactivate()
    {
        var payload = Payload;
        try { if (File.Exists(KeyPath)) File.Delete(KeyPath); } catch { /* ignore */ }

        if (Entitlements.ApiConfigured && payload is not null)
            _ = CallDeactivateAsync(payload.LicenseId); // best-effort, frees a device slot

        Set(LicenseState.Free, null, "License removed — back to Free tier.");
    }

    public bool Has(string featureCode) => IsPro;

    // ---------- server calls ----------

    private async Task<(string status, string detail)> CallActivateAsync(string licenseId, string key)
    {
        try
        {
            var body = new JsonObject
            {
                ["product"] = Entitlements.ProductId,
                ["licenseId"] = licenseId,
                ["key"] = key,
                ["machineId"] = MachineId.Current,
            };
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(Entitlements.LicensingApiBase.TrimEnd('/') + "/activate", content)
                .ConfigureAwait(false);
            var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
            string status = json?["status"]?.GetValue<string>() ?? "error";
            string detail = json?["limit"] is { } lim ? $"{json?["activations"]}/{lim} devices" : "";
            return (status, detail);
        }
        catch
        {
            return ("error", "");
        }
    }

    private async Task CallDeactivateAsync(string licenseId)
    {
        try
        {
            var body = new JsonObject
            {
                ["product"] = Entitlements.ProductId,
                ["licenseId"] = licenseId,
                ["machineId"] = MachineId.Current,
            };
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var _ = await Http.PostAsync(Entitlements.LicensingApiBase.TrimEnd('/') + "/deactivate", content)
                .ConfigureAwait(false);
        }
        catch { /* best-effort */ }
    }

    private async Task<bool> IsRevokedAsync(string licenseId)
    {
        try
        {
            var url = Entitlements.LicensingApiBase.TrimEnd('/') + "/revocations?product=" + Entitlements.ProductId;
            var json = JsonNode.Parse(await Http.GetStringAsync(url).ConfigureAwait(false));
            if (json?["revoked"] is JsonArray arr)
                foreach (var item in arr)
                    if (item?.GetValue<string>() == licenseId) return true;
        }
        catch { /* network issue — assume not revoked */ }
        return false;
    }

    // ---------- helpers ----------

    private static void Persist(string key)
    {
        Directory.CreateDirectory(ConfigStore.Dir);
        File.WriteAllText(KeyPath, key.Trim());
    }

    private void Set(LicenseState state, LicensePayload? payload, string message)
    {
        State = state;
        Payload = payload;
        LastMessage = message;
        Changed?.Invoke();
    }
}
