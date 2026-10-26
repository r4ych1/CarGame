# Vehicle movement

## Purpose

Provide the first playable driving slice: one top-down car that can accelerate,
reverse, steer, coast, and slide in an open test area using Box2D rigid-body
simulation.

## Player-visible behavior

- The game opens with a car centered in a camera-followed test grid.
- `W` or Up accelerates forward; `S` or Down brakes and reverses. The simulation supports
  a dedicated brake without selecting reverse.
- `A`/`D` or Left/Right steer. Steering has no effect while stationary and
  reverses direction when the car is backing up.
- The front wheels visibly steer and generate turning force at the front axle;
  the rear axle supplies lateral grip, so the car follows a car-like arc instead
  of rotating in place around its center.
- Releasing the throttle lets the car coast to a gradual stop.
- Sideways motion is resisted more strongly than forward motion, while abrupt
  turns at speed retain a small, visible drift. The default surface should feel
  like dry asphalt rather than ice: the car quickly regains grip after a slide.
- `R` resets the car at the origin, facing upward and at rest.
- The HUD shows controls and approximate speed in kilometres per hour.

## Functional requirements

- Simulate the vehicle as one dynamic Box2D body in a zero-gravity world.
- Advance Box2D at a fixed 60 Hz with four solver substeps, independent of the
  rendering frame rate.
- Model front-axle engine force, braking/reverse force, speed-sensitive front
  steering, separate front/rear lateral tire grip, rolling resistance,
  aerodynamic drag, and angular damping explicitly.
- Keep player input, vehicle physics, world ownership, and drawing in separate
  cohesive types.
- Define the pixels-per-metre conversion once and use `System.Numerics.Vector2`
  at the rendering/application boundary.
- Cap accumulated frame time so a paused or dragged window cannot trigger an
  unbounded physics catch-up.

## Acceptance criteria

- Holding forward from rest moves the car in the direction its nose points.
- The car cannot turn in place.
- At speed, left and right steering rotate the car in the expected direction.
- Turning force is applied at the axles; steering does not directly torque the
  body around its center.
- Reverse motion changes the steering direction.
- Releasing all controls reduces both longitudinal and lateral speed over time.
- Sideways velocity decays faster than longitudinal velocity.
- On the default surface, ordinary steering remains planted; noticeable drift
  requires an abrupt input at higher speed.
- Reset restores the origin, upward heading, and zero linear/angular velocity.
- The solution builds without warnings or errors.

## Non-goals

- Tracks, walls, collision damage, AI cars, lap timing, audio, particles, and
  generated vehicle artwork.
- Per-wheel bodies, suspension, drivetrain gears, or a realistic tire model.
- Final handling balance, controller support, menus, or saved settings.

## Technical constraints and tuning units

- Physics dimensions and positions are metres; rendering positions are pixels.
- Forces are newtons, torque is newton-metres, speeds are metres per second,
  and angular rates are radians per second.
- Road friction is represented by explicit axle tire forces, rolling resistance,
  and drag rather than Box2D contact friction because the car has no physical
  ground fixture in the top-down simulation.
- The car body is 1.8 m wide and 4.0 m long. Related tuning constants live
  together in a vehicle settings type.
- The 2.7 m wheelbase is represented by front and rear axle contact points on
  the single rigid body. This is an arcade bicycle-model approximation rather
  than four independently simulated wheels.
- The source/procedural car faces upward at zero rotation.

## Manual verification

1. Run `dotnet run --project src/CarGame/CarGame.csproj`.
2. Hold forward and confirm the car accelerates upward while the camera follows.
3. Steer both directions at low and high speed; confirm the nose is pulled into
   an arc by the front axle rather than the body spinning around its center, and
   that the rear can slide slightly during abrupt high-speed turns.
4. Reverse and confirm steering direction is inverted relative to forward travel.
5. Release the controls and confirm the car coasts to rest without sustained
   sideways sliding.
6. Press `R` after driving away and confirm position, heading, and speed reset.

## Open decisions

- Final acceleration, maximum speed, grip, and steering response remain tuning
  decisions after hands-on evaluation of this first implementation.
- A generated car sprite will be selected through the asset workflow in a later
  feature; this slice uses procedural drawing.
