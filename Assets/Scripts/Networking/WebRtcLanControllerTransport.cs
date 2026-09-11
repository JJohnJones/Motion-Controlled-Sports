using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.WebRTC;
using UnityEngine;

namespace MotionControllers
{
    [RequireComponent(typeof(ControllerManager))]
    public sealed class WebRtcLanControllerTransport : MonoBehaviour, IControllerTransport
    {
        [Tooltip("Public signaling service. Configure once, not per player. Must end in /signal.")]
        public string signalingUrl = "wss://SIGNALING-HOST/signal";
        public string pwaUrl = "https://jjohnjones.github.io/Motion-Controller-Website/";
        public ControllerConnectionState State { get; private set; } = ControllerConnectionState.Idle;
        public string Status { get; private set; } = "Idle";
        public string JoinUrl { get; private set; }
        public Texture2D PairingQr { get; private set; }
        public ControllerProtocolRouter Protocol { get; private set; }
        public int ConnectedCount { get; private set; }
        [Serializable] private sealed class Signal
        {
            public string type, session, peer, ticket, sdp, candidate, sdpMid, code;
            public int sdpMLineIndex;
            public double expiresAt;
        }
        private sealed class Peer : IDisposable
        {
            public string Id, Ticket;
            public RTCPeerConnection Pc;
            public RTCDataChannel Channel;
            public bool RemoteReady, AnswerStarted, Disposed, Open;
            public double Opened;
            public int ReceivedThisUpdate;
            public readonly Queue<Signal> Ice = new Queue<Signal>();
            public readonly Queue<string> Messages = new Queue<string>();
            public void Dispose()
            {
                Disposed = true;
                if (Channel != null) { Channel.OnOpen = null; Channel.OnClose = null; Channel.OnMessage = null; Channel.Close(); Channel.Dispose(); }
                if (Pc != null) { Pc.OnIceCandidate = null; Pc.OnConnectionStateChange = null; Pc.Close(); Pc.Dispose(); }
            }
        }
        private readonly Dictionary<string, Peer> peers = new Dictionary<string, Peer>();
        private readonly HashSet<string> closing = new HashSet<string>();
        private ControllerSignalingClient signaling;
        private bool createSent;
        private double started, retryAt, expiresAt;
        private void OnEnable()
        {
            Application.runInBackground = true;
            Protocol = new ControllerProtocolRouter(GetComponent<ControllerManager>(), this, true);
            StartCoroutine(WebRTC.Update());
            BeginSession();
        }
        private void BeginSession()
        {
            LogRuntimeConfiguration();
            if (!ControllerEndpointConfiguration.TryValidate(pwaUrl, signalingUrl, Application.isEditor, out var uri, out var error))
            { SetState(ControllerConnectionState.Failed, error); retryAt = double.PositiveInfinity; return; }
            signaling = new ControllerSignalingClient(uri); createSent = false;
            started = Time.realtimeSinceStartupAsDouble;
            SetState(ControllerConnectionState.CreatingSession, "Creating controller session…");
        }
        private void LogRuntimeConfiguration()
        {
            var transports = FindObjectsByType<WebRtcLanControllerTransport>(FindObjectsInactive.Include);
            int active = 0;
            foreach (var transport in transports) if (transport.isActiveAndEnabled) active++;
            UnityEngine.Debug.Log($"[LAN config] Validating {Describe(this)}; source=component fields (serialized Inspector values unless assigned by code); " +
                $"pwaUrl=\"{pwaUrl}\"; signalingUrl=\"{signalingUrl}\"; transports={transports.Length}, active={active}; " +
                $"persistentOwner={(PersistentControllerRoot.Instance != null ? PersistentControllerRoot.Instance.GetEntityId().ToString() : "none")}", this);
            if (transports.Length > 1)
            {
                foreach (var transport in transports)
                    UnityEngine.Debug.Log($"[LAN config] Candidate {Describe(transport)}; active={transport.isActiveAndEnabled}; " +
                        $"pwaUrl=\"{transport.pwaUrl}\"; signalingUrl=\"{transport.signalingUrl}\"", transport);
                if (active > 1) UnityEngine.Debug.LogWarning("[LAN config] Multiple active transports found. Check startup/scene duplicates; the persistent controller root should own the active transport.", this);
            }
        }
        private static string Describe(WebRtcLanControllerTransport transport)
        {
            string path = transport.name;
            for (var parent = transport.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return $"object='{path}', entity={transport.GetEntityId()}, scene='{transport.gameObject.scene.name}'";
        }
        private void SetState(ControllerConnectionState state, string text) { State = state; Status = text; }
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (signaling == null) { if (now >= retryAt) BeginSession(); return; }
            if (signaling.Finished || JoinUrl == null && now - started > 15 || expiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= expiresAt)
            { FailSession(signaling.Error ?? "Signaling disconnected or session expired; creating a new QR…"); return; }
            if (signaling.Connected && !createSent) { createSent = true; SignalTo(new Signal { type = "create" }); }
            for (int i = 0; i < 128 && signaling.TryRead(out var json); i++)
            {
                try { HandleSignal(JsonUtility.FromJson<Signal>(json)); }
                catch (Exception) { FailSession("Invalid signaling response. Retrying…"); return; }
                if (signaling == null) return;
            }
            foreach (var p in peers.Values)
            {
                if (!p.Open && now - p.Opened > 25) { Status = "Negotiation timed out. Check LAN, firewall and network isolation."; Close(p.Id); }
                for (int i = 0; i < 128 && p.Messages.Count > 0; i++) Protocol.Handle(p.Id, p.Messages.Dequeue(), now);
                p.ReceivedThisUpdate = 0;
            }
            Protocol.Tick(now);
            foreach (var id in closing) RemovePeer(id);
            closing.Clear();
            ConnectedCount = 0;
            foreach (var c in Protocol.Connections.Values) if (c.ControllerId != null) ConnectedCount++;
            if (ConnectedCount > 0) SetState(ControllerConnectionState.Connected, $"{ConnectedCount} controller(s) connected · direct WebRTC (no relay configured)");
            else if (peers.Count == 0 && JoinUrl != null && State != ControllerConnectionState.Disconnected)
                SetState(ControllerConnectionState.WaitingForController, "Waiting for controller · scan with your phone");
        }
        private static bool Id(string value) => value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-f0-9]{32}$");
        private void HandleSignal(Signal s)
        {
            if (s == null) throw new ArgumentException();
            if (s.type == "error") { FailSession("Signaling: " + s.code); return; }
            if (s.type == "created" && Id(s.session) && JoinUrl == null)
            {
                expiresAt = s.expiresAt;
                JoinUrl = pwaUrl + "#session=" + s.session;
                PairingQr = ControllerPairingQr.Create(JoinUrl);
                SetState(ControllerConnectionState.WaitingForController, "Scan with your phone");
            }
            else if (s.type == "joined" && Id(s.peer) && Id(s.ticket) && !peers.ContainsKey(s.peer))
            {
                if (peers.Count >= GetComponent<ControllerManager>().maximumControllers) { SignalTo(new Signal { type = "leave", peer = s.peer }); return; }
                var config = new RTCConfiguration { iceServers = Array.Empty<RTCIceServer>() };
                var p = new Peer { Id = s.peer, Ticket = s.ticket, Pc = new RTCPeerConnection(ref config), Opened = Time.realtimeSinceStartupAsDouble };
                peers.Add(p.Id, p);
                p.Pc.OnIceCandidate = c => {
                    if (!p.Disposed && c != null) SignalTo(new Signal { type = "ice", peer = p.Id, candidate = c.Candidate, sdpMid = c.SdpMid ?? "0", sdpMLineIndex = c.SdpMLineIndex ?? 0 });
                };
                p.Pc.OnConnectionStateChange = state => {
                    if (state == RTCPeerConnectionState.Failed || state == RTCPeerConnectionState.Closed) Close(p.Id);
                };
                p.Channel = p.Pc.CreateDataChannel("controller-v1", new RTCDataChannelInit { ordered = true });
                p.Channel.OnOpen = () => { if (p.Disposed) return; p.Open = true; Protocol.Open(p.Id, p.Ticket, Time.realtimeSinceStartupAsDouble); p.Ticket = null; };
                p.Channel.OnClose = () => Close(p.Id);
                p.Channel.OnMessage = bytes => {
                    if (p.Disposed) return;
                    if (bytes.Length > 8192 || p.Messages.Count >= 128 || ++p.ReceivedThisUpdate > 128) { Close(p.Id); return; }
                    p.Messages.Enqueue(Encoding.UTF8.GetString(bytes));
                };
                StartCoroutine(Offer(p));
            }
            else if (s.type == "left") Close(s.peer);
            else if (s.peer != null && peers.TryGetValue(s.peer, out var p))
            {
                if (s.type == "answer" && !p.AnswerStarted && !string.IsNullOrEmpty(s.sdp)) { p.AnswerStarted = true; StartCoroutine(Answer(p, s.sdp)); }
                else if (s.type == "ice" && s.candidate != null && s.candidate.Length < 2048)
                { if (p.RemoteReady) AddIce(p, s); else if (p.Ice.Count < 64) p.Ice.Enqueue(s); else Close(p.Id); }
            }
        }
        private IEnumerator Offer(Peer p)
        {
            SetState(ControllerConnectionState.Signaling, "Negotiating controller…");
            var op = p.Pc.CreateOffer(); yield return op;
            if (p.Disposed) yield break;
            if (op.IsError) { Close(p.Id); yield break; }
            var desc = op.Desc;
            var local = p.Pc.SetLocalDescription(ref desc); yield return local;
            if (p.Disposed) yield break;
            if (local.IsError) { Close(p.Id); yield break; }
            SignalTo(new Signal { type = "offer", peer = p.Id, sdp = desc.sdp });
            SetState(ControllerConnectionState.Connecting, "Connecting directly over LAN…");
        }
        private IEnumerator Answer(Peer p, string sdp)
        {
            var desc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
            var op = p.Pc.SetRemoteDescription(ref desc); yield return op;
            if (p.Disposed) yield break;
            if (op.IsError) { Close(p.Id); yield break; }
            p.RemoteReady = true;
            while (p.Ice.Count > 0) AddIce(p, p.Ice.Dequeue());
        }
        private void AddIce(Peer p, Signal s)
        {
            using (var ice = new RTCIceCandidate(new RTCIceCandidateInit { candidate = s.candidate, sdpMid = s.sdpMid, sdpMLineIndex = s.sdpMLineIndex }))
                if (!p.Pc.AddIceCandidate(ice)) Close(p.Id);
        }
        private void SignalTo(Signal s) => signaling?.Send(JsonUtility.ToJson(s));
        public void Send(string peerId, string json)
        {
            if (!peers.TryGetValue(peerId, out var p) || p.Disposed) return;
            if (p.Channel.ReadyState != RTCDataChannelState.Open || p.Channel.BufferedAmount > 8192) { Close(peerId); return; }
            try { p.Channel.Send(json); } catch (Exception) { Close(peerId); }
        }
        public void Close(string peerId) { if (peerId != null) closing.Add(peerId); }
        private void RemovePeer(string id)
        {
            Protocol?.Drop(id);
            if (!peers.TryGetValue(id, out var p)) return;
            peers.Remove(id); p.Dispose(); SignalTo(new Signal { type = "leave", peer = id });
            SetState(ControllerConnectionState.Disconnected, "Controller disconnected · reconnect or scan again");
        }
        public void RestartSession() { FailSession("Creating a new controller session…"); retryAt = 0; }
        private void FailSession(string reason)
        {
            Cleanup(); SetState(ControllerConnectionState.Failed, reason);
            retryAt = Time.realtimeSinceStartupAsDouble + 5;
        }
        private void Cleanup()
        {
            signaling?.Dispose(); signaling = null;
            foreach (var p in peers.Values) p.Dispose();
            peers.Clear(); closing.Clear(); Protocol?.Dispose();
            if (PairingQr != null) Destroy(PairingQr);
            PairingQr = null; JoinUrl = null; expiresAt = 0; ConnectedCount = 0;
        }
        private void OnDisable() { StopAllCoroutines(); Cleanup(); SetState(ControllerConnectionState.Idle, "Stopped"); }
    }
}
