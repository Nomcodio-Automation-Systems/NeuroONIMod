using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jennifer.Wpf;

/// <summary>
/// Hosted TestServer which exposes a simple WebSocket endpoint compatible with Randy-style messages.
/// Incoming text frames are forwarded to the JenniferServer event bus and relayed to all other
/// connected clients so that the game and Jennifer can exchange messages through this broker.
/// </summary>
public sealed class TestServerService : BackgroundService
{
    private const string WebSocketPrefix = "http://localhost:8000/";
    private const string HttpPrefix = "http://localhost:1337/";
    private HttpListener? _listener;
    private int _clientCount;

    // All currently connected WebSocket clients, keyed by a unique id so we can exclude the sender.
    private readonly ConcurrentDictionary<Guid, WebSocket> _connectedClients = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(WebSocketPrefix);
        _listener.Prefixes.Add(HttpPrefix);
        _listener.Start();
        // Notify UI that the test server is listening
        try { JenniferServer.RaiseMessageReceived($"[TestServer] Listening on {WebSocketPrefix} and {HttpPrefix}"); } catch { }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                // Only accept WebSocket upgrade requests
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                _ = Task.Run(async () => await HandleWebSocketContextAsync(context, stoppingToken), stoppingToken);
            }
        }
        catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
        {
            // Listener was stopped due to cancellation - ignore
        }
        finally
        {
            try { _listener?.Stop(); } catch { }
            try { JenniferServer.RaiseMessageReceived($"[TestServer] Stopped"); } catch { }
        }
    }

    private async Task HandleWebSocketContextAsync(HttpListenerContext context, CancellationToken token)
    {
        HttpListenerWebSocketContext wsContext = null!;
        Guid clientId = Guid.NewGuid();
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            using WebSocket webSocket = wsContext.WebSocket;

            // Track connected clients and notify UI
            _connectedClients[clientId] = webSocket;
            Interlocked.Increment(ref _clientCount);
            try { JenniferServer.RaiseStatusChanged($"Clients: {_clientCount}"); } catch { }
            try { JenniferServer.RaiseMessageReceived($"[TestServer] WebSocket client connected"); } catch { }

            // If the UI provided startup/register payloads, send them to the newly connected client
            try
            {
                string? startup = JenniferServer.BuildStartupPayload?.Invoke();
                if (!string.IsNullOrWhiteSpace(startup))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(startup);
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                string? actionsRegister = JenniferServer.BuildActionsRegisterPayload?.Invoke();
                if (!string.IsNullOrWhiteSpace(actionsRegister))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(actionsRegister);
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch
            {
                // ignore send failures to avoid breaking the accept loop
            }

            var buffer = new byte[4096];

            while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                int count = result.Count;
                var sb = new StringBuilder();
                sb.Append(Encoding.UTF8.GetString(buffer, 0, count));

                while (!result.EndOfMessage)
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }

                string message = sb.ToString();

                // Relay the frame to all other connected clients so the game and Jennifer can talk.
                await BroadcastAsync(clientId, message, token);

                // Do NOT fire RaiseMessageReceived for relayed frames — Jennifer's own WebSocket
                // receive loop already processes them via the broadcast above, so raising the event
                // here would cause every message to be processed twice.
            }

            // Notify UI that the websocket client disconnected and update client count
            Interlocked.Decrement(ref _clientCount);
            try { JenniferServer.RaiseStatusChanged($"Clients: {_clientCount}"); } catch { }
            try { JenniferServer.RaiseMessageReceived($"[TestServer] WebSocket client disconnected"); } catch { }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (Exception) { }
        finally
        {
            _connectedClients.TryRemove(clientId, out _);
            try { wsContext?.WebSocket?.Dispose(); } catch { }
            try { context.Response.Close(); } catch { }
        }
    }

    /// <summary>
    /// Sends a text frame to every connected client except the originating sender.
    /// </summary>
    /// <param name="senderId">The client id that produced the message.</param>
    /// <param name="message">The raw text frame to relay.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task BroadcastAsync(Guid senderId, string message, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var (id, ws) in _connectedClients)
        {
            if (id == senderId || ws.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, token);
            }
            catch
            {
                // A stale or closing socket – skip it; it will be cleaned up by its own receive loop.
            }
        }
    }
}
