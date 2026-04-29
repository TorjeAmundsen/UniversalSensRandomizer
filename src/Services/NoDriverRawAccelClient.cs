using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using UniversalSensRandomizer.Interop;

namespace UniversalSensRandomizer.Services;

public sealed class NoDriverRawAccelClient : IRawAccelClient
{
    private readonly string logPath;
    private readonly object syncRoot = new();

    public NoDriverRawAccelClient()
    {
        logPath = Path.Combine(AppContext.BaseDirectory, "rawaccel_log.txt");
        Log("--no-driver mode active. Driver calls will be logged, not executed.");
    }

    public string LogPath => logPath;

    public byte[] Read()
    {
        Log("Read() called → returning synthetic baseline (1 modifier, 0 devices, output_dpi=1000).");

        int total = RawAccelLayout.IoBaseSize + RawAccelLayout.ModifierSettingsSize;
        byte[] buffer = new byte[total];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(RawAccelLayout.ModifierDataSizeOffset), 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(RawAccelLayout.DeviceDataSizeOffset), 0u);

        int outputDpiOffset = RawAccelLayout.IoBaseSize + RawAccelLayout.OutputDpiOffsetInModifier;
        BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(outputDpiOffset), RawAccelLayout.NormalizedDpi);

        return buffer;
    }

    public void Write(byte[] buffer)
    {
        if (buffer.Length < RawAccelLayout.IoBaseSize + RawAccelLayout.OutputDpiOffsetInModifier + 8)
        {
            Log($"Write() called with {buffer.Length}-byte buffer (too small to inspect).");
            return;
        }
        int outputDpiOffset = RawAccelLayout.IoBaseSize + RawAccelLayout.OutputDpiOffsetInModifier;
        double outputDpi = BinaryPrimitives.ReadDoubleLittleEndian(buffer.AsSpan(outputDpiOffset));
        double multiplier = outputDpi / RawAccelLayout.NormalizedDpi;
        Log($"Write() called → modifier 0 output_dpi={outputDpi.ToString("F2", CultureInfo.InvariantCulture)} (={multiplier.ToString("F2", CultureInfo.InvariantCulture)}x).");
    }

    private void Log(string message)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine;
        lock (syncRoot)
        {
            File.AppendAllText(logPath, line);
        }
    }
}
