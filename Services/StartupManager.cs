using System.Diagnostics;

namespace TaskFirst.Services;

/// <summary>
/// Manages "start with Windows" via a Scheduled Task that runs at logon with highest privileges.
/// A task (not the Run registry key) is used so the elevated app can start at login WITHOUT a UAC
/// prompt every time. Creating a highest-privilege task requires the app to be running elevated.
/// </summary>
public static class StartupManager
{
    public const string TaskName = "TaskFirst";

    public static string ExePath => Process.GetCurrentProcess().MainModule?.FileName ?? "";

    public static bool IsEnabled() => RunSchtasks("/Query", "/TN", TaskName) == 0;

    public static bool SetEnabled(bool enabled) => enabled ? Install() : Remove();

    public static bool Install()
    {
        var exe = ExePath;
        if (string.IsNullOrEmpty(exe)) return false;

        // /TR value is the command the task runs. Inner-quote the path so spaces are safe.
        string tr = $"\"{exe}\" --tray";
        return RunSchtasks(
            "/Create", "/TN", TaskName,
            "/TR", tr,
            "/SC", "ONLOGON",
            "/RL", "HIGHEST",
            "/F") == 0;
    }

    public static bool Remove() => RunSchtasks("/Delete", "/TN", TaskName, "/F") == 0;

    private static int RunSchtasks(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return -1;
            p.WaitForExit(8000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch
        {
            return -1;
        }
    }
}
