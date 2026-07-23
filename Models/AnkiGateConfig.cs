namespace TaskFirst.Models;

/// <summary>
/// The condition that "unlocks" a blocked app. When enabled, the app stays
/// blocked (windows get minimized) until the Anki requirements are satisfied.
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

    public bool HasAnyRequirement => MinCardsReviewedToday > 0 || RequireDeckCleared;
}
