using System.Diagnostics;
using System.Security.Principal;

namespace TaskFirst.Security;

public static class AdminHelper
{
    public static bool IsElevated()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Relaunch this executable elevated (triggers a UAC prompt). Returns false if declined.</summary>
    public static bool RelaunchAsAdmin(params string[] args)
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exe)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(' ', args),
            });
            return true;
        }
        catch
        {
            return false; // user cancelled the UAC prompt
        }
    }
}
