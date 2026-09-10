# Phone → Unity cube prototype

Only the technical controller prototype is implemented. No sports gameplay or absolute position integration is included.

## Repository ownership

**Unity repository:** `git@github.com:JJohnJones/Motion-Controlled-Sports.git`

Open the Unity project folder containing `Assets`, `Packages`, and `ProjectSettings` in Unity Hub. Here that folder is `Motion Controlled Sports` inside the workspace. It targets the existing Unity **6000.6.0f1** editor and URP project. Additions:

- `Assets/Scripts/Networking`: loopback WebSocket host, controller receiver, JSON codec.
- `Assets/Scripts/Motion`: `MotionFrame` and explicit coordinate conversion.
- `Assets/Scripts/Controllers`: `IMotionInputSource`, manager and independent sessions.
- `Assets/Scripts/Debug`: cube visualizer and engineering panel.
- `Assets/Editor`: scene-creation menu command.
- `Assets/Tests/Editor`: motion/math and real WebSocket tests.
- `Documentation`: this guide and the protocol specification.

**PWA repository:** `git@github.com:JJohnJones/Motion-Controller-Website.git`

Copy the contents of the separate `Motion-Controller-Website` handoff folder/ZIP into that repository root: `index.html`, `styles.css`, `controller.js`, `motion-math.js`, `manifest.json`, `service-worker.js`, `icons/`, `.nojekyll`, `package.json`, `tests/`, and `README.md`. It has no runtime npm dependencies and requires no build. The PWA is not inside the Unity project.

Inspection found no `.git` directory or `.gitignore` in the supplied workspace/project folders. No Git initialization, remote changes, commits, pushes or ignore-file modifications were made. When placing this project in the actual Unity checkout, retain its standard Unity `.gitignore`; commit `Assets` including `.meta`, `Packages`, `ProjectSettings`, and documentation, not generated `Library`, `Temp`, `Logs`, `obj`, or build folders.

## 1. Create the Unity scene

1. Let Unity import the new C# scripts. Resolve any Console compilation errors before proceeding.
2. Choose **Tools → Motion Controllers → Create Phone Cube Prototype Scene**. Save any existing scene if prompted. This creates a separate `Assets/Scenes/PhoneCubePrototype.unity` (a unique suffix is used if it already exists). It does not overwrite SampleScene.
3. The generated scene contains **Controller System** with `ControllerManager`, `ControllerReceiver`, `ControllerDebugPanel`; **Phone Cube** with `PhoneOrientationVisualizer`, a raised screen face and top marker; a camera and light. References are wired automatically.
4. On Controller System, keep receiver **Port = 8080**. Set **Allowed Origin** to the exact origin serving the PWA, e.g. `https://jjohnj.github.io`. Do **not** include `/Motion-Controller-Website/` in the origin. For another HTTPS host, use that host's origin instead.
5. Set the debug panel **Pwa Url** to the full website URL including its repository path, e.g. `https://jjohnj.github.io/Motion-Controller-Website/`. Configure these fields before Play; restart Play after changing receiver settings.
6. Press Play. Use a reasonably wide Game view (e.g. 1280×720) so the left debug panel and right cube both fit. The panel automatically scales with the render height. To enlarge it further, select **Controller System → Controller Debug Panel → Ui Scale** and try **1.25** or **1.5**; set this outside Play mode to retain it. The panel scrolls if its content exceeds the available height. Confirm `Listening on 127.0.0.1:8080`. Copy the displayed session token. A new token is generated each time the receiver starts.

Manual scene alternative: create the system components above, a cube and camera/light. Assign ControllerManager to the visualizer's `Input Source`, and the visualizer to the debug panel. Use an unrotated cube facing a camera on its -Z side; screen-face decoration is optional. Do not attach rigidbody/gravity motion to the cube.

For a Windows executable, add the generated scene to the active **Build Profile → Scene List**, choose Windows, and Build and Run. The initial target is the standard Mono/.NET Standard desktop configuration; separately validate other backends before adopting them. Unity sets `Application.runInBackground = true`, so moving focus to the tunnel terminal does not pause the receiver. The PWA must remain visible on the phone.

## 2. Run a secure tunnel on the PC

Install `cloudflared` using the [official Windows instructions](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/), or Windows Package Manager:

```powershell
winget install --id Cloudflare.cloudflared --exact
```

Open a new terminal if needed, then with Unity in Play:

```powershell
cloudflared tunnel --url http://127.0.0.1:8080
```

Keep it running. It prints a temporary HTTPS address, for example `https://random-words.trycloudflare.com`. Your phone endpoint is then:

```
wss://random-words.trycloudflare.com/controller
```

Replace the hostname with the actual output. A root URL returning 403 is expected: this server only accepts a WebSocket upgrade on `/controller` from the configured Origin. The tunnel handles browser-trusted TLS; do not use a self-signed certificate, disable browser security, or enter `ws://PC-IP` in the PWA.

The local unencrypted hop is confined to loopback on the PC. The phone and PC both need internet for this tunnel route, even if they share Wi-Fi. Start on the same good Wi-Fi for consistent testing; this route does not use direct LAN sensor delivery. Quick tunnel names change when restarted and have no uptime/latency guarantee. If a local cloudflared configuration conflicts with quick-tunnel mode, follow the [quick-tunnel documentation](https://developers.cloudflare.com/tunnel/setup/) or use a dedicated named tunnel profile.

This work does not install cloudflared, open a public tunnel, or publish your repository automatically.

## 3. Serve the separate PWA over HTTPS

1. Put the complete PWA handoff files in the mobile repository and commit/push them when ready.
2. For GitHub Pages, in that repository's **Settings → Pages**, publish from the intended branch's root, then use the HTTPS URL GitHub reports. Keep **Enforce HTTPS** enabled. Hosting options depend on repository visibility/account configuration; another static HTTPS host works equally well. See the [GitHub Pages publishing guide](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).
3. Open the deployed URL directly in **Safari on iPhone** or **Chrome on Android**. Avoid embedded social/messaging app webviews for the initial test. HTTP on a LAN IP is not a suitable phone sensor test.
4. Paste the **WSS endpoint** and **pairing token**, then tap **Connect**. The UI must show `Connected to Unity` and an assigned controller ID. A WebSocket connection alone is not proof that application pairing completed.

Optional pairing link: paste the actual WSS endpoint into Unity's debug panel and click **Copy pairing link**. Open that link on the phone. It prefills the endpoint and token using a URL fragment, then clears the fragment from the visible URL. Tap Connect. Keep this link private. No automatic LAN discovery or QR scanner is implemented.

## 4. Enable sensors and calibrate

1. Tap **Enable Motion** on the phone. On iPhone accept the motion/orientation permission prompt; the requests are invoked directly from that tap. If denied, update the site's permissions/reset the denial as supported by your Safari version, reload the page, and tap again. Returning from another app can disconnect the controller; reconnect before calibrating.
2. On Android, permission dialogs differ and may not appear. Grant any motion/sensor site permission the browser presents. Null or absent sensor readings are displayed as unavailable; orientation alone is sufficient for the cube test.
3. Confirm live orientation numbers change. Some desktop browsers have no physical sensors; a permission success does not guarantee readings.
4. Hold the phone comfortably upright with its screen facing you. Tap **Calibrate** and wait for the confirmation that Unity acknowledged it. The cube returns to neutral.
5. Tilt left/right, tilt forward/back, and twist gently. The cube's raised front represents the screen, and the small top marker distinguishes its +Y direction. Verify each motion separately.
6. Hold a different pose and tap Calibrate again: it becomes the new neutral. Return to the old pose and confirm it now gives a relative rotation.
7. Try landscape, then calibrate in landscape. If the UI changes orientation during movement, recalibrate when requested. Lock phone screen rotation for uninterrupted wrist-roll testing.

Do not expect acceleration to move the cube through space. Acceleration and angular velocity are collected for later gesture work only.

## 5. Verify packets, latency and multiple controllers

- Unity's frame/sequence counters must increase when new orientation events arrive. **Arrival age** should remain low, and the panel should show receiving/calibrated. A cube without calibration holds neutral; a stream stale for more than 500 ms holds the last pose.
- The phone shows sent count, skipped sends, buffered bytes and application **RTT** through Unity. RTT includes tunnel routing and Unity scheduling. Neither phone-to-PC clock subtraction nor half-RTT is reported as measured sensor latency.
- Compare **Use light smoothing** on/off in Unity. Default smoothing time constant is 25 ms; set the manager's value to zero for raw input. Display refresh, browser event cadence and the tunnel all contribute to perceived latency.
- For a practical first check, aim for low/stable RTT (roughly below 50–80 ms is preferable), no growing buffer, and convincing immediate visual response. These are diagnostic targets, not guarantees or measured results. If RTT is consistently high or motion stalls, evaluate direct LAN WebRTC next; more smoothing cannot fix network latency.
- Stop the tunnel or turn off Wi-Fi. The cube should hold, the phone should report loss or heartbeat timeout, and Unity should free the session after disconnect/timeout. Restart the tunnel, enter its new endpoint if needed, reconnect, and recalibrate.
- Connect a second phone using the same token: IDs and sensor/calibration histories are independent. The demo cube selects the first connected controller by default. To view another one, enter its assigned ID into the visualizer's Controller Id field. Four controllers are permitted; a fifth is rejected.
- Backgrounding/locking the phone intentionally disconnects. Return, tap Connect, and recalibrate. Previously granted sensor permission/listeners may remain available; reload and Enable Motion again if needed.

## Troubleshooting

| Symptom | Check |
|---|---|
| Unity listener failed | Port 8080 may be occupied or another prototype scene is running. Stop the other instance or change the receiver port and the tunnel command together. |
| WSS cannot connect | Unity in Play, tunnel still running, exact hostname and `/controller`, correct Allowed Origin, working HTTPS/certificate, and no corporate/VPN WebSocket blocking. |
| Connects then immediately drops | Old/wrong pairing token, full four-controller capacity, mismatched protocol, or malformed message. Refresh PWA after changing protocol. |
| No sensor data | HTTPS, direct Safari/Chrome page, motion permission, supported sensors, visible foreground tab; distinguish unavailable readings from actual zeros. |
| Cube does not move | Check assigned ID, packet count, calibration acknowledgement and stale indicator; verify visualizer references. |
| Wrong perceived axes | First test screen facing you, inspect front/top markers, recalibrate after screen rotation, compare raw mode. Browser/device behavior still needs physical verification. |
| Delayed motion | Check RTT, buffer bytes, sensor freshness, Wi-Fi quality, VPN, tunnel routing and Unity frame rate. Temporarily disable smoothing. |
| Old PWA after deployment | Update service worker cache version with source changes, close all app tabs/installed instances and reopen; clear site data/unregister the worker for a clean engineering test. |

No inbound router port-forward or public Windows port 8080 rule is needed with this loopback tunnel design. Allow cloudflared outbound access if Windows security or a managed network blocks it; Cloudflare Tunnel normally needs outbound port 7844 (QUIC/UDP or HTTP2/TCP). Phone/PWA uses HTTPS/WSS port 443. If UDP is blocked, try `cloudflared tunnel --protocol http2 --url http://127.0.0.1:8080`. Do not disable the firewall globally. Router client isolation affects a future direct-LAN design, but usually not this internet tunnel route. See [Cloudflare firewall requirements](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/deploy-tunnels/tunnel-with-firewall/).

## Automated and remaining checks

In Unity, open **Window → General → Test Runner → EditMode → Run All** for the included calibration, coordinate mapping, filtering, malformed-frame and real WebSocket tests. In the separate PWA directory run `npm test` (Node, no install step). The delivered implementation passed 10 Unity tests and 8 PWA tests; see `VALIDATION.md` for exact coverage and limits.

Physical iPhone/Android sensor accuracy, HTTPS deployment, public WSS tunnel, Windows standalone runtime, and actual motion-to-display latency require the manual tests above. Automated quaternion and socket checks alone do not establish those success conditions. See `CONTROLLER_PROTOCOL.md` for units, multiplication order and the WebRTC evolution path.
