using System.Windows.Threading;
using TaskFirst.Models;

namespace TaskFirst.Services;

public enum PomodoroPhase { Work, ShortBreak, LongBreak }

/// <summary>Drives the Pomodoro state machine. UI-thread <see cref="DispatcherTimer"/> based.</summary>
public sealed class PomodoroController
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private PomodoroSettings _settings;

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Work;
    public TimeSpan Remaining { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>Completed work sessions in the current long-break cycle.</summary>
    public int CompletedWorkSessions { get; private set; }

    /// <summary>Total work sessions completed since launch.</summary>
    public int TotalWorkSessions { get; private set; }

    public event Action? Tick;
    public event Action<PomodoroPhase>? PhaseChanged;

    public PomodoroController(PomodoroSettings settings)
    {
        _settings = settings;
        Remaining = DurationFor(Phase);
        _timer.Tick += (_, _) => OnSecond();
    }

    public void UpdateSettings(PomodoroSettings settings)
    {
        _settings = settings;
        if (!IsRunning) Remaining = DurationFor(Phase);
        Tick?.Invoke();
    }

    public void StartPause()
    {
        if (IsRunning) { _timer.Stop(); IsRunning = false; }
        else { _timer.Start(); IsRunning = true; }
        Tick?.Invoke();
    }

    public void Reset()
    {
        _timer.Stop();
        IsRunning = false;
        Remaining = DurationFor(Phase);
        Tick?.Invoke();
    }

    /// <summary>Skip to the next phase immediately.</summary>
    public void Skip() => Advance(countAsCompleted: false);

    private void OnSecond()
    {
        Remaining -= TimeSpan.FromSeconds(1);
        if (Remaining <= TimeSpan.Zero)
            Advance(countAsCompleted: true);
        else
            Tick?.Invoke();
    }

    private void Advance(bool countAsCompleted)
    {
        var previous = Phase;
        if (previous == PomodoroPhase.Work)
        {
            if (countAsCompleted)
            {
                CompletedWorkSessions++;
                TotalWorkSessions++;
            }
            bool longDue = CompletedWorkSessions >= Math.Max(1, _settings.CyclesBeforeLongBreak);
            Phase = longDue ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak;
            if (longDue) CompletedWorkSessions = 0;
        }
        else
        {
            Phase = PomodoroPhase.Work;
        }

        Remaining = DurationFor(Phase);
        PhaseChanged?.Invoke(Phase);
        Tick?.Invoke();

        try { System.Media.SystemSounds.Asterisk.Play(); } catch { /* ignore */ }
    }

    private TimeSpan DurationFor(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Work => TimeSpan.FromMinutes(Math.Max(1, _settings.WorkMinutes)),
        PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(Math.Max(1, _settings.ShortBreakMinutes)),
        PomodoroPhase.LongBreak => TimeSpan.FromMinutes(Math.Max(1, _settings.LongBreakMinutes)),
        _ => TimeSpan.FromMinutes(25),
    };

    public double Progress
    {
        get
        {
            var total = DurationFor(Phase).TotalSeconds;
            return total <= 0 ? 0 : 1.0 - (Remaining.TotalSeconds / total);
        }
    }
}
