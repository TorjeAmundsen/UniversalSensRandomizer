using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversalSensRandomizer.Models;
using UniversalSensRandomizer.Services;
using UniversalSensRandomizer.Util;

namespace UniversalSensRandomizer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly RandomizerEngine engine;
    private readonly HotkeyService hotkeys;
    private readonly TimerService timer;
    private readonly SettingsStore settingsStore;

    [ObservableProperty]
    private double baseCm360;

    [ObservableProperty]
    private double minMultiplier;

    [ObservableProperty]
    private double maxMultiplier;

    [ObservableProperty]
    private int timerIntervalSeconds;

    [ObservableProperty]
    private bool timerEnabled;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private bool isDisabled;

    [ObservableProperty]
    private bool isCapturingHotkey;

    [ObservableProperty]
    private string hotkeyDisplay = "None";

    [ObservableProperty]
    private string liveOutputText = "Not running";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    private HotkeyCombination hotkey = HotkeyCombination.None;

    public MainWindowViewModel(
        RandomizerEngine engine,
        HotkeyService hotkeys,
        TimerService timer,
        SettingsStore settingsStore,
        PersistedSettings initial)
    {
        this.engine = engine;
        this.hotkeys = hotkeys;
        this.timer = timer;
        this.settingsStore = settingsStore;

        BaseCm360 = initial.BaseCm360;
        MinMultiplier = initial.MinMultiplier;
        MaxMultiplier = initial.MaxMultiplier;
        TimerIntervalSeconds = initial.TimerIntervalSeconds;
        TimerEnabled = initial.TimerEnabled;
        hotkey = initial.Hotkey;
        hotkeyDisplay = hotkey.IsEmpty ? "Set hotkey" : hotkey.ToDisplayString();

        engine.BaseCm360 = BaseCm360;
        engine.MultiplierChanged += OnMultiplierChanged;
        hotkeys.HotkeyPressed += OnHotkeyPressed;

        if (!hotkey.IsEmpty)
        {
            hotkeys.Register(hotkey);
        }
    }

    public HotkeyCombination Hotkey => hotkey;

    partial void OnBaseCm360Changed(double value)
    {
        engine.BaseCm360 = value;
    }

    [RelayCommand]
    private void Randomize()
    {
        if (IsDisabled)
        {
            return;
        }
        TryWithStatus(() => engine.Randomize(MinMultiplier, MaxMultiplier));
    }

    [RelayCommand]
    private void StartStop()
    {
        if (IsRunning)
        {
            StopRunning();
        }
        else
        {
            StartRunning();
        }
    }

    private void StartRunning()
    {
        IsRunning = true;
        IsDisabled = false;
        if (TimerEnabled && TimerIntervalSeconds > 0)
        {
            TryWithStatus(() => engine.Randomize(MinMultiplier, MaxMultiplier));
            timer.Start(TimeSpan.FromSeconds(TimerIntervalSeconds), () =>
            {
                if (!IsDisabled)
                {
                    Dispatcher.UIThread.Post(() => TryWithStatus(() => engine.Randomize(MinMultiplier, MaxMultiplier)));
                }
                return Task.CompletedTask;
            });
        }
    }

    private void StopRunning()
    {
        timer.Stop();
        IsRunning = false;
    }

    [RelayCommand]
    private void ToggleDisable()
    {
        IsDisabled = !IsDisabled;
        if (IsDisabled)
        {
            timer.Stop();
            TryWithStatus(() =>
            {
                engine.ApplyMultiplier(1.0);
            });
            LiveOutputText = InvariantFormat.LiveOutput(1.0, BaseCm360);
        }
        else if (IsRunning && TimerEnabled && TimerIntervalSeconds > 0)
        {
            timer.Start(TimeSpan.FromSeconds(TimerIntervalSeconds), () =>
            {
                Dispatcher.UIThread.Post(() => TryWithStatus(() => engine.Randomize(MinMultiplier, MaxMultiplier)));
                return Task.CompletedTask;
            });
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        PersistedSettings settings = new()
        {
            SchemaVersion = 1,
            MinMultiplier = MinMultiplier,
            MaxMultiplier = MaxMultiplier,
            BaseCm360 = BaseCm360,
            TimerIntervalSeconds = TimerIntervalSeconds,
            TimerEnabled = TimerEnabled,
            Hotkey = hotkey,
        };
        try
        {
            settingsStore.Save(settings);
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void CaptureHotkey()
    {
        IsCapturingHotkey = true;
        HotkeyDisplay = "Press a key…";
    }

    public void ApplyCapturedHotkey(uint virtualKey, HotkeyModifiers modifiers)
    {
        hotkey = new HotkeyCombination(virtualKey, modifiers);
        HotkeyDisplay = hotkey.ToDisplayString();
        IsCapturingHotkey = false;
        hotkeys.Register(hotkey);
    }

    public void CancelCapture()
    {
        IsCapturingHotkey = false;
        HotkeyDisplay = hotkey.IsEmpty ? "Set hotkey" : hotkey.ToDisplayString();
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsDisabled)
            {
                return;
            }
            TryWithStatus(() => engine.Randomize(MinMultiplier, MaxMultiplier));
        });
    }

    private void OnMultiplierChanged(double multiplier, string output)
    {
        Dispatcher.UIThread.Post(() => LiveOutputText = output);
    }

    private void TryWithStatus(Action action)
    {
        try
        {
            action();
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public void OnClosing()
    {
        timer.Stop();
        hotkeys.Unregister();
        try
        {
            engine.RestoreOriginal();
        }
        catch
        {
        }
        try
        {
            SaveSettings();
        }
        catch
        {
        }
    }
}
