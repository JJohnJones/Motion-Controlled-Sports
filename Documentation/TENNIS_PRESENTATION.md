# Tennis venue and feedback

Launch Tennis through Bootstrap / Game Select. No scene rebuild or Inspector wiring is needed. TennisGameSession initializes TennisPresentation after creating the players, and all new presentation objects belong to that game scene.

The cosmetic venue adds courtside benches, paths, fencing/windscreen detail, park planting, light fixtures, a Phone Sports sign and an in-world score/point display. Existing court lines, camera, net/contact model, automatic player movement and scoring are unchanged. New scenery colliders are disabled immediately and removed; TennisCourt remains the source of legal boundaries.

Feedback includes synthesized racket, bounce, net, toss, point and fault sounds; pooled contact rings; a ball ground shadow; a refined short trail; small ball-contact pulses; and cosmetic avatar lean/step motion. AI rackets have a short hit reaction. Human racket transforms are not animated by the presentation system and remain driven by phone orientation. Player roots and contact positions never move due to these effects.

Sound uses the existing saved Sound effects slider. The runtime TennisPresentation component also exposes effectsVolume, soundEnabled and contactRings. Clips are original synthesized placeholders and can later be replaced with recorded tennis Foley in TennisSoundBank.

Pause freezes effects and pauses audio. Point resets clear the trail to avoid a line across the court. Replay creates fresh presentation objects; scene unload releases runtime sound clips and materials. No PWA changes are needed.

Architecture: TennisVenue builds cosmetic geometry/materials; TennisSoundBank owns clips; TennisPresentation observes TennisGameSession.Feedback events. The game decides contact, point and fault outcomes before presentation reacts. Presentation never feeds into scoring, sensors, networking or ball trajectories.

Automated checks cover 1–4 player entry, toss/hit feedback, collision-free new scenery, pause, replay, results and existing scoring/contact regressions. Visual and audio quality still need review in the running game; visual checks were intentionally skipped.
