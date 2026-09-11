# LAN controller migration verification

Verified with Unity **6000.6.0f1** on Windows, official WebRTC **3.0.0**, and desktop Chrome. Tests ran in an isolated copy of source, without modifying the active scene.

- **20 Unity EditMode tests passed**, including the existing motion, calibration, release, bowling and WebSocket tests, plus router authentication/isolation, player allocation, server RTT/expiry and QR rendering checks. The optional browser integration test is skipped in normal regression runs.
- **Real browser-to-native integration passed separately**: a local TLS signaling endpoint opened the actual PWA in Chrome; it negotiated against the native Unity LAN component, authenticated, calibrated, sent synthetic sensor events, sent hold/release with a snapshot, and exchanged the Unity heartbeat. The test asserted that these arrived in the existing `ControllerSession` pipeline.
- Browser ICE statistics showed a selected **direct UDP** candidate pair, with no relay configured. No microphone or camera permission was used. This was a same-PC browser/native test, not a physical Wi-Fi test.
- The generated **45×45-module QR** was independently decoded with jsQR and exactly matched Unity's join URL, including the session fragment.
- **16 PWA tests passed**, covering sensor math/permissions, stale-data/backpressure behavior, button semantics, LAN negotiation and early ICE ordering, session expiry, unexpected messages and server ping responses.
- **4 signaling tests passed**, covering routing/authentication boundaries, disallowed origins, four peers/fifth rejection, host departure and session expiry.

Not verified yet: public signaling deployment, real iPhone/Safari and Android/Chrome on Wi-Fi, restrictive/router/VPN networks, Windows standalone player and IL2CPP/stripping. These are the remaining acceptance checks before retiring the legacy transport. See `LAN_CONTROLLER_MODE.md` at the Unity project root for deployment and exact manual steps.

The optional `LanBrowserIntegrationTests` test uses `MCS_TEST_SIGNAL` for a local signaling endpoint and `MCS_TEST_DIR` for a private harness exchange directory. The external browser harness consumes `join.txt`, opens the real PWA, and emits motion/button input. The Unity fixture outputs `qr.png` and writes `unity-result.txt` after accepting calibration, motion, release and RTT. Without those environment variables it deliberately skips. Do not publish generated join files or QR screenshots for live sessions.
