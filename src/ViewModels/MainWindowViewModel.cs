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
    private double timerIntervalSeconds;

    [ObservableProperty]
    private bool timerEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartStopCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartStopCommand))]
    private bool isWaitingForDriver;

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
        TimerIntervalSeconds = Math.Max(1.5, initial.TimerIntervalSeconds);
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

    partial void OnMinMultiplierChanged(double value)
    {
        if (value > MaxMultiplier)
        {
            MaxMultiplier = value;
        }
    }

    partial void OnMaxMultiplierChanged(double value)
    {
        if (value < MinMultiplier)
        {
            MinMultiplier = value;
        }
    }

    private bool CanRandomize() => !IsWaitingForDriver;

    [RelayCommand(CanExecute = nameof(CanRandomize))]
    private Task Randomize() => RunRandomizeAsync();

    private async Task RunRandomizeAsync()
    {
        if (IsWaitingForDriver)
        {
            return;
        }
        IsWaitingForDriver = true;
        StatusMessage = "Waiting for 1000ms RawAccel delay...";
        try
        {
            await Task.Run(() => engine.Randomize(MinMultiplier, MaxMultiplier));
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsWaitingForDriver = false;
        }
    }

    private async Task RunApplyMultiplierAsync(double multiplier)
    {
        if (IsWaitingForDriver)
        {
            return;
        }
        IsWaitingForDriver = true;
        StatusMessage = "Waiting for 1000ms RawAccel delay...";
        try
        {
            await Task.Run(() => engine.ApplyMultiplier(multiplier));
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsWaitingForDriver = false;
        }
    }

    private bool CanStartStop() => !IsWaitingForDriver;

    [RelayCommand(CanExecute = nameof(CanStartStop))]
    private async Task StartStop()
    {
        if (IsRunning)
        {
            await StopRunningAsync();
        }
        else
        {
            await StartRunningAsync();
        }
    }

    private async Task StartRunningAsync()
    {
        IsRunning = true;
        if (TimerEnabled && TimerIntervalSeconds > 0)
        {
            await RunRandomizeAsync();
            timer.Start(TimeSpan.FromSeconds(TimerIntervalSeconds), () =>
            {
                Dispatcher.UIThread.Post(() => _ = RunRandomizeAsync());
                return Task.CompletedTask;
            });
        }
    }

    private async Task StopRunningAsync()
    {
        timer.Stop();
        IsRunning = false;
        await RunApplyMultiplierAsync(1.0);
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
            if (IsWaitingForDriver)
            {
                return;
            }
            _ = RunRandomizeAsync();
        });
    }

    private void OnMultiplierChanged(double multiplier, string output)
    {
        Dispatcher.UIThread.Post(() => LiveOutputText = output);
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
