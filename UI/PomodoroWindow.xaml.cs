using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using TaskFirst.Anki;
using TaskFirst.Services;

namespace TaskFirst.UI;

public partial class PomodoroWindow : Window
{
    private readonly PomodoroController _c = App.Instance.Pomodoro;
    private readonly DispatcherTimer _ankiTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    private static readonly Color WorkColor = Color.FromRgb(0x5B, 0x8C, 0xFF);
    private static readonly Color BreakColor = Color.FromRgb(0x3F, 0xD0, 0x7A);

    public PomodoroWindow()
    {
        InitializeComponent();

        RestorePosition();

        _c.Tick += Render;
        _c.PhaseChanged += _ => Render();

        _ankiTimer.Tick += async (_, _) => await RefreshAnkiAsync();
        Loaded += async (_, _) =>
        {
            Render();
            _ankiTimer.Start();
            await RefreshAnkiAsync();
        };

        LocationChanged += (_, _) => SavePosition();
        Closed += (_, _) =>
        {
            _c.Tick -= Render;
            _ankiTimer.Stop();
        };
    }

    private void RestorePosition()
    {
        var p = App.Instance.Config.Pomodoro;
        if (p.Left >= 0 && p.Top >= 0)
        {
            Left = p.Left;
            Top = p.Top;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - 280;
            Top = area.Top + 40;
        }
    }

    private void SavePosition()
    {
        var p = App.Instance.Config.Pomodoro;
        p.Left = Left;
        p.Top = Top;
    }

    private void Render()
    {
        TimeLabel.Text = $"{(int)_c.Remaining.TotalMinutes:00}:{_c.Remaining.Seconds:00}";
        StartBtn.Content = _c.IsRunning ? "Pause" : "Start";
        SessionLabel.Text = $"Sessions: {_c.TotalWorkSessions}";

        (string label, Color color) = _c.Phase switch
        {
            PomodoroPhase.Work => ("FOCUS", WorkColor),
            PomodoroPhase.ShortBreak => ("SHORT BREAK", BreakColor),
            PomodoroPhase.LongBreak => ("LONG BREAK", BreakColor),
            _ => ("FOCUS", WorkColor),
        };
        PhaseLabel.Text = label;
        var brush = new SolidColorBrush(color);
        PhaseLabel.Foreground = brush;
        ProgressFill.Background = brush;

        // Progress bar width — bar track is Width 208 (240 - 2*16 padding).
        const double track = 208;
        ProgressFill.Width = Math.Max(0, Math.Min(1, _c.Progress)) * track;
    }

    private async Task RefreshAnkiAsync()
    {
        try
        {
            var url = App.Instance.Config.Rules
                .Select(r => r.Gate.AnkiConnectUrl)
                .FirstOrDefault() ?? "http://127.0.0.1:8765";
            var client = new AnkiConnectClient(url);
            if (await client.IsReachableAsync())
            {
                int n = await client.GetNumCardsReviewedTodayAsync();
                AnkiLabel.Text = $"Anki: {n} today";
            }
            else
            {
                AnkiLabel.Text = "Anki: offline";
            }
        }
        catch
        {
            AnkiLabel.Text = "Anki: —";
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnStartPause(object sender, RoutedEventArgs e) => _c.StartPause();
    private void OnSkip(object sender, RoutedEventArgs e) => _c.Skip();
    private void OnReset(object sender, RoutedEventArgs e) => _c.Reset();
    private void OnHide(object sender, RoutedEventArgs e) => Hide();
    private void OnSettings(object sender, RoutedEventArgs e) => App.Instance.ShowMain();
}
