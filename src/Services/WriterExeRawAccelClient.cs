using System;
using System.Diagnostics;
using System.IO;

namespace UniversalSensRandomizer.Services;

public sealed class WriterExeRawAccelClient : IRawAccelClient
{
    private readonly string writerExePath;

    public WriterExeRawAccelClient(string writerExePath)
    {
        this.writerExePath = writerExePath;
    }

    public static string? FindWriterExe()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "writer", "writer.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "rawaccel", "writer.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "rawaccel", "writer.exe"),
        ];
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    public byte[] Read()
    {
        throw new NotSupportedException("writer.exe does not support reading from the driver. Use IoctlRawAccelClient for reads.");
    }

    public void Write(byte[] buffer)
    {
        // writer.exe consumes a JSON settings file rather than a raw driver buffer.
        // The composite client reconstructs JSON from the snapshot before invoking us; this lower-level client does not handle that directly.
        // Caller must supply a path to a settings.json file in lieu of binary buffer is not feasible here, so this method is intentionally unused
        // when the caller routes through FallbackRawAccelClient — which serializes JSON itself and shells out via WriteJsonFile below.
        throw new NotSupportedException("Use WriteJsonFile instead.");
    }

    public void WriteJsonFile(string settingsJsonPath)
    {
        if (!File.Exists(writerExePath))
        {
            throw new FileNotFoundException("writer.exe not found.", writerExePath);
        }

        ProcessStartInfo psi = new()
        {
            FileName = writerExePath,
            Arguments = "\"" + settingsJsonPath + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(writerExePath) ?? AppContext.BaseDirectory,
        };

        using Process? process = Process.Start(psi);
        if (process is null)
        {
            throw new IOException("Failed to start writer.exe.");
        }

        if (!process.WaitForExit(10_000))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }
            throw new TimeoutException("writer.exe did not exit within 10 seconds.");
        }

        if (process.ExitCode != 0)
        {
            string stderr = process.StandardError.ReadToEnd();
            throw new IOException($"writer.exe exited with code {process.ExitCode}. {stderr}");
        }
    }
}
