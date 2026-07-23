using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TaskFirst.Anki;
using TaskFirst.Models;
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

        if (_rules.Count > 0) RulesList.SelectedIndex = 0;
        UpdateStatus();
    }

    public void RefreshBlockingState()
    {
        MasterBlockBox.IsChecked = App.Instance.Config.BlockingEnabled;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = App.Instance.Config.BlockingEnabled
            ? $"Blocking ON · {_rules.Count(r => r.Enabled)} active rule(s)"
            : "Blocking OFF";
    }

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
        var rule = new BlockRule { Name = "New rule", ProcessPatterns = { } };
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

        RulesList.Items.Refresh();
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

    // ---------- Bottom bar ----------

    private void OnToggleMaster(object sender, RoutedEventArgs e)
        => App.Instance.SetBlocking(MasterBlockBox.IsChecked == true);

    private void OnToggleStartup(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupManager.SetEnabled(StartupBox.IsChecked == true);
            App.Instance.Config.StartWithWindows = StartupBox.IsChecked == true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't update startup setting: {ex.Message}");
            StartupBox.IsChecked = StartupManager.IsEnabled();
        }
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
