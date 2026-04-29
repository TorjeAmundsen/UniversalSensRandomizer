using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace UniversalSensRandomizer.Interop;

// Layout constants derived from rawaccel/common/rawaccel.hpp and rawaccel-base.hpp (v1.7.1).
// Validated at runtime against the buffer length returned by the driver.
public static class RawAccelLayout
{
    public const int IoBaseSize = 40;
    public const int ModifierSettingsSize = 5184;
    public const int DeviceSettingsSize = 1456;
    public const int OutputDpiOffsetInModifier = 4952;

    public const int ModifierDataSizeOffset = 32;
    public const int DeviceDataSizeOffset = 36;

    public const double NormalizedDpi = 1000.0;

    // profile layout (5016 bytes, starts at offset 0 of modifier_settings)
    public const int ProfileNameOffset = 0;
    public const int ProfileNameMaxChars = 256;
    public const int ProfileDomainWeightsOffset = 512;
    public const int ProfileRangeWeightsOffset = 528;
    public const int ProfileAccelXOffset = 544;
    public const int ProfileAccelYOffset = 2728;
    public const int ProfileSpeedArgsOffset = 4912;
    public const int ProfileOutputDpiOffset = 4952;
    public const int ProfileYxRatioOffset = 4960;
    public const int ProfileLrRatioOffset = 4968;
    public const int ProfileUdRatioOffset = 4976;
    public const int ProfileSize = 5016;

    public const int AccelArgsSize = 2184;

    // data_t layout (168 bytes), starts at offset 5016 of modifier_settings
    public const int DataTOffset = 5016;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct IoBaseHeader
{
    public byte DefaultDevDisable;
    public byte DefaultDevSetExtraInfo;
    public byte DefaultDevPollTimeLock;
    private byte pad0;
    public int DefaultDevDpi;
    public int DefaultDevPollingRate;
    public double DefaultDevClampMin;
    public double DefaultDevClampMax;
    public uint ModifierDataSize;
    public uint DeviceDataSize;
}
