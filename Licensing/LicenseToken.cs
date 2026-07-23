using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskFirst.Licensing;

public readonly record struct VerifyResult(bool Ok, LicensePayload? Payload, string Error);

/// <summary>
/// Compact, offline-verifiable license token: <c>base64url(payloadJson).base64url(signature)</c>,
/// signed with ECDSA P-256 / SHA-256. The app ships only the PUBLIC key and can verify keys
/// with no network call; only the key-minting tool holds the PRIVATE key.
/// </summary>
public static class LicenseToken
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // ---- signing (tool side) ----

    public static (string privateKeyBase64, string publicKeyBase64) GenerateKeyPair()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var priv = Convert.ToBase64String(ec.ExportPkcs8PrivateKey());
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        return (priv, pub);
    }

    public static string Sign(LicensePayload payload, string privateKeyBase64)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        using var ec = ECDsa.Create();
        ec.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        var sig = ec.SignData(payloadBytes, HashAlgorithmName.SHA256);
        return B64Url(payloadBytes) + "." + B64Url(sig);
    }

    // ---- verifying (app side) ----

    public static VerifyResult Verify(string token, string publicKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new VerifyResult(false, null, "Empty key.");

        var parts = token.Trim().Split('.');
        if (parts.Length != 2)
            return new VerifyResult(false, null, "Malformed key.");

        byte[] payloadBytes, sig;
        try
        {
            payloadBytes = FromB64Url(parts[0]);
            sig = FromB64Url(parts[1]);
        }
        catch
        {
            return new VerifyResult(false, null, "Malformed key.");
        }

        try
        {
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            if (!ec.VerifyData(payloadBytes, sig, HashAlgorithmName.SHA256))
                return new VerifyResult(false, null, "Invalid signature — this key wasn't issued for TaskFirst.");
        }
        catch (Exception ex)
        {
            return new VerifyResult(false, null, "Verification failed: " + ex.Message);
        }

        LicensePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, Json);
        }
        catch
        {
            return new VerifyResult(false, null, "Corrupt key contents.");
        }

        if (payload is null)
            return new VerifyResult(false, null, "Corrupt key contents.");

        return new VerifyResult(true, payload, "");
    }

    // ---- base64url helpers ----

    private static string B64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromB64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
