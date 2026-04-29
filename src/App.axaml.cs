using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UniversalSensRandomizer.Models;
using UniversalSensRandomizer.Services;
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

        IRawAccelClient client;
        if (Program.NoDriverMode)
        {
            client = new NoDriverRawAccelClient();
        }
        else
        {
            client = new FallbackRawAccelClient(new IoctlRawAccelClient());
        }

        SensitivitySnapshot snapshot = BaselineSnapshot.Capture(client);
        BaselineSnapshot baseline = new(snapshot);

        LiveOutputWriter liveOutput = new();
        RandomizerEngine engine = new(client, baseline, liveOutput);
        HotkeyService hotkeys = new();
        TimerService timer = new();

        MainWindowViewModel vm = new(engine, hotkeys, timer, settingsStore, settings);
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
            // Restore is handled by the window Closing handler (BeginCloseAsync) so
            // we don't repeat the 1000ms WRITE_DELAY here. Only dispose plumbing.
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
        };
    }

    private static void ShowFatalError(IClassicDesktopStyleApplicationLifetime desktop, System.Exception ex)
    {
        FatalErrorWindow errorWindow = new(ex.Message);
        desktop.MainWindow = errorWindow;
    }
}
