# UniversalSensRandomizer

Randomizes your mouse sensitivity at the RawAccel driver level so the change applies system-wide and to any game.

**Download the [latest release here](https://github.com/TorjeAmundsen/UniversalSensRandomizer/releases/latest).**

<img width="524" height="280" alt="UniversalSensRandomizer_gXcmBBmFmk" src="https://github.com/user-attachments/assets/5e2ef750-6ed0-4b9e-9519-96bb8750e942" />

## Anti-cheat safety

This tool only writes to the official RawAccel driver via its public IOCTL interface (the same API the official RawAccel writer uses). It does not inject into game processes, hook input APIs, modify game memory, or touch any anti-cheat protected surface. Sensitivity changes happen below the application layer in the same way they would if you opened the RawAccel GUI and applied a profile manually.

## Prerequisite

Install RawAccel from [rawaccel.net](https://rawaccel.net/).

Tested on **RawAccel 1.7.1**. Other versions may work but no promises. If a version doesn't work, open a GitHub issue with your RawAccel version and I'll look at adding support.

## Usage

- **Base cm/360** - your normal cm/360. Display only, used to show effective cm/360 in the live output.
- **Min / Max multiplier** - randomization range. 1.0 = unchanged, 0.5 = half sens, 2.0 = double sens.
- **Hotkey** - global hotkey that randomizes once. `⊘` button clears it.
- **Enable timer + Interval** - auto-rerandomize every N seconds (min 1.5s).
- **Randomize once** - fire one randomize.
- **Start / Stop randomizer** - start or stop the timer. Stop also restores your original sens.

Sampling is log-uniform, so 0.5x↔1.0x and 1.0x↔2.0x are equally likely. No skew to high sens.

When RawAccel is mid-write, the UI shows "Waiting for 1000ms RawAccel delay..." for ~1 second.

## Twitch integration

Viewers can spend channel points to randomize your sens. Requires **Twitch Affiliate or Partner** (channel point rewards aren't available below that).

- **Connect to Twitch** - opens browser, asks you to authorize. Token is stored DPAPI-encrypted next to settings.
- **Create reward** - creates a channel point reward owned by this app. Required for refunds to work (Twitch only lets the app that created a reward refund its redemptions). Existing manually-created rewards aren't selectable.
- **Reward picker** - shows only rewards this app can manage. Use **Refresh** if you create more outside the app.
- **Enable** - master switch for acting on redemptions.
- **Cooldown** - minimum seconds between randomizes triggered by redemptions.
- **Queue cap** - if more than N redemptions are queued, extras are dropped.

The reward is auto-paused on Twitch (shows as "Unavailable" to viewers) when the randomizer is stopped or **Enable** is unchecked, so viewers can't spend points on something that won't fire. Redemptions that arrive while paused/disabled are refunded automatically.

Token expires after ~60 days; you'll see a "Token expired" status and need to click Connect again.

## OBS overlay

Current multiplier + cm/360 is written to `current_sensitivity.txt` next to the exe on every change. Point an OBS **Text (GDI+)** source at it with "Read from file" for a live overlay.

## Settings

Auto-saved on close to `%APPDATA%\UniversalSensRandomizer\settings.json`.

## Build

Requires .NET 10 SDK on Windows.

```
.\build.ps1
```

`--zip` packages a release zip. `--debug` keeps symbols.
