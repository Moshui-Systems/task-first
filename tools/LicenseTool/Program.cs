using System.Text.Json;
using TaskFirst.Licensing;

// TaskFirst license-minting tool (developer/seller side).
//
//   keygen                         -> generate a new ECDSA keypair (run once, keep private key secret)
//   issue <email> [options]        -> mint a signed Pro key for a buyer
//
// The private key is read from --key <base64>, or the TASKFIRST_PRIVATE_KEY env var,
// or a file passed with --key-file <path>. NEVER commit the private key.

if (args.Length == 0) { PrintUsage(); return 0; }

switch (args[0].ToLowerInvariant())
{
    case "keygen": return KeyGen();
    case "issue": return Issue(args.Skip(1).ToArray());
    case "verify": return Verify(args.Skip(1).ToArray());
    default: PrintUsage(); return 1;
}

static int Verify(string[] a)
{
    if (a.Length == 0) { Console.Error.WriteLine("Usage: verify <token>"); return 1; }
    var r = LicenseToken.Verify(a[0], Entitlements.PublicKeyBase64);
    if (!r.Ok) { Console.WriteLine("INVALID: " + r.Error); return 1; }
    Console.WriteLine($"VALID against embedded public key. tier={r.Payload!.Tier} email={r.Payload.Email} " +
                      $"expires={(r.Payload.ExpiresUtc?.ToString("yyyy-MM-dd") ?? "never")} expired={r.Payload.IsExpired}");
    return 0;
}

static int KeyGen()
{
    var (priv, pub) = LicenseToken.GenerateKeyPair();
    Console.WriteLine();
    Console.WriteLine("=== PRIVATE KEY (keep secret — used to mint license keys) ===");
    Console.WriteLine(priv);
    Console.WriteLine();
    Console.WriteLine("=== PUBLIC KEY (paste into Licensing/Entitlements.cs -> PublicKeyBase64) ===");
    Console.WriteLine(pub);
    Console.WriteLine();
    Console.WriteLine("Tip: save the private key somewhere safe, e.g.");
    Console.WriteLine("  setx TASKFIRST_PRIVATE_KEY \"<private-key>\"   (Windows, new shells)");
    return 0;
}

static int Issue(string[] a)
{
    if (a.Length == 0) { Console.Error.WriteLine("Usage: issue <email> [--days N] [--tier pro|free] [--key <b64> | --key-file <path>]"); return 1; }

    string email = a[0];
    int? days = null;
    string tier = "pro";
    string? privInline = null;
    string? keyFile = null;

    for (int i = 1; i < a.Length; i++)
    {
        switch (a[i])
        {
            case "--days": days = int.Parse(a[++i]); break;
            case "--tier": tier = a[++i]; break;
            case "--key": privInline = a[++i]; break;
            case "--key-file": keyFile = a[++i]; break;
            default: Console.Error.WriteLine($"Unknown option: {a[i]}"); return 1;
        }
    }

    string? priv = privInline
                   ?? (keyFile is not null ? File.ReadAllText(keyFile).Trim() : null)
                   ?? Environment.GetEnvironmentVariable("TASKFIRST_PRIVATE_KEY");
    if (string.IsNullOrWhiteSpace(priv))
    {
        Console.Error.WriteLine("No private key. Pass --key <b64>, --key-file <path>, or set TASKFIRST_PRIVATE_KEY.");
        return 1;
    }

    var payload = new LicensePayload
    {
        LicenseId = Guid.NewGuid().ToString("N")[..12],
        Email = email,
        Product = Entitlements.ProductId,
        Tier = tier,
        IssuedUtc = DateTime.UtcNow,
        ExpiresUtc = days is { } d ? DateTime.UtcNow.AddDays(d) : null,
    };

    string token = LicenseToken.Sign(payload, priv!);

    Console.WriteLine();
    Console.WriteLine($"Issued {tier} license for {email}" + (days is { } dd ? $" ({dd} days)" : " (perpetual)"));
    Console.WriteLine("License id: " + payload.LicenseId);
    Console.WriteLine();
    Console.WriteLine("=== LICENSE KEY (send this to the buyer) ===");
    Console.WriteLine(token);
    Console.WriteLine();
    Console.WriteLine("Payload: " + JsonSerializer.Serialize(payload));
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("TaskFirst license tool");
    Console.WriteLine("  dotnet run --project tools/LicenseTool -- keygen");
    Console.WriteLine("  dotnet run --project tools/LicenseTool -- issue <email> [--days N] [--tier pro|free] [--key-file priv.txt]");
}
