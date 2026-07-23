namespace TaskFirst.Models;

/// <summary>
/// One blocking rule: which apps/windows it targets and the gate that unlocks them.
/// </summary>
public sealed class BlockRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "New rule";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Process names to match (case-insensitive substring, without ".exe").
    /// e.g. "chrome", "steam", "discord".
    /// </summary>
    public List<string> ProcessPatterns { get; set; } = new();

    /// <summary>
    /// Optional window-title patterns (case-insensitive substring). A window matches the
    /// rule if its process matches AND (there are no title patterns OR one title pattern hits).
    /// Lets you block "youtube.com" inside a browser while leaving other tabs alone.
    /// </summary>
    public List<string> TitlePatterns { get; set; } = new();

    public AnkiGateConfig Gate { get; set; } = new();

    /// <summary>PRO: restrict the hours/days this rule is active. Disabled = always active.</summary>
    public RuleSchedule Schedule { get; set; } = new();
}
