using System;
using System.Buffers.Binary;
using System.IO;
using Microsoft.Win32.SafeHandles;
using UniversalSensRandomizer.Interop;

namespace UniversalSensRandomizer.Services;

public sealed class IoctlRawAccelClient : IRawAccelClient
{
    public byte[] Read()
    {
        using SafeFileHandle handle = RawAccelInterop.Open();

        byte[] header = new byte[RawAccelLayout.IoBaseSize];
        uint headerBytes = RawAccelInterop.Read(handle, header);
        if (headerBytes != RawAccelLayout.IoBaseSize)
        {
            throw new IOException($"Driver returned {headerBytes} bytes for IoBase header; expected {RawAccelLayout.IoBaseSize}. Driver/struct mismatch.");
        }

        uint modifierCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(RawAccelLayout.ModifierDataSizeOffset));
        uint deviceCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(RawAccelLayout.DeviceDataSizeOffset));

        if (modifierCount == 0)
        {
            modifierCount = 1;
        }

        long total = RawAccelLayout.IoBaseSize
            + (long)modifierCount * RawAccelLayout.ModifierSettingsSize
            + (long)deviceCount * RawAccelLayout.DeviceSettingsSize;

        if (total > int.MaxValue)
        {
            throw new IOException("RawAccel buffer size exceeds int.MaxValue.");
        }

        byte[] full = new byte[total];
        uint fullBytes = RawAccelInterop.Read(handle, full);
        if (fullBytes != total)
        {
            throw new IOException($"Driver returned {fullBytes} bytes; expected {total}. RawAccel struct version mismatch — UniversalSensRandomizer may need an update.");
        }

        return full;
    }

    public void Write(byte[] buffer)
    {
        if (buffer is not { Length: >= RawAccelLayout.IoBaseSize })
        {
            throw new ArgumentException("Write buffer too small.", nameof(buffer));
        }
        using SafeFileHandle handle = RawAccelInterop.Open();
        RawAccelInterop.Write(handle, buffer);
    }
}
