using NUnit.Framework;

namespace MotionControllers.Tests
{
    public class ControllerEndpointConfigurationTests
    {
        private const string Pwa = "https://jjohnjones.github.io/Motion-Controller-Website/";
        private const string Signal = "wss://motion-controller-signaling.onrender.com/signal";

        [Test] public void RealGitHubPagesAndRenderUrlsAreAcceptedInProduction()
        {
            Assert.That(ControllerEndpointConfiguration.TryValidate(Pwa, Signal, false, out var uri, out var error), Is.True, error);
            Assert.That(uri.AbsoluteUri, Is.EqualTo(Signal));
        }
        [TestCase("wss://SIGNALING-HOST/signal")]
        [TestCase("wss://signaling-host/signal")]
        [TestCase("ws://motion-controller-signaling.onrender.com/signal")]
        [TestCase("ws://127.0.0.1:8081/signal")]
        [TestCase("wss://motion-controller-signaling.onrender.com/wrong")]
        [TestCase("wss://motion-controller-signaling.onrender.com/signal?token=x")]
        [TestCase("wss://motion-controller-signaling.onrender.com/signal#secret")]
        [TestCase("wss://user:password@motion-controller-signaling.onrender.com/signal")]
        [TestCase("")]
        [TestCase(null)]
        public void UnsafeOrUnconfiguredSignalingIsRejectedInProduction(string url)
        {
            Assert.That(ControllerEndpointConfiguration.TryValidate(Pwa, url, false, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }
        [TestCase("http://jjohnjones.github.io/Motion-Controller-Website/")]
        [TestCase("https://jjohnjones.github.io/Motion-Controller-Website/#session=x")]
        [TestCase("https://user:password@jjohnjones.github.io/")]
        [TestCase("")]
        [TestCase(null)]
        public void UnsafePwaUrlIsRejected(string url)
        {
            Assert.That(ControllerEndpointConfiguration.TryValidate(url, Signal, false, out _, out _), Is.False);
        }
        [Test] public void InsecureTestExceptionIsEditorLoopbackOnly()
        {
            Assert.That(ControllerEndpointConfiguration.TryValidate(Pwa, "ws://127.0.0.1:8081/signal", true, out _, out _), Is.True);
            Assert.That(ControllerEndpointConfiguration.TryValidate(Pwa, "ws://192.168.1.2:8081/signal", true, out _, out _), Is.False);
        }
    }
}
