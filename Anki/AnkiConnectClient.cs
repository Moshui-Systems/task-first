using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TaskFirst.Anki;

/// <summary>
/// Thin client for the AnkiConnect add-on (https://foosoft.net/projects/anki-connect/).
/// AnkiConnect listens on http://127.0.0.1:8765 while the Anki desktop app is open.
/// </summary>
public sealed class AnkiConnectClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly string _url;

    public AnkiConnectClient(string url) => _url = string.IsNullOrWhiteSpace(url)
        ? "http://127.0.0.1:8765"
        : url;

    private async Task<JsonNode?> InvokeAsync(string action, object? paramsObj, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["action"] = action,
            ["version"] = 6,
        };
        if (paramsObj is not null)
            payload["params"] = JsonSerializer.SerializeToNode(paramsObj);

        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await Http.PostAsync(_url, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var node = JsonNode.Parse(body);
        var error = node?["error"];
        if (error is not null && error.GetValueKind() != JsonValueKind.Null)
            throw new AnkiException(error.GetValue<string>());

        return node?["result"];
    }

    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        try
        {
            await InvokeAsync("version", null, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetDeckNamesAsync(CancellationToken ct = default)
    {
        var result = await InvokeAsync("deckNames", null, ct).ConfigureAwait(false);
        var list = new List<string>();
        if (result is JsonArray arr)
            foreach (var item in arr)
                if (item is not null) list.Add(item.GetValue<string>());
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>Total distinct cards reviewed today across the whole collection.</summary>
    public async Task<int> GetNumCardsReviewedTodayAsync(CancellationToken ct = default)
    {
        var result = await InvokeAsync("getNumCardsReviewedToday", null, ct).ConfigureAwait(false);
        return result?.GetValue<int>() ?? 0;
    }

    /// <summary>Number of cards matching a search query (see Anki "Browse" search syntax).</summary>
    public async Task<int> FindCardsCountAsync(string query, CancellationToken ct = default)
    {
        var result = await InvokeAsync("findCards", new { query }, ct).ConfigureAwait(false);
        return result is JsonArray arr ? arr.Count : 0;
    }

    /// <summary>Distinct cards reviewed today within a specific deck (and its subdecks).</summary>
    public Task<int> GetCardsReviewedTodayInDeckAsync(string deck, CancellationToken ct = default)
        => FindCardsCountAsync($"deck:{QuoteDeck(deck)} rated:1", ct);

    /// <summary>Cards currently due in a deck (0 == deck cleared for today).</summary>
    public Task<int> GetDueCountInDeckAsync(string deck, CancellationToken ct = default)
        => FindCardsCountAsync(string.IsNullOrWhiteSpace(deck)
            ? "is:due"
            : $"deck:{QuoteDeck(deck)} is:due", ct);

    private static string QuoteDeck(string deck) =>
        deck.Contains(' ') ? $"\"{deck}\"" : deck;
}

public sealed class AnkiException(string message) : Exception(message);
