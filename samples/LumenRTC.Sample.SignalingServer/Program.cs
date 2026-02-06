using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LumenRTC.Sample.SignalingServer;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var url = GetArg(args, "--url", "http://localhost:8080/ws/");
        if (!url.EndsWith("/"))
        {
            url += "/";
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        Console.WriteLine($"Signaling server listening on {url}");
        Console.WriteLine("Clients connect with ws://host:port/ws/?room=demo");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var rooms = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, WebSocket>>();

        while (!cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cts.IsCancellationRequested)
            {
                break;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            _ = Task.Run(() => HandleConnectionAsync(context, rooms, cts.Token));
        }
    }

    private static async Task HandleConnectionAsync(
        HttpListenerContext context,
        ConcurrentDictionary<string, ConcurrentDictionary<Guid, WebSocket>> rooms,
        CancellationToken token)
    {
        string room = context.Request.QueryString["room"] ?? "default";
        WebSocket socket;
        try
        {
            var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            socket = wsContext.WebSocket;
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.Close();
            Console.WriteLine($"Failed to accept websocket: {ex.Message}");
            return;
        }

        var id = Guid.NewGuid();
        var roomSockets = rooms.GetOrAdd(room, _ => new ConcurrentDictionary<Guid, WebSocket>());
        roomSockets[id] = socket;
        Console.WriteLine($"[{room}] connected {id}");
        await SendRoomStateAsync(socket, roomSockets.Count, token).ConfigureAwait(false);
        await BroadcastRoomEventAsync(roomSockets, "peer_joined", id, roomSockets.Count, token, excludePeerId: id).ConfigureAwait(false);

        try
        {
            await ReceiveLoopAsync(room, id, socket, rooms, token).ConfigureAwait(false);
        }
        finally
        {
            roomSockets.TryRemove(id, out _);
            if (!roomSockets.IsEmpty)
            {
                await BroadcastRoomEventAsync(roomSockets, "peer_left", id, roomSockets.Count, token, excludePeerId: Guid.Empty).ConfigureAwait(false);
            }
            if (roomSockets.IsEmpty)
            {
                rooms.TryRemove(room, out _);
            }

            try
            {
                socket.Abort();
                socket.Dispose();
            }
            catch
            {
                // ignore
            }

            Console.WriteLine($"[{room}] disconnected {id}");
        }
    }

    private static async Task SendRoomStateAsync(
        WebSocket socket,
        int peerCount,
        CancellationToken token)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new
        {
            type = "room_state",
            peerCount
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }
        catch
        {
            // ignore send errors
        }
    }

    private static async Task BroadcastRoomEventAsync(
        ConcurrentDictionary<Guid, WebSocket> peers,
        string type,
        Guid peerId,
        int peerCount,
        CancellationToken token,
        Guid excludePeerId)
    {
        var json = JsonSerializer.Serialize(new
        {
            type,
            peerId = peerId.ToString(),
            peerCount
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var peer in peers)
        {
            if (excludePeerId != Guid.Empty && peer.Key == excludePeerId)
            {
                continue;
            }
            if (peer.Value.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                await peer.Value.SendAsync(bytes, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            }
            catch
            {
                // ignore send errors
            }
        }
    }

    private static async Task ReceiveLoopAsync(
        string room,
        Guid id,
        WebSocket socket,
        ConcurrentDictionary<string, ConcurrentDictionary<Guid, WebSocket>> rooms,
        CancellationToken token)
    {
        var buffer = new byte[64 * 1024];

        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            string? message = await ReceiveMessageAsync(socket, buffer, token).ConfigureAwait(false);
            if (message == null)
            {
                break;
            }

            if (!rooms.TryGetValue(room, out var peers))
            {
                continue;
            }

            foreach (var peer in peers)
            {
                if (peer.Key == id)
                {
                    continue;
                }

                if (peer.Value.State != WebSocketState.Open)
                {
                    continue;
                }

                try
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await peer.Value.SendAsync(bytes, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                }
                catch
                {
                    // ignore send errors
                }
            }
        }
    }

    private static async Task<string?> ReceiveMessageAsync(WebSocket socket, byte[] buffer, CancellationToken token)
    {
        using var stream = new MemoryStream();
        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.Count > 0)
            {
                stream.Write(buffer, 0, result.Count);
            }

            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string GetArg(string[] args, string name, string fallback)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring(name.Length + 1);
            }
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return fallback;
    }
}
