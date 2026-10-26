# Vehicle explosion

## Purpose

Add a one-shot, physics-driven breakup of the player car for the driving
prototype. Pressing E replaces the intact car with independently moving rigid
pieces whose initial motion comes from the car's current motion plus an
outward explosion impulse.

## Player-visible behavior

- Pressing Space while the car is intact immediately breaks it into six body
  panels, four wheels, an engine block, two bumpers, and six miscellaneous car
  fragments.
- Every piece continues the velocity it had at its location on the moving and
  rotating car, then receives an outward impulse and spin.
- Pieces move and rotate independently under Box2D. The intact vehicle is no
  longer drawn or controllable after detonation.
- After detonation, WASD and arrow keys pan the camera around the playable map
  at 28 metres per second instead of attempting to control the destroyed car.
- Pressing Space again while exploded has no effect.
- Pressing R removes every explosion piece and restores the intact car at its
  initial pose and simulation state.
- The HUD identifies Space as the explode control and reports the exploded state.
- Detonation produces a bright central flash and fireball, an expanding
  concussion ring that reaches the 32-metre world blast radius, radial debris
  streaks, numerous smoke plumes distributed through the blast area, and a large
  dense ground-level smoke plume centered on the blast point.
- The explosion's close-range deadly zone is 16 metres; its physical impact
  radius remains 32 metres. People and buildings in the outer band are pushed
  or cracked respectively without being killed or destroyed.

## Functional requirements

- Detonation is edge-triggered from the Space key and occurs outside the fixed-step
  input record, like reset.
- Fragment bodies use metres, kilograms, seconds, newtons, and newton-seconds
  at the Box2D boundary.
- Breakup layout and impulse magnitudes are deterministic; no random-number
  source participates in simulation.
- Explosion pieces are owned and cleaned up with the vehicle so reset does not
  leak Box2D bodies.
- Normal vehicle force generation stops after detonation.
- Camera navigation is clamped to the playable world boundary and resets to
  normal vehicle-follow behavior when the vehicle is restored.

## Technical constraints and tuning

- The 19 pieces divide the 1,300 kg First Car into six rectangular body
  sections, four wheels, one engine block, two bumpers, and six miscellaneous
  fragments with a combined mass equal to the intact chassis mass.
- Each piece begins at a local offset transformed by the chassis pose.
- Initial linear velocity is the intact center-of-mass velocity plus the
  tangential velocity from intact angular velocity.
- A baseline radial impulse of 18 N s per kilogram produces at least 18 m/s of
  initial outward speed before each piece's tangential component. Per-part
  multipliers for the engine, bumpers, and small fragments raise the total
  applied radial impulse to about 32,700 N s. The blast is intentionally much
  stronger than ordinary driving speed so every side of the car visibly erupts
  away from the center. A small, fixed tangential component prevents a perfectly
  symmetric burst, and each piece receives deterministic angular velocity.
- The engine block begins near the front-center of the chassis and receives a
  2.5-times impulse multiplier, launching it forward at 45 m/s from a stationary
  car. Bumpers and smaller mechanical fragments use fixed layouts and distinct
  sizes, colors, impulse multipliers, and spins so the breakup reads as car
  wreckage rather than ten uniform rectangles.
- Because the simulation is top-down with zero vertical gravity, debris applies
  an explicit Coulomb-like ground deceleration of 0.75 g (7.36 m/s squared)
  every fixed step. Mild Box2D damping supplements that surface resistance, so
  pieces burst violently and then lose speed on the ground instead of coasting
  like objects in space.
- Rendering uses code-drawn rectangles and circles; no generated asset is
  required.
- The flash lasts 0.25 seconds, glowing debris lasts up to 3 seconds, and the
  concussion ring lasts 0.9 seconds. Fire begins as a concentrated fireball.
  A separate set of smaller, dimmer animated ground fires is scattered through
  the broad blast footprint and gradually fades away over 12 seconds. Varied-size,
  filled smoke puffs repeatedly emerge throughout that footprint, expand, and
  trail outward before fading as new puffs replace them. The dense smoke at
  ground zero uses four large, filled concentric layers that gently pulse and
  expand from the crater without forming a many-particle geometric pattern. It
  gradually fades away over 12 seconds.
  Particle placement is deterministic and effect time advances on the fixed
  simulation timestep.

## Acceptance criteria

- A detonation request transitions an intact vehicle to exploded exactly once.
- Nineteen dynamic fragment bodies replace the intact rendering and have finite
  poses and velocities after simulation steps.
- Exactly one engine block launches in the car's forward direction at no less
  than 44 m/s from rest, and the additional bumpers and miscellaneous fragments
  are visibly distinct from panels and wheels.
- Explosion motion is reproducible from the same initial vehicle state.
- A moving or rotating car transfers its pre-explosion motion to the pieces.
- At least one piece moves away from the blast center in every cardinal local
  direction.
- From rest, every piece initially has at least 18 m/s of radial outward
  velocity and subsequently loses speed from ground resistance.
- Controls no longer accelerate or steer the car after detonation.
- WASD and arrow keys move the camera after detonation, and cannot pan its
  target outside the playable world boundary.
- Reset destroys the fragments, restores the intact car, and permits another
  detonation.
- The concussion ring visibly expands from the blast center, debris travels
  radially, and at least 20 smoke plumes remain after the initial fire fades.
- Four large, filled smoke layers cover ground zero immediately after
  detonation, remain centered on the crater while gently pulsing and expanding,
  and completely fade after 12 seconds.
- At least 20 animated ground fires remain visible after the initial 3-second
  blast phase, distributed through most of the blast area rather than clustered
  at its center.
- The scattered aftermath contains multiple distinct fire sizes and multiple
  distinct filled smoke-puff sizes. The fires completely fade after 12 seconds,
  while the repeating outward smoke trails remain present after at least 60
  simulated seconds and are removed by reset.
- `dotnet test CarGame.sln`, `dotnet build CarGame.sln`, and
  `dotnet format CarGame.sln --verify-no-changes` pass.

## Manual verification

1. From rest, press Space and confirm 19 visible pieces burst outward while
   rotating independently; the dark engine block should shoot strongly forward.
2. Drive and turn, then press Space; confirm the debris retains the car's travel
   and rotational motion while spreading outward.
3. Hold driving controls after detonation and confirm they do not propel the
   debris.
4. Press Space repeatedly and confirm no extra debris appears.
5. Use WASD and the arrow keys after detonation; confirm the camera can inspect
   the map while the debris remains unaffected.
6. Confirm the flash, expanding concussion ring, and debris streaks fade while
   varied-size filled smoke puffs expand and trail outward across the blast area.
7. Confirm ground-zero smoke reads as a few large, layered, filled pulses rather
   than a patterned cluster of small circles. Confirm it and the scattered fires
   fade away within about 12 seconds, then wait at least one minute and confirm
   the repeating smoke trails remain visible without obscuring the complete play
   area.
8. Press R, confirm all effects and debris disappear and the intact stationary car returns,
   then press Space once more to confirm the feature resets.

## Non-goals

- Sound, damage accumulation, collision-triggered destruction,
  fragment-to-fragment joints, or secondary explosions.
- Three-dimensional ballistics, vertical gravity, terrain interaction, injury,
  or manufacturer-accurate crash deformation.
- Randomized debris, persistent wreckage, repair gameplay, or camera shake.

## Open decisions

None for this increment. The breakup count, deterministic tuning, and reset
behavior above define the initial prototype and can be retuned in a later spec.
