namespace TaskFirst.Models;

/// <summary>
/// PRO feature. Restricts when a rule is active. When disabled, the rule is always active.
/// Times are minutes-from-midnight, local time. Supports overnight ranges (start &gt; end).
/// </summary>
public sealed class RuleSchedule
{
    public bool Enabled { get; set; } = false;

    /// <summary>Days the rule applies. Index 0 = Sunday … 6 = Saturday.</summary>
    public bool[] Days { get; set; } = { true, true, true, true, true, true, true };

    public int StartMinutes { get; set; } = 9 * 60;   // 09:00
    public int EndMinutes { get; set; } = 17 * 60;    // 17:00

    public bool IsActiveAt(DateTime now)
    {
        if (!Enabled) return true;

        int mins = now.Hour * 60 + now.Minute;
        int today = (int)now.DayOfWeek;           // Sunday = 0
        int yesterday = (today + 6) % 7;

        if (StartMinutes <= EndMinutes)
        {
            // Same-day window, e.g. 09:00–17:00.
            return DayOn(today) && mins >= StartMinutes && mins < EndMinutes;
        }

        // Overnight window, e.g. 22:00–06:00 — belongs to the day it started on.
        if (DayOn(today) && mins >= StartMinutes) return true;
        if (DayOn(yesterday) && mins < EndMinutes) return true;
        return false;
    }

    private bool DayOn(int dayIndex) =>
        dayIndex >= 0 && dayIndex < Days.Length && Days[dayIndex];

    public string Describe()
    {
        if (!Enabled) return "Always";
        string[] names = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        var on = Enumerable.Range(0, 7).Where(DayOn).Select(i => names[i]);
        return $"{FormatMin(StartMinutes)}–{FormatMin(EndMinutes)} · {string.Join(" ", on)}";
    }

    private static string FormatMin(int m) => $"{m / 60:00}:{m % 60:00}";
}
