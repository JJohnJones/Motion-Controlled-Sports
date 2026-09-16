using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotionControllers
{
    // Transport-independent controller protocol. Called on Unity's main thread.
    public sealed class ControllerProtocolRouter : IDisposable
    {
        public sealed class Connection
        {
            public string ControllerId;
            internal string UiState = "";
            public int PlayerNumber { get; internal set; }
            internal string Token;
            internal double LastSeen, Opened, PingSent = -1, NextPing;
            internal bool AwaitHello = true, Paused;
            internal readonly Queue<double> Pings = new Queue<double>();
            public double LastPacketAt => LastSeen;
            public double LastPingAt { get; internal set; } = -1;
            public double LastPongAt { get; internal set; } = -1;
            public double LastPongRequestAt { get; internal set; } = -1;
            public bool InputPaused => Paused;
            public bool Authenticated => ControllerId != null && !AwaitHello;
            public double RttMs { get; internal set; } = -1;
        }
        [Serializable] private sealed class Reply
        {
            public int version = 1;
            public int playerNumber;
            public string type, controllerId;
            public double timestamp;
            public long sequence;
        }
        [Serializable] private sealed class UiModeMessage
        {
            public int version = 1;
            public string type = "ui-mode", mode = "menu", state = "";
            public bool paused;
        }
        private readonly UiModeMessage uiMode = new UiModeMessage();
        public void SetUiMode(string mode, bool paused)
        {
            if (string.IsNullOrEmpty(mode) || mode.Length > 32) mode = "menu";
            if (uiMode.mode == mode && uiMode.paused == paused) return;
            uiMode.mode = mode; uiMode.paused = paused;
            if (Application.isEditor || Debug.isDebugBuild) Debug.Log($"[Controller UI] mode={mode}, paused={paused}, peers={connections.Count}");
            foreach (var pair in connections)
                if (pair.Value.Authenticated) SendUi(pair.Key);
        }
        private void SendUi(string peer)
        {
            uiMode.state = connections[peer].UiState;
            transport.Send(peer, JsonUtility.ToJson(uiMode));
        }
        public void SetControllerUiState(string peer, string state)
        {
            if (!connections.TryGetValue(peer, out var c) || !c.Authenticated || c.UiState == state) return;
            c.UiState = state ?? ""; SendUi(peer);
        }
        private readonly ControllerManager manager;
        private readonly IControllerTransport transport;
        private readonly bool serverHeartbeat;
        private readonly bool manageTimeouts;
        private readonly Dictionary<string, Connection> connections = new Dictionary<string, Connection>();
        private readonly List<string> expired = new List<string>();
        public IReadOnlyDictionary<string, Connection> Connections => connections;
        public long InvalidPackets { get; private set; }
        public ControllerProtocolRouter(ControllerManager manager, IControllerTransport transport, bool serverHeartbeat = false, bool manageTimeouts = true)
        { this.manager = manager; this.transport = transport; this.serverHeartbeat = serverHeartbeat; this.manageTimeouts = manageTimeouts; }
        public void Open(string peer, string token, double now)
        {
            if (!manageTimeouts && connections.TryGetValue(peer, out var existing))
            { existing.AwaitHello = true; existing.Token = token; existing.Opened = now; existing.NextPing = now; return; }
            Drop(peer);
            connections.Add(peer, new Connection { Token = token, LastSeen = now, Opened = now });
        }
        public void PauseInput(string peer, bool pause)
        {
            if (!connections.TryGetValue(peer, out var c)) return;
            bool wasPaused = c.Paused;
            c.Paused = pause;
            if (wasPaused && !pause && c.Authenticated) SendUi(peer);
            if (pause && c.ControllerId != null) manager.CancelHeldInput(c.ControllerId);
        }
        public void Drop(string peer)
        {
            if (connections.TryGetValue(peer, out var c) && c.ControllerId != null) manager.Remove(c.ControllerId);
            connections.Remove(peer);
        }
        public void Dispose()
        {
            foreach (var c in connections.Values) if (c.ControllerId != null) manager.Remove(c.ControllerId);
            connections.Clear();
        }
        private void Reject(string peer) { InvalidPackets++; Drop(peer); transport.Close(peer); }
        public void Tick(double now)
        {
            expired.Clear();
            foreach (var pair in connections)
            {
                var c = pair.Value;
                if (manageTimeouts && (now - c.LastSeen > 15 || c.ControllerId == null && now - c.Opened > 5 || c.PingSent >= 0 && now - c.PingSent > 10))
                    expired.Add(pair.Key);
                else if (serverHeartbeat && c.Authenticated && (!manageTimeouts || c.PingSent < 0) && now >= c.NextPing)
                {
                    c.PingSent = now; c.NextPing = now + 2;
                    c.LastPingAt = now;
                    SendUi(pair.Key); // Refresh presentation after a missed transition or resumed page.
                    if (c.Pings.Count >= 32) c.Pings.Dequeue();
                    c.Pings.Enqueue(now);
                    ReplyTo(pair.Key, new Reply { type = "serverPing", controllerId = c.ControllerId, timestamp = now });
                }
            }
            foreach (var peer in expired) { Drop(peer); transport.Close(peer); }
        }
        public void Handle(string peer, string json, double now)
        {
            if (!connections.TryGetValue(peer, out var c)) return;
            if (json == null || json.Length > 8192 || !MotionJsonCodec.TryDecode(json, out var p)) { Reject(peer); return; }
            if (c.AwaitHello)
            {
                if (p.type != "hello" || string.IsNullOrEmpty(c.Token) || p.token != c.Token) { Reject(peer); return; }
                var id = c.ControllerId ?? Guid.NewGuid().ToString("N");
                if (c.ControllerId == null && !manager.Register(id)) { Reject(peer); return; }
                c.ControllerId = id; c.AwaitHello = false;
                c.PlayerNumber = manager.GetPlayerNumber(id);
                ReplyTo(peer, new Reply { type = "welcome", controllerId = id, playerNumber = c.PlayerNumber });
                SendUi(peer);
            }
            else if (p.controllerId != c.ControllerId) { Reject(peer); return; }
            else if (p.type == "ping" && MotionJsonCodec.Finite(p.timestamp) && p.timestamp >= 0)
                ReplyTo(peer, new Reply { type = "pong", timestamp = p.timestamp, controllerId = c.ControllerId });
            else if (serverHeartbeat && p.type == "serverPong" && MotionJsonCodec.Finite(p.timestamp))
            {
                // Late/duplicate pongs are harmless; never remove a player for a delayed heartbeat.
                if (c.Pings.Contains(p.timestamp) && p.timestamp > c.LastPongRequestAt)
                { c.RttMs = (now - p.timestamp) * 1000; c.LastPongAt = now; c.LastPongRequestAt = p.timestamp; c.PingSent = -1; }
            }
            else if (p.type == "button")
            {
                if (!MotionJsonCodec.TryButton(p, c.ControllerId, now, out var input)) { Reject(peer); return; }
                if (c.Paused) { c.LastSeen = now; return; }
                if (!manager.SubmitButton(input)) InvalidPackets++;
            }
            else if (p.type == "motion" || p.type == "calibrate")
            {
                if (!MotionJsonCodec.TryFrame(p, c.ControllerId, now, out var frame)) { Reject(peer); return; }
                if (c.Paused) { c.LastSeen = now; return; }
                bool accepted = manager.Submit(frame, p.type == "calibrate");
                if (!accepted) InvalidPackets++;
                else if (p.type == "calibrate") ReplyTo(peer, new Reply { type = "calibrated", controllerId = c.ControllerId, sequence = p.sequence });
            }
            else { Reject(peer); return; }
            c.LastSeen = now;
        }
        private void ReplyTo(string peer, Reply reply) => transport.Send(peer, JsonUtility.ToJson(reply));
    }
}
