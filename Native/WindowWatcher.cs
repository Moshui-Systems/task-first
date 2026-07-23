using System.Windows.Threading;

namespace TaskFirst.Native;

/// <summary>
/// Raises an event whenever a window comes to the foreground. Uses a low-overhead
/// system WinEvent hook (no global CBT hook / no injected DLL), plus the caller can
/// also run a periodic sweep as a safety net.
/// </summary>
public sealed class WindowWatcher : IDisposable
{
    private readonly NativeMethods.WinEventDelegate _proc;
    private IntPtr _hook = IntPtr.Zero;
    private readonly uint _ownProcessId;

    /// <summary>Fired on the UI thread when a foreground window changes. Arg is the HWND.</summary>
    public event Action<IntPtr>? ForegroundChanged;

    public WindowWatcher()
    {
        _ownProcessId = (uint)Environment.ProcessId;
        // Keep the delegate as a field so it is never garbage-collected while the hook lives.
        _proc = OnWinEvent;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _proc,
            0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    /// <summary>Manually push the current foreground window through the pipeline (used by the poll timer).</summary>
    public void PollNow()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero) ForegroundChanged?.Invoke(hwnd);
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        // idObject 0 == the window itself (OBJID_WINDOW). Ignore child-object events.
        if (idObject != 0 || hwnd == IntPtr.Zero) return;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == _ownProcessId) return;

        // Hop to the WPF dispatcher; the hook callback runs on our message loop already,
        // but marshalling keeps consumers simple and thread-safe.
        var app = System.Windows.Application.Current;
        if (app is null) { ForegroundChanged?.Invoke(hwnd); return; }
        app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => ForegroundChanged?.Invoke(hwnd));
    }

    public void Dispose() => Stop();
}
