using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TaskFirst.Anki;
using TaskFirst.Licensing;
using TaskFirst.Models;
using TaskFirst.Security;
using TaskFirst.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace TaskFirst.UI;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BlockRule> _rules;
    private BlockRule? _current;
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();

        _rules = new ObservableCollection<BlockRule>(App.Instance.Config.Rules);
        RulesList.ItemsSource = _rules;

        MasterBlockBox.IsChecked = App.Instance.Config.BlockingEnabled;
        StartupBox.IsChecked = StartupManager.IsEnabled();
        TamperBox.IsChecked = App.Instance.Config.TamperLockEnabled;

        if (_rules.Count > 0) RulesList.SelectedIndex = 0;
        RefreshProState();
        UpdateStatus();
    }

    public void RefreshBlockingState()
    {
        MasterBlockBox.IsChecked = App.Instance.Config.BlockingEnabled;
        UpdateStatus();
    }

    /// <summary>Reflect the current license tier in the UI (badge, gated controls).</summary>
    public void RefreshProState()
    {
        bool pro = App.Instance.License.IsPro;

        ProBadgeText.Text = pro ? "PRO" : "FREE";
        ProBadgeText.Foreground = pro
            ? (System.Windows.Media.Brush)FindResource("AccentGreen")
            : (System.Windows.Media.Brush)FindResource("FgDim");
        UpgradeBtn.Visibility = pro ? Visibility.Collapsed : Visibility.Visible;

        // Schedules are Pro-only.
        SchedulePanel.IsEnabled = pro;
        SchedProTag.Visibility = pro ? Visibility.Collapsed : Visibility.Visible;

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int enabled = _rules.Count(r => r.Enabled);
        bool pro = App.Instance.License.IsPro;
        string cap = (!pro && enabled > Entitlements.FreeMaxRules)
            ? $" (Free enforces {Entitlements.FreeMaxRules})"
            : "";
        string admin = AdminHelper.IsElevated() ? " · admin" : " · not admin";
        StatusText.Text = (App.Instance.Config.BlockingEnabled
            ? $"Blocking ON · {enabled} enabled rule(s){cap}"
            : "Blocking OFF") + admin;
    }

    private int EnabledRuleCount() => _rules.Count(r => r.Enabled);

    // ---------- Rule list ----------

    private void OnRuleSelected(object sender, SelectionChangedEventArgs e)
    {
        // Commit edits on the outgoing rule before switching.
        if (_current is not null && !_loading) ApplyEditorTo(_current);

        _current = RulesList.SelectedItem as BlockRule;
        LoadEditor(_current);
    }

    private void OnAddRule(object sender, RoutedEventArgs e)
    {
        if (_current is not null) ApplyEditorTo(_current);
        // On Free, new rules past the cap start disabled so the cap is obvious.
        bool startEnabled = App.Instance.License.IsPro || EnabledRuleCount() < Entitlements.FreeMaxRules;
        var rule = new BlockRule { Name = "New rule", Enabled = startEnabled };
        _rules.Add(rule);
        RulesList.SelectedItem = rule;
    }

    private void OnDeleteRule(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        if (MessageBox.Show($"Delete rule \"{_current.Name}\"?", "TaskFirst",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        var toRemove = _current;
        _current = null;
        _rules.Remove(toRemove);
        if (_rules.Count > 0) RulesList.SelectedIndex = 0;
        else LoadEditor(null);
    }

    // ---------- Editor <-> model ----------

    private void LoadEditor(BlockRule? rule)
    {
        _loading = true;
        EditorPanel.IsEnabled = rule is not null;
        if (rule is null) { _loading = false; return; }

        NameBox.Text = rule.Name;
        EnabledBox.IsChecked = rule.Enabled;
        ProcBox.Text = string.Join(Environment.NewLine, rule.ProcessPatterns);
        TitleBox.Text = string.Join(Environment.NewLine, rule.TitlePatterns);

        GateEnabledBox.IsChecked = rule.Gate.Enabled;
        DeckBox.Text = rule.Gate.DeckName;
        MinCardsBox.Text = rule.Gate.MinCardsReviewedToday.ToString();
        ClearedBox.IsChecked = rule.Gate.RequireDeckCleared;
        UrlBox.Text = rule.Gate.AnkiConnectUrl;
        TestResult.Text = "";

        var s = rule.Schedule;
        SchedEnabledBox.IsChecked = s.Enabled;
        DaySu.IsChecked = s.Days.ElementAtOrDefault(0);
        DayMo.IsChecked = s.Days.ElementAtOrDefault(1);
        DayTu.IsChecked = s.Days.ElementAtOrDefault(2);
        DayWe.IsChecked = s.Days.ElementAtOrDefault(3);
        DayTh.IsChecked = s.Days.ElementAtOrDefault(4);
        DayFr.IsChecked = s.Days.ElementAtOrDefault(5);
        DaySa.IsChecked = s.Days.ElementAtOrDefault(6);
        StartTimeBox.Text = MinutesToHhmm(s.StartMinutes);
        EndTimeBox.Text = MinutesToHhmm(s.EndMinutes);

        _loading = false;
    }

    private void ApplyEditorTo(BlockRule rule)
    {
        rule.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Unnamed rule" : NameBox.Text.Trim();
        rule.Enabled = EnabledBox.IsChecked == true;
        rule.ProcessPatterns = SplitLines(ProcBox.Text);
        rule.TitlePatterns = SplitLines(TitleBox.Text);

        rule.Gate.Enabled = GateEnabledBox.IsChecked == true;
        rule.Gate.DeckName = DeckBox.Text?.Trim() ?? "";
        rule.Gate.MinCardsReviewedToday = int.TryParse(MinCardsBox.Text, out var n) ? Math.Max(0, n) : 0;
        rule.Gate.RequireDeckCleared = ClearedBox.IsChecked == true;
        rule.Gate.AnkiConnectUrl = string.IsNullOrWhiteSpace(UrlBox.Text)
            ? "http://127.0.0.1:8765" : UrlBox.Text.Trim();

        var s = rule.Schedule;
        s.Enabled = SchedEnabledBox.IsChecked == true;
        s.Days = new[]
        {
            DaySu.IsChecked == true, DayMo.IsChecked == true, DayTu.IsChecked == true,
            DayWe.IsChecked == true, DayTh.IsChecked == true, DayFr.IsChecked == true,
            DaySa.IsChecked == true,
        };
        s.StartMinutes = HhmmToMinutes(StartTimeBox.Text, s.StartMinutes);
        s.EndMinutes = HhmmToMinutes(EndTimeBox.Text, s.EndMinutes);

        RulesList.Items.Refresh();
    }

    private static string MinutesToHhmm(int m) => $"{m / 60:00}:{m % 60:00}";

    private static int HhmmToMinutes(string text, int fallback)
    {
        if (TimeSpan.TryParse((text ?? "").Trim(), out var ts) && ts < TimeSpan.FromDays(1))
            return (int)ts.TotalMinutes;
        return fallback;
    }

    private static List<string> SplitLines(string text) =>
        (text ?? "")
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim())
        .Where(s => s.Length > 0)
        .ToList();

    private AnkiGateConfig BuildGateFromEditor() => new()
    {
        Enabled = GateEnabledBox.IsChecked == true,
        DeckName = DeckBox.Text?.Trim() ?? "",
        MinCardsReviewedToday = int.TryParse(MinCardsBox.Text, out var n) ? Math.Max(0, n) : 0,
        RequireDeckCleared = ClearedBox.IsChecked == true,
        AnkiConnectUrl = string.IsNullOrWhiteSpace(UrlBox.Text) ? "http://127.0.0.1:8765" : UrlBox.Text.Trim(),
    };

    // ---------- Anki buttons ----------

    private async void OnLoadDecks(object sender, RoutedEventArgs e)
    {
        var url = string.IsNullOrWhiteSpace(UrlBox.Text) ? "http://127.0.0.1:8765" : UrlBox.Text.Trim();
        var client = new AnkiConnectClient(url);
        TestResult.Text = "Loading decks…";
        try
        {
            var decks = await client.GetDeckNamesAsync();
            var current = DeckBox.Text;
            DeckBox.ItemsSource = decks;
            DeckBox.Text = current;
            TestResult.Text = $"Loaded {decks.Count} deck(s).";
        }
        catch (Exception ex)
        {
            TestResult.Text = $"Could not reach Anki: {ex.Message}. Is Anki open with the AnkiConnect add-on installed?";
        }
    }

    private async void OnTestGate(object sender, RoutedEventArgs e)
    {
        var gate = BuildGateFromEditor();
        TestResult.Text = "Checking…";
        try
        {
            var result = await new AnkiGate(5).EvaluateFreshAsync(gate);
            TestResult.Text = result.Message;
            TestResult.Foreground = result.Unlocked
                ? (System.Windows.Media.Brush)FindResource("AccentGreen")
                : (System.Windows.Media.Brush)FindResource("AccentRed");
        }
        catch (Exception ex)
        {
            TestResult.Text = $"Error: {ex.Message}";
        }
    }

    // ---------- Pro gating ----------

    private void OnUpgrade(object sender, RoutedEventArgs e)
    {
        App.Instance.ShowActivation();
        RefreshProState();
    }

    private void OnRuleEnabledToggled(object sender, RoutedEventArgs e)
    {
        // Free tier: block enabling more than the cap of rules.
        if (EnabledBox.IsChecked == true && !App.Instance.License.IsPro
            && EnabledRuleCountExcludingCurrent() >= Entitlements.FreeMaxRules)
        {
            EnabledBox.IsChecked = false;
            PromptUpgrade($"Free tier allows {Entitlements.FreeMaxRules} active rules. Upgrade to Pro for unlimited rules?");
        }
        UpdateStatus();
    }

    private int EnabledRuleCountExcludingCurrent()
        => _rules.Count(r => r.Enabled && !ReferenceEquals(r, _current));

    private void PromptUpgrade(string message)
    {
        if (MessageBox.Show(message, "TaskFirst Pro", MessageBoxButton.YesNo, MessageBoxImage.Information)
            == MessageBoxResult.Yes)
        {
            App.Instance.ShowActivation();
            RefreshProState();
        }
    }

    private void OnToggleTamper(object sender, RoutedEventArgs e)
    {
        var cfg = App.Instance.Config;

        if (TamperBox.IsChecked == true)
        {
            if (!App.Instance.License.IsPro)
            {
                TamperBox.IsChecked = false;
                PromptUpgrade("The tamper-lock is a Pro feature. Upgrade to Pro?");
                return;
            }
            if (string.IsNullOrEmpty(cfg.TamperPasswordHash) && !SetTamperPassword())
            {
                TamperBox.IsChecked = false;
                return;
            }
            cfg.TamperLockEnabled = true;
        }
        else
        {
            // Turning the lock off is itself a protected action.
            if (!App.Instance.ConfirmTamperUnlock())
            {
                TamperBox.IsChecked = true;
                return;
            }
            cfg.TamperLockEnabled = false;
        }
        App.Instance.SaveConfig();
    }

    private void OnSetTamperPassword(object sender, RoutedEventArgs e)
    {
        if (!App.Instance.License.IsPro)
        {
            PromptUpgrade("Setting a tamper-lock password is a Pro feature. Upgrade to Pro?");
            return;
        }
        // Changing an existing password requires the current one.
        if (!string.IsNullOrEmpty(App.Instance.Config.TamperPasswordHash)
            && !App.Instance.ConfirmTamperUnlock())
            return;
        SetTamperPassword();
    }

    private bool SetTamperPassword()
    {
        var pwd = PasswordDialog.AskNew("Set a tamper-lock password:");
        if (pwd is null) return false;
        var confirm = PasswordDialog.AskNew("Confirm the password:");
        if (confirm is null) return false;
        if (pwd != confirm)
        {
            MessageBox.Show("Passwords didn't match.", "TaskFirst");
            return false;
        }
        App.Instance.Config.TamperPasswordHash = PasswordHasher.Hash(pwd);
        App.Instance.SaveConfig();
        StatusText.Text = "Tamper-lock password set ✓";
        return true;
    }

    // ---------- Bottom bar ----------

    private void OnToggleMaster(object sender, RoutedEventArgs e)
    {
        bool want = MasterBlockBox.IsChecked == true;
        bool applied = App.Instance.SetBlocking(want);
        if (!applied) MasterBlockBox.IsChecked = !want;   // tamper-lock refused; revert the checkbox
        UpdateStatus();
    }

    private void OnToggleStartup(object sender, RoutedEventArgs e)
    {
        bool want = StartupBox.IsChecked == true;

        if (want && !AdminHelper.IsElevated())
        {
            if (MessageBox.Show(
                    "Elevated auto-start needs administrator rights. Restart TaskFirst as administrator now?",
                    "TaskFirst", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                App.Instance.RestartAsAdmin();
            StartupBox.IsChecked = StartupManager.IsEnabled();
            return;
        }

        bool ok = StartupManager.SetEnabled(want);
        if (!ok)
        {
            MessageBox.Show(want
                ? "Couldn't create the startup task."
                : "Couldn't remove the startup task.", "TaskFirst");
            StartupBox.IsChecked = StartupManager.IsEnabled();
            return;
        }

        App.Instance.Config.StartWithWindows = want;
        App.Instance.SaveConfig();
        UpdateStatus();
    }

    private void OnShowPomodoro(object sender, RoutedEventArgs e) => App.Instance.ShowPomodoro();

    private void OnRecheck(object sender, RoutedEventArgs e)
    {
        App.Instance.Engine.ForceRefreshAll();
        StatusText.Text = "Re-checking Anki…";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_current is not null) ApplyEditorTo(_current);

        var cfg = App.Instance.Config;
        cfg.Rules = _rules.ToList();
        App.Instance.SaveConfig();
        UpdateStatus();
        StatusText.Text = "Saved ✓";
    }

    protected override void OnClosed(EventArgs e)
    {
        // Persist edits when the window is closed via the X too.
        if (_current is not null) ApplyEditorTo(_current);
        App.Instance.Config.Rules = _rules.ToList();
        App.Instance.SaveConfig();
        base.OnClosed(e);
    }
}
