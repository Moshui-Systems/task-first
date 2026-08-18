namespace TaskFirst.Models;

/// <summary>
/// Conditions that unlock a blocked app. Requirements are combined with AND.
/// Anki is the first external integration; Pomodoro is tracked locally.
/// </summary>
public sealed class AnkiGateConfig
{
    /// <summary>If false, the rule is a hard block (Cold-Turkey style, never unlocks).</summary>
    public bool Enabled { get; set; } = true;

    public string AnkiConnectUrl { get; set; } = "http://127.0.0.1:8765";

    /// <summary>Deck to check. Empty = whole collection (all decks).</summary>
    public string DeckName { get; set; } = "";

    /// <summary>Minimum distinct cards reviewed today required to unlock. 0 disables this check.</summary>
    public int MinCardsReviewedToday { get; set; } = 100;

    /// <summary>If true, also requires the deck to have 0 cards currently due.</summary>
    public bool RequireDeckCleared { get; set; } = false;

    /// <summary>Completed focus sessions required since TaskFirst was opened.</summary>
    public int RequiredPomodoros { get; set; } = 0;

    public bool HasAnkiRequirement => MinCardsReviewedToday > 0 || RequireDeckCleared;
    public bool HasAnyRequirement => HasAnkiRequirement || RequiredPomodoros > 0;
}
