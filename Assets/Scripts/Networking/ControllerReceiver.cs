using System;
using System.Collections.Generic;
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
        public long InvalidPackets { get; private set; }
        private ControllerManager manager;
        private LoopbackWebSocketHost host;
        private readonly Dictionary<string, Connection> connections = new Dictionary<string, Connection>();
        private readonly List<string> expired = new List<string>();
        private sealed class Connection { public string ControllerId; public double LastSeen, Opened; }
        [Serializable] private sealed class Reply
        {
            public int version = 1;
            public string type, controllerId;
            public double timestamp;
            public long sequence;
        }

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
                Status = "Listening on 127.0.0.1:" + port;
            }
            catch (Exception e) { Status = "Listener failed: " + e.Message; UnityEngine.Debug.LogError(Status, this); }
        }
        private void OnDisable()
        {
            host?.Dispose(); host = null;
            connections.Clear(); manager?.Clear(); Status = "Stopped";
        }
        private void Update()
        {
            if (host == null) return;
            if (host.LastError != null) Status = host.LastError;
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < 256 && host.TryRead(out var incoming); i++)
            {
                if (incoming.Kind == LoopbackWebSocketHost.EventKind.Connected)
                    connections[incoming.PeerId] = new Connection { LastSeen = now, Opened = now };
                else if (incoming.Kind == LoopbackWebSocketHost.EventKind.Disconnected) Drop(incoming.PeerId);
                else Handle(incoming.PeerId, incoming.Text, now);
            }
            expired.Clear();
            foreach (var pair in connections)
                if (now - pair.Value.LastSeen > 15 || (pair.Value.ControllerId == null && now - pair.Value.Opened > 5)) expired.Add(pair.Key);
            foreach (var id in expired) { host.Close(id); Drop(id); }
        }
        private void Drop(string peer)
        {
            if (connections.TryGetValue(peer, out var c) && c.ControllerId != null) manager.Remove(c.ControllerId);
            connections.Remove(peer);
        }
        private void Reject(string peer) { InvalidPackets++; host.Close(peer); Drop(peer); }
        private void Handle(string peer, string json, double now)
        {
            if (!connections.TryGetValue(peer, out var c)) return;
            if (!MotionJsonCodec.TryDecode(json, out var p)) { Reject(peer); return; }
            if (c.ControllerId == null)
            {
                if (p.type != "hello" || p.token != PairingToken) { Reject(peer); return; }
                var id = Guid.NewGuid().ToString("N");
                if (!manager.Register(id)) { Reject(peer); return; }
                c.ControllerId = id;
                ReplyTo(peer, new Reply { type = "welcome", controllerId = id });
            }
            else if (p.controllerId != c.ControllerId) { Reject(peer); return; }
            else if (p.type == "ping" && MotionJsonCodec.Finite(p.timestamp))
                ReplyTo(peer, new Reply { type = "pong", timestamp = p.timestamp, controllerId = c.ControllerId });
            else if (p.type == "button")
            {
                if (!MotionJsonCodec.TryButton(p, c.ControllerId, now, out var input)) { Reject(peer); return; }
                if (!manager.SubmitButton(input)) InvalidPackets++;
            }
            else if (p.type == "motion" || p.type == "calibrate")
            {
                if (!MotionJsonCodec.TryFrame(p, c.ControllerId, now, out var frame)) { Reject(peer); return; }
                bool accepted = manager.Submit(frame, p.type == "calibrate");
                if (!accepted) InvalidPackets++;
                else if (p.type == "calibrate") ReplyTo(peer, new Reply { type = "calibrated", controllerId = c.ControllerId, sequence = p.sequence });
            }
            else { Reject(peer); return; }
            c.LastSeen = now;
        }
        private void ReplyTo(string peer, Reply reply) => host.Send(peer, JsonUtility.ToJson(reply));
    }
}
