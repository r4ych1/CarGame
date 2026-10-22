# Vehicle dynamics architecture

CarGame simulates four tire contact patches around one Box2D chassis. Box2D owns
planar translation, yaw, collision geometry, and force integration. The
deterministic vehicle solver owns wheel rotation, tire forces, sprung-body
heave/roll/pitch, the powertrain, gearbox, and ABS. Wheels are not Box2D bodies.

## Responsibilities and update flow

- `VehicleDefinition` composes immutable `ChassisSettings`,
  `SuspensionSettings`, `TireSettings`, `PowertrainSettings`,
  `TransmissionSettings`, and `BrakeSettings`. Each group validates finite and
  physical ranges.
- `VehicleCatalog` is the only composition point for First Car tuning.
- `VehicleDynamics` has no Box2D or Raylib dependency. Given the definition,
  persistent `VehicleSimulationState`, a sampled `ChassisSample`, input, and the
  fixed timestep, it returns updated state, four wheel forces, and diagnostics.
- `Vehicle` is the Box2D adapter. It samples chassis velocity, angular velocity,
  and heading, calls the deterministic solver, then applies each returned force
  at its fixed contact location. It separately applies rolling resistance,
  aerodynamic drag, and small yaw damping.
- `GameRenderer` reads diagnostics and simulation state but never changes them.

Each 1/60-second game step follows this path:

```text
VehicleInput + Box2D chassis sample + persistent state
                         |
                         v
  steering -> suspension loads -> transmission/driver intent
                         |
                         v
     four wheel velocities, slip ratios, and slip angles
                         |
                         v
      nonlinear pure tire curves -> friction ellipse
                         |
                         v
      wheel torque integration + independent ABS state
                         |
                         v
     tire-force roll/pitch moments -> suspension integration
                         |
                         v
  four world forces + next state + per-wheel diagnostics
                         |
                         v
               one Box2D chassis body
```

Rendering can run at any rate. It does not pass frame time into this flow.

## Coordinates and wheel layout

At zero yaw the vehicle points toward screen/world negative Y. Local positive X
is right. `VehicleMath.Forward` and `VehicleMath.Right` are the shared conversion
boundary. Box2D metres become pixels only through `SimulationUnits`.

The four contact locations are `front-left`, `front-right`, `rear-left`, and
`rear-right`, using half wheelbase longitudinally and half track laterally. The
body origin remains at the geometric center for artwork and collision shape.
Box2D mass data moves the physical center of mass forward using:

```text
cgOffsetY = (0.5 - frontStaticWeightFraction) * wheelbase
```

The solver accounts for that offset when deriving contact-point velocity from
the chassis center-of-mass velocity and yaw rate.

## Steering

The existing speed-sensitive central steering request is retained. Ackermann
geometry converts it to distinct front-left and front-right angles:

```text
turnRadius = wheelbase / tan(abs(centerAngle))
inner      = atan(wheelbase / (turnRadius - halfTrack))
outer      = atan(wheelbase / (turnRadius + halfTrack))
```

The inside front wheel therefore turns farther. Rear wheels remain aligned with
the body. Tire force is blended toward zero at very low contact speed, so a
steering request alone cannot yaw a stationary chassis.

## Sprung body and normal load

Heave, roll, and pitch are deterministic generalized suspension coordinates;
they do not rotate or move the Box2D collision body. Each coordinate and its
velocity persist between steps. Corner displacement is derived from those six
values, wheelbase, and track, then bounded by bump and droop travel.

Each wheel begins with its share of the configured static weight. The current
normal load adds spring and damper response:

```text
normalLoad = max(0, staticLoad + springRate * compression
                                  + damping * compressionVelocity)
damping    = 2 * dampingRatio * sqrt(springRate * supportedCornerMass)
```

The sum of contact loads drives heave. Lateral tire force acting through CG
height drives roll; longitudinal tire force drives pitch. Differences in the
four suspension forces provide restoring moments. Semi-implicit integration at
the fixed timestep updates velocity before displacement. Reset sets every
vertical displacement and velocity to static equilibrium.

For the sign convention, positive roll compresses the left side and positive
pitch compresses the front. Thus a rightward tire force loads the left/outside
wheels, forward acceleration pitches rearward, and braking pitches forward.

## Tire and wheel model

Wheel-frame contact velocity provides:

```text
slipRatio = (wheelAngularSpeed * radius - longitudinalSpeed) /
            max(abs(wheelSurfaceSpeed), abs(longitudinalSpeed), minimumSlipSpeed)

slipAngle = atan2(lateralSpeed,
                  max(abs(longitudinalSpeed), minimumSlipSpeed))
```

Compact Pacejka-like curves turn slip ratio and slip angle into normalized pure
longitudinal and lateral forces. The initial longitudinal coefficients are
`B=10`, `C=1.9`, and `E=0.97`; lateral uses `B=5` and `C=1.3`. These fictional
car values deliberately produce a progressive peak and falloff.

Peak force starts with dry-road coefficient 1.05 and uses load sensitivity:

```text
peak = mu * referenceLoad * (normalLoad / referenceLoad)^loadExponent
```

An exponent below one makes a heavily loaded tire produce more total force but
less force per newton of normal load. Pure longitudinal and lateral outputs are
then normalized against the same peak and limited to a friction ellipse.
Acceleration or braking consequently reduces available cornering force.

Wheel angular velocity integrates shaft torque, brake torque, and the opposite
tire reaction torque. A crossing guard permits the contact impulse to reach
road speed without numerically overshooting it during one fixed step. Slip
denominators, finite guards, bounded angular speed, and low-speed blending keep
rest and stop transitions stable.

## Powertrain and automatic gearbox

The front wheels are driven. Engine torque is linearly interpolated from the
definition's RPM/torque points. Average absolute front-wheel speed, the active
gear ratio, and the final drive determine engine RPM, bounded between idle and
redline.

The five forward ratios are 3.55, 2.10, 1.36, 1.03, and 0.82. Reverse is 3.40
and final drive is 3.90. A shift begins only after crossing the upshift or
downshift threshold. While its deterministic timer is active, shaft torque is
zero. When the timer expires exactly one pending gear is engaged; the distinct
thresholds provide hysteresis.

The open differential divides axle torque equally. Both shaft torques are
limited to what the less-loaded front contact can support, so unloading one
driven wheel limits the axle without adding traction control.

W requests forward drive and S requests reverse drive only within the configured
near-rest selection speed. Outside that window, the pedal opposing current
travel requests braking instead of changing direction.

## Brakes and ABS

The 12 kN total braking capability is converted to torque with wheel radius and
split 68% front / 32% rear, then equally within each axle. Each wheel has its own
pressure modulation value. During braking above 1.5 m/s:

- braking slip beyond the release threshold reduces pressure quickly;
- recovered slip below the reapply threshold restores pressure more slowly;
- modulation between the thresholds is held to avoid chatter.

Below the minimum ABS speed, full pressure is allowed so the car can settle at
rest. There is no ABS toggle or traction-control path.

## Diagnostics and rendering

Every wheel reports normal load, slip angle, slip ratio, longitudinal and
lateral force, combined grip usage, steering angle, and ABS intervention. The
vehicle reports gear, engine RPM, shift state, heave, roll, pitch, and all four
wheel records.

The HUD displays gear, RPM, shift/ABS activity, and compact FL/FR/RL/RR load and
grip values. Front wheels use their individual steering angles. A tread marker
uses integrated wheel rotation. Wheels stay at the actual contact locations;
the body rectangle receives only a small clamped visual offset toward compressed
corners, leaving Box2D geometry unchanged.

## Tuning order

1. Chassis mass, dimensions, static distribution, and CG height.
2. Spring rates, damping ratio, travel, and roll/pitch inertia.
3. Tire peak coefficient, shape factors, and load sensitivity.
4. Engine torque curve, ratios, shift thresholds, and driveline efficiency.
5. Brake capacity/bias and ABS thresholds/rates.
6. Steering scale, resistance, drag, and yaw damping.

Change one group at a time and run deterministic tests before manual handling
checks. Do not compensate for an unstable suspension by hiding forces in the
Box2D adapter.

## Deliberate limitations

The road is flat; there are no wheel bodies, suspension constraints, camber,
toe, tire temperature/wear, downforce, damage, traction control, limited-slip
differential, manual gears, or manufacturer-derived values. Sprung-body motion
changes normal load and visuals only. Box2D continues to own planar chassis
translation and yaw.
