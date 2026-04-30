using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UniversalSensRandomizer.Models;
using UniversalSensRandomizer.Services;
using UniversalSensRandomizer.Services.Twitch;
using UniversalSensRandomizer.ViewModels;
using UniversalSensRandomizer.Views;

namespace UniversalSensRandomizer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                BootstrapDesktop(desktop);
            }
            catch (System.Exception ex)
            {
                ShowFatalError(desktop, ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void BootstrapDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        SettingsStore settingsStore = new();
        PersistedSettings settings = settingsStore.Load();

        IRawAccelClient client = Program.NoDriverMode
            ? new NoDriverRawAccelClient()
            : new IoctlRawAccelClient();

        SensitivitySnapshot snapshot = BaselineSnapshot.Capture(client);
        BaselineSnapshot baseline = new(snapshot);

        LiveOutputWriter liveOutput = new();
        RandomizerEngine engine = new(client, baseline, liveOutput);
        HotkeyService hotkeys = new();
        TimerService timer = new();

        MainWindowViewModel vm = new(engine, hotkeys, timer, settingsStore, settings);

        TwitchIntegrationService twitch = new(
            TwitchConfig.ClientId,
            engine,
            () => (vm.MinMultiplier, vm.MaxMultiplier),
            () => vm.IsRunning)
        {
            RewardId = settings.TwitchRewardId,
            CooldownSeconds = settings.TwitchCooldownSeconds,
            QueueCap = settings.TwitchQueueCap,
            Enabled = settings.TwitchEnabled,
        };
        vm.AttachTwitch(twitch);

        if (settings.TwitchEnabled)
        {
            string? resumeToken = TokenProtector.Unprotect(settings.TwitchProtectedToken);
            if (!string.IsNullOrEmpty(resumeToken))
            {
                _ = twitch.ResumeAsync(resumeToken, System.Threading.CancellationToken.None);
            }
        }

        MainWindow window = new() { DataContext = vm };
        bool closeFinalized = false;
        window.Closing += async (_, e) =>
        {
            if (closeFinalized)
            {
                return;
            }
            e.Cancel = true;
            await vm.BeginCloseAsync();
            closeFinalized = true;
            window.Close();
        };

        desktop.MainWindow = window;
        desktop.ShutdownRequested += (_, _) =>
        {
            if (!closeFinalized)
            {
                try
                {
                    engine.RestoreOriginal();
                }
                catch
                {
                }
            }
            hotkeys.Dispose();
            timer.Dispose();
            _ = twitch.DisposeAsync();
        };
    }

    private static void ShowFatalError(IClassicDesktopStyleApplicationLifetime desktop, System.Exception ex)
    {
        FatalErrorWindow errorWindow = new(ex.Message);
        desktop.MainWindow = errorWindow;
    }
}
