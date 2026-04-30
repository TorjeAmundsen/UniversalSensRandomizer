using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalSensRandomizer.Services.Twitch;

public sealed record AuthResult(string AccessToken, string UserId, string UserLogin);

public sealed class TwitchAuthService(string clientId, TwitchHelixClient helix, int redirectPort)
{
    private const string CallbackHtml =
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>UniversalSensRandomizer</title>" +
        "<style>body{font-family:sans-serif;background:#1e1e1e;color:#ddd;text-align:center;padding:60px;}</style>" +
        "</head><body><p id=\"m\">Connecting…</p><script>" +
        "const p=new URLSearchParams(location.hash.slice(1));" +
        "fetch('/token',{method:'POST',body:p.toString()," +
        "headers:{'Content-Type':'application/x-www-form-urlencoded'}})" +
        ".then(r=>r.ok?document.getElementById('m').textContent='Connected. You can close this tab.':" +
        "document.getElementById('m').textContent='Auth failed: '+r.status)" +
        ".catch(e=>document.getElementById('m').textContent='Error: '+e);" +
        "</script></body></html>";

    public async Task<AuthResult?> AuthenticateAsync(CancellationToken ct)
    {
        string state = GenerateState();
        TcpListener listener = new(IPAddress.Loopback, redirectPort);
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not bind localhost:{redirectPort} for OAuth callback. Another app may be using it.", ex);
        }
        string redirectUri = $"http://localhost:{redirectPort}/cb";
        const string scopes = "channel:manage:redemptions";
        string authUrl =
            "https://id.twitch.tv/oauth2/authorize"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=token"
            + $"&scope={Uri.EscapeDataString(scopes)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + "&force_verify=true";

        try
        {
            OpenBrowser(authUrl);

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(120));
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            string? capturedToken = null;

            while (capturedToken is null && !linked.Token.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await listener.AcceptTcpClientAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                using (tcp)
                {
                    tcp.NoDelay = true;
                    using NetworkStream stream = tcp.GetStream();
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;

                    (string method, string path, string body) = await ReadHttpRequestAsync(stream, linked.Token).ConfigureAwait(false);

                    if (method == "POST" && path.StartsWith("/token", StringComparison.Ordinal))
                    {
                        Dictionary<string, string> form = ParseForm(body);
                        form.TryGetValue("access_token", out string? tok);
                        form.TryGetValue("state", out string? st);
                        if (!string.IsNullOrEmpty(tok) && !string.IsNullOrEmpty(st) && ConstantTimeEquals(st, state))
                        {
                            capturedToken = tok;
                            await WriteResponseAsync(stream, 200, "text/plain", "OK", linked.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            await WriteResponseAsync(stream, 400, "text/plain", "Bad state", linked.Token).ConfigureAwait(false);
                        }
                    }
                    else if (method == "GET" && !path.StartsWith("/favicon", StringComparison.Ordinal))
                    {
                        await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", CallbackHtml, linked.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await WriteResponseAsync(stream, 404, "text/plain", "", linked.Token).ConfigureAwait(false);
                    }
                }
            }

            if (capturedToken is null)
            {
                return null;
            }

            ValidateResponse? validated = await helix.ValidateAsync(capturedToken, ct).ConfigureAwait(false);
            if (validated is null
                || string.IsNullOrEmpty(validated.UserId)
                || !validated.Scopes.Contains("channel:manage:redemptions"))
            {
                return null;
            }
            return new AuthResult(capturedToken, validated.UserId, validated.Login);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    private static string GenerateState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }

    private static Dictionary<string, string> ParseForm(string body)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(body))
        {
            return result;
        }
        foreach (string pair in body.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            string key = Uri.UnescapeDataString(pair[..eq].Replace('+', ' '));
            string value = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            result[key] = value;
        }
        return result;
    }

    private static async Task<(string method, string path, string body)> ReadHttpRequestAsync(NetworkStream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        int total = 0;
        int headerEnd = -1;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            total += read;
            for (int i = 0; i <= total - 4; i++)
            {
                if (buffer[i] == '\r' && buffer[i + 1] == '\n' && buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                {
                    headerEnd = i;
                    break;
                }
            }
            if (headerEnd >= 0)
            {
                break;
            }
        }
        if (headerEnd < 0)
        {
            return ("", "", "");
        }

        string headerText = Encoding.ASCII.GetString(buffer, 0, headerEnd);
        string[] lines = headerText.Split("\r\n");
        string[] requestLine = lines.Length > 0 ? lines[0].Split(' ') : Array.Empty<string>();
        string method = requestLine.Length > 0 ? requestLine[0] : "";
        string path = requestLine.Length > 1 ? requestLine[1] : "";

        int contentLength = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line.AsSpan(15).Trim(), out contentLength);
            }
        }

        string body = "";
        if (contentLength > 0)
        {
            int bodyStart = headerEnd + 4;
            int bodyAlready = total - bodyStart;
            int wanted = Math.Min(contentLength, 32 * 1024);
            byte[] bodyBuf = new byte[wanted];
            int copied = 0;
            if (bodyAlready > 0)
            {
                copied = Math.Min(bodyAlready, wanted);
                Buffer.BlockCopy(buffer, bodyStart, bodyBuf, 0, copied);
            }
            while (copied < wanted)
            {
                int read = await stream.ReadAsync(bodyBuf.AsMemory(copied, wanted - copied), ct).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                copied += read;
            }
            body = Encoding.UTF8.GetString(bodyBuf, 0, copied);
        }
        return (method, path, body);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string contentType, string body, CancellationToken ct)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string statusText = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            _ => "OK",
        };
        StringBuilder header = new();
        header.Append("HTTP/1.1 ").Append(status).Append(' ').Append(statusText).Append("\r\n");
        header.Append("Content-Type: ").Append(contentType).Append("\r\n");
        header.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
        header.Append("Cache-Control: no-store\r\n");
        header.Append("Connection: close\r\n");
        header.Append("\r\n");
        byte[] headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        await stream.WriteAsync(headerBytes, ct).ConfigureAwait(false);
        if (bodyBytes.Length > 0)
        {
            await stream.WriteAsync(bodyBytes, ct).ConfigureAwait(false);
        }
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }
}
