using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using UniversalSensRandomizer.Interop;
using UniversalSensRandomizer.Models;

namespace UniversalSensRandomizer.Services;

public sealed class BaselineSnapshot(SensitivitySnapshot snapshot)
{
    private readonly byte[] originalBuffer = snapshot.OriginalBuffer;
    private readonly double[] originalOutputDpis = [.. snapshot.OriginalOutputDpis];
    private readonly int modifierCount = snapshot.ModifierCount;

    public byte[] OriginalBuffer => originalBuffer;
    public IReadOnlyList<double> OriginalOutputDpis => originalOutputDpis;
    public int ModifierCount => modifierCount;

    public byte[] BuildBuffer(double multiplier)
    {
        byte[] buffer = (byte[])originalBuffer.Clone();
        for (int i = 0; i < modifierCount; i++)
        {
            int offset = RawAccelLayout.IoBaseSize
                + i * RawAccelLayout.ModifierSettingsSize
                + RawAccelLayout.OutputDpiOffsetInModifier;
            double value = originalOutputDpis[i] * multiplier;
            BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(offset), value);
        }
        return buffer;
    }

    public static SensitivitySnapshot Capture(IRawAccelClient client)
    {
        byte[] buffer = client.Read();

        uint modifierCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(RawAccelLayout.ModifierDataSizeOffset));
        uint deviceCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(RawAccelLayout.DeviceDataSizeOffset));

        if (modifierCount == 0)
        {
            modifierCount = 1;
        }

        long expected = RawAccelLayout.IoBaseSize
            + (long)modifierCount * RawAccelLayout.ModifierSettingsSize
            + (long)deviceCount * RawAccelLayout.DeviceSettingsSize;

        if (buffer.LongLength != expected)
        {
            throw new InvalidOperationException($"Snapshot buffer size mismatch: got {buffer.LongLength}, expected {expected}.");
        }

        double[] outputDpis = new double[modifierCount];
        for (int i = 0; i < modifierCount; i++)
        {
            int offset = RawAccelLayout.IoBaseSize
                + i * RawAccelLayout.ModifierSettingsSize
                + RawAccelLayout.OutputDpiOffsetInModifier;
            double value = BinaryPrimitives.ReadDoubleLittleEndian(buffer.AsSpan(offset));
            if (!double.IsFinite(value) || value < 1.0 || value > 100_000.0)
            {
                throw new InvalidOperationException($"Modifier {i} OutputDpi parsed as {value}, outside expected range. RawAccel struct version may have changed.");
            }
            outputDpis[i] = value;
        }

        return new SensitivitySnapshot
        {
            OriginalBuffer = buffer,
            OriginalOutputDpis = outputDpis,
            ModifierCount = (int)modifierCount,
            DeviceCount = (int)deviceCount,
        };
    }
}
