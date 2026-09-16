# Phone-to-cube setup

Use the [current WebRTC setup](../LAN_CONTROLLER_MODE.md). Controller data uses a DataChannel with direct ICE or TURN fallback. There is no local WebSocket listener or manual endpoint/token entry.

1. Open the existing PhoneCubePrototype scene, or Tools → Motion Controllers → Create Phone Cube Prototype Scene.
2. Configure the persistent WebRtcLanControllerTransport with the HTTPS PWA URL and public WSS signaling URL from the current setup guide. Deploy the matching PWA/backend with Cloudflare TURN configured on Render.
3. Enter Play, scan the QR, tap Enable Motion, then Calibrate with the phone held in a neutral pose. Rotate the phone and confirm the cube follows relative orientation. Recalibrate after changing screen orientation.
4. Check frame count, arrival age, sequence gaps and heartbeat RTT. Compare the visualizer smoothing toggle; default smoothing is 25 ms. These diagnostics do not measure one-way motion latency.
5. Briefly interrupt Wi-Fi and restore it within sixty seconds. Confirm the same player/calibration recovers. A held control must cancel rather than release during recovery.
6. Follow the signaling repository's TURN_SETUP.md for relay-only testing. A successful direct connection does not prove TURN works.

Unity EditMode tests cover protocol, calibration, motion and input behavior. The optional real-browser integration test covers DataChannel delivery, scene persistence and recovery. Actual phone permissions, Windows standalone/IL2CPP and restrictive-network relay behavior require deployment and physical-device testing.
