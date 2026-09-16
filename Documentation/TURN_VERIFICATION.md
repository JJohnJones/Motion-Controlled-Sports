# TURN migration and Bowling camera verification — 2026-09-14

Completed locally without visual/screenshot checks:

- 31 Node tests passed across the separate signaling and PWA repositories: provider credential requests/cache/limits, authentication, relay diagnostics, issuance failure fallback, protocol/input behavior, selected-pair stats and recovery.
- 43 Unity EditMode/UI/scene-flow tests passed, with the optional external browser test skipped in that final standalone regression run.
- The optional real headless Chrome → native Unity DataChannel test passed separately on the final networking implementation: pairing, motion, calibration, release snapshots, RTT, persistence through scene changes, signaling resume, ICE restart and DataChannel recreation with retained identity/calibration. Selected pair was direct host/host UDP.
- Camera regression checks resolved gameplay shell alpha = 0, hidden menu sky, correct menu/game camera ownership, active scene, MainCamera lookup, display/culling mask, and projected release-area/lane/pin framing between HUD and header. Results restore the menu background and camera.
- Repository whitespace checks passed. Removed legacy receiver GUID/config fields have no remaining scene/prefab/source references.

The existing test runner's default camera was isolated in the shell test fixture; production Bootstrap has only its configured menu camera. The blue-screen cause was an opaque Aero `.shell` style overriding the earlier `.playing` style. Gameplay-specific selector specificity now prevents this.

Not established by these tests: deployed Cloudflare credentials, live relay-only connectivity, actual iPhone/Android behavior on university Wi-Fi, more-than-ten-minute real TURN allocation renewal, or standalone IL2CPP. Cloudflare API behavior is tested with a controlled provider response, not a real account token. Configure the existing Render service and deploy both companion repositories using Motion Controller Signaling/TURN_SETUP.md, then run its acceptance checklist.

External verification artifacts are in the local Codex Verification folder: final-camera-tests.xml (43 pass, 1 optional skip), turn-camera-tests.xml (real network integration pass; earlier camera fixture failure superseded by final-camera-tests.xml), and turn-native-tests.xml (earlier all-44 pass before the additional camera assertions). No QR/tickets are copied into this document.
