namespace UniversalSensRandomizer.Models;

public sealed class PersistedSettings
{
    public int SchemaVersion { get; set; } = 1;
    public double MinMultiplier { get; set; } = 0.7;
    public double MaxMultiplier { get; set; } = 3.8;
    public double BaseCm360 { get; set; } = 30.0;
    public int TimerIntervalSeconds { get; set; } = 15;
    public bool TimerEnabled { get; set; }
    public HotkeyCombination Hotkey { get; set; } = HotkeyCombination.None;
}
