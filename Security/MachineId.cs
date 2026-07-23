using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace TaskFirst.Security;

/// <summary>
/// A stable, privacy-preserving per-machine identifier. We hash the Windows MachineGuid
/// (never sending the raw value) so the licensing server can enforce an activation limit
/// without learning anything reversible about the device.
/// </summary>
public static class MachineId
{
    private static string? _cached;

    public static string Current => _cached ??= Compute();

    private static string Compute()
    {
        string raw;
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            raw = key?.GetValue("MachineGuid") as string ?? "";
        }
        catch
        {
            raw = "";
        }

        if (string.IsNullOrWhiteSpace(raw))
            raw = Environment.MachineName + "|" + Environment.UserName;

        // Namespaced so the same machine yields a different id per product.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("taskfirst:" + raw));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant(); // 16 hex chars
    }
}
