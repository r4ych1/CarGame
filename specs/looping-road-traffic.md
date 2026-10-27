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
- Each traffic car has a subtle ground shadow matching its body pose and cast
  down and right by the environment's implied upper-left light source.
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
- Drivers treat persistent blast craters as impassable road hazards. A first car
  brakes to a stop before a crater instead of crossing or steering around it;
  following cars stop behind queued traffic to form a traffic jam.
- Moving traffic leaves subtle paired tire trails that fade gradually, with tiny
  rear-tire smoke puffs at higher speed.
- Any meaningful collision below the disabling threshold preserves the impact
  motion for a 0.55-second driver-reaction window, allowing the car to be
  shoved sideways or fishtail. The driver then brakes progressively and stops
  for a total response of about 1.4 seconds, then resumes forward route-following
  and obstacle avoidance. Traffic never deliberately reverses during recovery.
- A traffic car within the 16-metre close blast zone leaves autopilot and
  becomes uncontrolled dynamic wreckage. Its blast speed change rises
  nonlinearly toward 30 m/s near ground zero.
- A traffic car in the 16-to-32-metre damage band is disabled without receiving
  a blast impulse. Its engine fire and smoke start immediately, and the car
  retains its exact pre-blast linear and angular momentum before gradually
  rolling to a stop under 2.5 m/s squared of ground resistance.
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
- Crater stopping uses the fixed-step traffic controller and a forward stopping
  corridor; craters remain visual/nonphysical for the player and other objects.

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
- Blast-hit vehicles stop following the route. Cars within 16 metres receive a
  finite outward blast impulse, while cars in the 16-to-32-metre damage band
  receive no impulse, preserve their pre-blast momentum, display engine fire and
  smoke, and progressively roll to a stop.
- Lateral collision velocity is not erased on the next fixed step. Route
  recovery is progressive rather than an immediate snap.
- Obstacle avoidance examines a 14-metre forward corridor and chooses a passing
  side before impact. Disabled cars do not permanently block following traffic.
- A functioning car brakes for a crater in its forward lane and for a queued car
  immediately ahead, producing a stable stopped line rather than entering the
  crater.
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
8. Explode beside traffic and confirm cars within 16 metres are thrown outward.
   Confirm cars in the 16-to-32-metre damage band ignite and lose engine control
   but continue coasting in their original direction until rolling to a stop.
9. Cause both slow and faster non-disabling bumps and confirm the driver reacts,
   brakes to a stop, then attempts to continue forward without backing up.
10. Detonate on the traffic route and confirm the first arriving car stops before
    the crater and later arrivals queue behind it without collisions.

## Non-goals

- Traffic lights, free-form lane changing, parking behavior, navigation mesh
  pathfinding, or traffic audio.
