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
        public string signalingUrl = "wss://SIGNALING-HOST/signal";
        public string pwaUrl = "https://jjohnjones.github.io/Motion-Controller-Website/";
        [Min(2)] public float recoveryGraceSeconds = 8;
        [Min(30)] public float recoveryTimeoutSeconds = 60;
        [Min(6)] public float heartbeatStaleSeconds = 12;
        [Min(5)] public float restartAttemptSeconds = 15;
        public bool logDiagnostics = true;
        public enum IceDiagnosticMode { Normal, RelayOnly, DirectOnly }
        [Tooltip("Editor/development builds only. Backend must also enable ALLOW_ICE_DIAGNOSTICS. Restart session after changing.")]
        public IceDiagnosticMode iceDiagnosticMode;
        private ControllerIceConfiguration iceConfig;
        private double nextIceRequest;
        private string iceStatus = "ICE configuration pending";
        private string RequestedIceMode {
            get {
                if (Application.isEditor || UnityEngine.Debug.isDebugBuild) return iceDiagnosticMode == IceDiagnosticMode.RelayOnly ? "relay" : iceDiagnosticMode == IceDiagnosticMode.DirectOnly ? "direct" : "all";
                return "all";
            }
        }
        public ControllerConnectionState State { get; private set; } = ControllerConnectionState.Idle;
        public string Status { get; private set; } = "Idle";
        public string JoinUrl { get; private set; }
        public Texture2D PairingQr { get; private set; }
        public ControllerProtocolRouter Protocol { get; private set; }
        public int ConnectedCount { get; private set; }
        public string SignalingStatus => signalReady ? "Open / authenticated" : signaling?.State ?? "Offline / retrying";
        public IEnumerable<string> RecentLog => logs;
        private readonly Queue<string> logs = new Queue<string>();
        [Serializable] private sealed class SignalPeer { public string peer, ticket; public bool online; }
        [Serializable] private sealed class Signal
        {
            public string type, session, hostToken, peer, ticket, sdp, candidate, sdpMid, code;
            public int sdpMLineIndex, revision;
            public string iceMode;
            public ControllerIceConfiguration iceConfig;
            public bool reset;
            public double expiresAt;
            public SignalPeer[] peers;
        }
        private sealed class Peer : IDisposable
        {
            public string Id, Ticket, LastStates;
            public RTCPeerConnection Pc;
            public RTCDataChannel Channel;
            public bool Disposed, RemoteReady, Negotiating, AnswerStarted, OfferSent, SignalOnline = true;
            public bool ForceRestart, RebuildNext;
            public int Revision, Generation, Restarts, RelayCandidates, ReflexiveCandidates;
            public bool StatsPending;
            public double NextStats;
            public string Path = "Connection: negotiating", PathLabel = "Negotiating", PairKey;
            public double Opened, RecoverySince = -1, AttemptAt = -1;
            public string Reason = "Connecting";
            public readonly Queue<Signal> Ice = new Queue<Signal>(), LocalIce = new Queue<Signal>();
            public readonly Queue<string> Messages = new Queue<string>();
            public void DisposeConnection()
            {
                Generation++;
                if (Channel != null) { Channel.OnOpen = null; Channel.OnClose = null; Channel.OnError = null; Channel.OnMessage = null; Channel.Close(); Channel.Dispose(); Channel = null; }
                if (Pc != null) { Pc.OnIceCandidate = null; Pc.OnConnectionStateChange = null; Pc.OnIceConnectionChange = null; Pc.OnIceGatheringStateChange = null; Pc.OnDataChannel = null; Pc.Close(); Pc.Dispose(); Pc = null; }
                Ice.Clear(); LocalIce.Clear(); Messages.Clear();
            }
            public void Dispose() { Disposed = true; DisposeConnection(); }
        }
        private readonly Dictionary<string, Peer> peers = new Dictionary<string, Peer>();
        private readonly HashSet<string> closing = new HashSet<string>();
        private ControllerSignalingClient signaling;
        private Uri endpoint;
        private string session, hostToken, lastSignalState;
        private bool handshakeSent, signalReady;
        private double started, retryAt, expiresAt;
        private int signalAttempts;
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private void OnEnable()
        {
            Application.runInBackground = true;
            Protocol = new ControllerProtocolRouter(GetComponent<ControllerManager>(), this, true, false);
            StartCoroutine(WebRTC.Update());
            BeginSession();
        }
        private void BeginSession()
        {
            LogRuntimeConfiguration();
            if (!ControllerEndpointConfiguration.TryValidate(pwaUrl, signalingUrl, Application.isEditor, out endpoint, out var error))
            { State = ControllerConnectionState.Failed; Status = error; retryAt = double.PositiveInfinity; return; }
            ConnectSignaling();
        }
        private void ConnectSignaling()
        {
            signaling = new ControllerSignalingClient(endpoint); handshakeSent = signalReady = false; started = Now;
            Record(null, session == null ? "signaling connecting / create session" : "signaling reconnecting / resume existing session");
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

        private void Record(Peer p, string reason)
        {
            string line = $"{DateTime.UtcNow:O} t={Now:F3} peer={p?.Id ?? "host"} {reason}";
            if (logs.Count >= 80) logs.Dequeue(); logs.Enqueue(line);
            if (logDiagnostics) UnityEngine.Debug.Log("[LAN] " + line, this);
        }
        private string States(Peer p) => p.Pc == null ? "PC absent" :
            $"PC={p.Pc.ConnectionState} ICE={p.Pc.IceConnectionState} gathering={p.Pc.GatheringState} SDP={p.Pc.SignalingState} channel={p.Channel?.ReadyState.ToString() ?? "absent"}";
        public string GetConnectionPath(string controllerId)
        {
            foreach (var p in peers.Values)
                if (Protocol.Connections.TryGetValue(p.Id, out var c) && c.ControllerId == controllerId) return p.PathLabel;
            return "Disconnected";
        }
        public IEnumerable<string> PeerDiagnostics
        {
            get
            {
                foreach (var p in peers.Values)
                {
                    Protocol.Connections.TryGetValue(p.Id, out var c);
                    yield return $"Player {c?.PlayerNumber ?? 0}: {(p.RecoverySince >= 0 ? "Recovering: " + p.Reason : "Connected")} | {States(p)}";
                    yield return p.Path + $" | ICE restarts={p.Restarts} | gathered relay={p.RelayCandidates}, srflx={p.ReflexiveCandidates}";
                    yield return $"last packet t={c?.LastPacketAt:F2}, ping t={c?.LastPingAt:F2}, pong t={c?.LastPongAt:F2}, RTT={c?.RttMs:F0}ms | revision={p.Revision}";
                }
            }
        }
        private void Update()
        {
            double now = Now;
            if (endpoint == null) return;
            if (expiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= expiresAt)
            { EndSession("session expiration", true); return; }
            if (signaling != null)
            {
                if (lastSignalState != signaling.State) { lastSignalState = signaling.State; Record(null, "signaling WebSocket=" + lastSignalState); }
                if (signaling.Connected && !handshakeSent)
                {
                    handshakeSent = signaling.Send(JsonUtility.ToJson(new Signal { type = session == null ? "create" : "resumeHost", session = session, hostToken = hostToken, iceMode = RequestedIceMode }));
                }
                for (int i = 0; i < 128 && signaling != null && signaling.TryRead(out var json); i++)
                {
                    try { HandleSignal(JsonUtility.FromJson<Signal>(json)); }
                    catch (Exception e) { EndSession("invalid signaling message: " + e.GetType().Name, true); return; }
                }
                if (signaling != null && (signaling.Finished || !signalReady && now - started > 20))
                {
                    Record(null, "signaling loss: " + (signaling.Error ?? "handshake timeout") + "; retaining DataChannels");
                    signaling.Dispose(); signaling = null; signalReady = false;
                    retryAt = now + Math.Min(10, Math.Pow(2, Math.Min(signalAttempts++, 4)));
                }
            }
            if (signaling == null && endpoint != null && now >= retryAt) ConnectSignaling();
            if (signalReady && now >= nextIceRequest && (iceConfig == null || iceConfig.expiresAt < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 120000))
            { SignalTo(new Signal { type = "ice-config" }); nextIceRequest = now + 15; }
            foreach (var p in peers.Values)
            {
                if ((Application.isEditor || UnityEngine.Debug.isDebugBuild) && p.Pc != null && !p.StatsPending && now >= p.NextStats)
                { p.NextStats = now + 3; StartCoroutine(ReadIceStats(p, p.Generation)); }
                string states = States(p);
                if (states != p.LastStates) { p.LastStates = states; Record(p, states); }
                // Detect a stalled main thread/link before accepting a backlog of old releases.
                if (Protocol.Connections.TryGetValue(p.Id, out var before) && before.Authenticated &&
                    p.RecoverySince < 0 && now - before.LastPacketAt > heartbeatStaleSeconds)
                    Recover(p, "controller packet gap before queue drain");
                for (int i = 0; i < 128 && p.Messages.Count > 0; i++) Protocol.Handle(p.Id, p.Messages.Dequeue(), now);
                Protocol.Connections.TryGetValue(p.Id, out var c);
                if (c?.Authenticated == true && p.RecoverySince < 0 &&
                    (now - c.LastPacketAt > heartbeatStaleSeconds || now - Math.Max(c.LastPongAt, p.Opened) > heartbeatStaleSeconds))
                    Recover(p, "heartbeat timeout / no recent successful pong");
                if (c?.Authenticated != true && now - p.Opened > 25 && p.RecoverySince < 0) Recover(p, "initial channel/authentication timeout", true);
                if (p.RecoverySince >= 0)
                {
                    // Only a fresh post-interruption round trip proves the channel has drained.
                    if (!p.ForceRestart && !p.Negotiating && Healthy(p) && c?.Authenticated == true && c.LastPongRequestAt >= p.RecoverySince &&
                        now - c.LastPongAt < 3 && c.RttMs < 3000)
                    {
                        Record(p, $"recovered; same player={c.PlayerNumber}, last packet={c.LastPacketAt:F3}, ping={c.LastPingAt:F3}, pong={c.LastPongAt:F3}");
                        p.RecoverySince = -1; p.ForceRestart = p.RebuildNext = false; p.AttemptAt = -1; p.Reason = "Connected";
                        Protocol.PauseInput(p.Id, false);
                    }
                    else if (now - p.RecoverySince >= Math.Max(30, recoveryTimeoutSeconds)) { Record(p, "recovery exhausted: " + p.Reason); Close(p.Id); }
                    else if (signalReady && p.SignalOnline)
                    {
                        bool overdue = p.AttemptAt >= 0 && now - p.AttemptAt >= restartAttemptSeconds;
                        if (overdue || !p.Negotiating && (p.ForceRestart || p.AttemptAt < 0 && now - p.RecoverySince >= recoveryGraceSeconds))
                        {
                            bool rebuild = overdue || p.RebuildNext || p.Pc == null || p.Pc.ConnectionState == RTCPeerConnectionState.Closed ||
                                p.Channel == null || p.Channel.ReadyState == RTCDataChannelState.Closed;
                            p.ForceRestart = false; p.AttemptAt = now;
                            StartNegotiation(p, rebuild, true);
                        }
                    }
                }
                else if (p.Negotiating && now - p.AttemptAt > restartAttemptSeconds) Recover(p, "SDP negotiation timeout", true);
            }
            Protocol.Tick(now);
            foreach (var id in closing) RemovePeer(id);
            closing.Clear();
            if (signaling == null && session == null && State == ControllerConnectionState.Failed) return;
            ConnectedCount = 0; bool recovering = false, reconnecting = false;
            foreach (var p in peers.Values)
            {
                if (Protocol.Connections.TryGetValue(p.Id, out var c) && c.Authenticated) ConnectedCount++;
                recovering |= p.RecoverySince >= 0; reconnecting |= p.RecoverySince >= 0 && p.Negotiating;
            }
            State = reconnecting ? ControllerConnectionState.Reconnecting : recovering ? ControllerConnectionState.Recovering :
                ConnectedCount > 0 ? ControllerConnectionState.Connected : peers.Count > 0 ? ControllerConnectionState.Connecting :
                JoinUrl != null ? ControllerConnectionState.WaitingForController : ControllerConnectionState.CreatingSession;
            Status = $"{ConnectedCount} paired controller(s) | signaling {SignalingStatus} | {iceStatus}";
        }
        private bool Healthy(Peer p) => p.Pc != null && p.Pc.ConnectionState == RTCPeerConnectionState.Connected &&
            (p.Pc.IceConnectionState == RTCIceConnectionState.Connected || p.Pc.IceConnectionState == RTCIceConnectionState.Completed) &&
            p.Channel?.ReadyState == RTCDataChannelState.Open;
        private void Recover(Peer p, string reason, bool immediate = false, bool rebuild = false)
        {
            if (p.Disposed || closing.Contains(p.Id)) return;
            if (p.RecoverySince < 0)
            {
                if (!p.Negotiating) p.AttemptAt = -1;
                p.RecoverySince = Now; p.Reason = reason; Protocol.PauseInput(p.Id, true);
                Record(p, "Recovering: " + reason);
            }
            p.ForceRestart |= immediate; p.RebuildNext |= rebuild;
        }
        private static bool Id(string value) => value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-f0-9]{32}$");
        private void HandleSignal(Signal s)
        {
            if (s == null) throw new ArgumentException();
            if (s.iceConfig != null && (s.type == "created" || s.type == "resumed" || s.type == "ice-config"))
            {
                try {
                    var config = s.iceConfig.Build(); bool renewed = iceConfig != null && iceConfig.expiresAt != s.iceConfig.expiresAt; iceConfig = s.iceConfig;
                    foreach (var peer in peers.Values) if (peer.Pc != null) {
                        var result = peer.Pc.SetConfiguration(ref config);
                        if (result != RTCErrorType.None) Record(peer, "ICE configuration update failed: " + result);
                        else if (renewed && iceConfig.HasTurn) Recover(peer, "temporary TURN credentials renewed; refreshing allocations", true);
                    }
                    iceStatus = iceConfig.warning ?? (iceConfig.HasTurn ? "TURN available" : "Direct only / TURN unavailable");
                    if (!string.IsNullOrEmpty(iceConfig.warning)) Record(null, iceConfig.warning);
                    Record(null, $"ICE configuration refreshed; mode={iceConfig.mode}; TURN={(iceConfig.HasTurn ? "available" : "NOT CONFIGURED / disabled")}; credentials omitted");
                } catch (ArgumentException e) { iceStatus = e.Message; Record(null, e.Message); }
            }
            if (s.type == "ice-config") return;
            if (s.type == "error") { EndSession("signaling rejected: " + s.code, true); return; }
            if (s.type == "created" && Id(s.session) && Id(s.hostToken) && session == null)
            {
                session = s.session; hostToken = s.hostToken; expiresAt = s.expiresAt; signalReady = true; signalAttempts = 0;
                JoinUrl = pwaUrl + "#session=" + session; PairingQr = ControllerPairingQr.Create(JoinUrl);
                Record(null, "session created (capabilities omitted)");
            }
            else if (s.type == "resumed" && s.session == session)
            {
                signalReady = true; signalAttempts = 0; Record(null, "signaling session resumed; controller identities retained");
                var live = new HashSet<string>();
                foreach (var remote in s.peers ?? Array.Empty<SignalPeer>())
                {
                    live.Add(remote.peer);
                    if (peers.TryGetValue(remote.peer, out var existing)) { existing.SignalOnline = remote.online; if (existing.Negotiating) Recover(existing, "signaling resumed during negotiation", true); }
                    else SignalTo(new Signal { type = "leave", peer = remote.peer });
                }
                foreach (var p in peers.Values) if (!live.Contains(p.Id)) { Record(p, "peer explicitly left while host signaling was offline"); Close(p.Id); }
            }
            else if (s.type == "joined" && Id(s.peer) && Id(s.ticket) && !peers.ContainsKey(s.peer))
            {
                if (peers.Count >= GetComponent<ControllerManager>().maximumControllers) { SignalTo(new Signal { type = "leave", peer = s.peer }); return; }
                var p = new Peer { Id = s.peer, Ticket = s.ticket, Opened = Now };
                peers.Add(p.Id, p); StartNegotiation(p, true, false);
            }
            else if (s.type == "left") { Record(null, "peer left: " + s.peer + " reason=" + s.code); Close(s.peer); }
            else if (s.peer != null && peers.TryGetValue(s.peer, out var p))
            {
                if (s.type == "peer-offline" || s.type == "peer-online")
                { p.SignalOnline = s.type == "peer-online"; Record(p, "signaling " + s.type + "; DataChannel retained"); }
                else if (s.type == "restart-request") Recover(p, "phone requested recovery", true);
                else if (s.revision == p.Revision && s.type == "answer" && p.Negotiating && !p.AnswerStarted && !string.IsNullOrEmpty(s.sdp))
                { p.AnswerStarted = true; StartCoroutine(Answer(p, s.sdp, p.Generation, p.Revision)); }
                else if (s.revision == p.Revision && s.type == "ice" && s.candidate != null && s.candidate.Length < 2048)
                { if (p.RemoteReady) AddIce(p, s); else if (p.Ice.Count < 64) p.Ice.Enqueue(s); else Close(p.Id); }
            }
        }
        private void CreateConnection(Peer p)
        {
            p.DisposeConnection(); int generation = p.Generation;
            var config = iceConfig.Build();
            p.StatsPending = false; p.Path = "Connection: negotiating"; p.PairKey = null;
            p.Pc = new RTCPeerConnection(ref config);
            p.Pc.OnConnectionStateChange = state => {
                if (!Current(p, generation)) return;
                Record(p, "PeerConnection=" + state);
                if (state == RTCPeerConnectionState.Disconnected) Recover(p, "PeerConnection disconnected");
                if (state == RTCPeerConnectionState.Failed) Recover(p, "PeerConnection failed (ICE/DTLS)", true);
                if (state == RTCPeerConnectionState.Closed) Recover(p, "PeerConnection closed", true, true);
            };
            p.Pc.OnIceConnectionChange = state => {
                if (!Current(p, generation)) return;
                Record(p, "ICE=" + state);
                if (state == RTCIceConnectionState.Disconnected) Recover(p, "ICE disconnected");
                if (state == RTCIceConnectionState.Failed) Recover(p, "all ICE paths failed; inspect gathered relay count, TURN reachability/authentication and PWA ICE errors", true);
            };
            p.Pc.OnIceGatheringStateChange = state => { if (Current(p, generation)) {
                Record(p, "ICE gathering=" + state);
                if (state == RTCIceGatheringState.Complete && iceConfig.HasTurn && p.RelayCandidates == 0)
                    Record(p, "No relay candidates generated: check TURN credentials, DNS, UDP/TCP/TLS reachability; Unity API does not expose ICE server error codes.");
                if (state == RTCIceGatheringState.Complete && p.ReflexiveCandidates == 0)
                    Record(p, "No srflx candidates observed (STUN may be absent, unreachable, or candidate redundant).");
            } };
            p.Pc.OnIceCandidate = c => {
                if (!Current(p, generation) || c == null) return;
                if (c.Candidate.Contains(" typ relay")) p.RelayCandidates++;
                if (c.Candidate.Contains(" typ srflx")) p.ReflexiveCandidates++;
                var ice = new Signal { type = "ice", peer = p.Id, revision = p.Revision, candidate = c.Candidate, sdpMid = c.SdpMid ?? "0", sdpMLineIndex = c.SdpMLineIndex ?? 0 };
                if (p.OfferSent) SignalTo(ice); else if (p.LocalIce.Count < 64) p.LocalIce.Enqueue(ice);
            };
            p.Pc.OnDataChannel = channel => { channel.Close(); channel.Dispose(); Record(p, "unexpected remote-created channel"); Close(p.Id); };
            p.Channel = p.Pc.CreateDataChannel("controller-v1", new RTCDataChannelInit { ordered = true });
            p.Channel.OnOpen = () => {
                if (!Current(p, generation)) return;
                Record(p, "DataChannel=open"); Protocol.Open(p.Id, p.Ticket, Now);
                if (p.RecoverySince >= 0) Protocol.PauseInput(p.Id, true);
            };
            p.Channel.OnClose = () => { if (Current(p, generation)) { Record(p, "DataChannel=closed"); Recover(p, "DataChannel closed", true, true); } };
            p.Channel.OnError = e => { if (Current(p, generation)) { Record(p, "DataChannel error: " + e.errorType + " " + e.message); Recover(p, "DataChannel error", true, true); } };
            p.Channel.OnMessage = bytes => {
                if (!Current(p, generation)) return;
                if (bytes.Length > 8192) { Record(p, "controller protocol size violation"); Close(p.Id); return; }
                if (p.Messages.Count >= 128)
                {
                    // A reliable channel can deliver a burst after Wi-Fi returns. Discard stale
                    // input and require a fresh round trip rather than deleting the player.
                    p.Messages.Clear(); Recover(p, "controller queue backlog"); return;
                }
                p.Messages.Enqueue(Encoding.UTF8.GetString(bytes));
            };
        }
        private bool Current(Peer p, int generation) => !p.Disposed && p.Generation == generation;
        private void StartNegotiation(Peer p, bool rebuild, bool restart)
        {
            if (iceConfig == null || !iceConfig.IsFresh) {
                if (Now >= nextIceRequest) { SignalTo(new Signal { type = "ice-config" }); nextIceRequest = Now + 15; }
                Recover(p, "waiting for fresh ICE configuration", true); return;
            }
            if (rebuild || p.Pc == null) { rebuild = true; CreateConnection(p); }
            else { var config = iceConfig.Build(); if (p.Pc.SetConfiguration(ref config) != RTCErrorType.None) { Recover(p, "ICE configuration update failed", true, true); return; } }
            if (restart) p.Restarts++;
            p.RelayCandidates = p.ReflexiveCandidates = 0;
            p.Revision++; p.RemoteReady = p.AnswerStarted = p.OfferSent = false; p.Negotiating = true;
            p.Ice.Clear(); p.LocalIce.Clear(); p.AttemptAt = Now;
            Record(p, $"{(rebuild ? "recreate PeerConnection" : "ICE restart")} revision={p.Revision}; identity retained");
            StartCoroutine(Offer(p, rebuild, restart, p.Generation, p.Revision));
        }
        private IEnumerator Offer(Peer p, bool rebuild, bool restart, int generation, int revision)
        {
            if (restart && !rebuild) p.Pc.RestartIce();
            var op = p.Pc.CreateOffer(); yield return op;
            if (!Current(p, generation) || p.Revision != revision) yield break;
            if (op.IsError) { NegotiationFailed(p, "createOffer: " + op.Error.message); yield break; }
            var desc = op.Desc;
            var local = p.Pc.SetLocalDescription(ref desc); yield return local;
            if (!Current(p, generation) || p.Revision != revision) yield break;
            if (local.IsError) { NegotiationFailed(p, "setLocalDescription: " + local.Error.message); yield break; }
            p.OfferSent = SignalTo(new Signal { type = "offer", peer = p.Id, sdp = desc.sdp, revision = revision, reset = rebuild });
            if (p.OfferSent) while (p.LocalIce.Count > 0) SignalTo(p.LocalIce.Dequeue());
        }
        private IEnumerator Answer(Peer p, string sdp, int generation, int revision)
        {
            var desc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
            var op = p.Pc.SetRemoteDescription(ref desc); yield return op;
            if (!Current(p, generation) || p.Revision != revision) yield break;
            if (op.IsError) { NegotiationFailed(p, "setRemoteDescription: " + op.Error.message); yield break; }
            p.RemoteReady = true; p.Negotiating = false;
            if (p.RecoverySince < 0) p.AttemptAt = -1;
            while (p.Ice.Count > 0) AddIce(p, p.Ice.Dequeue());
            Record(p, "answer applied; waiting for a fresh heartbeat");
        }
        private void NegotiationFailed(Peer p, string reason)
        { p.Negotiating = false; p.RebuildNext = true; Record(p, reason); Recover(p, reason); }
        private void AddIce(Peer p, Signal s)
        {
            using (var ice = new RTCIceCandidate(new RTCIceCandidateInit { candidate = s.candidate, sdpMid = s.sdpMid, sdpMLineIndex = s.sdpMLineIndex }))
                if (!p.Pc.AddIceCandidate(ice)) Record(p, "ICE candidate rejected for revision " + s.revision);
        }
        private IEnumerator ReadIceStats(Peer p, int generation)
        {
            p.StatsPending = true;
            var op = p.Pc.GetStats(); yield return op;
            if (op.IsError) { if (Current(p, generation)) p.StatsPending = false; yield break; }
            using (var report = op.Value) {
                if (!Current(p, generation)) yield break;
                p.StatsPending = false;
                RTCIceCandidatePairStats pair = null;
                foreach (var stat in report.Stats.Values)
                    if (stat is RTCTransportStats transport && !string.IsNullOrEmpty(transport.selectedCandidatePairId) &&
                        report.Stats.TryGetValue(transport.selectedCandidatePairId, out var selected)) pair = selected as RTCIceCandidatePairStats;
                if (pair == null || !report.Stats.TryGetValue(pair.localCandidateId, out var localStat) ||
                    !report.Stats.TryGetValue(pair.remoteCandidateId, out var remoteStat)) yield break;
                var local = localStat as RTCIceCandidateStats; var remote = remoteStat as RTCIceCandidateStats;
                if (local == null || remote == null) yield break;
                bool relay = local.candidateType == "relay" || remote.candidateType == "relay";
                // Host candidates alone do not prove both devices are on the same LAN (VPNs exist).
                string path = relay ? "TURN Relay" : "Direct peer-to-peer"; p.PathLabel = path;
                string key = pair.localCandidateId + "/" + pair.remoteCandidateId;
                p.Path = $"Connection: {path} | {local.candidateType}/{remote.candidateType} | {local.protocol} relay transport={local.relayProtocol} | ICE RTT={pair.currentRoundTripTime * 1000:F0}ms";
                if (key != p.PairKey) {
                    p.PairKey = key; Record(p, p.Path + $" | local debug address={local.address}");
                }
            }
        }
        private bool SignalTo(Signal s) => signalReady && signaling != null && signaling.Send(JsonUtility.ToJson(s));
        public void Send(string peerId, string json)
        {
            if (!peers.TryGetValue(peerId, out var p) || p.Disposed) return;
            if (p.Channel?.ReadyState != RTCDataChannelState.Open || p.Channel.BufferedAmount > 8192) { Recover(p, "DataChannel unavailable/backpressure"); return; }
            try { p.Channel.Send(json); } catch (Exception e) { Record(p, "DataChannel send: " + e.GetType().Name); Recover(p, "DataChannel send error"); }
        }
        public void Close(string peerId) { if (peerId != null) closing.Add(peerId); }
        private void RemovePeer(string id)
        {
            Protocol?.Drop(id);
            if (!peers.TryGetValue(id, out var p)) return;
            peers.Remove(id); p.Dispose(); SignalTo(new Signal { type = "leave", peer = id });
            Record(p, "controller removed after terminal close/recovery failure");
        }
        // Diagnostic action exercises the real host-owned ICE restart, not a simulated disconnect.
        public void RestartIceForControllers() { foreach (var p in peers.Values) Recover(p, "manual ICE restart diagnostic", true); }
        public void ReconnectSignalingForDiagnostics() { signaling?.Dispose(); }
        public void RestartSession() { EndSession("explicit new session", false); BeginSession(); }
        private void EndSession(string reason, bool retry)
        {
            Record(null, reason);
            signaling?.End(JsonUtility.ToJson(new Signal { type = "end" })); signaling = null; signalReady = false;
            foreach (var p in peers.Values) p.Dispose();
            peers.Clear(); closing.Clear(); Protocol?.Dispose();
            if (PairingQr != null) Destroy(PairingQr);
            iceConfig = null; iceStatus = "ICE configuration pending"; nextIceRequest = 0; PairingQr = null; JoinUrl = session = hostToken = null; expiresAt = 0; ConnectedCount = 0;
            State = ControllerConnectionState.Failed; Status = reason;
            retryAt = retry ? Now + 5 : double.PositiveInfinity;
        }
        private void OnDisable() { StopAllCoroutines(); EndSession("explicit host stop", false); State = ControllerConnectionState.Idle; }
    }
}
