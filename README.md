# UniversalSensRandomizer

Randomizes your mouse sensitivity at the RawAccel driver level so the change applies system-wide and to any game.

## Anti-cheat safety

This tool only writes to the official RawAccel driver via its public IOCTL interface (the same API the official RawAccel writer uses). It does not inject into game processes, hook input APIs, modify game memory, or touch any anti-cheat protected surface. Sensitivity changes happen below the application layer in the same way they would if you opened the RawAccel GUI and applied a profile manually.

## Prerequisite

You must have **RawAccel 1.7.1** installed (driver and GUI). Grab it from the official RawAccel releases page at [rawaccel.net](https://rawaccel.net/).

This program reads RawAccel's settings struct directly. The struct layout can change between RawAccel versions, so any other version may not work. If you are running a different version, install 1.7.1 alongside it (or roll back) before using this tool.

If you have never opened the RawAccel GUI before, the driver has no settings loaded yet. The tool will detect this and start from a neutral 1.0x baseline. If you have already configured RawAccel, the tool uses your current settings as the baseline and only modifies the output sensitivity multiplier.

## How it works

The randomizer multiplies your existing RawAccel `output_dpi` value by a random factor sampled within the range you configure. All other RawAccel settings (acceleration curves, smoothing, polling, etc.) are left untouched. When you stop the randomizer or close the app, your original sensitivity is restored.

The name `output_dpi` is misleading - it is **not** your mouse hardware DPI. RawAccel uses it as a post-acceleration sensitivity multiplier in a normalized 1000-DPI space, where a value of 1000 corresponds to 1.0x sensitivity (neutral). 2000 means 2.0x, 500 means 0.5x, and so on. Your real mouse DPI is independent of this.

A multiplier of 1.0 means no change. 0.5 means half sensitivity, 2.0 means double sensitivity.

Sampling is **log-uniform**, meaning the perceived change is evenly distributed across the range. A range of 0.5x to 2.0x feels balanced (50% chance below 1.0x, 50% above), instead of skewing high.

## UI

- **Base cm/360 (at 1.00x)** - the cm/360 you measured at your normal sensitivity. **Display only** - this value is shown next to the multiplier in the live output so you know what your current effective cm/360 is. It does not affect randomization or anything written to the driver.
- **Min multiplier** / **Max multiplier** - the randomization range. The min cannot be set higher than the max (and vice versa).
- **Hotkey** - global hotkey that triggers a random sensitivity. Click the field, press the key combination you want. Click the circled-slash button to clear it.
- **Enable timer** + **Interval (seconds)** - automatically rerandomize every N seconds while the randomizer is running. Minimum 1.5 seconds (RawAccel's internal write delay is 1 second; values lower than this would queue up).
- **Randomize once** - randomize immediately, regardless of the timer.
- **Start randomizer** - begin the timer-driven randomization (also fires once immediately if the timer is enabled).
- **Stop randomizer** - restore your original sensitivity (1.0x baseline) and stop the timer.
- **Live output panel** - shows the current multiplier and effective cm/360.

While RawAccel is applying a change, the driver enforces a 1-second write delay. The UI shows "Waiting for 1000ms RawAccel delay..." during this window. The randomizer button is greyed out while waiting; the stop button stays clickable and queues a reset for as soon as the current write completes.

## OBS / streaming overlay

Each time the multiplier changes, the current value (and effective cm/360) is also written to `current_sensitivity.txt` next to the executable. Point an OBS **Text (GDI+)** source at that file via "Read from file" to display your current sensitivity live on stream. The file is overwritten atomically on every update.

## Settings

Settings are saved automatically when you close the app. The file lives at:

```
%APPDATA%\UniversalSensRandomizer\settings.json
```

## Building from source

Requires the .NET 10 SDK and Windows. From the project root:

```
.\build.ps1
```

This produces a Native AOT single-file build at `build/win-x64/`. Add `--zip` to package it as a release zip, or `--debug` to keep native debug symbols.
