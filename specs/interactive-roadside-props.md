# Interactive roadside props

## Purpose

Enrich the rural road and town with numerous small physical objects that react
to the vehicle, explosions, stationary scenery, the map boundary, and each
other.

## Player-visible behavior

- The world contains trash cans, short guardrail sections, traffic cones, and
  small wooden crates in deterministic roadside and town arrangements.
- Exactly three guardrails occupy the road corridor: two staggered checkpoint
  barriers and the single town-entrance barrier. No other road guardrails or
  cones clutter the rural route. Cones appear only along the checkpoint road
  edges and the short approach from the checkpoint to town.
- The car can push, scatter, and spin every prop. Props rebound according to
  material and lose speed from ground resistance rather than sliding forever.
- A car explosion violently throws all props inside the 35-metre blast radius
  outward with force decreasing by distance. At the blast center, cones can
  receive about 97.5 m/s, trash cans 75 m/s, crates 60 m/s, and heavy guardrail
  sections about 41.25 m/s of immediate speed change. Even objects at the edge
  receive a substantial outward kick.
- Props bounce from buildings, trees, rocks, boundaries, and other props.
- R restores every prop to its original pose.

## Technical constraints

- Props are independent Box2D dynamic bodies from world creation onward.
- Cones are light and relatively bouncy; trash cans are medium-weight metal;
  crates are heavier with low bounce; guardrail sections are the heaviest and
  resist movement while remaining physically reactive.
- Guardrail sections use strong rotational damping, negligible blast spin, and
  a 0.65 rad/s yaw-speed cap. Off-center impacts can turn them slightly, but
  they primarily bump sideways instead of spinning like loose lightweight props.
- Explicit top-down ground resistance is applied on the fixed timestep.
- Props use continuous collision detection because blast speeds are high enough
  to otherwise tunnel through thin stationary objects.
- Rendering is entirely code-drawn and uses the body's simulated pose.

## Acceptance criteria

- At least 50 props populate the environment, with every requested prop kind
  represented.
- Exactly three guardrail props and eight traffic cones are present; all cones
  are confined to the checkpoint and final town approach.
- Props have finite physical state, collide with solid world objects, respond
  to a blast inside its radius, and remain unaffected by blasts outside it.
- Explosion response is outward and distance-scaled.
- Near-blast light props launch at more than 50 m/s while heavy guardrails
  receive a visibly lower but still violent response.
- Corner impacts do not make guardrail sections rotate continuously like tops.
- Reset recreates the original deterministic prop layout.
- Build, tests, and formatting verification pass.

## Manual verification

1. Drive through cones and crates and confirm they scatter, rotate, and rebound
   from nearby solid scenery.
2. Push a trash can and guardrail section and compare their different masses.
3. Explode near a group and confirm nearby props are thrown outward while
   distant props remain still.
4. Press R and confirm all props return to their starting positions.

## Non-goals

- Collecting props, inventory, breakable prop meshes, sharp-body damage,
  articulated guardrails, audio, or persistent state across game launches.
