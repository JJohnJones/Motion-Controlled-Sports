# Sword Duel prototype

## Start

Open `Assets/Scenes/Bootstrap.unity`, pair/enable motion/calibrate your phones, and choose Sword Duel. One participant faces AI; two face each other. The first two connected lobby slots participate if more controllers are present; others wait. Deploy the separate PWA repository update (cache v11-sword), close old phone tabs and reopen. No signaling changes are required.

Before **every round**, hold the phone upright in your comfortable sword-ready position and tap its large ready surface. That captures a Sword-only neutral offset from the existing calibrated controller pose. It does not change Bowling/Tennis calibration. Both humans must ready up; the AI readies automatically. The countdown then starts combat.

Use deliberate slashes and return briefly to a quiet guard between attacks. Left/right phone tilt swings classify horizontal cuts; forward/back pitch swings classify vertical cuts. Hold the blade upright to intercept horizontal slashes; turn it sideways to intercept vertical cuts. An appropriate blade angle blocks without a touch button. Moving into that guard toward the incoming strike during the final 120 ms can parry. Move defensively with control: random continuous shaking cannot rearm attacks.

The phone shows Ready, Guard, Attack/Recovery, Staggered or Waiting. Combat is motion-only. Pause remains on the desktop with Resume, Restart Game and Return to Game Select. Round resets and replay retain the controller identities; a new ready tap captures each next round's stance.

## Format and feedback

Default: 100 health, first to two round wins. Hits deal 18–28 damage depending on bounded swing strength. Correct blocks and parries prevent normal damage; blocks cause small recoil, and parries stagger the attacker for .85 s. Normal attack recovery is .55 s. A round ends immediately on zero health; there is a short round-result pause before resetting and readying again. Final results show winner and round wins, with Play Again and Game Select.

Blue/orange fighters, sword trails, yellow block flashes, cyan parry flashes, red hit flashes, recoil and short status text provide feedback. No sound assets or camera shake were added. The shared angled camera follows only small changes in the duel center.

## Tuning

Select **Sword Duel Game** in `Assets/Scenes/Games/SwordDuel.unity`:

- Motion: minimum speed 3 rad/s, minimum arc 22 degrees, minimum sustained direction 55 ms, direction consistency .75, maximum speed 13 rad/s; orientation sensitivity, rotation limit and response. `Left Handed` mirrors attack classification for the setup; independent per-player handedness is an extension point.
- Combat: windup .28 s, active sweep .24 s, recovery .55 s, parry window .12 s, parry angular speed 1.6 rad/s, opposition .3, guard tolerance 28 degrees, damage and reach.
- AI: reaction time, aggression, attack interval, variation, block probability, parry accuracy and bounded swing strength. It reacts to visible attack state rather than hidden phone input; lower probabilities introduce mistakes. Its default upright guard can naturally intercept horizontal cuts even when it chooses not to react.
- Rounds To Win and Maximum Health control match length.
- Diagnostics logs attack pose/direction/peak/strength, contact result, guard direction, parry timing offset, health and misses. Disabled by default.

## Boundaries and simplifications

`SwordMotion` consumes recent ControllerSession history. It ignores stale or uncalibrated samples, requires a coherent arc, and only rearms after a low-speed interval outside recovery. `SwordCombat` resolves one result in parry > block > hit order. `SwordRounds` is a plain health/round model independent of Unity objects. `SwordFighter` owns transient fighter state; `SwordAI` is a small timed policy; `SwordPresentation` owns placeholder visuals; `SwordDuelSession` coordinates them via the shared game interfaces.

Guard orientation is live phone rotation relative to the Sword ready pose, with no XYZ integration. Attacks use authored directional sweeps anchored to the fighter, rather than pretending to track a physical hand. A swept tip segment plus intermediate full-blade segments is checked against a forgiving torso region. Guard tests are directional and abstract, not physics-engine blade-on-blade contact. Movement is a bounded lunge/recoil around fixed duel positions; there are no stabs, stamina, dismemberment, rigged limbs, or ragdolls. In an exactly simultaneous contact tie, the lower player slot resolves first. The environment is a simple contained mat with boundaries and pillars.

The module has no WebRTC dependencies. It uses IGameSession, IGameCompletion, IGameControllerFeedback and the new reusable IGameVitals HUD interface. Core transports, pairing, motion/button protocols, and networking lifetimes are unchanged.

## Assets and verification

The included scene and SwordDuel GameDefinition are registered in the build/catalog. No Inspector wiring or scene regeneration is required. `Tools > Motion Sports > Create or Open Sword Duel` recreates a missing scene and registers the definition; it opens an existing scene without replacing it.

Tests cover four directional attacks, noise/shake rejection, rearm, guard and parry direction/timing, swept contact, health/round wins, AI choices, phone layout, actual scene damage/block, pause, results/replay, and one/two-controller mapping. Visual checks remain skipped. Real-phone swing/guard feel, parry difficulty, and display readability still need device playtesting. Try both sides, slow/fast deliberate swings, late guards, pauses mid-attack and reconnecting before shipping.
