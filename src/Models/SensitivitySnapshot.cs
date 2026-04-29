using System.Collections.Generic;

namespace UniversalSensRandomizer.Models;

public sealed class SensitivitySnapshot
{
    public required byte[] OriginalBuffer { get; init; }
    public required IReadOnlyList<double> OriginalOutputDpis { get; init; }
    public required int ModifierCount { get; init; }
    public required int DeviceCount { get; init; }
}
