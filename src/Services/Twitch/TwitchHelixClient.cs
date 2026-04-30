using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalSensRandomizer.Services.Twitch;

public sealed class TwitchHelixClient : IDisposable
{
    private readonly HttpClient http;
    private readonly string clientId;
    private string? bearer;

    public TwitchHelixClient(string clientId)
    {
        this.clientId = clientId;
        http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("UniversalSensRandomizer/1.0");
    }

    public void SetToken(string? accessToken)
    {
        bearer = accessToken;
    }

    public async Task<ValidateResponse?> ValidateAsync(string accessToken, CancellationToken ct)
    {
        using HttpRequestMessage req = new(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        req.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        using HttpResponseMessage res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            return null;
        }
        await using System.IO.Stream stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync(stream, TwitchJsonContext.Default.ValidateResponse, ct).ConfigureAwait(false);
    }

    public async Task<List<CustomReward>> GetCustomRewardsAsync(string broadcasterId, bool onlyManageable, CancellationToken ct)
    {
        string url = "https://api.twitch.tv/helix/channel_points/custom_rewards"
            + $"?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
            + (onlyManageable ? "&only_manageable_rewards=true" : "");
        using HttpRequestMessage req = new(HttpMethod.Get, url);
        AddAuth(req);
        using HttpResponseMessage res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"GetCustomRewards failed: {(int)res.StatusCode} {body}");
        }
        await using System.IO.Stream stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        CustomRewardsResponse? parsed = await JsonSerializer.DeserializeAsync(stream, TwitchJsonContext.Default.CustomRewardsResponse, ct).ConfigureAwait(false);
        return parsed?.Data ?? new List<CustomReward>();
    }

    public async Task<string> SubscribeRedemptionAsync(string broadcasterId, string rewardId, string sessionId, CancellationToken ct)
    {
        SubscribeRequest body = new()
        {
            Type = "channel.channel_points_custom_reward_redemption.add",
            Version = "1",
            Condition = new SubscribeCondition
            {
                BroadcasterUserId = broadcasterId,
                RewardId = rewardId,
            },
            Transport = new SubscribeTransport
            {
                Method = "websocket",
                SessionId = sessionId,
            },
        };
        string json = JsonSerializer.Serialize(body, TwitchJsonContext.Default.SubscribeRequest);

        using HttpRequestMessage req = new(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");
        AddAuth(req);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string respBody = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Subscribe failed: {(int)res.StatusCode} {respBody}");
        }
        await using System.IO.Stream stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        SubscribeResponse? parsed = await JsonSerializer.DeserializeAsync(stream, TwitchJsonContext.Default.SubscribeResponse, ct).ConfigureAwait(false);
        if (parsed is null || parsed.Data.Count == 0)
        {
            throw new InvalidOperationException("Subscribe returned no data.");
        }
        return parsed.Data[0].Id;
    }

    public async Task<CustomReward> CreateCustomRewardAsync(string broadcasterId, string title, int cost, CancellationToken ct)
    {
        CreateRewardRequest body = new()
        {
            Title = title,
            Cost = cost,
            IsEnabled = true,
        };
        string json = JsonSerializer.Serialize(body, TwitchJsonContext.Default.CreateRewardRequest);
        string url = $"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={Uri.EscapeDataString(broadcasterId)}";
        using HttpRequestMessage req = new(HttpMethod.Post, url);
        AddAuth(req);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string respBody = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"CreateReward failed: {(int)res.StatusCode} {respBody}");
        }
        await using System.IO.Stream stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        CustomRewardsResponse? parsed = await JsonSerializer.DeserializeAsync(stream, TwitchJsonContext.Default.CustomRewardsResponse, ct).ConfigureAwait(false);
        if (parsed is null || parsed.Data.Count == 0)
        {
            throw new InvalidOperationException("CreateReward returned no data.");
        }
        return parsed.Data[0];
    }

    public async Task UpdateRewardPausedAsync(string broadcasterId, string rewardId, bool paused, CancellationToken ct)
    {
        UpdateRewardRequest body = new() { IsPaused = paused };
        string json = JsonSerializer.Serialize(body, TwitchJsonContext.Default.UpdateRewardRequest);
        string url = "https://api.twitch.tv/helix/channel_points/custom_rewards"
            + $"?broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
            + $"&id={Uri.EscapeDataString(rewardId)}";
        using HttpRequestMessage req = new(HttpMethod.Patch, url);
        AddAuth(req);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string respBody = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"UpdateReward failed: {(int)res.StatusCode} {respBody}");
        }
    }

    public async Task RefundRedemptionAsync(string broadcasterId, string rewardId, string redemptionId, CancellationToken ct)
    {
        string url = "https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions"
            + $"?id={Uri.EscapeDataString(redemptionId)}"
            + $"&broadcaster_id={Uri.EscapeDataString(broadcasterId)}"
            + $"&reward_id={Uri.EscapeDataString(rewardId)}";
        using HttpRequestMessage req = new(HttpMethod.Patch, url);
        AddAuth(req);
        req.Content = new StringContent("{\"status\":\"CANCELED\"}", Encoding.UTF8, "application/json");
        using HttpResponseMessage res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Refund failed: {(int)res.StatusCode} {body}");
        }
    }

    public async Task RevokeAsync(string accessToken, CancellationToken ct)
    {
        Dictionary<string, string> form = new()
        {
            ["client_id"] = clientId,
            ["token"] = accessToken,
        };
        using HttpRequestMessage req = new(HttpMethod.Post, "https://id.twitch.tv/oauth2/revoke")
        {
            Content = new FormUrlEncodedContent(form),
        };
        try
        {
            using HttpResponseMessage _ = await http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void AddAuth(HttpRequestMessage req)
    {
        if (!string.IsNullOrEmpty(bearer))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        req.Headers.TryAddWithoutValidation("Client-Id", clientId);
    }

    public void Dispose()
    {
        http.Dispose();
    }
}
