using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MotionControllers.Tests
{
    public sealed class WebSocketHostTests
    {
        private const string Origin = "https://controller.example";
        [Test] public void RealClientsCanExchangeFragmentedTextAndReconnect() => Task.Run(Exchange).GetAwaiter().GetResult();
        private static async Task Exchange()
        {
            int port = FreePort();
            using (var host = new LoopbackWebSocketHost(port, Origin))
            using (var timeout = new CancellationTokenSource(10000))
            {
                string firstId;
                using (var first = await Connect(port, timeout.Token))
                using (var second = await Connect(port, timeout.Token))
                {
                    firstId = (await Next(host, LoopbackWebSocketHost.EventKind.Connected, timeout.Token)).PeerId;
                    var secondId = (await Next(host, LoopbackWebSocketHost.EventKind.Connected, timeout.Token)).PeerId;
                    Assert.That(firstId, Is.Not.EqualTo(secondId));
                    var a = Encoding.UTF8.GetBytes("{\"value\":\""); var b = Encoding.UTF8.GetBytes("phone ✓\"}");
                    await first.SendAsync(new ArraySegment<byte>(a), WebSocketMessageType.Text, false, timeout.Token);
                    await first.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, timeout.Token);
                    var message = await Next(host, LoopbackWebSocketHost.EventKind.Text, timeout.Token);
                    Assert.That(message.PeerId, Is.EqualTo(firstId));
                    Assert.That(message.Text, Is.EqualTo("{\"value\":\"phone ✓\"}"));
                    host.Send(firstId, "ack");
                    var buffer = new byte[32];
                    var result = await first.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                    Assert.That(Encoding.UTF8.GetString(buffer, 0, result.Count), Is.EqualTo("ack"));
                    host.Close(firstId);
                    Assert.That((await Next(host, LoopbackWebSocketHost.EventKind.Disconnected, timeout.Token)).PeerId, Is.EqualTo(firstId));
                }
                using (var replacement = await Connect(port, timeout.Token))
                    Assert.That((await Next(host, LoopbackWebSocketHost.EventKind.Connected, timeout.Token)).PeerId, Is.Not.EqualTo(firstId));
            }
        }
        [Test] public void WrongOriginFailsUpgrade() => Task.Run(WrongOrigin).GetAwaiter().GetResult();
        private static async Task WrongOrigin()
        {
            int port = FreePort();
            using (var host = new LoopbackWebSocketHost(port, Origin))
            using (var client = new ClientWebSocket())
            using (var timeout = new CancellationTokenSource(10000))
            {
                client.Options.SetRequestHeader("Origin", "https://wrong.example");
                try { await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/controller"), timeout.Token); Assert.Fail("Upgrade must reject the wrong origin."); }
                catch (WebSocketException) { }
            }
        }
        [Test] public void OversizedMessageDisconnectsPeer() => Task.Run(Oversized).GetAwaiter().GetResult();
        private static async Task Oversized()
        {
            int port = FreePort();
            using (var host = new LoopbackWebSocketHost(port, Origin))
            using (var timeout = new CancellationTokenSource(10000))
            using (var client = await Connect(port, timeout.Token))
            {
                var id = (await Next(host, LoopbackWebSocketHost.EventKind.Connected, timeout.Token)).PeerId;
                try { await client.SendAsync(new ArraySegment<byte>(new byte[9000]), WebSocketMessageType.Text, true, timeout.Token); }
                catch (WebSocketException) { }
                Assert.That((await Next(host, LoopbackWebSocketHost.EventKind.Disconnected, timeout.Token)).PeerId, Is.EqualTo(id));
            }
        }
        private static async Task<ClientWebSocket> Connect(int port, CancellationToken token)
        {
            var client = new ClientWebSocket(); client.Options.SetRequestHeader("Origin", Origin);
            await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/controller"), token);
            return client;
        }
        private static async Task<LoopbackWebSocketHost.Incoming> Next(LoopbackWebSocketHost host, LoopbackWebSocketHost.EventKind kind, CancellationToken token)
        {
            while (true)
            {
                while (host.TryRead(out var item)) if (item.Kind == kind) return item;
                await Task.Delay(5, token);
            }
        }
        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
        }
    }
}
