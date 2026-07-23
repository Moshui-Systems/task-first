using System.Diagnostics;
using System.Windows.Threading;
using TaskFirst.Anki;
using TaskFirst.Licensing;
using TaskFirst.Models;
using TaskFirst.Native;

namespace TaskFirst.Services;

public sealed record BlockEvent(string RuleName, string ProcessName, string Message, bool Minimized);

/// <summary>
/// Ties the window watcher, rules and Anki gate together. When a window belonging to a
/// blocked app comes to the foreground and its gate isn't satisfied, the window is minimized.
///
/// Decisions use the last-known gate result (defaulting to "locked" when unknown) so blocking
/// is instant with no flash-of-allowed; gate results refresh in the background on a short TTL.
/// </summary>
public sealed class BlockingEngine : IDisposable
{
    private readonly WindowWatcher _watcher = new();
    private readonly DispatcherTimer _pollTimer = new();
    private readonly AnkiGate _gate;
    private AppConfig _config;

    private readonly Dictionary<string, GateResult> _lastGate = new();
    private readonly Dictionary<string, DateTime> _lastEval = new();
    private readonly HashSet<string> _refreshing = new();

    /// <summary>Fired (on UI thread) each time the engine acts on a window. For logging/toasts.</summary>
    public event Action<BlockEvent>? Acted;

    /// <summary>Fired when a gate result is refreshed, so UI can show live status.</summary>
    public event Action<BlockRule, GateResult>? GateRefreshed;

    public bool IsRunning { get; private set; }

    /// <summary>When false, only the first <see cref="Entitlements.FreeMaxRules"/> enabled rules
    /// are enforced and per-rule schedules are ignored.</summary>
    public bool IsPro { get; set; }

    public BlockingEngine(AppConfig config)
    {
        _config = config;
        _gate = new AnkiGate(config.AnkiCacheSeconds);
        _watcher.ForegroundChanged += OnForeground;
        _pollTimer.Tick += (_, _) => _watcher.PollNow();
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _pollTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(400, config.PollIntervalMs));
        _gate.Invalidate();
        lock (_lastGate) { _lastGate.Clear(); _lastEval.Clear(); }
    }

    public void Start()
    {
        if (IsRunning) return;
        _pollTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(400, _config.PollIntervalMs));
        _watcher.Start();
        _pollTimer.Start();
        IsRunning = true;
        _watcher.PollNow();
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _watcher.Stop();
        _pollTimer.Stop();
        IsRunning = false;
    }

    /// <summary>Force every rule's gate to re-evaluate now (used by "Re-check" in the UI).</summary>
    public void ForceRefreshAll()
    {
        _gate.Invalidate();
        lock (_lastGate) _lastEval.Clear();
        foreach (var rule in _config.Rules.Where(r => r.Enabled))
            _ = RefreshGateAsync(rule);
    }

    private void OnForeground(IntPtr hwnd)
    {
        if (!_config.BlockingEnabled || !IsRunning) return;
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hwnd)) return;

        var (processName, title) = Describe(hwnd);
        if (processName is null) return;

        var rule = FindMatchingRule(processName, title);
        if (rule is null) return;

        // Decide from last-known gate state; unknown == locked.
        GateResult known;
        bool stale;
        lock (_lastGate)
        {
            bool have = _lastGate.TryGetValue(rule.Id, out known);
            stale = !have ||
                    !_lastEval.TryGetValue(rule.Id, out var when) ||
                    (DateTime.UtcNow - when).TotalSeconds >= Math.Max(2, _config.AnkiCacheSeconds);
            if (!have) known = new GateResult(false, 0, 0, "Checking Anki…");
        }

        if (stale) _ = RefreshGateAsync(rule);

        if (!known.Unlocked)
        {
            bool minimized = Minimize(hwnd);
            Acted?.Invoke(new BlockEvent(rule.Name, processName, known.Message, minimized));
        }
    }

    private async Task RefreshGateAsync(BlockRule rule)
    {
        lock (_lastGate)
        {
            if (_refreshing.Contains(rule.Id)) return;
            _refreshing.Add(rule.Id);
        }
        try
        {
            var result = await _gate.EvaluateFreshAsync(rule.Gate).ConfigureAwait(true);
            lock (_lastGate)
            {
                _lastGate[rule.Id] = result;
                _lastEval[rule.Id] = DateTime.UtcNow;
            }
            GateRefreshed?.Invoke(rule, result);
        }
        catch
        {
            // Network hiccup — keep prior state; leave locked if unknown.
        }
        finally
        {
            lock (_lastGate) _refreshing.Remove(rule.Id);
        }
    }

    private BlockRule? FindMatchingRule(string processName, string title)
    {
        int enabledSeen = 0;
        var nowLocal = DateTime.Now;

        foreach (var rule in _config.Rules)
        {
            if (!rule.Enabled || rule.ProcessPatterns.Count == 0) continue;

            enabledSeen++;
            // Free tier: only the first N enabled rules are enforced.
            if (!IsPro && enabledSeen > Entitlements.FreeMaxRules) continue;

            // Pro schedules: outside the active window the rule doesn't apply.
            if (IsPro && !rule.Schedule.IsActiveAt(nowLocal)) continue;

            bool procHit = rule.ProcessPatterns.Any(p =>
                !string.IsNullOrWhiteSpace(p) &&
                processName.Contains(p.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!procHit) continue;

            if (rule.TitlePatterns.Count > 0)
            {
                bool titleHit = rule.TitlePatterns.Any(p =>
                    !string.IsNullOrWhiteSpace(p) &&
                    title.Contains(p.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!titleHit) continue;
            }
            return rule;
        }
        return null;
    }

    private static (string? processName, string title) Describe(IntPtr hwnd)
    {
        try
        {
            uint pid = NativeMethods.GetProcessId(hwnd);
            if (pid == 0) return (null, "");
            using var proc = Process.GetProcessById((int)pid);
            return (proc.ProcessName, NativeMethods.GetWindowTitle(hwnd));
        }
        catch
        {
            return (null, "");
        }
    }

    private static bool Minimize(IntPtr hwnd)
    {
        if (NativeMethods.IsIconic(hwnd)) return false;
        return NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_MINIMIZE);
    }

    public void Dispose()
    {
        Stop();
        _watcher.Dispose();
    }
}
