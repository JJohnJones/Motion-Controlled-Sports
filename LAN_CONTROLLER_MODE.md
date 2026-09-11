# Local / LAN phone controllers

The WebRTC transport feeds the existing `ControllerManager`, `ControllerSession`, motion codec and button interfaces. Sensor math, calibration, smoothing, history, cube orientation and bowling behavior are preserved. Online multiplayer is not implemented.

For scene transitions, run the one-time controller-root migration described in [Persistent controllers](Documentation/PERSISTENT_CONTROLLERS.md). The persistent controller-only root retains the network session while gameplay and UI unload normally. The LAN setup menu now includes this migration.

```text
HTTPS PWA ──WSS handshake── separate signaling service ──WSS handshake── Unity
      └──────── encrypted WebRTC DataChannel over local network ─────────┘
                                      ↓
                       ControllerProtocolRouter / ControllerManager
                                      ↓
                          local motion interpretation / gameplay
                                      ↓
                    future normalized action/state replication boundary
```

The signaling connection remains open for room ownership, departure and reconnect notifications, with low-frequency liveness checks. It never carries sensor/button traffic. Losing it deliberately closes controller peers and invalidates the old pairing capability; Unity retries and creates a new QR. This version needs internet for website loading/signaling and is not a completely offline LAN discovery system.

## Packages and boundaries

- `com.unity.webrtc` **3.0.0**, official Unity package with native Windows x64 DataChannel support. Official requirements list Unity 6000.3; this project uses 6000.6.0f1 and was compiled/tested locally. See https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/requirements.html . It is not a WebGL/UWP transport. Test Windows IL2CPP and stripping in a real player build before shipping; Editor/Mono success does not prove IL2CPP.
- QRCoder **1.8.0**, MIT, plus its managed CodePages dependency. The .NET Standard 1.3 build avoids System.Drawing and WPF dependencies. Only the module matrix is rendered to a Unity texture. No remote QR service receives tokens. See `Assets/Plugins/QRCoder/THIRD_PARTY.md`.
- `IControllerTransport` supplies delivery/close operations. `ControllerProtocolRouter` owns authentication, expected message handling and heartbeat. Both `ControllerReceiver` (legacy WebSocket) and `WebRtcLanControllerTransport` use it.
- Gameplay still consumes `IMotionInputSource` / `IControllerButtonSource`. A future keyboard/gamepad or normalized gameplay input adapter can be added above these without changing transport. A future online layer should replicate interpreted actions (aim/power/spin/release) and authoritative state, not raw phone sensor streams. No online service depends on this signaling backend.

## Configure the scene

First deploy the sibling `Controller Signaling` service and update the separate PWA's `config.js`. That backend's README contains a Docker/Caddy HTTPS setup. No public backend was deployed automatically.

1. Exit Play Mode and let Package Manager finish importing WebRTC. Resolve any Console/package download error before proceeding.
2. Open your existing cube or bowling scene. Select **Controller System**, the object containing `ControllerManager` and `ControllerDebugPanel`.
3. Choose **Tools → Motion Controllers → Enable LAN WebRTC On Selected Controller System**. This adds `WebRtcLanControllerTransport` and disables, but does not remove, `ControllerReceiver`.
4. On the new component, set **Signaling Url** to `wss://YOUR-SIGNAL-DOMAIN/signal` and **Pwa Url** to the complete deployed HTTPS controller page, e.g. `https://jjohnjones.github.io/Motion-Controller-Website/`. Confirm your actual Pages URL; Unity and PWA deployment settings must agree.
5. Keep `ControllerManager`, visualizer input references, calibration settings and gameplay components as they are. Save the scene. No scene or bowling script was automatically rewritten.
6. Enter Play. The existing left debug panel shows **Connect Controller**, a QR, state and per-player RTT. The QR changes on each session. **New controller session / QR** disconnects all current controllers and rotates the invitation.

If configuring manually, put `WebRtcLanControllerTransport`, `ControllerManager`, and `PersistentControllerRoot` on a top-level controller-only GameObject, configure the fields above, and uncheck the legacy receiver component. Keep `ControllerDebugPanel` and gameplay on separate scene-local objects. Only one LAN host component should run in the game. If you create a new cube scene with the scene generator, run the LAN setup command afterward to enable LAN instead of the fallback transport.

## Phone test

1. Put PC and phone on the same private LAN (PC Ethernet and phone Wi-Fi is fine when bridged). Keep the game running.
2. Scan the QR using the phone camera. Open the link in Safari or Chrome. No IP, port, token or tunnel URL needs typing.
3. Expect **Signaling → Connecting → Connected · LAN WebRTC**, with a player number. Unity shows **Player 1 Connected**. Each additional phone has its own peer, controller ID, player slot and calibration/history, up to the manager's configured capacity (maximum four).
4. Tap **Enable Motion**. On iPhone, grant both motion/orientation requests triggered by that tap. If denied, allow the site's sensor permissions and retry/reload. HTTPS remains required. The page does not request microphone/camera permission for WebRTC.
5. Hold the phone in its intended neutral pose and tap **Calibrate**. Wait for Unity's acknowledgement, then rotate the phone. The existing object should match the previous transport's behavior. Screen rotation requires recalibration as before.
6. Check the Unity frame count/sequence increases, arrival age stays low, and RTT becomes available. PWA diagnostics contain the raw sensor values, buffer size and its own ping RTT. RTT is diagnostic only; it does not modify sensor timestamps or release calculations.
7. In the existing bowling scene, confirm Hold Ball press/release/cancel still arrives. The networking migration does not alter throw calculations.
8. Close the phone tab: its controller should disappear. Reopen via QR. Temporarily disconnect Wi-Fi: the phone makes up to five delayed reconnect attempts, then **Reconnect** can retry. Reconnect creates a fresh controller ID and requires calibration. Hiding the page cancels the hold and disconnects; returning resumes pairing while the session remains valid.
9. Exit/re-enter Play: the old link must report expiry; scan the new QR. Also test a second phone and confirm separate calibration and disconnection.

Android Chrome generally exposes sensors without iOS's permission prompt; device/browser policies still may block them. In-app/social browsers can restrict sensors/WebRTC: open in the system browser. Keep the controller page visible and the phone awake; mobile background suspension is expected.

## ICE, STUN, TURN and firewall

Both peers use `iceServers: []`. Native/browser ICE host candidates are used, with **no STUN and no TURN**. STUN is not required for a reachable same-LAN candidate pair. It can later help discovery on some routed/NAT networks, but is not a cure for guest Wi-Fi isolation. TURN would relay traffic and is intentionally absent. A failed direct connection fails visibly; there is no hidden cloud motion relay.

Normal successful traffic goes phone ↔ Wi-Fi/router ↔ PC. ICE chooses a reachable candidate pair; this is LAN-focused, not an IP-subnet enforcement security boundary. Browser mDNS/privacy policies, VPN adapters, IPv6 configuration and network policy can affect discovery. The browser can probe Unity's IP host candidates without requiring camera access. Verify physical iPhone/Android behavior on the target network before removing fallback.

Windows Firewall must allow the **Unity Editor** during tests and later the **built game's executable** on the Private network profile. WebRTC uses dynamically allocated local UDP ports; there is no fixed controller TCP port to forward. Do not open router port forwarding or disable the firewall globally. Outbound TCP 443 must reach the website/signaling service. The old localhost TCP 8080 is unused in LAN mode.

In Chrome desktop diagnostics (`chrome://webrtc-internals`) or browser `getStats()`, inspect the selected candidate pair: UDP with `host` or peer-reflexive (`prflx`) candidates, rather than `relay`, demonstrates a direct path. Packet counters continue over the DataChannel, not WSS signaling. A local automated browser test proves native compatibility, but does not prove that your phone's Wi-Fi/firewall allows the pair.

## Failure behavior

| Symptom | Check / behavior |
| --- | --- |
| No QR / Failed | Configure real PWA and WSS URLs; check backend `/health`, TLS certificate, DNS and internet. Unity retries service failures after five seconds. |
| Session expired | Host stopped, service restarted, or four-hour lifetime elapsed. Scan the current QR. |
| Session full | Four signaling peers or the smaller Unity controller limit is occupied. Close an unused controller. |
| Connecting then timeout | Same LAN, Private firewall permission for the correct executable, no guest/client isolation, VPN policy, browser ICE restrictions. No TURN fallback is configured. |
| Connected but no sensors | Tap Enable Motion, grant access, use HTTPS/system browser, keep the page visible. |
| DataChannel drops | Unity removes that peer and its state; phone retries, then recalibration is required. An interrupted hold is not a release. |
| Signaling drops | Host invalidates the session and closes peers. New QR is required after recovery. |
| Second phone joins | Independent PeerConnection/controller ID; original controller remains connected. Gameplay player selection remains the existing game's responsibility. |

## Protocol and timing

One **reliable ordered** `controller-v1` channel carries existing version-1 JSON hello/welcome, motion, calibrate/calibrated, button, ping/pong plus `serverPing`/`serverPong`. Welcome adds `playerNumber`; old clients can ignore it. Per-peer one-time tickets replace the legacy shared tunnel token for LAN hello. The existing sequence numbers, browser monotonic timestamps, raw sensor fields and release snapshots are unchanged.

Ordered delivery preserves calibration/button/snapshot semantics during migration. Under packet loss retransmission can briefly delay motion behind an earlier packet. Sender backpressure avoids growing an application motion queue; failed essential button delivery disconnects rather than silently losing release. A later split into reliable events and an unordered/latest-motion channel needs explicit cross-channel ordering rules first. Binary serialization can replace JSON behind the transport boundary later.

Messages are limited to 8 KiB; the existing codec validates IDs, sequences, timestamps, quaternion/vector values and button phases/snapshot age. Per-peer input queues are bounded. Only expected message types reach the controller manager. Removing either transport cleans up only the controller IDs it owns.

## Fallback and verification

To return to the tunnel while diagnosing a LAN issue: stop Play, disable the LAN component, enable `ControllerReceiver`, then use the existing WSS setup. The PWA's **Legacy WebSocket fallback** remains available when opened without a LAN session link. Neither fallback source nor `.gitignore` was removed.

Run Unity EditMode tests and `node --test tests/*.test.cjs` in the PWA repository, plus `node --test` in the signaling directory. `LanBrowserIntegrationTests` is optional and skips unless its external browser harness is configured through `MCS_TEST_SIGNAL` and `MCS_TEST_DIR`. Test physical phones and a Windows build before considering the migration validated for release.
