using System.Runtime.InteropServices;

namespace UniversalSensRandomizer.Interop;

// Layout constants derived from D:\rawaccel\common\rawaccel.hpp and rawaccel-base.hpp.
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
