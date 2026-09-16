# Controller protocol v1

Owner: Unity repository `JJohnJones/Motion-Controlled-Sports`.
Companion: `JJohnJones/Motion-Controller-Website` (separate source delivery).

## Boundaries

`WebRtcLanControllerTransport` handles DataChannel delivery; `ControllerProtocolRouter` authenticates peers, deserializes on the Unity main thread, binds IDs to connections, and passes converted `MotionFrame` values to `ControllerManager`.
`ControllerSession` holds latest input, calibration, raw/filtered rotation, counters and a 120-frame ring buffer. The visualizer reads `IMotionInputSource` and never sees sockets or JSON.
Native clients or a WebRTC receiver can submit the same converted frame. A binary codec can replace JSON without changing gameplay input consumers.

## Connection and messages

The ordered reliable `controller-v1` DataChannel carries this protocol. Public WSS `/signal` carries only pairing/SDP/ICE and expiring ICE configuration. Direct and TURN-relayed connections use identical input packets.

1. Phone sends `{"version":1,"type":"hello","token":"32-hex-character-token"}` within five seconds.
2. Unity replies `{"version":1,"type":"welcome","controllerId":"server-assigned-32-hex-id",...}`.
3. Every later client message includes `controllerId` and `version:1`. An ID is valid only for that connection. Recovery/recreated channels authenticate with the retained ticket and preserve the same ID/calibration. A newly joined phone receives a new ID. Up to four controllers are supported.
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

Phone sends at most 60 fresh samples/second, skipping when the DataChannel buffer exceeds 8 KiB. Reliable ordering is retained; sequence gaps include locally skipped frames, not measured UDP packet loss. The PC reports arrival age rather than comparing unsynchronized clocks. Smoothing remains configurable (default 25 ms).

Hidden pages cancel held input while retaining pairing. Transient disconnection has an eight-second grace, heartbeat gaps are detected after twelve seconds, and recovery has a sixty-second budget. Only terminal failure removes identity/history. See CONTROLLER_RECOVERY.md. `serverPing`/`serverPong` measures application RTT entirely on the host clock; selected-candidate ICE RTT is separate and neither alters motion timestamps.

## Networking boundary

Cloudflare TURN is a fallback ICE path, configured with temporary credentials from authenticated signaling. The DataChannel remains encrypted and feeds the same router/input abstractions. Future multiplayer should replicate locally interpreted game actions/state. See ../LAN_CONTROLLER_MODE.md for setup and the separate signaling repository's TURN_SETUP.md for credential security and testing.

References: [device orientation](https://www.w3.org/TR/orientation-event/), [screen orientation](https://www.w3.org/TR/screen-orientation/).
