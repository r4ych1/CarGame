# Four-wheel vehicle dynamics

## Purpose

Replace the axle-level bicycle model with a deterministic four-contact-patch
model while retaining one Box2D chassis. Simulate individual wheel rotation,
nonlinear combined-slip tires, sprung-body load transfer, an automatic geared
front-wheel-drive powertrain with an open differential, and always-enabled ABS.
The feature preserves the existing WASD controls and retains the fixed 60 Hz
update. The service-brake simulation remains available to deterministic
callers, but Space is reserved for the vehicle explosion.

The First Car remains a fictional 1,300 kg front-engine hatchback tuned for a
stable real-time game rather than manufacturer accuracy. Earlier vehicle specs
remain historical foundations; this specification supersedes their axle-level
dynamics behavior.

## Units and conventions

- Distance is metres, time is seconds, mass is kilograms, force is newtons,
  torque is newton-metres, wheel speed is radians per second, engine speed is
  revolutions per minute, and angles are radians internally.
- The chassis nose is local negative Y, local positive X is right, and positive
  steering turns right.
- `WheelCorner` order is front-left, front-right, rear-left, rear-right.
- Suspension compression is positive upward from static equilibrium. Heave is
  positive upward; positive roll compresses the left side and positive pitch
  compresses the front.
- Longitudinal tire force is positive in a wheel's forward direction. Lateral
  tire force opposes lateral slip in the wheel frame.
- The deterministic dynamics layer receives a sampled planar chassis state and
  fixed timestep, and returns four contact forces plus updated simulation state.
  `Vehicle` alone reads and writes Box2D state.

## Definition and state

`VehicleDefinition` is immutable composition data split into validated chassis,
suspension, tire, powertrain, transmission, and brake settings. Every numeric
value must be finite and in a physical range. Related First Car tuning values
remain together in `VehicleCatalog`.

Persistent simulation state contains, per wheel, angular speed, suspension
compression and velocity, and ABS modulation. Vehicle state also contains the
automatic gear, engine RPM, deterministic shift phase, and sprung chassis
heave, roll, and pitch with their velocities.

Diagnostics expose normal load, slip angle, slip ratio, longitudinal and
lateral force, normalized combined grip usage, steering angle, and ABS state
for all four corners. Vehicle diagnostics additionally expose current gear,
engine RPM, shift state, heave, roll, and pitch.

## Suspension and load transfer

- Four independent spring/damper corners support the sprung mass on a flat
  road and begin at the configured 60/40 front/rear static distribution.
- First Car spring rates begin at 30 kN/m front and 25 kN/m rear, damping ratio
  near 0.7, bump travel of 0.16 m, and droop travel of 0.10 m.
- Corner compression is derived from heave, roll, and pitch. Spring and damper
  forces produce non-negative normal loads within bounded travel.
- Tire forces acting at road height feed longitudinal and lateral moments back
  through the 0.55 m CG height. The resulting sprung-body motion creates
  longitudinal and left/right load transfer rather than directly assigning
  dynamic axle loads.
- Vertical states use semi-implicit fixed-step integration. Reset restores
  static equilibrium and zero vertical velocity.

## Steering and tire forces

- Contact points use the configured 2.7 m wheelbase and 1.5 m track. Velocity
  is sampled and force is applied at each wheel center.
- Speed-sensitive steering retains the current 32 degree low-speed limit and
  produces distinct Ackermann front angles; the inner wheel has the larger
  magnitude. Rear wheels remain aligned to the chassis.
- Low-speed force blending prevents steering from rotating a stationary car.
- Longitudinal slip ratio and lateral slip angle feed compact Pacejka-like
  curves. Initial shape factors are `Bx=10`, `Cx=1.9`, `Ex=0.97`, `By=5`, and
  `Cy=1.3`, with dry peak friction coefficient 1.05.
- Peak force scales with current wheel load and a configurable load-sensitivity
  exponent. A normalized friction ellipse combines longitudinal and lateral
  output so drive or braking consumes cornering capacity.
- Wheel angular velocity integrates drive torque, brake torque, tire reaction
  torque, wheel radius, and inertia. Finite guards and a stable near-zero-speed
  transition prevent invalid or permanently locked state.

## Powertrain, direction selection, and braking

- The engine uses a piecewise-linear torque curve, 850 RPM idle, 6,500 RPM
  redline, automatic shift thresholds near 4,800/1,800 RPM, and explicit
  closed-throttle engine-braking torque.
- Forward ratios are 3.55, 2.10, 1.36, 1.03, and 0.82; final drive is 3.90 and
  reverse is 3.40. Driveline efficiency, wheel radius, and wheel inertia are
  explicit definition values.
- The front open differential gives equal shaft torque to both front wheels.
  Engine speed follows average driven-wheel speed while remaining finite and
  between idle and redline.
- Shifts are hysteretic, interrupt torque for a short deterministic interval,
  and perform at most one gear transition per shift event.
- The lower upshift threshold and 0.14-second shift interval let sustained
  acceleration reach fifth gear earlier and with less delay.
- W requests forward drive except while braking a reversing car. S brakes a
  forward-moving car and selects reverse only near rest; W behaves symmetrically
  while reversing.
- A direct service-brake input applies the brakes independently of the selected
  travel direction. Braking takes priority over simultaneous throttle input
  and does not select reverse. It has no keyboard binding in the prototype.
- Releasing the accelerator while moving in gear applies engine braking through
  the driven front wheels. Its strength follows the engaged gear ratio, so the
  car slows noticeably faster than a free-rolling chassis without matching the
  stronger service brake.
- With no forward or reverse request, chassis motion below 0.5 m/s (1.8 km/h)
  and 0.1 rad/s settles to a complete stop, including residual wheel rotation.
- Existing 12 kN braking capability becomes wheel brake torque using tire
  radius and a 68/32 front/rear bias.
- Each wheel has an independent always-enabled ABS modulator. It rapidly
  reduces brake pressure above about 15% braking slip, restores it more slowly
  below a release threshold, and bypasses modulation below about 1.5 m/s.

## Rendering and HUD

- Render both front wheels at their distinct Ackermann angles and render a tread
  marker based on each wheel's angular position.
- Keep contact locations fixed. Shift and shade the chassis subtly toward more
  compressed corners to show pitch and roll, with a clamped visual-only offset
  that never changes collision geometry.
- Render a subtle chassis-sized ground shadow cast down and right from the player
  car by the environment's implied upper-left light source.
- The gameplay HUD replaces diagnostic wheel readouts with a translucent bottom
  bar: speed is fixed at center, small RPM-over-Gear readouts occupy the left,
  and a nonnumeric HP bar occupies the right. Controls use a separate sidebar.
- Player and traffic tires leave subtle fading trails; tiny rear-tire smoke puffs
  appear only at higher speed.

## Acceptance criteria

- Static left/right wheel loads are symmetric, represent 60/40 front/rear
  distribution, and sum to vehicle weight.
- Longitudinal tire force transfers load rearward under acceleration and forward
  under braking. Lateral force loads outside wheels and unloads inside wheels.
  Normal loads remain finite and non-negative.
- Suspension returns toward equilibrium and remains bounded at 1/60 second.
- Ackermann steering gives the inside front wheel the larger angle.
- Tire forces are zero at zero slip, build nonlinearly, reach and fall from a
  peak, reflect load sensitivity, and lose lateral capacity under combined slip.
- Wheel torque changes angular speed; the open differential gives equal front
  shaft torque.
- Automatic shifts observe thresholds and hysteresis, perform one ratio change
  per shift, and interrupt drive torque. Reverse engages only near rest and
  engine RPM remains finite and bounded.
- Direct service-brake input slows a moving car through the existing four-wheel brake and ABS model
  without changing a forward gear into reverse.
- With no pedal input, an in-gear moving car produces driven-wheel force that
  opposes travel and reduces speed more quickly than road resistance alone.
- Coast-down reaches exactly zero linear and angular speed instead of retaining
  persistent motion near 1 km/h.
- ABS can release and reapply each wheel independently and avoids sustained
  wheel lock in a deterministic hard-stop scenario.
- Steering alone cannot rotate a stationary chassis. Forward and reverse
  steering remain directionally correct.
- Combined throttle and steering produces finite four-wheel state with
  progressive FWD understeer. Hard braking activates ABS and slows the chassis
  without non-finite or permanently locked wheel state.
- Reset restores planar pose and velocity plus suspension, wheel, gearbox,
  engine, shift, and ABS state.
- `dotnet test CarGame.sln`, `dotnet build CarGame.sln`, and
  `dotnet format CarGame.sln --verify-no-changes` pass.

## Manual verification

1. Hold a steady medium-speed turn and observe higher outside-wheel loads and a
   subtle, bounded chassis roll visual.
2. Accelerate and brake with S in a straight line; confirm opposite pitch
   directions, visible automatic up/down shifts, and no unintended reverse
   selection. Confirm S still brakes before selecting reverse near rest.
3. Apply full throttle while cornering; confirm front grip is consumed and the
   car progressively runs wide. Confirm an unloaded driven wheel limits both
   sides through the open differential.
4. Brake hard from road speed; confirm individual ABS activity appears in the
   HUD, the car slows, and steering authority remains available.
5. Reverse and steer both directions; confirm the front wheel geometry and
   chassis response are directionally correct.
6. Press reset after driving and braking; confirm the car, RPM, gear, suspension,
   wheel rotation, and ABS indications return to their initial state.

## Non-goals

- Separate Box2D wheel bodies or suspension constraints.
- Road bumps, camber, toe, tire temperature or wear, aerodynamic downforce, or
  manufacturer-accurate calibration.
- Traction control, a limited-slip differential, an ABS toggle, intentional
  locked-wheel behavior, or manual transmission controls.
- Suspension influence on Box2D planar orientation or collision geometry;
  heave, roll, and pitch affect contact loads and rendering only.
