using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MotionControllers
{
    // No Unity API here. TCP is loopback-only; a trusted TLS proxy exposes WSS.
    // .NET owns RFC6455 framing, masking, fragmentation, ping/pong and close handling.
    public sealed class LoopbackWebSocketHost : IDisposable, IControllerTransport
    {
        public enum EventKind { Connected, Text, Disconnected }
        public readonly struct Incoming
        {
            public readonly EventKind Kind;
            public readonly string PeerId, Text;
            public Incoming(EventKind kind, string id, string text = null) { Kind = kind; PeerId = id; Text = text; }
        }
        private sealed class Peer
        {
            public readonly TcpClient Client;
            public WebSocket Socket;
            public readonly ConcurrentQueue<string> Outgoing = new ConcurrentQueue<string>();
            public readonly SemaphoreSlim Ready = new SemaphoreSlim(0);
            public int Pending;
            public Peer(TcpClient client) { Client = client; }
        }
        private readonly ConcurrentDictionary<string, Peer> peers = new ConcurrentDictionary<string, Peer>();
        private readonly ConcurrentQueue<Incoming> incoming = new ConcurrentQueue<Incoming>();
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly TcpListener listener;
        private readonly string allowedOrigin;
        private int queued;
        public string LastError { get; private set; }
        public const int MaxMessageBytes = 8192;

        public LoopbackWebSocketHost(int port, string allowedOrigin)
        {
            this.allowedOrigin = allowedOrigin.Trim().TrimEnd('/');
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(8);
            _ = AcceptLoop();
        }

        public bool TryRead(out Incoming item)
        {
            if (!incoming.TryDequeue(out item)) return false;
            Interlocked.Decrement(ref queued);
            return true;
        }
        private bool Enqueue(Incoming item)
        {
            // Lifecycle events are bounded by the eight-peer limit. Text backlog is capped.
            if (item.Kind == EventKind.Text && Volatile.Read(ref queued) >= 256) return false;
            Interlocked.Increment(ref queued);
            incoming.Enqueue(item);
            return true;
        }
        public void Send(string id, string text)
        {
            if (!peers.TryGetValue(id, out var peer)) return;
            if (Interlocked.Increment(ref peer.Pending) > 16) { Close(id); return; }
            peer.Outgoing.Enqueue(text);
            peer.Ready.Release();
        }
        public void Close(string id)
        {
            if (peers.TryGetValue(id, out var peer)) { peer.Socket?.Abort(); peer.Client.Close(); }
        }
        private async Task AcceptLoop()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    if (stop.IsCancellationRequested || peers.Count >= 8 || Volatile.Read(ref queued) >= 256)
                    { client.Close(); continue; }
                    client.NoDelay = true;
                    var id = Guid.NewGuid().ToString("N");
                    var peer = new Peer(client);
                    peers[id] = peer;
                    _ = Serve(id, peer);
                }
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            { if (!stop.IsCancellationRequested) LastError = e.Message; }
        }

        private async Task Serve(string id, Peer peer)
        {
            bool connected = false;
            using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                Task sender = null;
                try
                {
                    var stream = peer.Client.GetStream();
                    using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                    {
                        handshake.CancelAfter(TimeSpan.FromSeconds(5));
                        if (!await Upgrade(stream, handshake.Token).ConfigureAwait(false)) return;
                    }
                    peer.Socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(5));
                    connected = true;
                    Enqueue(new Incoming(EventKind.Connected, id));
                    sender = SendLoop(peer, lifetime.Token);
                    var buffer = new byte[MaxMessageBytes];
                    var utf8 = new UTF8Encoding(false, true);
                    while (!lifetime.IsCancellationRequested && peer.Socket.State == WebSocketState.Open)
                    {
                        int length = 0;
                        WebSocketReceiveResult part;
                        do
                        {
                            part = await peer.Socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), lifetime.Token).ConfigureAwait(false);
                            if (part.MessageType == WebSocketMessageType.Close) return;
                            if (part.MessageType != WebSocketMessageType.Text) throw new IOException("Only text protocol v1 is supported.");
                            length += part.Count;
                            if (length >= buffer.Length) throw new IOException("Message too large.");
                        } while (!part.EndOfMessage);
                        if (!Enqueue(new Incoming(EventKind.Text, id, utf8.GetString(buffer, 0, length))))
                            throw new IOException("Input queue full; reconnect to discard stale input.");
                    }
                }
                catch (Exception e) when (e is IOException || e is WebSocketException || e is OperationCanceledException ||
                    e is ObjectDisposedException || e is SocketException || e is ArgumentException)
                { /* Disconnect is surfaced on the main thread. Malformed clients cannot crash the listener. */ }
                finally
                {
                    lifetime.Cancel();
                    peer.Socket?.Abort();
                    peer.Client.Close();
                    if (sender != null) { try { await sender.ConfigureAwait(false); } catch (Exception) { } }
                    peer.Socket?.Dispose();
                    peers.TryRemove(id, out _);
                    if (connected) Enqueue(new Incoming(EventKind.Disconnected, id));
                }
            }
        }

        private static async Task SendLoop(Peer peer, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await peer.Ready.WaitAsync(token).ConfigureAwait(false);
                    if (!peer.Outgoing.TryDequeue(out var text)) continue;
                    Interlocked.Decrement(ref peer.Pending);
                    var bytes = Encoding.UTF8.GetBytes(text);
                    await peer.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                }
            }
            catch (Exception) { peer.Socket?.Abort(); peer.Client.Close(); }
        }

        private async Task<bool> Upgrade(NetworkStream stream, CancellationToken token)
        {
            // Read exactly through CRLFCRLF so we cannot consume the first WebSocket frame.
            var bytes = new byte[MaxMessageBytes];
            int length = 0;
            while (length < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes, length, 1, token).ConfigureAwait(false);
                if (read == 0) return false;
                length++;
                if (length >= 4 && bytes[length - 4] == 13 && bytes[length - 3] == 10 &&
                    bytes[length - 2] == 13 && bytes[length - 1] == 10) break;
            }
            var lines = Encoding.ASCII.GetString(bytes, 0, length).Split(new[] { "\r\n" }, StringSplitOptions.None);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0) headers[lines[i].Substring(0, colon)] = lines[i].Substring(colon + 1).Trim();
            }
            bool valid = length < bytes.Length && lines[0] == "GET /controller HTTP/1.1" &&
                headers.TryGetValue("Upgrade", out var upgrade) && upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase) &&
                headers.TryGetValue("Connection", out var connection) && Array.Exists(connection.Split(','), v => v.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) &&
                headers.TryGetValue("Sec-WebSocket-Version", out var version) && version == "13" &&
                headers.TryGetValue("Origin", out var origin) && origin == allowedOrigin;
            string key = headers.TryGetValue("Sec-WebSocket-Key", out var k) ? k : "";
            try { valid &= Convert.FromBase64String(key).Length == 16; } catch (FormatException) { valid = false; }
            string response;
            if (!valid) response = "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            else
            {
                string accept;
                using (var sha = SHA1.Create()) accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";
            }
            var output = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(output, 0, output.Length, token).ConfigureAwait(false);
            return valid;
        }

        public void Dispose()
        {
            stop.Cancel();
            listener.Stop();
            foreach (var id in peers.Keys) Close(id);
        }
    }
}
