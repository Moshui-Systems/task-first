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

    public bool StartWithWindows { get; set; } = true;

    /// <summary>PRO: when set, disabling blocking or quitting requires this password.</summary>
    public bool TamperLockEnabled { get; set; } = false;

    /// <summary>PBKDF2 "salt.hash" of the tamper-lock password. Empty = not set.</summary>
    public string TamperPasswordHash { get; set; } = "";

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
                    Name = "Earn your game time",
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
