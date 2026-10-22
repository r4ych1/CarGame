# Realistic vehicle dynamics foundation

## Purpose

Replace the prototype's fixed axle-grip tuning with a reusable, documented
bicycle-model dynamics foundation. Configure the first car as a front-wheel-drive
road hatchback while keeping vehicle-specific data separate from the solver so
additional vehicles can be introduced without duplicating physics code.

## Player-visible behavior

- The first car feels planted on dry asphalt during ordinary steering.
- Acceleration consumes front-tire grip and transfers normal load rearward, so
  applying full throttle while cornering produces progressive understeer.
- Braking transfers normal load forward. The rear becomes relatively lighter,
  but remains controllable during normal braking and steering.
- Abrupt steering at speed builds tire slip progressively rather than instantly
  snapping the velocity into alignment with the car.
- When a tire axle exceeds available grip, force saturates smoothly and the car
  slides until speed or steering demand is reduced.

## First car definition

- Name: `First Car`
- Layout: front-engine, front-wheel drive
- Mass: 1,300 kg
- Body: 1.8 m wide and 4.2 m long
- Wheelbase: 2.7 m; track width: 1.5 m
- Static weight distribution: 60% front / 40% rear
- Center-of-gravity height: 0.55 m
- Dry-road friction coefficient: 1.05
- Maximum steering angle: 32 degrees, reduced at high speed

These are an initial representative road-car baseline, not measurements of a
specific production vehicle.

## Functional requirements

- Store all vehicle-specific physical and tuning data in an immutable vehicle
  definition selected when the vehicle is created.
- Give the Box2D body the configured total mass, longitudinal center of mass,
  and rectangle-derived yaw inertia.
- Calculate static front and rear normal loads from the center-of-mass position.
- Estimate longitudinal acceleration from the force request and calculate
  longitudinal load transfer using mass, CG height, and wheelbase.
- Keep axle normal loads non-negative and preserve total vehicle weight.
- Calculate front and rear lateral force from tire slip angle and axle cornering
  stiffness.
- Apply a friction-circle limit to the combined longitudinal and lateral force
  at each axle. The front axle receives all drive demand; braking follows a
  configurable 68% front / 32% rear bias for the first car.
- Continue to use fixed-step Box2D integration and explicit top-down road forces.
- Expose diagnostic front/rear axle loads and grip usage for testing and future
  handling displays.

## Acceptance criteria

- Static axle loads match the configured 60/40 distribution within floating-point
  tolerance and sum to vehicle weight.
- Forward acceleration transfers load from front to rear; braking transfers it
  from rear to front.
- The friction-circle calculation never returns a combined axle force greater
  than `normal load × road friction coefficient`.
- Adding longitudinal force reduces lateral force available on that axle.
- Steering alone cannot rotate the stationary car.
- Holding throttle and steering moves and rotates the car in the commanded
  direction without non-finite state.
- Vehicle-specific constants are not embedded in the generic dynamics solver.
- The solution builds, deterministic tests pass, and formatting is clean.

## Non-goals

- Four independently simulated wheels, suspension geometry, body roll, pitch
  animation, aerodynamics with downforce, tire temperature/wear, ABS, traction
  control, a differential, torque steer, or individual wheel locking.
- Lateral left/right load transfer. This bicycle model combines the two tires on
  each axle; track width is retained for rendering and future four-wheel work.
- Manufacturer-accurate engine, gearbox, or tire data.
- Additional selectable vehicles or road surfaces in this increment.

## Technical model and units

- Distances are metres, mass is kilograms, acceleration is m/s², force and
  normal load are newtons, angles are radians internally, and cornering
  stiffness is newtons per radian.
- Positive longitudinal acceleration means forward acceleration. Dynamic front
  load is `static front load - mass × acceleration × CG height / wheelbase`.
- Tire slip angle is the signed angle between the wheel heading and velocity at
  that axle. Lateral demand is proportional to negative slip angle and is
  limited by the axle friction circle.
- Low-speed slip calculations use a configured minimum reference speed to avoid
  unstable angles near rest. Tire force naturally approaches zero with axle
  velocity at very low speed.
- The body origin remains at the geometric center for rendering. Box2D mass data
  offsets the physical center of mass toward the front axle.

## Manual verification

1. Accelerate in a straight line and confirm stable forward motion.
2. Enter a steady moderate-speed turn and confirm the car follows a smooth arc.
3. Apply full throttle during the turn and confirm the car runs slightly wider
   instead of gaining implausible extra cornering force.
4. Release throttle and confirm front grip returns progressively.
5. Brake while turning and confirm the car remains controllable but feels more
   responsive as load moves forward.
6. Make an abrupt high-speed steering input and confirm the car slides
   progressively, then regains grip when steering is reduced.
7. Reverse and confirm steering behavior remains directionally correct.

## Open decisions

- Engine torque curve, gearing, speed-sensitive steering assistance, and exact
  tire stiffness require hands-on tuning after this foundation is evaluated.
- Four-wheel load transfer is the likely next realism step if the bicycle model
  cannot deliver the desired feel.
