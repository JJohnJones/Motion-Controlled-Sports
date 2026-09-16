using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MotionControllers
{
    // Background WSS I/O. Unity objects and JSON are handled by the main-thread transport.
    public sealed class ControllerSignalingClient : IDisposable
    {
        private readonly ClientWebSocket socket = new ClientWebSocket();
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<byte[]> outgoing = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim ready = new SemaphoreSlim(0);
        public volatile bool Connected, Finished;
        public volatile string State = "Connecting";
        private bool ending;
        public string Error { get; private set; }
        public ControllerSignalingClient(Uri uri) { _ = Task.Run(() => Run(uri)); }
        public bool TryRead(out string json) => incoming.TryDequeue(out json);
        public bool Send(string json)
        {
            if (ending || !Connected || outgoing.Count >= 128) return false;
            outgoing.Enqueue(Encoding.UTF8.GetBytes(json)); ready.Release();
            return true;
        }
        private async Task Run(Uri uri)
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                { timeout.CancelAfter(10000); await socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false); }
                Connected = true; State = "Open";
                var writer = Write();
                var buffer = new byte[65536];
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        int count = 0; WebSocketReceiveResult result;
                        do
                        {
                            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), stop.Token).ConfigureAwait(false);
                            if (result.MessageType == WebSocketMessageType.Close)
                            { Error = "WebSocket close: " + result.CloseStatus + " " + result.CloseStatusDescription; return; }
                            count += result.Count;
                            if (result.MessageType != WebSocketMessageType.Text || count >= buffer.Length || incoming.Count >= 256)
                                throw new InvalidOperationException("Signaling limits exceeded");
                        } while (!result.EndOfMessage);
                        incoming.Enqueue(Encoding.UTF8.GetString(buffer, 0, count));
                    }
                }
                finally { stop.Cancel(); try { await writer.ConfigureAwait(false); } catch (OperationCanceledException) { } }
            }
            catch (OperationCanceledException) { if (!stop.IsCancellationRequested) Error = "Signaling timed out"; }
            catch (Exception e) { Error = e.Message; }
            finally { Connected = false; State = "Closed"; Finished = true; socket.Dispose(); }
        }
        private async Task Write()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    await ready.WaitAsync(stop.Token).ConfigureAwait(false);
                    if (outgoing.TryDequeue(out var bytes))
                        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, stop.Token).ConfigureAwait(false);
                    if (ending && outgoing.IsEmpty) stop.Cancel();
                }
            }
            catch { stop.Cancel(); throw; }
        }
        public void Dispose()
        {
            stop.Cancel();
            // The receive task may already have disposed a failed/closed socket.
            try { socket.Abort(); } catch (ObjectDisposedException) { }
        }
        public void End(string json)
        {
            if (!Send(json)) { Dispose(); return; }
            ending = true; State = "Closing"; stop.CancelAfter(1000);
        }
    }
}
