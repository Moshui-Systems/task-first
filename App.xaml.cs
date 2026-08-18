using System.Drawing;
using System.Windows;
using TaskFirst.Licensing;
using TaskFirst.Models;
using TaskFirst.Security;
using TaskFirst.Services;
using TaskFirst.UI;
using Forms = System.Windows.Forms;

namespace TaskFirst;

public partial class App : System.Windows.Application
{
    public static App Instance => (App)Current;

    public AppConfig Config { get; private set; } = null!;
    public BlockingEngine Engine { get; private set; } = null!;
    public PomodoroController Pomodoro { get; private set; } = null!;
    public LicenseService License { get; private set; } = null!;

    private Forms.NotifyIcon _tray = null!;
    private Forms.ToolStripMenuItem _blockingItem = null!;
    private MainWindow? _main;
    private PomodoroWindow? _pomodoroWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Install/remove the elevated startup task and exit (used by install.ps1 / uninstall.ps1).
        if (e.Args.Any(a => a.Equals("--install", StringComparison.OrdinalIgnoreCase)))
        {
            StartupManager.SetEnabled(true);
            Shutdown();
            return;
        }
        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            StartupManager.SetEnabled(false);
            Shutdown();
            return;
        }

        Config = ConfigStore.Load();

        // "Start with Windows" is on by default. We run elevated, so we can create the highest-
        // privilege logon task ourselves the first time — no separate installer needed.
        if (Config.StartWithWindows && AdminHelper.IsElevated() && !StartupManager.IsEnabled())
            StartupManager.SetEnabled(true);

        License = new LicenseService();

        Pomodoro = new PomodoroController(Config.Pomodoro);

        Engine = new BlockingEngine(Config, () => Pomodoro.TotalWorkSessions) { IsPro = License.IsPro };
        Pomodoro.PhaseChanged += _ => Engine.ForceRefreshAll();
        Engine.Acted += OnEngineActed;
        if (Config.BlockingEnabled) Engine.Start();

        BuildTray();

        if (Config.Pomodoro.ShowOnStartup)
            ShowPomodoro();

        bool startInTray = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        if (!startInTray)
            ShowMain();

        // Background: honour the revocation list without blocking startup.
        _ = RecheckLicenseAsync();
    }

    private async Task RecheckLicenseAsync()
    {
        await License.RefreshAsync();
        if (!License.IsPro)
        {
            Engine.IsPro = false;
            _main?.RefreshProState();
        }
    }

    // ---------- Tray ----------

    private void BuildTray()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add(new Forms.ToolStripMenuItem("Settings…", null, (_, _) => ShowMain()));
        menu.Items.Add(new Forms.ToolStripMenuItem("Upgrade to Pro…", null, (_, _) => ShowActivation()));
        menu.Items.Add(new Forms.ToolStripMenuItem("Show / hide Pomodoro", null, (_, _) => TogglePomodoro()));

        _blockingItem = new Forms.ToolStripMenuItem("Blocking enabled", null, (_, _) => ToggleBlocking())
        {
            CheckOnClick = false,
            Checked = Config.BlockingEnabled,
        };
        menu.Items.Add(_blockingItem);

        menu.Items.Add(new Forms.ToolStripMenuItem("Re-check Anki now", null, (_, _) => Engine.ForceRefreshAll()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));

        _tray = new Forms.NotifyIcon
        {
            Icon = MakeTrayIcon(),
            Visible = true,
            Text = AdminHelper.IsElevated() ? "TaskFirst (admin)" : "TaskFirst",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowMain();
    }

    private static Icon MakeTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(91, 140, 255));
            g.FillEllipse(bg, 1, 1, 30, 30);
            using var pen = new Pen(Color.White, 3f);
            // A simple check-mark = "task first".
            g.DrawLines(pen, new[]
            {
                new PointF(8, 17),
                new PointF(14, 23),
                new PointF(24, 9),
            });
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void OnEngineActed(BlockEvent ev)
    {
        if (!ev.Minimized) return;
        try
        {
            _tray.BalloonTipTitle = $"Blocked: {ev.RuleName}";
            _tray.BalloonTipText = ev.Message;
            _tray.ShowBalloonTip(2500);
        }
        catch { /* ignore */ }
    }

    // ---------- Windows ----------

    public void ShowMain()
    {
        if (_main is null)
        {
            _main = new MainWindow();
            _main.Closed += (_, _) => _main = null;
        }
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void ShowPomodoro()
    {
        if (_pomodoroWindow is null)
        {
            _pomodoroWindow = new PomodoroWindow();
            _pomodoroWindow.Closed += (_, _) => _pomodoroWindow = null;
        }
        _pomodoroWindow.Show();
        _pomodoroWindow.Activate();
    }

    public void TogglePomodoro()
    {
        if (_pomodoroWindow is { IsVisible: true })
            _pomodoroWindow.Hide();
        else
            ShowPomodoro();
    }

    public void ShowActivation()
    {
        var w = new ActivationWindow();
        if (_main is { IsVisible: true }) w.Owner = _main;
        w.ShowDialog();
    }

    /// <summary>Pro tamper-lock is active (a password is set and the license is Pro).</summary>
    public bool IsTamperProtected =>
        Config.TamperLockEnabled &&
        !string.IsNullOrEmpty(Config.TamperPasswordHash) &&
        License.IsPro;

    /// <summary>Returns true if the user is allowed to proceed with a protected action.</summary>
    public bool ConfirmTamperUnlock() =>
        !IsTamperProtected || PasswordDialog.Challenge(Config.TamperPasswordHash);

    public void ToggleBlocking() => SetBlocking(!Config.BlockingEnabled);

    /// <summary>Returns false if the change was blocked by the tamper-lock (user cancelled/failed).</summary>
    public bool SetBlocking(bool enabled)
    {
        if (Config.BlockingEnabled == enabled) return true;
        if (!enabled && !ConfirmTamperUnlock()) return false;   // disabling requires the password

        Config.BlockingEnabled = enabled;
        _blockingItem.Checked = enabled;
        if (enabled) Engine.Start(); else Engine.Stop();
        ConfigStore.Save(Config);
        _main?.RefreshBlockingState();
        return true;
    }

    public void OnLicenseChanged()
    {
        Engine.IsPro = License.IsPro;
        _main?.RefreshProState();
    }

    /// <summary>Relaunch elevated (for the rare case the app is running non-elevated).</summary>
    public void RestartAsAdmin()
    {
        if (!AdminHelper.RelaunchAsAdmin("--tray")) return;
        try { _tray.Visible = false; Engine.Dispose(); } catch { /* ignore */ }
        Shutdown();
    }

    public void SaveConfig()
    {
        ConfigStore.Save(Config);
        Engine.UpdateConfig(Config);
        Pomodoro.UpdateSettings(Config.Pomodoro);
        _blockingItem.Checked = Config.BlockingEnabled;
    }

    private void ExitApp()
    {
        // Quitting is a way to defeat blocking, so it's gated by the tamper-lock too.
        if (!ConfirmTamperUnlock()) return;

        try
        {
            Config.Pomodoro.ShowOnStartup = _pomodoroWindow is { IsVisible: true };
            ConfigStore.Save(Config);
        }
        catch { /* ignore */ }

        Engine.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Shutdown();
    }
}
