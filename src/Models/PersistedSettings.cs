namespace UniversalSensRandomizer.Models;

public sealed class PersistedSettings
{
    public int SchemaVersion { get; set; } = 2;
    public double MinMultiplier { get; set; } = 0.33;
    public double MaxMultiplier { get; set; } = 3;
    public double BaseCm360 { get; set; } = 30.0;
    public double TimerIntervalSeconds { get; set; } = 15.0;
    public bool TimerEnabled { get; set; }
    public HotkeyCombination Hotkey { get; set; } = HotkeyCombination.None;

    public bool TwitchEnabled { get; set; }
    public string TwitchProtectedToken { get; set; } = "";
    public string TwitchUserId { get; set; } = "";
    public string TwitchUserLogin { get; set; } = "";
    public string TwitchRewardId { get; set; } = "";
    public double TwitchCooldownSeconds { get; set; } = 5.0;
    public int TwitchQueueCap { get; set; } = 20;
}
