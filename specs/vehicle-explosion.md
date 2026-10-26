# Vehicle explosion

## Purpose

Add a one-shot, physics-driven breakup of the player car for the driving
prototype. Pressing E replaces the intact car with independently moving rigid
pieces whose initial motion comes from the car's current motion plus an
outward explosion impulse.

## Player-visible behavior

- Pressing Space while the car is intact immediately breaks it into six body
  panels and four wheels.
- Every piece continues the velocity it had at its location on the moving and
  rotating car, then receives an outward impulse and spin.
- Pieces move and rotate independently under Box2D. The intact vehicle is no
  longer drawn or controllable after detonation.
- Pressing Space again while exploded has no effect.
- Pressing R removes every explosion piece and restores the intact car at its
  initial pose and simulation state.
- The HUD identifies Space as the explode control and reports the exploded state.
- Detonation produces a bright central flash and fireball, an expanding
  concussion ring that reaches the 35-metre world blast radius, radial debris
  streaks, and numerous smoke plumes distributed through the blast area.

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

## Technical constraints and tuning

- The ten pieces divide the 1,300 kg First Car into six rectangular body
  sections and four wheel sections with a combined mass equal to the intact
  chassis mass.
- Each piece begins at a local offset transformed by the chassis pose.
- Initial linear velocity is the intact center-of-mass velocity plus the
  tangential velocity from intact angular velocity.
- A radial impulse of 23,400 N s total is distributed by piece mass, producing
  18 m/s of initial outward speed before each piece's tangential component. The
  blast is intentionally much stronger than ordinary driving speed so every
  side of the car visibly erupts away from the center. A small, fixed tangential
  component prevents a perfectly symmetric burst, and each piece receives
  deterministic angular velocity.
- Because the simulation is top-down with zero vertical gravity, debris applies
  an explicit Coulomb-like ground deceleration of 0.75 g (7.36 m/s squared)
  every fixed step. Mild Box2D damping supplements that surface resistance, so
  pieces burst violently and then lose speed on the ground instead of coasting
  like objects in space.
- Rendering uses code-drawn rectangles and circles; no generated asset is
  required.
- The flash lasts 0.25 seconds, glowing debris lasts up to 3 seconds, the
  concussion ring lasts 0.9 seconds, and smoke fades over 12 seconds. Fire
  remains a concentrated initial fireball. A separate set of smaller, dimmer
  animated ground fires is scattered through the broad smoke footprint and
  fades during the final 3 seconds.
  Particle placement is deterministic and effect time advances on the fixed
  simulation timestep.

## Acceptance criteria

- A detonation request transitions an intact vehicle to exploded exactly once.
- Ten dynamic fragment bodies replace the intact rendering and have finite
  poses and velocities after simulation steps.
- Explosion motion is reproducible from the same initial vehicle state.
- A moving or rotating car transfers its pre-explosion motion to the pieces.
- At least one piece moves away from the blast center in every cardinal local
  direction.
- From rest, every piece initially has at least 18 m/s of radial outward
  velocity and subsequently loses speed from ground resistance.
- Controls no longer accelerate or steer the car after detonation.
- Reset destroys the fragments, restores the intact car, and permits another
  detonation.
- The concussion ring visibly expands from the blast center, debris travels
  radially, and at least 20 smoke plumes remain after the initial fire fades.
- At least 20 animated ground fires remain visible after the initial 3-second
  blast phase, distributed through most of the blast area rather than clustered
  at its center.
- `dotnet test CarGame.sln`, `dotnet build CarGame.sln`, and
  `dotnet format CarGame.sln --verify-no-changes` pass.

## Manual verification

1. From rest, press Space and confirm ten visible pieces burst outward while
   rotating independently.
2. Drive and turn, then press Space; confirm the debris retains the car's travel
   and rotational motion while spreading outward.
3. Hold driving controls after detonation and confirm they do not propel the
   debris.
4. Press Space repeatedly and confirm no extra debris appears.
5. Confirm the flash, expanding ring, debris streaks, fire, and lingering smoke
   are visible without obscuring the complete play area.
6. Press R, confirm all effects and debris disappear and the intact stationary car returns,
   then press Space once more to confirm the feature resets.

## Non-goals

- Fire, smoke, particles, sound, damage accumulation, collision-triggered
  destruction, fragment-to-fragment joints, or secondary explosions.
- Three-dimensional ballistics, vertical gravity, terrain interaction, injury,
  or manufacturer-accurate crash deformation.
- Randomized debris, persistent wreckage, repair gameplay, or camera shake.

## Open decisions

None for this increment. The breakup count, deterministic tuning, and reset
behavior above define the initial prototype and can be retuned in a later spec.
