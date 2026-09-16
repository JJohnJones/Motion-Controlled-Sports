# Local multiplayer bowling

Open **Assets/Scenes/Bootstrap.unity**, enter Play, pair and calibrate 1–4 phones, then choose Bowling. No PWA or signaling deployment is needed for this phase. Bowling's existing GameDefinition now allows four players. The saved game scene already contains BowlingGameSession; no scene regeneration or new manual components are required.

## Match flow

Connected player slots are captured when Play is pressed, in player-number order, up to four. Their existing controller IDs remain the roster for the match. Only the active player's input is accepted. A new phone joining mid-match waits for the next newly selected game; Play Again preserves the original roster instead of silently replacing players.

Each player completes their frame before the next player. Frames 1–9 finish after a strike or two balls. In frame ten, an open frame finishes after two balls, a spare earns one fresh-rack bonus ball, and a strike earns two bonus balls. After a tenth-frame strike followed by fewer than ten, the last ball uses the remaining pins. After another strike, it uses a fresh rack.

Scoring is standard ten-pin: strike = ten plus the next two deliveries; spare = ten plus the next delivery; open frame = pinfall; tenth frame = its two/three deliveries. Unresolved cumulative frame totals remain blank. Player totals show the latest resolved cumulative score, so a pending strike is not prematurely credited as a final ten. Misses use `-`, strikes `X`, and spares `/`. See [USBC Keeping Score](https://bowl.com/keeping-score).

The game finishes automatically only when all participants complete frame ten and any bonus balls. Results list final scores and competition rankings: ties share a rank, with the following rank skipped (1,1,3). Play Again starts fresh scores/racks at Player 1's first frame with the same roster. Exit to Game Select abandons the unfinished game without inventing final scores. Controller connections persist.

## HUD

The current player/frame/roll banner is always visible. Compact tabs show every player's resolved total; the active player is green and the selected scorecard has a bright rim. Click any player tab to inspect their ten-frame score strip. Turn changes automatically select the new active player. This avoids four vertically stacked scorecards and leaves the lane visible at the existing 16:9 UI scale. There is no score ScrollView.

The PWA retains its current controls. Inactive players may still see Hold Ball, but Unity rejects their input. BowlingGameSession exposes ActivePlayerChanged, Match and Phase for a future phone turn-state presenter. No new WebRTC/session protocol is introduced.

## Physics and Inspector tuning

BowlingThrowController still interprets alpha aim, forward acceleration/increasing beta, hold/release timing and wrist spin. In a match it delegates roll completion and rack resets; its standalone cube/bowling input-test behavior remains available in the older prototype scene.

BowlingGameSession → **Resolution**:

- Quiet Seconds: 1 second of quiet ball/pin conditions.
- Maximum Seconds: 20 seconds per launched roll, bounding unstable physics.
- Pin Linear Threshold: 0.08 m/s; Pin Angular Threshold: 0.15 rad/s.
- Turn Transition Seconds: 1.5 seconds with throw input locked.

BowlingPinRack:

- Knockdown Angle: 45 degrees relative to the pin's original up axis.
- Fallen Distance: 0.2 m below its original position.

Resolution starts only after a launched throw and does not score during aiming/holding. It waits at least two seconds and for the ball to stop/leave the playable bounds plus quiet pin velocities. On timeout it logs the condition and freezes residual movement before recording final poses. Fallen pins are counted once and removed from play; standing pin positions are retained. Ball-only reset prepares a second roll. Full-rack reset occurs only when the rules require it. Pins that slide but remain upright on the deck remain standing.

All participating controllers are required by the existing global connection-pause policy. A recovering participant pauses gameplay and cancels holds; restoring the same identity resumes it. Terminal loss/new controller IDs may require returning to pairing and starting a new game. No player is silently substituted.

## Responsibilities

- BowlingMatch / BowlingPlayer / BowlingFrame / BowlingScoring: plain C# rules, rack requirements, turn order and delayed bonuses, independent of Unity objects.
- BowlingRollResolution: physics-settling timer and timeout.
- BowlingPinRack: standing state, knockdown collection, physical reset.
- BowlingThrowController: existing motion interpretation and active-controller gate.
- BowlingGameSession: small coordinator connecting the above to IGameSession and optional completion/scoreboard interfaces.
- BowlingScorePresentation: scorecard and ranking projection.
- GameScoreboardView: reusable UI Toolkit score strip; AppShellView owns its placement and existing pause/results navigation.
- SceneFlowManager: captures/preserves the roster, observes game completion and loads results. It does not calculate bowling scores.

## Manual acceptance

1. Pair and calibrate 1–4 phones before selecting Bowling. Verify all participants appear and Player 1 owns frame 1, roll 1.
2. Try another phone's button: it must not hold or release the ball. Try again during settling and turn transition.
3. Knock down part of the rack. Wait for settling; verify those pins disappear, the remaining pins keep their poses and the same player gets roll 2.
4. Make a spare or strike; verify notation and that the cumulative bonus stays blank until the next necessary deliveries. A strike immediately advances the player in frames 1–9.
5. Complete frame ten with an open frame, spare, X 7 /, and consecutive strikes. Check the fresh/remaining rack behavior for bonus balls.
6. Pause during a roll and during a transition, then resume. Verify no premature score or throw; briefly interrupt one participant's connection and recover.
7. Finish every player's tenth frame. Verify automatic rankings, ties, Play Again with fresh scores and unchanged controllers, and return to Game Select without another QR scan.

Automated coverage includes open/spare/strike/consecutive-strike/turkey scoring, 300, all spares, gutters, tenth-frame combinations/invalid rolls, 1–4 independent player orders, ties, active-controller rejection, settling/standing-pin retention, spare-to-next-player integration, four-player shell completion/results/replay and the existing motion/physics regressions. Tests use deterministic poses and input fixtures; actual four-phone throw feel and physical pin classification still need the manual playthrough. Visual checks are intentionally skipped.


## Compact HUD and play-area cleanup

The normal HUD contains compact player score tabs, one selected player's ten-frame strip, a turn/status line and Pause. Click another player's tab to inspect their frames; the active player is highlighted and selected automatically at turn changes. Instructions and Return to Game Select are in Pause, along with Resume and Restart Game. No persistent bottom panel remains.

On BowlingPinRack, **Deck Clearance** expands the captured starting rack bounds in rack-local X/Y/Z (defaults 1.2 / 1 / 2.5 metres on each side). Select the rack in Play Mode to see its cyan bounds gizmo. Configure clearance before Play. Pins outside these bounds stop simulating and disappear; their knockdown is counted once at roll resolution. Tilted pins still inside play remain physical until settled, so they can hit other pins. Upright surviving pins retain their current positions for a second roll; a full rack reset restores removed pins.

BowlingGameSession > Resolution now exposes **Ball Play Bounds** (default world center 0,2,10; size 6,6,24) and optional **Play Area Reference**. Set the reference to the lane root when placing a lane elsewhere; bounds then use that transform's local coordinates. The default contains the existing lane and ends beyond the deck. A ball leaving it is hidden and made kinematic until reset. This is a gameplay volume, independent of a void or a future alley floor/back wall.

Relevant pins must stay below linear/angular thresholds for Quiet Seconds (default 1), after the ball is out of play or below its own linear/angular thresholds. The initial two-second roll guard and 20-second timeout remain. Off-deck bodies no longer force the timeout. No scene regeneration is required for the current lane.

Manual acceptance: bowl pins over the edge and verify prompt resolution after remaining pins stop, then make a second roll and verify only standing pins remain. Verify rack restoration after a strike/frame, all player tabs, and Pause > Return to Game Select. Automated tests cover escaped-pin accounting, translated rack bounds, ball cleanup/reset and existing scoring/turn rules. Visual checks remain skipped.
