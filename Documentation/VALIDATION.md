# Prototype validation

Validated on this Windows machine with **Unity 6000.6.0f1** and Node.

- **10/10 Unity EditMode tests passed** in an isolated project using the same runtime/editor/test C# sources and Unity Test Framework 1.8.0. This compiled both runtime and editor assemblies in Unity. The isolated project used minimal test dependencies; the user's open scene and full project settings were not changed for the run.
- Tests cover right-/left-handed basis conversion, counter-clockwise screen-angle compensation in landscape, noncommuting calibration multiplication, duplicate sequence rejection, per-controller history isolation/bounds, configurable time-based smoothing, invalid frames, real WebSocket fragmentation/UTF-8 and replies, two connections/reconnects, wrong-origin rejection, and oversized-message rejection.
- The integration test traverses a real loopback WebSocket → token handshake → JSON codec → calibrated controller state → visualizer transform, then verifies rejection of another client's incorrect token.
- **8/8 Node tests passed** for the browser quaternion against the W3C matrix, missing sensor fields, concurrent user-gesture permission requests, WSS enforcement, pairing/calibration snapshots and acknowledgements, stale samples/backpressure, RTT, screen changes, hidden-page disconnects and permission denial. Browser behavior tests use a mocked DOM/WebSocket/sensor harness; they are not physical Safari/Chrome tests.
- PWA JavaScript syntax checked. Static files include PNG icons, a relative-scope manifest and service worker. The separate ZIP includes complete source and tests; there are no runtime npm dependencies.

Initial sandboxed Unity startup could not reach the desktop licensing client. Running the isolated tests with desktop access resolved this; the final test suite passed.

**Not yet verified:** actual HTTPS deployment, public tunnel/WSS operation, physical iPhone/Android permission and sensor behavior, the generated scene's visual appearance in the original URP project, a Windows standalone build, installed-PWA behavior, or motion-to-display latency. Follow `PHONE_CUBE_SETUP.md` to complete these checks. The physical prototype success condition remains open until that manual test succeeds.
