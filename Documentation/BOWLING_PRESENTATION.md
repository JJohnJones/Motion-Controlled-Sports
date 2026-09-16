# Bowling alley and feedback

Start Bowling through Bootstrap / Game Select. No scene rebuild or manual setup is required: BowlingGameSession initializes its scene-owned BowlingPresentation when participants are assigned.

The indoor environment includes a carpeted floor, approach, maple-style lane boards, neighboring decorative lanes/pins, seating/tables, side/back walls, ceiling fixtures, lighting, pinsetter fascia and Phone Sports signage. The existing gameplay camera is preserved. Cosmetic geometry has disabled/removed colliders: the original lane, gutters, ball/pin physics, cleanup bounds and ten-frame rules remain authoritative.

Presentation includes a short ball trail, speed-dependent rolling sound, bounded/rate-limited pin impact voices and flashes, strike/spare audio flourishes and backboard messages, and a visual pinsetter sweep during turn transitions. The sweep is not a physical pinsetter and never changes standing pins. Pause stops sounds and presentation animations; replay creates a fresh scene-owned presentation. Clips/materials are released on scene unload.

Sound effects volume is saved in Settings alongside menu music. The scene's runtime BowlingPresentation component additionally exposes effectsVolume, soundEnabled and impactFlashes. Sounds are original synthesized placeholders, not recorded bowling Foley. Replace the sound bank later for more realism.

Implementation boundaries:
- BowlingAlley: environment geometry/materials (replaceable with authored art).
- BowlingSoundBank: generated mono sound clips.
- BowlingPinFeedback: collision feedback notification only.
- BowlingPresentation: pooled feedback, audio and cosmetic animation.
- BowlingGameSession.RollResolved: score-authoritative announcement event; the presentation does not decide scores.

Visual and audio review in the real game is still needed; automated verification covers game flow and physics, not aesthetic quality.
