using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversalSensRandomizer.Models;
using UniversalSensRandomizer.Services;
using UniversalSensRandomizer.Services.Twitch;
using UniversalSensRandomizer.Util;

namespace UniversalSensRandomizer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly RandomizerEngine engine;
    private readonly HotkeyService hotkeys;
    private readonly TimerService timer;
    private readonly SettingsStore settingsStore;
    private TwitchIntegrationService? twitch;

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
    [NotifyPropertyChangedFor(nameof(StartStopText))]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand))]
    private bool isWaitingForDriver;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartStopCommand))]
    [NotifyPropertyChangedFor(nameof(StartStopText))]
    private bool isStopping;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartStopCommand))]
    private bool isClosing;

    public string StartStopText =>
        IsStopping ? "Waiting for RawAccel..."
        : IsRunning ? "Stop randomizer"
        : "Start randomizer";

    private readonly SemaphoreSlim driverLock = new(1, 1);

    [ObservableProperty]
    private bool isCapturingHotkey;

    [ObservableProperty]
    private string hotkeyDisplay = "None";

    [ObservableProperty]
    private string liveOutputText = "Not running";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool twitchEnabled;

    [ObservableProperty]
    private string twitchStatusText = "Disconnected";

    [ObservableProperty]
    private string twitchUserLogin = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectTwitchCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectTwitchCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshRewardsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateRewardCommand))]
    private bool twitchConnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectTwitchCommand))]
    private bool twitchBusy;

    [ObservableProperty]
    private string twitchRewardId = "";

    [ObservableProperty]
    private double twitchCooldownSeconds;

    [ObservableProperty]
    private int twitchQueueCap;

    public ObservableCollection<TwitchRewardOption> TwitchRewards { get; } = new();

    [ObservableProperty]
    private TwitchRewardOption? selectedTwitchReward;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateRewardCommand))]
    private string newRewardTitle = "";

    [ObservableProperty]
    private int newRewardCost = 1000;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateRewardCommand))]
    private bool newRewardTitleConflict;

    [ObservableProperty]
    private string newRewardConflictMessage = "";

    [ObservableProperty]
    private bool newRewardSuccess;

    [ObservableProperty]
    private string newRewardSuccessMessage = "";

    private string persistedTwitchProtectedToken = "";
    private string persistedTwitchUserId = "";
    private string persistedTwitchUserLogin = "";

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

        twitchEnabled = initial.TwitchEnabled;
        twitchRewardId = initial.TwitchRewardId;
        twitchCooldownSeconds = Math.Max(1.0, initial.TwitchCooldownSeconds);
        twitchQueueCap = Math.Clamp(initial.TwitchQueueCap, 1, 100);
        persistedTwitchProtectedToken = initial.TwitchProtectedToken;
        persistedTwitchUserId = initial.TwitchUserId;
        persistedTwitchUserLogin = initial.TwitchUserLogin;
        if (!string.IsNullOrEmpty(persistedTwitchUserLogin))
        {
            twitchUserLogin = persistedTwitchUserLogin;
        }

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

    private bool CanRandomize() => IsRunning && !IsWaitingForDriver && !IsStopping && !IsClosing;

    [RelayCommand(CanExecute = nameof(CanRandomize))]
    private Task Randomize() => RunRandomizeAsync();

    private async Task RunRandomizeAsync()
    {
        // Drop overlapping randomize calls (e.g. timer fires while previous still running).
        if (!await driverLock.WaitAsync(0))
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
            driverLock.Release();
        }
    }

    private bool CanStartStop() => !IsStopping && !IsClosing;

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
        }
        ApplyTimerState();
    }

    private void ApplyTimerState()
    {
        if (IsRunning && TimerEnabled && TimerIntervalSeconds > 0)
        {
            timer.Start(TimeSpan.FromSeconds(TimerIntervalSeconds), () =>
            {
                Dispatcher.UIThread.Post(() => _ = RunRandomizeAsync());
                return Task.CompletedTask;
            });
        }
        else
        {
            timer.Stop();
        }
    }

    partial void OnTimerEnabledChanged(bool value)
    {
        if (IsRunning)
        {
            ApplyTimerState();
        }
    }

    partial void OnTimerIntervalSecondsChanged(double value)
    {
        if (IsRunning && TimerEnabled)
        {
            ApplyTimerState();
        }
    }

    private async Task StopRunningAsync()
    {
        timer.Stop();
        IsRunning = false;
        IsStopping = true;
        StatusMessage = "Waiting for 1000ms RawAccel delay...";
        try
        {
            await driverLock.WaitAsync();
            try
            {
                IsWaitingForDriver = true;
                await Task.Run(() => engine.ApplyMultiplier(1.0));
                StatusMessage = string.Empty;
            }
            finally
            {
                IsWaitingForDriver = false;
                driverLock.Release();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsStopping = false;
        }
    }

    private void SaveSettings()
    {
        PersistedSettings settings = new()
        {
            SchemaVersion = 2,
            MinMultiplier = MinMultiplier,
            MaxMultiplier = MaxMultiplier,
            BaseCm360 = BaseCm360,
            TimerIntervalSeconds = TimerIntervalSeconds,
            TimerEnabled = TimerEnabled,
            Hotkey = hotkey,
            TwitchEnabled = TwitchEnabled,
            TwitchProtectedToken = persistedTwitchProtectedToken,
            TwitchUserId = persistedTwitchUserId,
            TwitchUserLogin = persistedTwitchUserLogin,
            TwitchRewardId = TwitchRewardId,
            TwitchCooldownSeconds = TwitchCooldownSeconds,
            TwitchQueueCap = TwitchQueueCap,
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

    [RelayCommand]
    private void ClearHotkey()
    {
        hotkey = HotkeyCombination.None;
        HotkeyDisplay = "Set hotkey";
        IsCapturingHotkey = false;
        hotkeys.Unregister();
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
            if (!IsRunning)
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

    public async Task BeginCloseAsync()
    {
        if (IsClosing)
        {
            return;
        }
        IsClosing = true;
        timer.Stop();
        hotkeys.Unregister();
        LiveOutputText = "Resetting RawAccel...";
        StatusMessage = "Waiting for 1000ms RawAccel delay...";
        try
        {
            await driverLock.WaitAsync();
            try
            {
                IsWaitingForDriver = true;
                await Task.Run(() => engine.RestoreOriginal());
            }
            finally
            {
                IsWaitingForDriver = false;
                driverLock.Release();
            }
        }
        catch
        {
        }
        if (twitch is not null)
        {
            try
            {
                await twitch.DisposeAsync();
            }
            catch
            {
            }
        }
        try
        {
            SaveSettings();
        }
        catch
        {
        }
    }

    public void AttachTwitch(TwitchIntegrationService service)
    {
        twitch = service;
        service.StateChanged += OnTwitchStateChanged;
        service.TokenAcquired += OnTwitchTokenAcquired;
        service.TokenCleared += OnTwitchTokenCleared;
        service.RewardCreated += OnTwitchRewardCreated;
    }

    private void OnTwitchRewardCreated(CustomReward reward)
    {
        Dispatcher.UIThread.Post(() =>
        {
            NewRewardTitle = "";
            NewRewardTitleConflict = false;
            NewRewardConflictMessage = "";
            NewRewardSuccessMessage = $"Created '{reward.Title}'";
            NewRewardSuccess = true;
        });
    }

    private void OnTwitchStateChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (twitch is null)
            {
                return;
            }
            TwitchStatusText = twitch.StatusText;
            TwitchUserLogin = twitch.UserLogin;
            TwitchConnected = twitch.State == TwitchState.Connected;
            TwitchRewards.Clear();
            foreach (CustomReward r in twitch.AvailableRewards)
            {
                TwitchRewards.Add(new TwitchRewardOption(r.Id, $"{r.Title} ({r.Cost})"));
            }
            if (!string.IsNullOrEmpty(TwitchRewardId))
            {
                SelectedTwitchReward = TwitchRewards.FirstOrDefault(o => o.Id == TwitchRewardId);
            }
            UpdateTitleConflict();
        });
    }

    private void OnTwitchTokenAcquired(AuthResult result)
    {
        Dispatcher.UIThread.Post(() =>
        {
            persistedTwitchProtectedToken = TokenProtector.Protect(result.AccessToken);
            persistedTwitchUserId = result.UserId;
            persistedTwitchUserLogin = result.UserLogin;
            TwitchUserLogin = result.UserLogin;
            SaveSettings();
        });
    }

    private void OnTwitchTokenCleared()
    {
        Dispatcher.UIThread.Post(() =>
        {
            persistedTwitchProtectedToken = "";
            persistedTwitchUserId = "";
            persistedTwitchUserLogin = "";
            TwitchUserLogin = "";
            TwitchEnabled = false;
            SaveSettings();
        });
    }

    private bool CanConnectTwitch() => !TwitchConnected && !TwitchBusy;
    private bool CanDisconnectTwitch() => TwitchConnected || TwitchBusy;
    private bool CanRefreshRewards() => TwitchConnected;
    private bool CanCreateReward() => TwitchConnected
        && !string.IsNullOrWhiteSpace(NewRewardTitle)
        && !NewRewardTitleConflict;

    [RelayCommand(CanExecute = nameof(CanConnectTwitch))]
    private async Task ConnectTwitch()
    {
        if (twitch is null)
        {
            return;
        }
        TwitchBusy = true;
        try
        {
            await twitch.ConnectAsync(CancellationToken.None);
        }
        finally
        {
            TwitchBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnectTwitch))]
    private async Task DisconnectTwitch()
    {
        if (twitch is null)
        {
            return;
        }
        await twitch.DisconnectAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanRefreshRewards))]
    private async Task RefreshRewards()
    {
        if (twitch is null)
        {
            return;
        }
        await twitch.RefreshRewardsAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanCreateReward))]
    private async Task CreateReward()
    {
        if (twitch is null || NewRewardCost < 1)
        {
            return;
        }
        await twitch.CreateRewardAsync(NewRewardTitle.Trim(), NewRewardCost, CancellationToken.None);
    }

    partial void OnNewRewardTitleChanged(string value)
    {
        if (NewRewardSuccess)
        {
            NewRewardSuccess = false;
            NewRewardSuccessMessage = "";
        }
        UpdateTitleConflict();
    }

    private void UpdateTitleConflict()
    {
        if (twitch is null)
        {
            NewRewardTitleConflict = false;
            NewRewardConflictMessage = "";
            return;
        }
        string title = NewRewardTitle?.Trim() ?? "";
        if (string.IsNullOrEmpty(title))
        {
            NewRewardTitleConflict = false;
            NewRewardConflictMessage = "";
            return;
        }
        bool conflict = twitch.AllRewardTitles.Contains(title);
        NewRewardTitleConflict = conflict;
        NewRewardConflictMessage = conflict
            ? $"Channel already has a reward titled '{title}' (titles tracked: {twitch.AllRewardTitles.Count})"
            : "";
    }

    partial void OnSelectedTwitchRewardChanged(TwitchRewardOption? value)
    {
        if (value is null)
        {
            return;
        }
        TwitchRewardId = value.Id;
    }

    partial void OnTwitchRewardIdChanged(string value)
    {
        if (twitch is not null)
        {
            twitch.RewardId = value;
            if (twitch.State == TwitchState.Connected && !string.IsNullOrEmpty(value))
            {
                _ = twitch.TryStartListeningAsync(CancellationToken.None);
            }
        }
    }

    partial void OnTwitchCooldownSecondsChanged(double value)
    {
        if (twitch is not null)
        {
            twitch.CooldownSeconds = value;
        }
    }

    partial void OnTwitchQueueCapChanged(int value)
    {
        if (twitch is not null)
        {
            twitch.QueueCap = value;
        }
    }

    partial void OnTwitchEnabledChanged(bool value)
    {
        if (twitch is not null)
        {
            twitch.Enabled = value;
            _ = twitch.SyncRewardStateAsync();
        }
    }

    partial void OnIsRunningChanged(bool value)
    {
        if (twitch is not null)
        {
            _ = twitch.SyncRewardStateAsync();
        }
    }
}

public sealed record TwitchRewardOption(string Id, string Display);
