# Bowling validation — 2026-09-10

Verified in an isolated Unity **6000.6.0f1** project with matching URP **17.6.0**, plus Node tests for the PWA. The user's existing bowling scene was preserved.

- **17 Unity EditMode tests:** controller/cube regression tests, real WebSocket motion and button snapshots, wrong-token/origin rejection, calibration, coordinate conversion, bounded history, release speed mapping, hold duration independence, stale/pre-hold/future sample rejection, duplicate/orphan button handling, locked aim, release-only launch, ignored input during rolling, reset transitions, cancellation/disconnection, alpha-only aiming, forward-acceleration and increasing-beta gates, and signed gamma-to-spin calculation.
- **12 PWA tests:** permissions, sensor availability and original angle payloads, pairing, calibration snapshots, WSS enforcement, stale input/backpressure, finger capture and authoritative lift-off, multiple-finger isolation, canceled/hidden touches, and disconnect on failed required button delivery.
- **Generated-scene physics simulation:** ten pins stayed standing for two seconds before throwing; a central 3.5 m/s ball knocked down one pin; a central 12 m/s ball knocked down ten. Reset cleared pin velocities and restored a stable upright rack. This checks representative deterministic test throws, not a promise of strikes on every physical throw.
- **Hook simulation:** at 8 m/s with hook acceleration 0.25 m/s², opposite spin values (-0.6/+0.6) moved the ball about -0.103/+0.103 m sideways after 1.5 seconds; zero spin stayed centered. Hook was exercised through the same Rigidbody force method used by FixedUpdate in Play.

The low-speed physics check exposed upright pin sliding. Raising the configurable pin center of mass from 0.16 m to 0.22 m allowed slow impacts to topple pins while retaining pre-throw/reset stability. Existing scene pins receive this configuration through BowlingPinRack on initialization.

**Still requires physical testing:** publish/reload the updated PWA, recalibrate with the phone screen-up and top pointing down the lane, then tune alpha aiming, positive-beta/forward-acceleration thresholds, swing sensitivity and wrist hook for the user's phone and grip. Installed Safari/Chrome pointer behavior, public WSS timing, and Windows standalone behavior are not established by these automated checks. The original cube interaction was confirmed working by the user.

Run Unity's EditMode Test Runner and `npm test` in the PWA repository to repeat the committed tests. `BOWLING_SETUP.md` documents scene setup, parameters and manual acceptance checks.
