# Controller protocol v1

Owner: Unity repository `JJohnJones/Motion-Controlled-Sports`.
Companion: `JJohnJones/Motion-Controller-Website` (separate source delivery).

## Boundaries

`LoopbackWebSocketHost` deals only with connections and UTF-8 text. It has no Unity dependencies.
`ControllerReceiver` authenticates peers, deserializes on the Unity main thread, binds the assigned ID to the connection, and passes converted `MotionFrame` values to `ControllerManager`.
`ControllerSession` holds latest input, calibration, raw/filtered rotation, counters and a 120-frame ring buffer. The visualizer reads `IMotionInputSource` and never sees sockets or JSON.
Native clients or a WebRTC receiver can submit the same converted frame. A binary codec can replace JSON without changing gameplay input consumers.

## Connection and messages

The only endpoint is `/controller`. Accept exactly the configured browser Origin and a valid WebSocket version 13 upgrade. The local server binds `127.0.0.1:8080`; TLS is terminated at the development tunnel. The browser connects using **WSS**. No token appears in the WebSocket URL.

1. Phone sends `{"version":1,"type":"hello","token":"32-hex-character-token"}` within five seconds.
2. Unity replies `{"version":1,"type":"welcome","controllerId":"server-assigned-32-hex-id",...}`.
3. Every later client message includes `controllerId` and `version:1`. An ID is valid only for that connection. A new connection gets a new ID and must recalibrate. Up to four authenticated controllers are supported (eight connections including pending authentication).
4. `motion` and `calibrate` share one strictly increasing sequence counter starting at 1. A calibration is a **complete sensor snapshot** from the button press, with its own sequence. Unity accepts that snapshot and records its converted orientation as neutral; it returns `type:"calibrated"` and the accepted sequence. Motion timestamps may be equal for reused snapshots but cannot regress.
5. `{"version":1,"type":"ping","controllerId":"…","timestamp":1234.5}` returns `type:"pong"` with the echoed timestamp. The phone measures RTT entirely on its own clock; Unity main-thread scheduling is included. This is not one-way latency.

Example sensor message (values illustrate schema, not a particular physical pose):

```json
{
  "version": 1,
  "type": "motion",
  "controllerId": "server-assigned-id",
  "sequence": 42,
  "timestamp": 12345.67,
  "motionTimestamp": 12342.12,
  "orientation": { "x": 0, "y": 0, "z": 0, "w": 1 },
  "screenAngle": 0,
  "absoluteOrientation": false,
  "angularVelocity": { "x": 0, "y": 0, "z": 0 },
  "acceleration": { "x": 0, "y": 0, "z": 0 },
  "accelerationIncludingGravity": { "x": 0, "y": 0, "z": 9.81 },
  "hasAngularVelocity": true,
  "hasAcceleration": true,
  "hasGravity": true
}
```

Wire timestamps are **milliseconds since this page's performance time origin**, captured at the sensor event callback (`performance.now()`), not synchronized hardware capture times. `timestamp` belongs to orientation; `motionTimestamp` belongs to the separate motion callback. These browser events are asynchronous and their readings need not have identical sample times. The flags indicate availability and freshness, not measured zero. Samples older than 250 ms are not presented as fresh. `absoluteOrientation` is diagnostic: compass north is not required for relative calibration, and heading drift remains possible.

Quaternion layout is `x,y,z,w`. The wire orientation is a normalized **right-handed device-to-browser-reference** rotation, intrinsic `Z(alpha) * X(beta) * Y(gamma)`. Browser `rotationRate` maps `x=beta`, `y=gamma`, `z=alpha`, in **degrees/second**. Both acceleration vectors are in **m/s²** in natural device axes. Browser frame values have not been screen-corrected or reflected yet.

## Coordinate conversion

Browser device axes: +X right, +Y toward the top of the natural screen, +Z outward through the screen. On typical phones the natural screen is portrait. W3C defines axes independently of UI rotation.

Let `qD = qZ(alpha) qX(beta) qY(gamma)`, and `theta = screen.orientation.angle` (fallback `window.orientation`). The screen specification measures physical rotation counter-clockwise. The upright UI basis compensates with `qZ(-theta)`, hence `qScreen = qD qZ(-theta)`. A physical +90° roll paired with screen angle +90° preserves the upright UI frame.

Unity-facing axes: +X screen-right, +Y screen-up, +Z into the screen. Use `S=diag(1,1,-1)` on both rotation bases: `RUnity = S RScreen S`. The equivalent quaternion is **(-x,-y,z,w)**. Do not use `Quaternion.Euler(alpha,beta,gamma)`; its order and axes differ from the browser.

For a local linear acceleration, first rotate the device vector by `qZ(theta)` (the inverse basis conversion), then reflect Z: `(x,y,-z)`. Angular velocity is an **axial** vector, so the reflection is `det(S)S`: `(-x,-y,z)`, followed by degrees-to-radians conversion. The transport-independent `MotionFrame` contains Unity-screen-local vector values and Unity-convention orientation.

Calibration stores `qReference` in the converted frame. Relative local rotation is:

```
qRelative = inverse(qReference) * qCurrent
cube.localRotation = cubeNeutralLocalRotation * qRelative
```

This expresses movement in the screen axes at calibration time. Hold the phone upright facing you for the first test; the cube's raised screen face is -Z and its top marker is +Y. Portrait and landscape are both supported at calibration. A subsequent UI screen-angle change invalidates calibration; hold the cube and ask for recalibration rather than reinterpret an old neutral basis. Lock screen rotation if you want continuous physical roll without UI rotation changing that basis. This convention is explicitly testable, not a claim that every browser/device has been physically verified.

## Freshness, performance, lifetime

Phone sends at most 60 fresh orientation samples/second (browser/timer cadence may be lower), with no duplicate-orientation timer stream. It skips sends when the socket buffer exceeds 8 KiB instead of building a sensor queue. JSON is intentionally readable. Unity caps messages below 8192 bytes, incoming queue around 256 events, outgoing queue 16 messages per connection, handshakes five seconds, and authenticated inactivity fifteen seconds. Malformed, spoofed, oversized and overloaded peers are disconnected. Sequence gaps include locally skipped messages; TCP itself is reliable and ordered.

The PC shows time since last packet **arrival**, not sensor age or one-way network delay. The cube holds after 500 ms without a new frame. On hidden phone pages, the PWA disconnects immediately; on return, Connect and Calibrate again. RTT timeout is ten seconds. Disconnection removes that controller's state/history and frees its slot. A stop/restart generates a fresh random 128-bit session pairing token.

Optional quaternion Slerp smoothing uses `weight = 1-exp(-dt/tau)`; default tau 25 ms. Raw and smoothed orientations are both available; set tau to zero or uncheck the debug toggle. Calibration resets both. No Kalman filter, position integration, gesture detection or sports logic is present.

## Production direction

The tunnel is development infrastructure, exposes only this token-authenticated endpoint, and relays motion via a third party. Keep the token/pairing link private; stop the tunnel when done. It needs working internet, has no LAN-latency guarantee, and is not a production pairing service. Do not weaken TLS checks or expose this loopback HTTP endpoint directly.

A production path can retain the PWA and motion layer, introduce HTTPS room/QR pairing with expiring per-player credentials, and use a Unity-supported WebRTC package for encrypted data channels. Evaluate unordered, non-retransmitted motion messages and reliable calibration/control messages; handle their ordering explicitly (e.g. calibration generations). ICE can select direct LAN connectivity; STUN/TURN supports other networks, with relay fallback. Measure actual hardware latency before choosing. Trusted LAN WSS through a managed TLS reverse proxy is another deployment option when certificate/DNS setup is controlled.

References:
- [W3C device orientation and motion axes, angles and units](https://www.w3.org/TR/orientation-event/)
- [W3C screen orientation angle convention](https://www.w3.org/TR/screen-orientation/)
- [HTTPS/WebSocket mixed-content guidance](https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_client_applications)
- [iOS-style permission request and transient user activation](https://developer.mozilla.org/en-US/docs/Web/API/DeviceMotionEvent/requestPermission_static)
- [WebRTC signaling and ICE infrastructure](https://webrtc.org/getting-started/peer-connections?hl=en)
- [Cloudflare development tunnel setup](https://developers.cloudflare.com/tunnel/setup/)
