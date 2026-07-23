using TaskFirst.Models;

namespace TaskFirst.Anki;

public readonly record struct GateResult(
    bool Unlocked,
    int CardsReviewedToday,
    int DueRemaining,
    string Message);

/// <summary>
/// Evaluates an <see cref="AnkiGateConfig"/> against live Anki state and caches the
/// result briefly so foreground events don't spam AnkiConnect.
/// </summary>
public sealed class AnkiGate
{
    private readonly int _cacheSeconds;
    private readonly Dictionary<string, (DateTime When, GateResult Result)> _cache = new();
    private readonly object _lock = new();

    public AnkiGate(int cacheSeconds) => _cacheSeconds = Math.Max(2, cacheSeconds);

    private static string CacheKey(AnkiGateConfig g) =>
        $"{g.AnkiConnectUrl}|{g.DeckName}|{g.MinCardsReviewedToday}|{g.RequireDeckCleared}";

    public void Invalidate()
    {
        lock (_lock) _cache.Clear();
    }

    /// <summary>Cached evaluation. Returns the last known result if still fresh.</summary>
    public async Task<GateResult> EvaluateCachedAsync(AnkiGateConfig gate, CancellationToken ct = default)
    {
        string key = CacheKey(gate);
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached) &&
                (DateTime.UtcNow - cached.When).TotalSeconds < _cacheSeconds)
                return cached.Result;
        }

        var result = await EvaluateFreshAsync(gate, ct).ConfigureAwait(false);
        lock (_lock) _cache[key] = (DateTime.UtcNow, result);
        return result;
    }

    /// <summary>Always hits AnkiConnect. Used by the "Test / Re-check" buttons.</summary>
    public async Task<GateResult> EvaluateFreshAsync(AnkiGateConfig gate, CancellationToken ct = default)
    {
        // A gate that is disabled or has no requirement is a hard block: never unlocks.
        if (!gate.Enabled || !gate.HasAnyRequirement)
            return new GateResult(false, 0, 0, "Hard block — no unlock condition set.");

        var client = new AnkiConnectClient(gate.AnkiConnectUrl);

        if (!await client.IsReachableAsync(ct).ConfigureAwait(false))
            return new GateResult(false, 0, 0, "Anki isn't running (AnkiConnect unreachable). Open Anki to unlock.");

        int reviewed;
        try
        {
            reviewed = string.IsNullOrWhiteSpace(gate.DeckName)
                ? await client.GetNumCardsReviewedTodayAsync(ct).ConfigureAwait(false)
                : await client.GetCardsReviewedTodayInDeckAsync(gate.DeckName, ct).ConfigureAwait(false);
        }
        catch (AnkiException ex)
        {
            return new GateResult(false, 0, 0, $"Anki error: {ex.Message}");
        }

        int due = 0;
        bool clearedOk = true;
        if (gate.RequireDeckCleared)
        {
            try
            {
                due = await client.GetDueCountInDeckAsync(gate.DeckName, ct).ConfigureAwait(false);
                clearedOk = due == 0;
            }
            catch (AnkiException ex)
            {
                return new GateResult(false, reviewed, 0, $"Anki error: {ex.Message}");
            }
        }

        bool countOk = gate.MinCardsReviewedToday <= 0 || reviewed >= gate.MinCardsReviewedToday;
        bool unlocked = countOk && clearedOk;

        string where = string.IsNullOrWhiteSpace(gate.DeckName) ? "all decks" : $"\"{gate.DeckName}\"";
        string msg = unlocked
            ? $"Unlocked ✓  ({reviewed} reviewed today in {where})"
            : BuildLockedMessage(gate, reviewed, due, countOk, clearedOk, where);

        return new GateResult(unlocked, reviewed, due, msg);
    }

    private static string BuildLockedMessage(AnkiGateConfig g, int reviewed, int due,
        bool countOk, bool clearedOk, string where)
    {
        var parts = new List<string>();
        if (!countOk)
            parts.Add($"{reviewed}/{g.MinCardsReviewedToday} cards done in {where}");
        if (!clearedOk)
            parts.Add($"{due} still due in {where}");
        return "Locked — " + string.Join("; ", parts) + ".";
    }
}
