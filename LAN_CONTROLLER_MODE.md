# Local phone controllers: WebRTC with TURN fallback

Controller data has one transport: the encrypted `controller-v1` WebRTC DataChannel. HTTPS QR pairing and the existing public WSS signaling service remain. Signaling routes SDP/ICE/session messages, never motion or gameplay. ICE normally selects a direct host/srflx/prflx pair; Cloudflare TURN provides a relay when direct paths are blocked. This is local controller networking, independent of future online game replication.

## Configure and run

Use `Assets/Scenes/Bootstrap.unity` for the application shell. Its `Assets/UI/Generated/ControllerCore.prefab` owns the persistent manager and WebRTC transport. Existing URL values remain:

- PWA: `https://jjohnjones.github.io/Motion-Controller-Website/`
- Signaling: `wss://motion-controller-signaling.onrender.com/signal`

No TURN secret goes in the Inspector. `ControllerIceConfiguration` is a runtime configuration object received from authenticated signaling. It supplies STUN/TURN URLs and temporary credentials to new/restarted peers. Normal mode explicitly allows all ICE candidates.

Deploy the separate **Motion Controller Signaling** repository using its `TURN_SETUP.md`: configure Cloudflare TURN Key ID/API token in Render environment variables, deploy the backend, then the matching **Motion Controller Website** repository. The backend and PWA are separate repositories beside this Unity project. No endpoint changes are needed. Missing TURN configuration is a developer deployment issue, never a player setup step.

Exit Play Mode before importing. The obsolete receiver components have been removed from the saved cube/bowling prototype scenes and ControllerCore prefab. Reopen a scene if an Editor instance has stale in-memory components. No ball/pin/gameplay settings were changed. For a new standalone prototype use Tools → Motion Controllers → Create Phone Cube Prototype Scene or Create Bowling Prototype Scene; configure the generated WebRTC transport URLs. Keep only controller systems on the persistent root.

Scan the QR, enable motion from a tap, calibrate, and verify live frames/buttons. Scene changes keep the same controller. iPhone requires explicit motion permission; Android behavior depends on browser policy. Keep the browser visible. A page reload loses in-memory pairing information; transient network recovery retains it.

## Test and diagnose

The application's pairing screen shows a development-only per-player direct/relay label. The prototype debug panel's connection diagnostics show selected candidate types/protocol, local relay protocol, ICE RTT, heartbeat timestamps/RTT, restart count, and motion sequence gaps. Local addresses appear only in Editor/development logs. Host candidates do not prove physical LAN membership, so the direct label is `Direct peer-to-peer`.

To prove TURN: enable `ALLOW_ICE_DIAGNOSTICS=true` on the signaling service, choose **Ice Diagnostic Mode → Relay Only** on the active host, restart the session, and scan the new QR. Both clients must connect with a selected relay candidate and working input. Normal uses `all`; Direct Only omits TURN. Release builds always request Normal. Restore Normal/backend diagnostics false after testing. Normal success alone does not validate TURN.

Test each phone independently, including more than ten minutes connected to exercise temporary-credential renewal. Disconnect Wi-Fi briefly, restore it and verify the same ID/calibration. Recovery allows eight seconds for transient disconnection, restarts immediately after ICE failure, detects stale heartbeats after twelve seconds and removes a peer only after the sixty-second recovery budget is exhausted. Renewed TURN credentials are applied before host-owned ICE restart to refresh allocations. Signaling socket loss alone does not close healthy DataChannels.

No TURN/STUN setup can guarantee passage through every university firewall. UDP, TCP and TLS/443 TURN endpoints are supported when supplied by Cloudflare; HTTPS-only proxies can still block them. Windows Firewall must permit Unity/the built executable; direct WebRTC uses dynamic UDP ports. Do not disable the firewall or add router port forwarding by default.

## Boundaries and packages

`WebRtcLanControllerTransport` → `ControllerProtocolRouter` → `ControllerManager` → `IMotionInputSource` / `IControllerButtonSource` → gameplay. Bowling does not know about ICE paths. Future online replication should send interpreted actions/state, not raw sensor packets. `ControllerSignalingClient` and browser signaling WebSockets remain; no phone-to-Unity WebSocket listener remains.

Official Unity WebRTC 3.0.0 supports the current Windows desktop target; validate Windows IL2CPP/stripping separately before shipping. The installed API supports `SetConfiguration`, `RestartIce`, and candidate-pair stats but has no browser-style ICE candidate error callback. PWA diagnostics retain available browser ICE errors; Unity logs missing relay candidates/failed paths without pretending to know an unavailable server error code. QR generation remains QRCoder; motion/calibration/button protocols are unchanged.

Run Unity EditMode tests and the separate Node suites. The optional `LanBrowserIntegrationTests` harness covers real browser/native DataChannel input and recovery. Physical mobile devices, provider deployment and campus/relay-only acceptance still require live testing.
