using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniversalSensRandomizer.Services.Twitch;

public sealed class ValidateResponse
{
    public string ClientId { get; set; } = "";
    public string Login { get; set; } = "";
    public List<string> Scopes { get; set; } = new();
    public string UserId { get; set; } = "";
    public int ExpiresIn { get; set; }
}

public sealed class CustomRewardsResponse
{
    public List<CustomReward> Data { get; set; } = new();
}

public sealed class CustomReward
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Cost { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsPaused { get; set; }
}

public sealed class CreateRewardRequest
{
    public string Title { get; set; } = "";
    public int Cost { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Prompt { get; set; } = "";
}

public sealed class UpdateRewardRequest
{
    public bool IsPaused { get; set; }
}

public sealed class SubscribeRequest
{
    public string Type { get; set; } = "";
    public string Version { get; set; } = "1";
    public SubscribeCondition Condition { get; set; } = new();
    public SubscribeTransport Transport { get; set; } = new();
}

public sealed class SubscribeCondition
{
    public string BroadcasterUserId { get; set; } = "";
    public string RewardId { get; set; } = "";
}

public sealed class SubscribeTransport
{
    public string Method { get; set; } = "websocket";
    public string SessionId { get; set; } = "";
}

public sealed class SubscribeResponse
{
    public List<SubscribeData> Data { get; set; } = new();
}

public sealed class SubscribeData
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed class EventSubMessage
{
    public EventSubMetadata Metadata { get; set; } = new();
    public EventSubPayload Payload { get; set; } = new();
}

public sealed class EventSubMetadata
{
    public string MessageId { get; set; } = "";
    public string MessageType { get; set; } = "";
    public string MessageTimestamp { get; set; } = "";
    public string? SubscriptionType { get; set; }
    public string? SubscriptionVersion { get; set; }
}

public sealed class EventSubPayload
{
    public EventSubSession? Session { get; set; }
    public RedemptionEvent? Event { get; set; }
}

public sealed class EventSubSession
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public int KeepaliveTimeoutSeconds { get; set; }
    public string? ReconnectUrl { get; set; }
    public string ConnectedAt { get; set; } = "";
}

public sealed class RedemptionEvent
{
    public string Id { get; set; } = "";
    public string BroadcasterUserId { get; set; } = "";
    public string BroadcasterUserLogin { get; set; } = "";
    public string BroadcasterUserName { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserLogin { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserInput { get; set; } = "";
    public string Status { get; set; } = "";
    public RedemptionReward Reward { get; set; } = new();
    public string RedeemedAt { get; set; } = "";
}

public sealed class RedemptionReward
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Cost { get; set; }
    public string Prompt { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ValidateResponse))]
[JsonSerializable(typeof(CustomRewardsResponse))]
[JsonSerializable(typeof(CustomReward))]
[JsonSerializable(typeof(CreateRewardRequest))]
[JsonSerializable(typeof(UpdateRewardRequest))]
[JsonSerializable(typeof(SubscribeRequest))]
[JsonSerializable(typeof(SubscribeResponse))]
[JsonSerializable(typeof(EventSubMessage))]
internal sealed partial class TwitchJsonContext : JsonSerializerContext
{
}
