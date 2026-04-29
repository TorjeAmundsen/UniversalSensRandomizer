using System.Globalization;

namespace UniversalSensRandomizer.Util;

public static class InvariantFormat
{
    public static string LiveOutput(double multiplier, double cm360)
    {
        string mult = multiplier.ToString("F2", CultureInfo.InvariantCulture);
        string cm = cm360.ToString("F1", CultureInfo.InvariantCulture);
        return $"{mult}x ({cm} cm/360)";
    }
}
