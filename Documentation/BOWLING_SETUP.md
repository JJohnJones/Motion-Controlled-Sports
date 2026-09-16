# Bowling setup

For the complete 1–4 player ten-frame game, follow [BOWLING_MATCH.md](BOWLING_MATCH.md) and launch Bootstrap. The tuning and standalone input-prototype notes below remain useful for development.

The existing QR/WebRTC phone connection and calibration are reused. This adds one playable lane, a Rigidbody ball, ten pins, locked alpha aiming, forward-release validation, wrist-controlled hook, and automatic resets. No scoring, multiplayer gameplay, menus, sound or character animation is included.

## Start here

1. In Unity 6000.6.0f1, open `Assets/Scenes/BowlingPrototype.unity` if present. Alternatively choose **Tools → Motion Controllers → Create Bowling Prototype Scene**. The command creates a new scene and a unique `Assets/BowlingGenerated` asset folder; it does not overwrite the cube scene or previous bowling scenes. Save the current scene if asked. No downloaded models are needed.
2. Configure the persistent WebRtcLanControllerTransport with the existing HTTPS PWA and public WSS signaling URLs. See ../LAN_CONTROLLER_MODE.md; no local receiver port or browser-origin Inspector field remains.
3. The **Bowling Ball** has a Rigidbody, sphere collider, `BowlingBall`, and a release-point reference. **Ten Pin Rack** contains ten Rigidbody pins with compound foot/body/head colliders. The lane runs along world +Z, has a 2.4 m-wide surface, lowered gutter floors and outer walls. The pins use a shared procedural mesh with red neck bands. These are prototype proportions, not a regulation equipment simulation.
4. Publish the changes in the separate **Motion Controller Website** repository to your existing HTTPS host. New file: `hold-button.js`. Modified: `controller.js`, `index.html`, `styles.css`, `service-worker.js`, tests, package version and README. Deploy all of them together. The worker cache is now `motion-controller-shell-v6-turn`; close old tabs/installed app instances and reload. The new sensor packets include the original alpha/beta/gamma angles. Update Unity before using the new button; an old receiver rejects unknown button messages.
5. Press Play, scan the QR, connect and Enable Motion. Keep only one persistent controller root active.
6. Hold your phone like a Wii controller: approximately horizontal, screen facing up, with its top edge pointing down the lane. Calibrate in that grip. Forward acceleration is measured toward that calibrated top-edge direction, compensating for the phone's current rotation. Keep screen orientation locked during throws, or recalibrate after a portrait/landscape change. The cube scene continues to work with the updated PWA.
7. Watch the lane's green **Aim Indicator**. Turn the calibrated phone gently left/right to establish aim. The prototype uses the original **alpha** angle relative to calibration (wrapped across 0/360°), scaled and clamped. Positive alpha means counter-clockwise/left with the phone screen up. Gamma does not affect aim.
8. When Unity shows **Ready**, press and hold **HOLD BALL**. The button changes immediately, Unity shows **Holding**, and aim locks. Keep a secure grip on the phone itself throughout the swing.
9. Make a comfortable bowling swing, then **lift your finger from the button** while acceleration is forward and **beta is increasing**. Both direction gates must pass as well as the speed threshold. Crossing a threshold while still holding does nothing. Roll your wrist slightly (gamma) near release to add hook; neutral gamma gives a straight throw.
10. Watch the ball roll into the pins. Unity transitions **Rolling → Resetting → Ready** automatically. New presses/releases during Rolling or Resetting cannot launch another ball. If you began holding during a roll, lift that finger and make a fresh press after Ready.

For a desktop executable, add this scene to the Windows Build Profile Scene List and build normally. The debug panel remains available. Its **Ui Scale** setting controls readability; the panel scrolls when needed. The bowling scene itself is intended for a wide Game view such as 1280×720 or 1920×1080.

## Throw tuning (Inspector)

On **BowlingThrowController → Release**:

| Setting | Default | Effect |
|---|---:|---|
| Minimum Swing Speed | 0.75 rad/s | Minimum effective gyro speed to accept a throw. Below this, cancel and return to Ready. |
| Maximum Swing Speed | 10 rad/s | Effective gyro speed mapped to maximum ball velocity. Higher readings saturate. |
| Minimum Ball Velocity | 3.5 m/s | Launch velocity at the minimum valid swing. |
| Maximum Ball Velocity | 12 m/s | Upper launch speed bound. |
| Release Sampling Window | 0.2 s | Inspect 100–300 ms immediately before release, limited to the current hold. |
| Sensitivity | 1.5 | Multiplier on the measured gyro-speed blend. |
| Minimum Forward Acceleration | 0.25 m/s² | Require linear acceleration toward the calibrated phone top to exceed this at release. Gravity-inclusive data cannot substitute for linear acceleration. |
| Minimum Forward Beta Speed | 15°/s | Require increasing beta near release, measured over a 40–150 ms span within this hold. |
| Forward Beta Increases | enabled | Matches the user's confirmed forward-swing direction. |
| Hook Acceleration | 0.25 m/s² | Maximum lateral arcade-hook acceleration. Zero disables hook. |
| Full Spin Roll Degrees | 35° | Relative gamma wrist roll mapped to full left/right hook. |

After the forward-acceleration and beta checks pass, the release formula is `effectiveSwing = sensitivity × (0.65 × recentPeak + 0.35 × currentAngularSpeed)`, followed by a clamped linear mapping from the configured swing-speed range to the ball-speed range. Speeds here are **angular-velocity magnitudes**, in rad/s after Unity's existing coordinate conversion. PWA debug values remain deg/s. Hold duration does not enter the formula. These defaults are starting values; actual feel must be tuned with your phone.

**Aim Sensitivity** defaults to 0.3 and **Maximum Aim Degrees** to 6°. Aim locks on the accepted press, including the press snapshot, so the forward swing cannot whip the aim around. **Maximum Roll Seconds** defaults to 12 and **Reset Delay Seconds** to 2. A throw finishes when the ball leaves the defined playable bounds, remains almost stopped for a second after the first two seconds, or reaches the roll timeout. The reset delay lets pin action finish before restoring all poses and velocities.

A mostly translating phone with little angular rotation may produce a weak throw even if your arm moves far. This version measures angular swing speed rather than inferring absolute phone velocity from accelerometer integration. Linear acceleration gates the direction but does not add power. A negative beta change or non-forward acceleration blocks release. This is a simple directional test, not a full arm-motion reconstruction: releasing after forward acceleration has turned into braking can also be rejected. Release earlier in the forward swing or tune the positive minimum thresholds if needed.

## Timing and failure behavior

- Each button message has its own `buttonSequence` and phone `eventTimestamp`. Press/release carry a latest sensor snapshot; its sensor timestamps are preserved rather than relabeled as release time. This avoids waiting for another motion tick before launching.
- Unity retains the existing 120-frame ring buffer per controller and scans only the configured pre-release interval. It uses phone sensor timestamps, not WebSocket arrival times. Peak samples from before the hold or after the release are excluded. At 60 Hz, a 200 ms window is roughly 12 samples; scanning the fixed ring requires no list allocation.
- Release needs a gyro sample and orientation within 120 ms of finger-up. Older/missing data cancel the throw. The phone itself cancels stale holds and sends `canceled`, never `released`, for interrupted touches.
- Touch/pointer cancellation, lost pointer capture, screen rotation, calibration changes, hidden pages and disconnection cannot launch the held ball. Lost packets/connection cannot leave the game waiting indefinitely: fresh-motion timeout cancels the hold after 500 ms without arrival. Returning requires a new press; reconnecting also requires calibration.
- Required button events are never silently skipped under network backpressure. If they cannot be sent, the PWA closes the connection; Unity cancels the hold on stale input/disconnect. Ordinary high-frequency sensor samples may still be skipped to limit queueing.
- The phone's Held/Released text describes local touch state. Unity's panel is authoritative about Ready/Holding/Rolling and whether a weak/stale throw was accepted. There is no game-state feedback channel to the phone yet.
- Motion and buttons are currently ordered over the same WebSocket. A future unordered WebRTC transport must reorder timestamped data or attach sufficient history to the release; it must not assume these ordering guarantees automatically carry over.

## Diagnostics and manual acceptance checks

The Unity debug panel shows ball state, button phase, alpha aim, live angular speed, recent peak, effective release speed, release timestamp, launch velocity, forward acceleration, signed beta speed, wrist gamma and spin. Hook uses `spin=clamp(relativeGamma / FullSpinRollDegrees, -1, 1)`. The ball receives normal rolling angular velocity plus visible side spin, and a small lateral acceleration while in contact with the lane area. Positive gamma hooks right, negative hooks left. It stops applying in gutters or in the air. This is an intentional arcade approximation, not a simulation of lane oil and bowling-ball coverstock.

Check the following with the deployed PWA and your physical phone:

1. Aim left/right in Ready; press and verify the aiming line stays fixed throughout a swing.
2. Swing strongly without lifting your finger: the ball must remain at the release point in Holding.
3. Release slowly, then repeat with a faster forward swing at the same aim: compare launch m/s. Faster valid swings should be faster, up to the cap. Confirm that decreasing beta and backward acceleration each reject a release.
4. Hold still for several seconds and release: this should be rejected as too slow rather than gaining charge.
5. Lift during the forward swing: one ball launches, collides with pins, and resets. Repeat several throws to check that no pin retains its previous velocity/pose.
6. Press/release during Rolling: no second launch. Wait for Ready and make a fresh press.
7. While holding, slide off the button and lift: pointer capture should still produce one release. Add another finger: that finger must not release the first hold.
8. Background the page, interrupt the touch or rotate the screen while holding: no throw. Reconnect/recalibrate as needed.
9. Interrupt Wi-Fi while holding: the ball stays unlaunched and the hold cancels. Reconnect and verify the next new press/release works.
10. Keep alpha aim fixed and compare gamma near zero, positive, and negative at release. Neutral should go straight; opposite wrist rolls should curve in opposite directions. Gamma changes must not move the aiming line in Ready.

If the ball will not launch, check the Unity rejection message, calibrated/fresh input, gyro availability, and current state. If throws feel too weak, increase sensitivity moderately or reduce the maximum swing-speed mapping. If aiming is twitchy, reduce aim sensitivity. Check direct/relay status and RTT for network latency; swing sensitivity cannot remove it.

Automated coverage is described in `BOWLING_VALIDATION.md`. Physical iPhone/Android touch behavior and throw feel still require this manual acceptance pass.
