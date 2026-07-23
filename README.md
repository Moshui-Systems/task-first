<h1 align="center">TaskFirst</h1>

<p align="center"><strong>Earn your distractions.</strong> A Windows focus blocker that
<em>minimizes</em> distractions instead of killing them — and only unlocks apps once you've
finished your Anki reviews. With a floating Pomodoro timer.</p>

<p align="center">
  <a href="https://github.com/Moshui-Systems/task-first/actions/workflows/ci.yml"><img src="https://github.com/Moshui-Systems/task-first/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/Moshui-Systems/task-first/releases/latest"><img src="https://img.shields.io/github/v/release/Moshui-Systems/task-first?display_name=tag" alt="Release"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0a7bbb" alt="Windows">
  <img src="https://img.shields.io/badge/.NET-8.0-512bd4" alt=".NET 8">
</p>

<p align="center">
  <a href="https://github.com/Moshui-Systems/task-first/releases/latest"><b>⬇ Download</b></a> ·
  <a href="https://moshui-systems.github.io/task-first/"><b>🌐 Website</b></a> ·
  <a href="MONETIZATION.md"><b>💳 Sell it</b></a>
</p>

---

A distraction blocker for Windows that **minimizes** the windows of blocked apps instead of
killing them — and can *unlock* an app only once you've earned it, starting with **Anki**
("finish 100 cards / clear the deck before Steam opens"). Ships with a floating Pomodoro widget.

> Philosophy: it never closes your programs. If a blocked window comes to the front, it just
> gets pushed back down (minimized). No lost work, no killed processes — just friction.

---

## Features

- **Minimize-not-kill blocking.** Detects when a blocked app's window comes to the foreground
  and minimizes it. Uses a lightweight system event hook (no injected DLLs), plus a backup poll.
- **Anki unlock gate.** A rule stays locked until your Anki goal is met:
  - *N cards reviewed today* (whole collection or a specific deck), and/or
  - *deck fully cleared* (0 cards due).
  - You confirm progress simply by having Anki open and doing your reviews — TaskFirst reads the
    live count from AnkiConnect. When the goal is hit, the app unlocks automatically.
- **Hard block mode.** Uncheck the gate to make a rule a pure Cold-Turkey block (never unlocks).
- **Title matching.** Optionally only block a browser when the title contains e.g. `youtube` so
  the rest of your browsing is untouched.
- **Floating Pomodoro.** Always-on-top, draggable, work/break cycles, and a live "cards reviewed
  today" readout from Anki.
- **System tray.** Toggle blocking, re-check Anki, show/hide the widget, quit.
- **Start with Windows** (optional).

### Free vs Pro

| | Free | Pro |
|---|---|---|
| Minimize-not-kill blocking, Anki gate, Pomodoro | ✅ | ✅ |
| Blocking rules | 2 | Unlimited |
| **Schedules** — block only during set hours/days | — | ✅ |
| **Tamper-lock** — password required to disable blocking or quit | — | ✅ |

Upgrade in-app (**Upgrade to Pro** button / tray menu) by pasting a license key. Licensing is
offline and cryptographically signed — see [MONETIZATION.md](MONETIZATION.md) for selling and
[LicenseTool](tools/LicenseTool) for minting keys.

---

## Install

**Recommended (auto-start as admin):** from a checkout of this repo, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

It self-elevates (one UAC prompt), builds a self-contained exe, installs it to
`%LOCALAPPDATA%\Programs\TaskFirst`, adds a Start-Menu shortcut, and registers a **Scheduled Task**
that launches TaskFirst **at every logon with administrator rights — no per-login UAC prompt**.
Remove it any time with `.\uninstall.ps1` (your settings are kept). 

> **Runs as administrator by default.** TaskFirst requests elevation so it can minimize windows
> owned by other admin apps and resist being killed from a normal-rights Task Manager. Launching it
> manually shows the normal UAC prompt; the logon Scheduled Task starts it elevated silently.

**From a release:** download the latest `TaskFirst-vX.Y.Z-win-x64.zip` from the repo's
[Releases](https://github.com/Moshui-Systems/task-first/releases), unzip, run `TaskFirst.exe`
(self-contained — no .NET install needed). To enable elevated auto-start, run it once and tick
**Start with Windows (admin)**, or run `TaskFirst.exe --install` from an admin prompt.

**From source:** see below.

---

## Requirements

- Windows 10/11 (built and tested on Windows 11).
- .NET 8 Desktop Runtime (the SDK is already on this machine).
- **Anki** with the **AnkiConnect** add-on for the unlock feature:
  1. In Anki: *Tools → Add-ons → Get Add-ons…*
  2. Paste code **`2055492159`**, restart Anki.
  3. AnkiConnect then listens on `http://127.0.0.1:8765` whenever Anki is open.

The blocker works without Anki too — any rule with the gate unchecked is a hard block.

---

## Build & run

```powershell
cd c:\Users\anton\Downloads\task-first
dotnet build                       # compile
dotnet run                         # run with the settings window
dotnet run -- --tray               # run minimized to tray (used by "start with Windows")
```

The compiled app is at `bin\Debug\net8.0-windows\TaskFirst.exe`.
Config is stored at `%AppData%\TaskFirst\config.json`.

To make a standalone folder you can pin/launch without the SDK:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

---

## How to use

1. Launch it — the **Settings** window opens.
2. **Add a rule** (left panel). Give it a name.
3. Under **App / process names to block**, list process names (one per line), e.g.
   `steam`, `chrome`, `discord`. Matching is case-insensitive substring on the process name
   (no `.exe`). Tip: open Task Manager → *Details* to find a process name.
4. (Optional) **window title contains** — only block when the front window's title contains one
   of these, e.g. `youtube`.
5. **Unlock condition (Anki):**
   - Leave the gate **checked** and set *Min cards today* (e.g. 100) and/or a *Deck*.
   - Click **Load decks from Anki** to pick a deck from a dropdown (Anki must be open).
   - Click **Test now** to see live status ("34/100 cards done…").
   - **Uncheck** the gate for a permanent hard block.
6. **Save.** Make sure **Blocking active** (bottom bar) is on.
7. Now, opening a blocked app just bounces its window back to the taskbar until your Anki goal
   is met. A tray balloon tells you why.

Close the Settings window and TaskFirst keeps running in the tray. Right-click the tray icon
for quick actions.

### Pomodoro
Bottom bar → **Show Pomodoro** (or tray → *Show / hide Pomodoro*). Drag it anywhere; it remembers
its spot. Start/Pause/Skip/Reset; it auto-advances work → short/long breaks and plays a chime.

---

## How it works (architecture)

| Piece | File | Role |
|-------|------|------|
| Window watcher | `Native/WindowWatcher.cs` | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` fires when a window comes to front; a `DispatcherTimer` poll is a safety net. |
| Blocking engine | `Services/BlockingEngine.cs` | Matches the front window to a rule, checks the cached gate result, and `ShowWindowAsync(SW_MINIMIZE)` if locked. |
| Anki client | `Anki/AnkiConnectClient.cs` | Talks to AnkiConnect (`findCards`, `getNumCardsReviewedToday`, `deckNames`). |
| Anki gate | `Anki/AnkiGate.cs` | Turns a rule's requirement into an unlocked/locked verdict, with short-TTL caching. |
| Pomodoro | `Services/PomodoroController.cs` + `UI/PomodoroWindow.xaml` | State machine + floating widget. |
| Config | `Services/ConfigStore.cs`, `Models/*` | JSON persistence in `%AppData%`. |
| Shell | `App.xaml.cs` | Tray icon, window lifetime, wiring. |

**Decision policy:** to avoid a "flash of allowed" the engine treats unknown gate state as
**locked** and refreshes the real result in the background on a short TTL. So blocking is instant;
unlocking takes effect within a few seconds of finishing your cards.

---

## Known limitations

- Cannot minimize windows owned by **elevated (admin)** processes unless TaskFirst also runs
  elevated. (See `app.manifest`.)
- Matching is by process/title, not a strict allow-list of executables — deliberately simple.
- It is *friction*, not a hard lock: a determined user can quit it from the tray. That's by design
  (never kill user processes / never trap the user).

---

## Roadmap ideas

- Schedules (block windows only during certain hours).
- More gates: a minimum focus-time from the Pomodoro, a step counter, a "type this sentence"
  speed-bump, git-commit count, etc. (the `AnkiGate` shape generalizes to an `IGate` interface).
- A tamper-resistant mode (run as a service) for people who want real Cold-Turkey strictness.
- Per-rule schedules and stats.
