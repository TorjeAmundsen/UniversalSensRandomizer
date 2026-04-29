using System;
using Avalonia;

namespace UniversalSensRandomizer;

public static class Program
{
    public static bool NoDriverMode { get; private set; }

    [System.STAThread]
    public static int Main(string[] args)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, "--no-driver", StringComparison.OrdinalIgnoreCase))
            {
                NoDriverMode = true;
            }
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
