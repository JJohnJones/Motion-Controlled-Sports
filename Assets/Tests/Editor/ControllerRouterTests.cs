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
                    Assert.That(wire.Sent[1], Does.Contain("serverPing"));
                    router.Handle("p", "{\"version\":1,\"type\":\"serverPong\",\"controllerId\":\"" + id + "\",\"timestamp\":1}", 1.04);
                    Assert.That(router.Connections["p"].RttMs, Is.EqualTo(40).Within(.001));
                    router.Tick(3); router.Tick(14);
                    Assert.That(router.Connections.Count, Is.Zero);
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
