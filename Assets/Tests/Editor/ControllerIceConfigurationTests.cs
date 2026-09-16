using System;
using NUnit.Framework;
using Unity.WebRTC;
namespace MotionControllers.Tests
{
    public class ControllerIceConfigurationTests
    {
        private static ControllerIceConfiguration Config(string mode = "all") => new ControllerIceConfiguration {
            expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 600000, mode = mode,
            iceServers = new[] { new ControllerIceServer { urls = new[] {"stun:stun.cloudflare.com:3478"} },
                new ControllerIceServer { urls = new[] {"turn:turn.cloudflare.com:3478?transport=udp", "turn:turn.cloudflare.com:3478?transport=tcp", "turns:turn.cloudflare.com:443?transport=tcp"}, username = "temporary", credential = "temporary-password" } }
        };
        [Test] public void NormalPolicyAllowsDirectAndRelayAndPreservesAllProviderTransports() {
            var c = Config(); var rtc = c.Build();
            Assert.That(rtc.iceTransportPolicy, Is.EqualTo(RTCIceTransportPolicy.All));
            Assert.That(rtc.iceServers[1].urls.Length, Is.EqualTo(3)); Assert.That(c.HasTurn, Is.True);
            Assert.That(rtc.iceServers[1].credential, Is.EqualTo("temporary-password"));
        }
        [Test] public void EditorRelayDiagnosticIsExplicit() {
            Assert.That(Config("relay").Build().iceTransportPolicy, Is.EqualTo(RTCIceTransportPolicy.Relay));
        }
        [Test] public void ExpiredCredentialsAndUnexpectedURLsAreRejected() {
            var c = Config(); c.expiresAt = 0; Assert.Throws<ArgumentException>(() => c.Build());
            c = Config(); c.iceServers[1].urls[0] = "https://evil.example"; Assert.Throws<ArgumentException>(() => c.Build());
            c = Config(); c.iceServers[1].credential = null; Assert.Throws<ArgumentException>(() => c.Build());
        }
    }
}
