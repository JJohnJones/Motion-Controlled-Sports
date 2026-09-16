# Arcade Tennis prototype

## Run

Open `Assets/Scenes/Bootstrap.unity`, connect and calibrate 1–4 phones, then choose Tennis. `Assets/Scenes/Games/Tennis.unity` and its enabled GameDefinition are included; no manual scene setup is needed. Deploy the changed files from the separate Motion Controller Website repository to the existing HTTPS site. Close/reopen installed PWA windows after deployment for cache v8-tennis. Signaling configuration is unchanged.

Hold the phone upright in portrait, top pointing upward and screen facing you, like a racket handle. Tennis uses this upright grip while preserving your calibrated heading; an existing flat Bowling calibration also works without changing Bowling. Relative calibrated rotation drives the racket. The entire serving phone screen says **TAP TO TOSS / THEN SWING**. Tap, return briefly to a quiet ready pose, then swing while the automatically tossed ball is in the air. A button release never hits the Tennis ball. During rallies, time a deliberate swing as the ball reaches your avatar; do not repeatedly wave continuously. Allow a brief low-speed recovery between swings. Other phones show Waiting until the rally begins; all participants can swing during rallies. Timing and direction need real-device tuning.

The desktop displays teams, games, point scores and serve/rally state in a compact HUD. Pause contains Resume, Restart Game and Return to Game Select. Results and Play Again retain the controller roster and connections.

## Players and rules

- 1 human: P1 vs AI singles.
- 2 humans: P1 vs P2 singles.
- 3 humans: P1/P3 vs P2/AI doubles.
- 4 humans: P1/P3 vs P2/P4 doubles.

The roster is captured by the existing game shell. P labels use persistent controller player numbers. Teams follow roster order, alternating A/B. Avatars move automatically; doubles players cover different halves. Service rotates through slots A1, B1, A2, B2 (A1, B1 for singles), and doubles receivers alternate by service-box side.

On **Tennis Game > Format**, choose One Game, First To Three (default), Short Set (four games, win by two), or Standard Set (six games, win by two). Set formats play a seven-point, win-by-two tiebreak at 4–4 or 6–6. Normal games use 0/15/30/40, deuce and advantage. A first serve must bounce in the diagonally opposite singles-width service box before it can be returned. Two faults award the receiver a point. Rally first bounces use singles/doubles boundaries; out, net and second bounce end the point.

Intentional arcade simplifications: one set only; teams stay on fixed screen sides instead of changing ends. A net serve counts as a fault (no lets). Any rally net interception loses the point immediately rather than simulating a cord dribble. No foot faults, body-hit/racket-touch penalties, line challenges or professional movement AI. Contact regions are forgiving and do not use tracked real-world hand position.

## Input and physics

`TennisSwing` reads unseen samples from each ControllerSession's recent 220 ms history. It requires sustained angular speed, a short active window, cooldown and low-speed rearm. Stale/uncalibrated input and pause/reset cancel swings; old motion does not get replayed after pause. Rotation-rate units remain radians/second. No acceleration integration or absolute phone position is used.

`TennisPlayer` applies calibrated relative orientation in its own facing direction, clamps visual rotation and smooths response. Avatar locomotion is separate from the phone. `TennisShots` combines swing speed, face angle, signed swing direction, contact timing and a small incoming-velocity component to produce a ballistic shot. Vertical/wrist motion produces bounded topspin or slice. This is an assisted arcade trajectory model, not a full aerodynamic simulation; early/low contacts can hit the net and angle/spin/timing can send balls out.

`TennisFlight` integrates gravity, modest spin, ground restitution and friction at fixed steps. Net/ground crossings and racket contact use swept segments. Visual racket colliders are not the source of impact, preventing tunneling. Explicit court bounds decide in/out. `TennisMatch` is pure C# scoring/service state. `TennisGameSession` coordinates these systems through IGameSession, IGameCompletion and IGameControllerFeedback; it imports no WebRTC types.

## Inspector tuning

Select **Tennis Game** in the Tennis scene:

- Swing: minimum angular speed 2.8 rad/s, sustained 35 ms, active window 300 ms, cooldown 400 ms; sensitivity, orientation sensitivity, max visual rotation and response speed.
- Shots: minimum/maximum pace, maximum swing speed, face influence and spin sensitivity.
- Movement: avatar speed, contact radius, AI miss probability and AI swing speed.
- Diagnostics: logs contact face angles, speed, direction, timing, resulting velocity/spin and point reasons. Disabled by default.

Court dimensions and physics event geometry live in TennisCourt. Visual court generation lives in `Editor/CreateTennisScene.cs`; **Tools > Motion Sports > Create or Open Tennis** opens an existing scene without replacing it. The contained ground, court lines, net, walls and shared camera are placeholders for later art.

## Phone feedback

The existing ui-mode message is extended with an optional per-controller `state`: `serve`, `rally`, or `waiting`. The PWA layout registry defines these labels and whether its primary touch action is enabled. `SceneFlowManager` asks the optional game feedback interface; ControllerLobbyAdapter forwards it to the existing protocol router. The router caches/replays each controller's state on join/recovery. Tennis does not know about signaling or PeerConnections. Motion continues in every ready gameplay state.

## Acceptance testing

1. Test each of 1,2,3,4 humans; verify teams/AI and phone serve labels.
2. Tap to toss, swing during toss, then rally. Compare slow/fast swings, left/right face angles, early/late contacts and wrist spin. Test both court sides.
3. Miss two tosses or serve out twice; verify a double fault. Verify out, net and double bounce points, deuce/advantage and alternating service games.
4. Pause while serving and rallying; ball/input must stop. Resume, finish, replay and return to Bowling without rescanning.
5. Disconnect/recover a phone and confirm the existing global pause/recovery and restored phone state.

Automated tests cover rules, service order, formats, bounds, swing rearming, shot variation, swept events, per-phone UI state, all roster sizes, toss/swing, pause and results/replay. Physical iPhone/Android latency, motion feel and visual readability still require real-device testing. Visual checks were skipped as previously requested.

Rules reference: https://www.itftennis.com/en/about-us/governance/rules-and-regulations/
