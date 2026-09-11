using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotionControllers
{
    // Shared by the legacy WebSocket receiver and LAN WebRTC. Called on Unity's main thread.
    public sealed class ControllerProtocolRouter : IDisposable
    {
        public sealed class Connection
        {
            public string ControllerId;
            public int PlayerNumber { get; internal set; }
            internal string Token;
            internal double LastSeen, Opened, PingSent = -1, NextPing;
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
        private readonly ControllerManager manager;
        private readonly IControllerTransport transport;
        private readonly bool serverHeartbeat;
        private readonly Dictionary<string, Connection> connections = new Dictionary<string, Connection>();
        private readonly List<string> expired = new List<string>();
        public IReadOnlyDictionary<string, Connection> Connections => connections;
        public long InvalidPackets { get; private set; }
        public ControllerProtocolRouter(ControllerManager manager, IControllerTransport transport, bool serverHeartbeat = false)
        { this.manager = manager; this.transport = transport; this.serverHeartbeat = serverHeartbeat; }
        public void Open(string peer, string token, double now)
        {
            Drop(peer);
            connections.Add(peer, new Connection { Token = token, LastSeen = now, Opened = now });
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
                if (now - c.LastSeen > 15 || c.ControllerId == null && now - c.Opened > 5 || c.PingSent >= 0 && now - c.PingSent > 10)
                    expired.Add(pair.Key);
                else if (serverHeartbeat && c.ControllerId != null && c.PingSent < 0 && now >= c.NextPing)
                {
                    c.PingSent = now; c.NextPing = now + 2;
                    ReplyTo(pair.Key, new Reply { type = "serverPing", controllerId = c.ControllerId, timestamp = now });
                }
            }
            foreach (var peer in expired) { Drop(peer); transport.Close(peer); }
        }
        public void Handle(string peer, string json, double now)
        {
            if (!connections.TryGetValue(peer, out var c)) return;
            if (json == null || json.Length > 8192 || !MotionJsonCodec.TryDecode(json, out var p)) { Reject(peer); return; }
            if (c.ControllerId == null)
            {
                if (p.type != "hello" || string.IsNullOrEmpty(c.Token) || p.token != c.Token) { Reject(peer); return; }
                var id = Guid.NewGuid().ToString("N");
                if (!manager.Register(id)) { Reject(peer); return; }
                c.ControllerId = id; c.Token = null;
                c.PlayerNumber = manager.GetPlayerNumber(id);
                ReplyTo(peer, new Reply { type = "welcome", controllerId = id, playerNumber = c.PlayerNumber });
            }
            else if (p.controllerId != c.ControllerId) { Reject(peer); return; }
            else if (p.type == "ping" && MotionJsonCodec.Finite(p.timestamp) && p.timestamp >= 0)
                ReplyTo(peer, new Reply { type = "pong", timestamp = p.timestamp, controllerId = c.ControllerId });
            else if (serverHeartbeat && p.type == "serverPong" && c.PingSent >= 0 && p.timestamp == c.PingSent)
            { c.RttMs = (now - c.PingSent) * 1000; c.PingSent = -1; }
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
        private void ReplyTo(string peer, Reply reply) => transport.Send(peer, JsonUtility.ToJson(reply));
    }
}
