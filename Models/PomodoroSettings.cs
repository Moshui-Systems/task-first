namespace TaskFirst.Models;

public sealed class PomodoroSettings
{
    public int WorkMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int CyclesBeforeLongBreak { get; set; } = 4;

    /// <summary>Show the floating widget on startup.</summary>
    public bool ShowOnStartup { get; set; } = true;

    /// <summary>Last on-screen position of the widget (-1 = center-ish default).</summary>
    public double Left { get; set; } = -1;
    public double Top { get; set; } = -1;
}
