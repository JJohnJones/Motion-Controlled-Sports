# Tennis venue and feedback

Launch Tennis through Bootstrap / Game Select. No scene rebuild or Inspector wiring is needed. TennisGameSession initializes TennisPresentation after creating the players, and all new presentation objects belong to that game scene.

The cosmetic venue adds courtside benches, paths, fencing/windscreen detail, park planting, light fixtures, a Phone Sports sign and an in-world score/point display. Existing court lines, camera, net/contact model, automatic player movement and scoring are unchanged. New scenery colliders are disabled immediately and removed; TennisCourt remains the source of legal boundaries.

Feedback includes synthesized racket, bounce, net, toss, point and fault sounds; pooled contact rings; a ball ground shadow; a refined short trail; small ball-contact pulses; and cosmetic avatar lean/step motion. AI rackets have a short hit reaction. Human racket transforms are not animated by the presentation system and remain driven by phone orientation. Player roots and contact positions never move due to these effects.

Sound uses the existing saved Sound effects slider. The runtime TennisPresentation component also exposes effectsVolume, soundEnabled and contactRings. Clips are original synthesized placeholders and can later be replaced with recorded tennis Foley in TennisSoundBank.

Pause freezes effects and pauses audio. Point resets clear the trail to avoid a line across the court. Replay creates fresh presentation objects; scene unload releases runtime sound clips and materials. No PWA changes are needed.

Architecture: TennisVenue builds cosmetic geometry/materials; TennisSoundBank owns clips; TennisPresentation observes TennisGameSession.Feedback events. The game decides contact, point and fault outcomes before presentation reacts. Presentation never feeds into scoring, sensors, networking or ball trajectories.

Automated checks cover 1–4 player entry, toss/hit feedback, collision-free new scenery, pause, replay, results and existing scoring/contact regressions. Visual and audio quality still need review in the running game; visual checks were intentionally skipped.

## Proportions, rackets and motion

Players are now 85% of their previous size, including cosmetic details and hand anchors. The racket pivots at the grip and has an oval rim, open throat, strings and grip bands; head geometry extends upward from the hand. Phone orientation and shot face conversion remain unchanged.

Automatic movement now accelerates and brakes rather than snapping immediately to full speed. Short trajectory prediction helps lateral interception; players retain their home depth until the ball reaches their side, respect court-side bounds and clear movement velocity on point resets. Tune acceleration, braking and predictionSeconds in TennisMovementSettings.

TennisFlight adds speed-dependent drag, decaying spin, trapezoidal position integration and energy-losing ground rebounds. Restitution and surface retention are tunable on the simulation. It remains an arcade ballistic model with swept net/ground detection and forgiving contact regions, not a full aerodynamics/racket-string simulation. Visual checks remain skipped; assess feel with the phone before further tuning.

### Independent body/racket sizing and deliberate returns

The body/head presentation group now has an additional 0.78 scale, giving 0.663 overall relative to the original body. Rackets use a separate 1.18 multiplier, restoring approximately their unscaled design size. Tests assert the final world scales so reparenting cannot silently undo these choices.

Human swings must satisfy a minimum 12-degree arc and directional consistency as well as the existing speed/duration checks. Alternating shake does not initiate a swing; a resting sample closes an unused contact window immediately. Each hit still consumes its swing, and missing controller input resets the detector. AI returns retain their separate AI behavior.
