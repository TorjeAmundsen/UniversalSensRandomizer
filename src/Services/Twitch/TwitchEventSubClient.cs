using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalSensRandomizer.Services.Twitch;

public sealed class TwitchEventSubClient(string? url = null) : IAsyncDisposable
{
    private const string DefaultUrl = "wss://eventsub.wss.twitch.tv/ws";

    private ClientWebSocket? socket;
    private Task? loop;
    private CancellationTokenSource? cts;
    private readonly TaskCompletionSource<string> sessionTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Action<RedemptionEvent>? RedemptionReceived;
    public event Action<string>? Disconnected;

    public Task<string> SessionTask => sessionTcs.Task;

    public string Url { get; } = url ?? DefaultUrl;

    public Task StartAsync(CancellationToken externalCt)
    {
        if (loop is not null)
        {
            throw new InvalidOperationException("Already started.");
        }
        cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        loop = Task.Run(() => RunAsync(cts.Token));
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        ClientWebSocket ws = new();
        socket = ws;
        try
        {
            await ws.ConnectAsync(new Uri(Url), ct).ConfigureAwait(false);

            int keepaliveSec = 30;
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                using CancellationTokenSource recvTimeout = new(TimeSpan.FromSeconds(keepaliveSec * 3));
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, recvTimeout.Token);

                EventSubMessage? msg;
                try
                {
                    msg = await ReceiveJsonAsync(ws, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (recvTimeout.IsCancellationRequested)
                {
                    Disconnected?.Invoke("keepalive_timeout");
                    return;
                }

                if (msg is null)
                {
                    Disconnected?.Invoke("closed");
                    return;
                }

                switch (msg.Metadata.MessageType)
                {
                    case "session_welcome":
                        if (msg.Payload.Session is { } sw)
                        {
                            if (sw.KeepaliveTimeoutSeconds > 0)
                            {
                                keepaliveSec = sw.KeepaliveTimeoutSeconds;
                            }
                            sessionTcs.TrySetResult(sw.Id);
                        }
                        break;
                    case "session_keepalive":
                        break;
                    case "notification":
                        if (msg.Payload.Event is { } ev)
                        {
                            try
                            {
                                RedemptionReceived?.Invoke(ev);
                            }
                            catch
                            {
                            }
                        }
                        break;
                    case "session_reconnect":
                        Disconnected?.Invoke("session_reconnect");
                        return;
                    case "revocation":
                        Disconnected?.Invoke("revocation");
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke("error:" + ex.Message);
        }
        finally
        {
            sessionTcs.TrySetCanceled();
            socket = null;
            try
            {
                ws.Dispose();
            }
            catch
            {
            }
        }
    }

    private static async Task<EventSubMessage?> ReceiveJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(8192);
        using MemoryStream ms = new();
        try
        {
            while (true)
            {
                ValueWebSocketReceiveResult result = await ws.ReceiveAsync(rented.AsMemory(), ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }
                if (result.Count > 0)
                {
                    ms.Write(rented, 0, result.Count);
                }
                if (result.EndOfMessage)
                {
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
        ms.Position = 0;
        try
        {
            return await JsonSerializer.DeserializeAsync(ms, TwitchJsonContext.Default.EventSubMessage, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
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
        ClientWebSocket? s = socket;
        if (s is { State: WebSocketState.Open })
        {
            try
            {
                using CancellationTokenSource closeCts = new(TimeSpan.FromSeconds(2));
                await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", closeCts.Token).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch
            {
            }
        }
        cts?.Dispose();
    }
}
