# Sword Duel presentation and tuning

Sword Duel now creates a contained courtyard with walls, banners, spectator steps, helmet/boot details and a camera behind Player 1. No scene regeneration or PWA deployment is required. Scenery colliders are disabled immediately; combat still uses the existing swept blade tests, health, round rules and controller sessions.

`SwordDuelAtmosphere` owns scene-local scenery, camera tracking, pooled contact bursts and six audio voices. It follows after fighter updates; no other Sword script moves the camera. Defaults: 4.4 m behind Player 1, 2.65 m high, 0.95 m shoulder offset, 52 degree FOV. This is one shared view for both players, anchored to Player 1. Adjust its public camera fields in Play Mode to try other framing; edit defaults or attach/configure the component for lasting tuning. Impact shake is very small and can be set to zero.

Original synthesized placeholders distinguish swing, hit, block, parry, fight and round victory. They use the existing Sound effects volume setting, pause with the game and are disposed on unload. Recorded Foley can replace synthesis later. No external asset licenses are required.

Guard rotation now integrates quaternion deltas instead of clamping the shortest absolute rotation at 100 degrees (which flipped around 180 degrees). Ready-pose capture and calibration still establish neutral; transient attack recovery does not rebase orientation. The legacy rotationLimit field remains for serialized compatibility but is no longer applied. Blocked swings recover from the actual contact tip instead of teleporting to the completed slash. Attack arc, cooldown, directional guard, parry timing and damage rules remain intact.

Verification: run EditMode tests including SwordTests and ApplicationShellTests. These cover half-turn/sign continuity, contact recovery, attack classification/anti-shake, directional defense, AI, rounds, 1/2-player flow, behind-player camera framing, scenery isolation and replay. Visual checks are intentionally skipped at the user's request; sounds have not been auditioned. Play-test guard readability and camera comfort with the phone before tuning damage or timings further.

## Inward-facing upright grip

Keep initial calibration flat, screen up, top forward. For Sword Duel then lift the phone upright, top up, with the screen toward the opposite side of your body: screen left in the right hand, screen right in the left hand. Set handedness in the existing player Settings and tap Ready in that grip each round.

SwordMotion conjugates the captured ready-relative rotation by a +90 degree Y grip basis for right-handed play (-90 for left-handed). Both rotation and angular velocity use the same physical basis. Gyro samples use their own historical orientation relative to the captured absolute ready pose, rather than the latest sensitivity-adjusted visual pose. Wrist twist about the handle does not become a sideways slash. Controller calibration, Tennis/Bowling mapping and networking are unchanged. PWA v13 updates the grip instructions only; deploy that repository separately to show the new wording on phones.
