namespace TaskFirst.Models;

public sealed class AppConfig
{
    public List<BlockRule> Rules { get; set; } = new();

    /// <summary>Backup sweep interval (ms) that catches windows the foreground hook missed.</summary>
    public int PollIntervalMs { get; set; } = 1500;

    /// <summary>How long an Anki gate result is cached before we re-query AnkiConnect.</summary>
    public int AnkiCacheSeconds { get; set; } = 20;

    /// <summary>Master switch: is blocking currently active?</summary>
    public bool BlockingEnabled { get; set; } = true;

    public bool StartWithWindows { get; set; } = false;

    public PomodoroSettings Pomodoro { get; set; } = new();

    /// <summary>Create a sensible starter config on first run.</summary>
    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            Rules =
            {
                new BlockRule
                {
                    Name = "Games — do 100 cards first",
                    Enabled = false,
                    ProcessPatterns = { "steam", "epicgameslauncher" },
                    Gate = new AnkiGateConfig
                    {
                        Enabled = true,
                        DeckName = "",
                        MinCardsReviewedToday = 100,
                    },
                },
            },
        };
    }
}
