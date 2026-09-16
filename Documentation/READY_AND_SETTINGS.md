# Get Ready, settings, and Tennis orientation

Open Bootstrap. Choose a sport to enter Get Ready before the gameplay scene loads.

1. Enable motion on each participating phone.
2. Calibrate with the phone lying flat, screen face up, top pointing forward toward the TV/monitor. Keep it still when tapping Calibrate.
3. Pick it up: Bowling uses a top-forward Wii-style grip; Tennis and Sword Duel use a top-up, upright racket/sword-handle grip. Do not recalibrate in the upright grip. Sword's in-arena Ready tap captures its comfortable guard offset separately from global calibration.
4. Tap Ready on each phone, then Start game on the desktop. Recalibration or a disconnect clears that player's readiness. Extra controllers beyond a game's player limit wait out the game.

Already-calibrated phones may reuse their calibration. Calibrate again is available on the pre-game phone screen. Restart / Play Again preserve the previous roster and existing calibration. Ready uses the existing generic primary-button release; WebRTC, pairing, calibration packets and motion packets are unchanged.

Settings are available from Title, Game Select and Pause. Menu music volume, fullscreen/windowed mode, and per-player swing sensitivity and playing hand are saved through PlayerPrefs. Display mode affects desktop builds, not the Editor window. Sensitivity affects swing strength/detection, not one-to-one orientation. Handedness changes Tennis/Sword's weapon side; motion directions are not reversed. Bowling accepts physical left- or right-hand swings without mirroring aim. Player preferences are tied to local player number on this PC, not phone identity.

Tennis no longer clamps the absolute shortest quaternion angle to 100 degrees. That clamp could reverse its target near 180 degrees. The racket now integrates small orientation deltas, smooths toward the continuous target, and limits sudden visual angular speed. Stale packets leave the racket still. Calibration/recovery rebase the tracking state. This is covered by sign-flip and half-turn regression tests; actual phone feel still needs playtesting.

Deploy the updated separate PWA repository (controller v12), then close old controller tabs and reopen the QR link so the service worker can activate the new shell. The alpha/beta/gamma and screen-angle overlay is on by default for testing, is touch-transparent, and can be switched off from Calibrate / Setup. Full network diagnostics remain separate.

No manual scene or Inspector setup is required. Visual checks were intentionally skipped.
