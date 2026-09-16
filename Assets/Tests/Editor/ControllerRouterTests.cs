using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MotionControllers.Tests
{
    public class ControllerRouterTests
    {
        private sealed class Transport : IControllerTransport
        {
            public readonly List<string> Sent = new List<string>();
            public readonly List<string> Closed = new List<string>();
            public void Send(string id, string json) => Sent.Add(json);
            public void Close(string id) => Closed.Add(id);
        }
        [Test] public void PeerAuthenticationIsolationAndTransportCleanup()
        {
            var go = new GameObject("router test");
            try
            {
                var manager = go.AddComponent<ControllerManager>(); var wire = new Transport();
                using (var a = new ControllerProtocolRouter(manager, wire))
                using (var b = new ControllerProtocolRouter(manager, wire))
                {
                    a.Open("bad", "secret", 0); a.Handle("bad", "{\"version\":1,\"type\":\"hello\",\"token\":\"wrong\"}", 0);
                    Assert.That(manager.Sessions.Count, Is.Zero); Assert.That(wire.Closed, Does.Contain("bad"));
                    a.Open("a", "secret", 0); b.Open("b", "secret", 0);
                    const string hello = "{\"version\":1,\"type\":\"hello\",\"token\":\"secret\"}";
                    a.Handle("a", hello, 0); b.Handle("b", hello, 0);
                    Assert.That(manager.Sessions.Count, Is.EqualTo(2));
                    Assert.That(a.Connections["a"].PlayerNumber, Is.EqualTo(1));
                    Assert.That(b.Connections["b"].PlayerNumber, Is.EqualTo(2));
                    a.Dispose(); Assert.That(manager.Sessions.Count, Is.EqualTo(1));
                    Assert.That(manager.Sessions.ContainsKey(b.Connections["b"].ControllerId), Is.True);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void ServerHeartbeatMeasuresRttAndExpiresSilentController()
        {
            var go = new GameObject("heartbeat test");
            try
            {
                var wire = new Transport();
                using (var router = new ControllerProtocolRouter(go.AddComponent<ControllerManager>(), wire, true))
                {
                    router.Open("p", "secret", 0);
                    router.Handle("p", "{\"version\":1,\"type\":\"hello\",\"token\":\"secret\"}", 0);
                    string id = router.Connections["p"].ControllerId;
                    router.Tick(1);
                    Assert.That(wire.Sent[wire.Sent.Count - 1], Does.Contain("serverPing"));
                    router.Handle("p", "{\"version\":1,\"type\":\"serverPong\",\"controllerId\":\"" + id + "\",\"timestamp\":1}", 1.04);
                    Assert.That(router.Connections["p"].RttMs, Is.EqualTo(40).Within(.001));
                    router.Tick(3); router.Tick(14);
                    Assert.That(router.Connections.Count, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void LanHeartbeatRetriesAndReplacementChannelKeepsCalibratedPlayer()
        {
            var go = new GameObject("LAN recovery router");
            try
            {
                var manager = go.AddComponent<ControllerManager>(); var wire = new Transport();
                using (var router = new ControllerProtocolRouter(manager, wire, true, false))
                {
                    const string hello = "{\"version\":1,\"type\":\"hello\",\"token\":\"secret\"}";
                    router.Open("p", "secret", 0); router.Handle("p", hello, 0);
                    var c = router.Connections["p"]; var session = manager.Sessions[c.ControllerId];
                    router.Tick(1); router.Tick(3); router.Tick(13);
                    Assert.That(wire.Closed, Is.Empty, "Missed heartbeats must not delete a LAN controller");
                    Assert.That(wire.Sent.FindAll(message => message.Contains("serverPing")).Count, Is.EqualTo(3));
                    router.Handle("p", "{\"version\":1,\"type\":\"serverPong\",\"controllerId\":\"" + c.ControllerId + "\",\"timestamp\":3}", 13.1);
                    Assert.That(c.LastPongAt, Is.EqualTo(13.1));
                    router.PauseInput("p", true); router.Open("p", "secret", 14); router.Handle("p", hello, 14);
                    Assert.That(router.Connections["p"], Is.SameAs(c));
                    Assert.That(manager.Sessions[c.ControllerId], Is.SameAs(session));
                    Assert.That(c.PlayerNumber, Is.EqualTo(1)); Assert.That(c.Authenticated, Is.True);
                    router.Handle("p", "{\"version\":1,\"type\":\"serverPong\",\"controllerId\":\"" + c.ControllerId + "\",\"timestamp\":1}", 14.1);
                    Assert.That(c.LastPongAt, Is.EqualTo(13.1), "Old pong must not replace newer diagnostics");
                    Assert.That(router.InvalidPackets, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void UiModeIsReplayedOnJoinAndRecoveryWithoutChangingIdentity()
        {
            var go = new GameObject("UI mode router");
            try
            {
                var wire = new Transport();
                using (var router = new ControllerProtocolRouter(go.AddComponent<ControllerManager>(), wire, true, false))
                {
                    router.SetUiMode("bowling", false);
                    Assert.That(wire.Sent, Is.Empty);
                    const string hello = "{\"version\":1,\"type\":\"hello\",\"token\":\"secret\"}";
                    router.Open("p", "secret", 0); router.Handle("p", hello, 0);
                    string id = router.Connections["p"].ControllerId;
                    Assert.That(wire.Sent[wire.Sent.Count - 1], Does.Contain("bowling"));
                    int count = wire.Sent.Count; router.SetUiMode("bowling", false);
                    Assert.That(wire.Sent.Count, Is.EqualTo(count), "Unchanged modes are not streamed");
                    router.SetControllerUiState("p", "serve");
                    Assert.That(wire.Sent[wire.Sent.Count - 1], Does.Contain("serve"));
                    router.PauseInput("p", true); router.PauseInput("p", false);
                    Assert.That(wire.Sent[wire.Sent.Count - 1], Does.Contain("serve"), "Recovery replays per-controller presentation");
                    router.SetUiMode("bowling", true);
                    Assert.That(wire.Sent[wire.Sent.Count - 1], Does.Contain("\"paused\":true"));
                    router.PauseInput("p", true); router.SetUiMode("menu", false);
                    wire.Sent.Clear(); router.PauseInput("p", false);
                    Assert.That(wire.Sent[0], Does.Contain("menu"));
                    router.Open("p", "secret", 1); router.Handle("p", hello, 1);
                    Assert.That(wire.Sent[wire.Sent.Count - 1], Does.Contain("menu"));
                    Assert.That(router.Connections["p"].ControllerId, Is.EqualTo(id));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void PairingQrProducesOpaqueModulesWithQuietBorder()
        {
            var qr = ControllerPairingQr.Create("https://controller.example/#session=0123456789abcdef0123456789abcdef");
            try
            {
                Assert.That(qr.width, Is.GreaterThan(29));
                for (int x = 0; x < qr.width; x++) Assert.That(qr.GetPixel(x, 0), Is.EqualTo(Color.white));
                Assert.That(qr.filterMode, Is.EqualTo(FilterMode.Point));
            }
            finally { Object.DestroyImmediate(qr); }
        }
    }
}
