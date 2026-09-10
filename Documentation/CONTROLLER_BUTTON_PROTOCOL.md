# Additive controller buttons (wire version 1)

Buttons extend the existing version-1 motion protocol. Deploy the new Unity receiver before enabling the new PWA button. Existing motion-only clients still operate the cube. The input layer names the action **primary**; only bowling maps it to Hold Ball.

## Wire messages

After the existing hello/welcome handshake, a button event looks like this (sensor fields abbreviated here; the actual press/release includes the complete existing snapshot schema):

```json
{
  "version": 1,
  "type": "button",
  "controllerId": "assigned-id",
  "button": "primary",
  "phase": "released",
  "buttonSequence": 2,
  "eventTimestamp": 2025.25,
  "hasSnapshot": true,
  "sequence": 123,
  "timestamp": 2020.1,
  "motionTimestamp": 2022.5,
  "orientation": { "x": 0, "y": 0, "z": 0, "w": 1 },
  "angularVelocity": { "x": 100, "y": 0, "z": 0 },
  "hasAngularVelocity": true,
  "hasAcceleration": false,
  "hasGravity": false,
  "screenAngle": 0
}
```

`phase` is exactly `pressed`, `released`, or `canceled`. `buttonSequence` starts at 1 on each new connection and increases only on transitions. There are no repeated held messages: `ControllerSession.PrimaryHeld` is derived from the accepted transitions. Duplicate presses, duplicate/orphan releases, invalid IDs, invalid phases, regressing event times, and replayed sequences do not produce gameplay events.

`eventTimestamp` uses the phone's `performance.now()` clock captured in the pointer handler. The original orientation and gyro timestamps remain separate: attaching a sensor reading to release does not make the reading newer. The snapshot's `sequence` comes from the same counter as normal motion/calibration frames and its converted data is inserted into the same ring buffer before consumers receive `ButtonChanged`. `buttonSequence` is independent of that frame counter. Unity only accepts declared snapshots within 250 ms of the button event and rejects samples dated after the event; bowling imposes its stricter 120 ms freshness requirement at release.

Canceled events can omit the snapshot entirely (`hasSnapshot:false`): stopping a held action must still work when sensors are unavailable. No cancellation is interpreted as release. `ControllerReceiver` validates/decodes into `ControllerButtonEvent`; `ControllerManager` updates session button state and publishes the event via `IControllerButtonSource`. Bowling sees no JSON, WebSocket or browser classes. Future input implementations can submit the same converted data and events.

## Reliability/lifecycle

The current reliable ordered WebSocket carries both snapshots and transitions, preserving input ordering. A release includes the newest sensor state, so the game can immediately scan already-received history and launch without a deliberate buffering delay. The timestamps select the recent window even when PC arrival timing varies. History older than press is excluded.

The PWA uses pointer capture, `touch-action:none`, selection/callout suppression and a temporary page-scroll lock for the held button. It tracks only the initiating pointer. Normal pointer-up emits release exactly once; subsequent lost-capture is ignored. Pointer cancellation, unexpected lost capture, screen orientation change, calibration, hiding the page or leaving it cancels. If a required transition cannot be delivered because the socket is closed or backed up, it closes the connection instead of leaving a remotely held action active. Unity also cancels stale/disconnected holds, and a new press is required afterward.

The current generic protocol does not push bowling state back to the phone. Phone button text reflects touch state; the desktop is authoritative about game state and launch acceptance. The gameplay state machine ignores all presses/releases while Rolling/Resetting. A button held through a reset must be lifted and freshly pressed to start a new throw.

References: [Pointer Events and pointer capture](https://developer.mozilla.org/en-US/docs/Web/API/Pointer_events). Sensor units/coordinates remain defined in `CONTROLLER_PROTOCOL.md`.
