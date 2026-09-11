using System;
using System.Security.Cryptography;
using UnityEngine;

namespace MotionControllers
{
    [RequireComponent(typeof(ControllerManager))]
    public sealed class ControllerReceiver : MonoBehaviour
    {
        public int port = 8080;
        [Tooltip("Exact HTTPS origin, without a repository path. Restart Play after changing.")]
        public string allowedOrigin = "https://jjohnjones.github.io";
        public string PairingToken { get; private set; }
        public string Status { get; private set; } = "Stopped";
        public long InvalidPackets => router?.InvalidPackets ?? 0;
        private ControllerManager manager;
        private LoopbackWebSocketHost host;
        private ControllerProtocolRouter router;
        private void OnEnable()
        {
            manager = GetComponent<ControllerManager>();
            Application.runInBackground = true;
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            PairingToken = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            try
            {
                host = new LoopbackWebSocketHost(port, allowedOrigin);
                router = new ControllerProtocolRouter(manager, host);
                Status = "Listening on 127.0.0.1:" + port;
            }
            catch (Exception e) { Status = "Listener failed: " + e.Message; UnityEngine.Debug.LogError(Status, this); }
        }
        private void OnDisable()
        {
            host?.Dispose(); host = null;
            router?.Dispose(); router = null; Status = "Stopped";
        }
        private void Update()
        {
            if (host == null) return;
            if (host.LastError != null) Status = host.LastError;
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < 256 && host.TryRead(out var incoming); i++)
            {
                if (incoming.Kind == LoopbackWebSocketHost.EventKind.Connected)
                    router.Open(incoming.PeerId, PairingToken, now);
                else if (incoming.Kind == LoopbackWebSocketHost.EventKind.Disconnected) router.Drop(incoming.PeerId);
                else router.Handle(incoming.PeerId, incoming.Text, now);
            }
            router.Tick(now);
        }
    }
}
