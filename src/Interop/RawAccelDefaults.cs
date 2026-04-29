using System;
using System.Buffers.Binary;

namespace UniversalSensRandomizer.Interop;

// Mirrors C++ default-constructed rawaccel::modifier_settings from rawaccel/common/rawaccel.hpp
// and rawaccel-base.hpp (v1.7.1). Used to synthesize a baseline buffer when the driver
// reports modifier_data_size == 0 (no settings written), matching what the C++ user-mode
// wrapper rawaccel-io.hpp::read() produces in the same situation.
public static class RawAccelDefaults
{
    // accel_mode::noaccel = 6 (last enum value)
    private const int AccelModeNoaccel = 6;

    // cap_mode::out = 2
    private const int CapModeOut = 2;

    public static void WriteDefaultModifierSettings(Span<byte> dst)
    {
        if (dst.Length < RawAccelLayout.ModifierSettingsSize)
        {
            throw new ArgumentException("Destination span too small for modifier_settings.", nameof(dst));
        }

        dst[..RawAccelLayout.ModifierSettingsSize].Clear();

        WriteWideString(dst[RawAccelLayout.ProfileNameOffset..], "default", RawAccelLayout.ProfileNameMaxChars);

        WriteVec2d(dst, RawAccelLayout.ProfileDomainWeightsOffset, 1.0, 1.0);
        WriteVec2d(dst, RawAccelLayout.ProfileRangeWeightsOffset, 1.0, 1.0);

        WriteDefaultAccelArgs(dst.Slice(RawAccelLayout.ProfileAccelXOffset, RawAccelLayout.AccelArgsSize));
        WriteDefaultAccelArgs(dst.Slice(RawAccelLayout.ProfileAccelYOffset, RawAccelLayout.AccelArgsSize));

        WriteDefaultSpeedArgs(dst[RawAccelLayout.ProfileSpeedArgsOffset..]);

        WriteDouble(dst, RawAccelLayout.ProfileOutputDpiOffset, RawAccelLayout.NormalizedDpi);
        WriteDouble(dst, RawAccelLayout.ProfileYxRatioOffset, 1.0);
        WriteDouble(dst, RawAccelLayout.ProfileLrRatioOffset, 1.0);
        WriteDouble(dst, RawAccelLayout.ProfileUdRatioOffset, 1.0);

        // degrees_rotation, degrees_snap, speed_min, speed_max all default to 0 - already zeroed.
        // data_t (modifier_flags + rot_direction + accel_unions) all default-init to 0 - already zeroed.
        // Driver re-derives data_t when applying a modifier (mode=noaccel selects accel_noaccel callback).
    }

    private static void WriteDefaultAccelArgs(Span<byte> dst)
    {
        // mode (int) at 0
        BinaryPrimitives.WriteInt32LittleEndian(dst[0..], AccelModeNoaccel);
        // gain (bool) at 4
        dst[4] = 1;
        // pad 5..7

        // doubles starting at offset 8
        WriteDouble(dst, 8,   0.0);    // input_offset
        WriteDouble(dst, 16,  0.0);    // output_offset
        WriteDouble(dst, 24,  0.005);  // acceleration
        WriteDouble(dst, 32,  0.1);    // decay_rate
        WriteDouble(dst, 40,  1.0);    // gamma
        WriteDouble(dst, 48,  1.5);    // motivity
        WriteDouble(dst, 56,  2.0);    // exponent_classic
        WriteDouble(dst, 64,  1.0);    // scale
        WriteDouble(dst, 72,  0.05);   // exponent_power
        WriteDouble(dst, 80,  1.5);    // limit
        WriteDouble(dst, 88,  5.0);    // sync_speed
        WriteDouble(dst, 96,  0.5);    // smooth

        // vec2d cap = {15, 1.5} at offset 104
        WriteVec2d(dst, 104, 15.0, 1.5);

        // cap_mode (int) at 120
        BinaryPrimitives.WriteInt32LittleEndian(dst[120..], CapModeOut);

        // length (int) at 124 = 0 - already zeroed.
        // float data[514] at 128..2183 = 0 - already zeroed.
    }

    private static void WriteDefaultSpeedArgs(Span<byte> dst)
    {
        // whole (bool) at 0
        dst[0] = 1;
        // pad 1..7
        WriteDouble(dst, 8,  2.0); // lp_norm
        WriteDouble(dst, 16, 0.0); // input_speed_smooth_halflife
        WriteDouble(dst, 24, 0.0); // scale_smooth_halflife
        WriteDouble(dst, 32, 0.0); // output_speed_smooth_halflife
    }

    private static void WriteVec2d(Span<byte> dst, int offset, double x, double y)
    {
        BinaryPrimitives.WriteDoubleLittleEndian(dst.Slice(offset, 8), x);
        BinaryPrimitives.WriteDoubleLittleEndian(dst.Slice(offset + 8, 8), y);
    }

    private static void WriteDouble(Span<byte> dst, int offset, double value)
    {
        BinaryPrimitives.WriteDoubleLittleEndian(dst.Slice(offset, 8), value);
    }

    private static void WriteWideString(Span<byte> dst, string value, int maxChars)
    {
        int byteCap = maxChars * 2;
        dst[..byteCap].Clear();
        int copy = Math.Min(value.Length, maxChars - 1);
        for (int i = 0; i < copy; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(dst.Slice(i * 2, 2), value[i]);
        }
    }
}
