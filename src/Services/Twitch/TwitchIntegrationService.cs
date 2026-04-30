using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalSensRandomizer.Services.Twitch;

public enum TwitchState
{
    Disconnected,
    Connecting,
    Connected,
    TokenExpired,
    Error,
}

public sealed class     TwitchIntegrationService(
    string clientId,
    RandomizerEngine engine,
    Func<(double, double)> rangeProvider,
    Func<bool> isRunningProvider)
    : IAsyncDisposable
{
    private readonly Func<(double min, double max)> rangeProvider = rangeProvider;

    private TwitchHelixClient? helix;
    private TwitchEventSubClient? events;
    private CancellationTokenSource? lifetimeCts;
    private Task? workerTask;

    private readonly ConcurrentQueue<RedemptionEvent> queue = new();
    private readonly SemaphoreSlim queueSignal = new(0);

    private string accessToken = "";
    private bool? lastSyncedPaused;
    private string lastSyncedRewardId = "";

    public TwitchState State { get; private set; } = TwitchState.Disconnected;
    public string StatusText { get; private set; } = "Disconnected";
    public string UserId { get; private set; } = "";
    public string UserLogin { get; private set; } = "";
    public IReadOnlyList<CustomReward> AvailableRewards { get; private set; } = Array.Empty<CustomReward>();
    public IReadOnlySet<string> AllRewardTitles { get; private set; } = new HashSet<string>();

    public string RewardId { get; set; } = "";
    public double CooldownSeconds { get; set; } = 5.0;
    public int QueueCap { get; set; } = 20;
    public bool Enabled { get; set; }

    public event Action? StateChanged;
    public event Action<AuthResult>? TokenAcquired;
    public event Action? TokenCleared;
    public event Action<CustomReward>? RewardCreated;

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            SetState(TwitchState.Error, "Twitch Client ID not configured (set TWITCH_CLIENT_ID at build time)");
            return;
        }

        SetState(TwitchState.Connecting, "Authorizing in browser...");
        helix ??= new TwitchHelixClient(clientId);
        TwitchAuthService auth = new(clientId, helix, TwitchConfig.RedirectPort);
        AuthResult? r;
        try
        {
            r = await auth.AuthenticateAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetState(TwitchState.Error, "Auth error: " + ex.Message);
            return;
        }
        if (r is null)
        {
            SetState(TwitchState.Error, "Auth cancelled or failed");
            return;
        }

        accessToken = r.AccessToken;
        helix.SetToken(r.AccessToken);
        UserId = r.UserId;
        UserLogin = r.UserLogin;
        TokenAcquired?.Invoke(r);

        await RefreshRewardsAsync(ct).ConfigureAwait(false);
        await TryStartListeningAsync(ct).ConfigureAwait(false);
        if (State != TwitchState.Connected && State != TwitchState.Error)
        {
            SetState(TwitchState.Connected, "Connected as " + UserLogin);
        }
    }

    public async Task ResumeAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }
        helix ??= new TwitchHelixClient(clientId);
        helix.SetToken(token);
        ValidateResponse? v;
        try
        {
            v = await helix.ValidateAsync(token, ct).ConfigureAwait(false);
        }
        catch
        {
            SetState(TwitchState.TokenExpired, "Token validation failed");
            return;
        }
        if (v is null || string.IsNullOrEmpty(v.UserId) || !v.Scopes.Contains("channel:manage:redemptions"))
        {
            SetState(TwitchState.TokenExpired, "Token expired or scope missing. Reconnect required.");
            return;
        }
        accessToken = token;
        UserId = v.UserId;
        UserLogin = v.Login;
        SetState(TwitchState.Connected, "Connected as " + UserLogin);
        await RefreshRewardsAsync(ct).ConfigureAwait(false);
        await TryStartListeningAsync(ct).ConfigureAwait(false);
    }

    public async Task CreateRewardAsync(string title, int cost, CancellationToken ct)
    {
        if (helix is null || string.IsNullOrEmpty(UserId))
        {
            return;
        }
        if (AllRewardTitles.Contains(title))
        {
            SetState(State, $"A reward titled '{title}' already exists.");
            return;
        }
        try
        {
            CustomReward created = await helix.CreateCustomRewardAsync(UserId, title, cost, ct).ConfigureAwait(false);
            await RefreshRewardsAsync(ct).ConfigureAwait(false);
            RewardId = created.Id;
            RewardCreated?.Invoke(created);
            if (Enabled && State == TwitchState.Connected)
            {
                await TryStartListeningAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            SetState(TwitchState.Error, "Create reward failed: " + ex.Message);
        }
    }

    public async Task RefreshRewardsAsync(CancellationToken ct)
    {
        if (helix is null || string.IsNullOrEmpty(UserId))
        {
            return;
        }
        try
        {
            List<CustomReward> manageable = await helix.GetCustomRewardsAsync(UserId, true, ct).ConfigureAwait(false);
            List<CustomReward> all = await helix.GetCustomRewardsAsync(UserId, false, ct).ConfigureAwait(false);
            AvailableRewards = manageable;
            HashSet<string> titles = new(StringComparer.OrdinalIgnoreCase);
            foreach (CustomReward r in all)
            {
                titles.Add(r.Title);
            }
            AllRewardTitles = titles;
            if (!string.IsNullOrEmpty(RewardId) && !manageable.Exists(r => r.Id == RewardId))
            {
                RewardId = "";
                lastSyncedRewardId = "";
                lastSyncedPaused = null;
                SetState(State, "Saved reward is no longer manageable by this app. Pick or create a new one.");
            }
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            SetState(TwitchState.Error, "Reward fetch failed: " + ex.Message);
        }
    }

    public async Task TryStartListeningAsync(CancellationToken ct)
    {
        if (helix is null || string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(RewardId))
        {
            return;
        }

        await StopEventsOnlyAsync().ConfigureAwait(false);

        lifetimeCts ??= new CancellationTokenSource();

        TwitchEventSubClient client = new();
        client.RedemptionReceived += OnRedemption;
        client.Disconnected += OnEventSubDisconnected;
        events = client;

        try
        {
            await client.StartAsync(lifetimeCts.Token).ConfigureAwait(false);
            string sessionId = await client.SessionTask.WaitAsync(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
            await helix.SubscribeRedemptionAsync(UserId, RewardId, sessionId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetState(TwitchState.Error, "EventSub setup failed: " + ex.Message);
            return;
        }

        if (workerTask is null || workerTask.IsCompleted)
        {
            workerTask = Task.Run(() => WorkerLoopAsync(lifetimeCts.Token));
        }
        SetState(TwitchState.Connected, $"Listening as {UserLogin}");
        _ = SyncRewardStateAsync();
    }

    public async Task SyncRewardStateAsync(CancellationToken ct = default)
    {
        if (helix is null
            || string.IsNullOrEmpty(UserId)
            || string.IsNullOrEmpty(RewardId)
            || State != TwitchState.Connected)
        {
            return;
        }
        bool paused = !(Enabled && isRunningProvider());
        if (lastSyncedRewardId == RewardId && lastSyncedPaused == paused)
        {
            return;
        }
        try
        {
            await helix.UpdateRewardPausedAsync(UserId, RewardId, paused, ct).ConfigureAwait(false);
            lastSyncedPaused = paused;
            lastSyncedRewardId = RewardId;
        }
        catch (Exception ex)
        {
            SetState(State, $"Reward pause sync failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        if (helix is not null && !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(RewardId))
        {
            try
            {
                await helix.UpdateRewardPausedAsync(UserId, RewardId, true, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        if (helix is not null && !string.IsNullOrEmpty(accessToken))
        {
            try
            {
                await helix.RevokeAsync(accessToken, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        lastSyncedPaused = null;
        lastSyncedRewardId = "";
        await StopAllAsync().ConfigureAwait(false);
        accessToken = "";
        UserId = "";
        UserLogin = "";
        AvailableRewards = Array.Empty<CustomReward>();
        helix?.SetToken(null);
        TokenCleared?.Invoke();
        SetState(TwitchState.Disconnected, "Disconnected");
    }

    private async Task StopEventsOnlyAsync()
    {
        TwitchEventSubClient? c = events;
        events = null;
        if (c is not null)
        {
            c.RedemptionReceived -= OnRedemption;
            c.Disconnected -= OnEventSubDisconnected;
            await c.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task StopAllAsync()
    {
        CancellationTokenSource? cts = lifetimeCts;
        lifetimeCts = null;
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
            }
        }
        await StopEventsOnlyAsync().ConfigureAwait(false);
        if (workerTask is not null)
        {
            try
            {
                await workerTask.ConfigureAwait(false);
            }
            catch
            {
            }
            workerTask = null;
        }
        cts?.Dispose();
        // Drain queue
        while (queue.TryDequeue(out _))
        {
        }
    }

    private void OnRedemption(RedemptionEvent ev)
    {
        if (string.IsNullOrEmpty(UserId) || ev.BroadcasterUserId != UserId)
        {
            return;
        }
        if (string.IsNullOrEmpty(RewardId) || ev.Reward.Id != RewardId)
        {
            return;
        }
        if (!Enabled || !isRunningProvider())
        {
            RefundFireAndForget(ev);
            return;
        }
        if (queue.Count >= Math.Max(1, QueueCap))
        {
            Console.Error.WriteLine($"Twitch redemption dropped (queue full): {ev.UserLogin}");
            return;
        }
        queue.Enqueue(ev);
        try
        {
            queueSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private void RefundFireAndForget(RedemptionEvent ev)
    {
        TwitchHelixClient? h = helix;
        if (h is null)
        {
            return;
        }
        string broadcasterId = UserId;
        string rewardId = RewardId;
        _ = Task.Run(async () =>
        {
            try
            {
                await h.RefundRedemptionAsync(broadcasterId, rewardId, ev.Id, CancellationToken.None).ConfigureAwait(false);
                SetState(State, $"Refunded redemption from {ev.UserLogin}");
            }
            catch (Exception ex)
            {
                SetState(State, $"Refund failed ({ev.UserLogin}): {ex.Message}");
            }
        });
    }

    private void OnEventSubDisconnected(string reason)
    {
        if (lifetimeCts is null || lifetimeCts.IsCancellationRequested)
        {
            return;
        }
        if (reason == "revocation")
        {
            SetState(TwitchState.TokenExpired, "Subscription revoked. Reconnect required.");
            return;
        }
        SetState(TwitchState.Connecting, $"Reconnecting (reason: {reason})...");
        _ = Task.Run(async () =>
        {
            TimeSpan delay = TimeSpan.FromSeconds(5);
            CancellationTokenSource? cts = lifetimeCts;
            while (cts is not null && !cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                    await TryStartListeningAsync(cts.Token).ConfigureAwait(false);
                    if (State == TwitchState.Connected)
                    {
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                }
                delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
            }
        });
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        DateTime lastRandomize = DateTime.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await queueSignal.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (!queue.TryDequeue(out RedemptionEvent? ev))
            {
                continue;
            }

            TimeSpan since = DateTime.UtcNow - lastRandomize;
            TimeSpan cooldown = TimeSpan.FromSeconds(Math.Max(0, CooldownSeconds));
            if (since < cooldown)
            {
                try
                {
                    await Task.Delay(cooldown - since, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            (double min, double max) = rangeProvider();
            try
            {
                await Task.Run(() => engine.Randomize(min, max), ct).ConfigureAwait(false);
            }
            catch
            {
            }
            lastRandomize = DateTime.UtcNow;
        }
    }

    private void SetState(TwitchState newState, string status)
    {
        State = newState;
        StatusText = status;
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync().ConfigureAwait(false);
        helix?.Dispose();
    }
}
