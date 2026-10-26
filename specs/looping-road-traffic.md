# Looping road traffic

## Purpose

Add four autonomous physical cars that continuously demonstrate the main road,
checkpoint zigzag, and dirt bypass route.

## Player-visible behavior

- Four traffic cars begin at different randomized, obstacle-free stages of the
  route. Initial placement never uses checkpoint, bridge, or prop-lined route
  points. Each car draws from a separate route zone so every possible initial
  pairing remains at least 65 metres apart.
- Each car enters from the bottom, drives toward town in its inbound lane,
  slows and zigzags through the checkpoint, turns onto the dirt bypass, follows
  it back to the main road, and drives away from town to the bottom edge.
- A car despawns after crossing the bottom edge and respawns inbound after a
  random delay from 5 through 15 seconds.
- Traffic cars collide with the player, props, scenery, and one another.
- Drivers use bounded throttle force and steering torque rather than directly
  forcing pose or velocity. A nudge can push a car off line and change its
  heading; an undamaged driver then makes gradual corrections toward upcoming
  route points.
- A severe frontal collision at driving speed, or a very large impact from any
  direction at higher speed, disables the traffic car at the impact location.
  Disabled cars stop driving and remain physical obstacles until reset.
- Collision-disabled cars retain their original paint color. A small animated
  fire burns at the hood and several smoke puffs rise from the engine until
  reset. Explosion-wrecked cars remain charred but use the same persistent
  engine fire and smoke effect.
- Functioning drivers look ahead for disabled or wrecked traffic, the player,
  solid scenery, and movable props. They slow and steer laterally around a
  blocking obstacle, then resume route recovery after passing it.
- Any meaningful collision below the disabling threshold preserves the impact
  motion for a 0.55-second driver-reaction window, allowing the car to be
  shoved sideways or fishtail. The driver then brakes progressively and stops
  for a total response of about 1.4 seconds, then resumes forward route-following
  and obstacle avoidance. Traffic never deliberately reverses during recovery.
- A traffic car inside the player explosion radius leaves autopilot and becomes
  uncontrolled dynamic wreckage. Blast speed change rises nonlinearly from
  about 6 m/s near the edge to 30 m/s near ground zero so close cars travel
  substantially farther than cars near the perimeter.
- R restores four functioning cars at newly seeded route stages.

## Technical constraints

- Randomization uses a fixed local pseudo-random seed so simulation tests remain
  repeatable. No wall-clock timing participates.
- Traffic uses dynamic Box2D bodies. Drivers target about 9 m/s on pavement,
  6 m/s on dirt, and 3.5 m/s through the checkpoint, using at most 3.2 m/s
  squared of commanded acceleration and bounded steering torque.
- Inbound and outbound traffic use opposite lane offsets. Checkpoint waypoints
  begin slowing before the first barrier, hold each opening until the complete
  car clears its rail, and use the enlarged space between rails to change sides
  without intersecting them.
- After clearing the second checkpoint rail, low-speed waypoints form a gradual
  eastbound arc onto the dirt bypass. The arc remains south of the town wall and
  clear of checkpoint cones before accelerating to dirt-road speed.
- Despawned bodies are disabled until their fixed-step respawn timer expires.

## Acceptance criteria

- Exactly four traffic vehicles exist and begin active at distinct route stages.
- Every randomized initial stage is on a clear road or dirt-road section rather
  than between checkpoint props or alongside the bridge rails.
- Initial and reset placement keep every pair of functioning traffic cars at
  least 65 metres apart.
- The route starts and ends below the visible map, includes both checkpoint
  gaps, traverses the dirt-road control points in reverse, and returns south.
- Checkpoint route points use the slow speed and dirt points use the dirt speed.
- Traffic clears the checkpoint-to-bypass turn without contacting the town wall
  or intentionally driving through checkpoint props.
- Respawn delay is always between 5 and 15 seconds and restores the inbound
  spawn pose.
- Blast-hit vehicles stop following the route and receive finite outward motion
  with about 6 to 30 m/s of speed change depending nonlinearly on blast distance.
- Lateral collision velocity is not erased on the next fixed step. Route
  recovery is progressive rather than an immediate snap.
- Obstacle avoidance examines a 14-metre forward corridor and chooses a passing
  side before impact. Disabled cars do not permanently block following traffic.
- Non-disabling impacts with at least a 0.3 m/s instantaneous velocity change,
  while moving at least 0.5 m/s, enter the reaction and braking pause instead of
  being immediately overwritten by route control. Recovery never commands a
  reverse velocity.
- A longitudinal velocity loss of roughly 7.5 m/s in one physics step while
  moving at least 6.5 m/s is treated as a disabling frontal impact. Side impacts
  require at least 10 m/s initial speed and an 11 m/s velocity change. Lesser
  impacts retain physical shove and fishtail motion before driver recovery.
- Reset restores all four active routed vehicles.
- Build, tests, and formatting verification pass.

## Manual verification

1. Observe all four cars well spread across different parts of the environment
   after launch and reset.
2. Watch an inbound car slow before the checkpoint, clear the first rail, change
   sides, and clear the second rail without striking either guardrail.
3. Confirm it takes the dirt bypass, rejoins the main road southbound, despawns
   at the bottom, and later returns inbound.
4. Collide with a traffic car and confirm both bodies react.
5. Nudge a traffic car sideways and confirm it veers, then steers back over time.
6. Cause a high-speed frontal collision and confirm the traffic car stops and
   remains disabled at the scene.
7. Watch a following car slow, steer around the disabled car, and return to its
   route rather than joining a permanent traffic jam.
8. Explode beside traffic and confirm affected cars are thrown outward while
   retaining animated engine fire and smoke; cars close to ground zero should
   travel substantially farther than cars near the blast perimeter.
9. Cause both slow and faster non-disabling bumps and confirm the driver reacts,
   brakes to a stop, then attempts to continue forward without backing up.

## Non-goals

- Traffic lights, free-form lane changing, parking behavior, navigation mesh
  pathfinding, or traffic audio.
