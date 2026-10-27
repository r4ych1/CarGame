# Destructible driving environment

## Purpose

Replace the empty test grid with a compact code-drawn rural road and town that
provides scenery, collision, and deterministic blast destruction.

## Player-visible behavior

- The car begins on a curved two-lane road that approaches a small town.
- Rural space contains trees, bushes, rocks, sparse buildings and shacks, plus
  parked cars and trucks. A horizontal stream crosses the complete map and the
  main road crosses it on one bridge. Town buildings form rows around streets.
- Buildings have distinct code-drawn roofs with visible roof edges and surface
  details. Buildings, vegetation, parked vehicles, and movable roadside objects
  have subtle ground shadows that add depth without looking detached.
- A solid wall encloses every town building and its rows. The wall has one
  8-metre-wide entrance aligned with the approaching main road on its southern
  side, blocked by one movable guardrail section.
- Shortly before the town entrance, a checkpoint places two movable guardrails
  across opposite lanes at staggered distances. Their alternating openings form
  a driveable zigzag. A small destructible checkpoint shack stands beside the
  road without blocking that path.
- Short anchored guardrail sections line both long edges of the bridge while
  leaving its road entrances and full driving corridor open.
- Traffic cones are lightweight movable props, substantially lighter than trash
  cans, crates, and the movable town-entrance guardrail.
- Intact trees, rocks, and buildings block the car. Bushes are visual foliage
  and do not block it. Roads and bridges remain driveable.
- Driving beyond the shallow edge of the stream anywhere outside the bridge
  sinks the player car. A sunk car stops moving and ignores driving controls
  until R resets the world. It also cannot explode: Space creates no vehicle
  fragments, blast effects, crater, or environment damage while sunk.
- The car explosion has a 32-metre physical impact radius and a 16-metre building
  destruction radius. Buildings inside 16 metres split into charred rigid debris
  that settles as rubble; buildings from 16 to 32 metres remain standing with
  visible cracks. People inside 20 metres are killed, while people farther out
  are knocked back by physics and survive. Other destructible scenery still
  reacts throughout the 32-metre impact radius. Building fragments look like
  scorched chunks rather than miniature intact roofs, with irregular sizes and
  numerous small pieces contained within the original footprint at breakup.
- A blasted tree uses a long trunk-shaped debris body with its green canopy
  attached to the trunk's forward end. Trees receive strong outward knockback,
  high angular damping, and limited yaw so they slide and topple away from the
  blast instead of spinning in place.
- Each explosion leaves a persistent crater centered on the car. Roads can be
  visually cratered but remain driveable in this prototype.
- R restores the vehicle and the entire undamaged environment.
- A visible solid perimeter encloses the complete environment and prevents the
  vehicle or dynamic debris from leaving the playable area.
- The mouse wheel zooms the camera in and out while preserving its current
  target. Zoom is clamped between 0.35x and 1.65x so the closest view retains
  more surrounding context.

## Functional requirements

- World layout is handcrafted and deterministic, measured in metres, and
  contains no image assets.
- Bush placement tests the complete bush footprint against the paved main road,
  dirt bypass, bridge roadway, and town cross streets; no bush overlaps any road.
- Static colliders exist for intact solid props. Destruction removes each
  affected collider before creating dynamic debris bodies.
- Town walls are permanent static colliders and are immune to explosions.
- Blast strength falls off nonlinearly with distance and points away from the
  explosion center. Objects near ground zero receive substantially stronger
  launches than objects near the blast edge. Dynamic debris receives explicit
  ground resistance.
- Environment ownership, reset, and Box2D cleanup are separate from vehicle
  dynamics and rendering.
- The focused playable rectangle spans X -80 to 80 metres and Y -380 to 70 metres.
  Four static 4-metre-thick boundary walls sit along its edges and are immune
  to explosions and environment reset.
- Camera zoom changes in multiplicative 12% steps per wheel notch and remains
  a rendering concern; it does not affect simulation or input forces.

## Acceptance criteria

- The map includes a curved main road, town streets, one horizontal stream and
  road bridge, rural vegetation and rocks, sparse outskirts buildings and
  shacks, parked cars and trucks, and at least two rows of town buildings.
- Buildings have shaded roof surfaces, roof edges, and visible ridge or panel
  details.
- Every town-row building lies inside the wall, whose only opening contains one
  physical guardrail spanning the road entrance.
- The checkpoint has exactly two movable staggered lane barriers and one
  roadside shack; the intact car can zigzag between the barriers without
  leaving the road. Impacts and blasts can move either barrier.
- Each long bridge edge has a continuous-looking series of anchored guardrail
  sections, with no rail blocking either bridge entrance.
- A traffic cone has less mass than every other movable prop kind.
- The approximately 5-metre-wide dirt bypass stays east of both checkpoint
  barriers and reconnects before the town entrance.
- Intact solid objects collide with the vehicle.
- People inside 20 metres are killed, while buildings inside 16 metres are destroyed;
  buildings in the 16-to-32-metre band stay standing and show cracks, and people
  in the 20-to-32-metre band are pushed back and survive.
- Other blast-reactive objects inside 32 metres receive an outward physical
  impact; objects outside remain unaffected.
- Destroyed buildings produce multiple independently simulated pieces, trees
  produce charred chunks without intact roof graphics, trees produce a
  knocked-over trunk, rocks become dynamic, and a crater is recorded.
- Large buildings inside 16 metres fracture into at least 24 independently
  simulated larger pieces. Their fragments vary in size and are accompanied by
  at least 20 smaller independently simulated fragments. Pieces remain within
  the original building footprint at breakup and launch away from ground zero
  with speed based on their own distance from the blast center. Some fragments
  carry short-lived fire and smoke.
- Knocked-over trees move outward at more than 20 m/s near the blast center and
  remain below 0.55 rad/s of planar spin.
- Debris motion is finite, outward, slows over time, and reset restores the
  original deterministic world.
- Mouse-wheel input changes camera zoom within the specified limits without
  changing the camera target.
- The vehicle and dynamic debris collide with every perimeter edge and cannot
  travel indefinitely beyond the rendered environment.
- The bridge corridor is safe to drive; deep-water points outside it put the
  intact vehicle into a persistent stopped state that reset clears.
- The road transitions to a straight north-south alignment before reaching the
  bridge, remains perpendicular to the horizontal stream for the entire bridge
  span, and resumes its curve beyond the crossing.
- A one-lane dirt bypass branches right immediately after the bridge, snakes
  east around the checkpoint, and rejoins the main road between the checkpoint
  and guarded town entrance.
- Curved road segments use filled full-width joins, so outward bend edges have
  no triangular ground gaps between sampled sections.
- Bushes remain completely beyond every paved or dirt road edge and shoulder.
- Intact buildings, vegetation, parked vehicles, and movable props have subtle,
  consistently directed code-drawn shadows cast down and right from an implied
  upper-left light source. Taller scenery casts a slightly longer shadow.
- Trees and bushes are excluded from the stream and its immediate banks. Short,
  translucent current streaks drift horizontally across the water on the fixed
  timestep to show a gentle flow.
- Build, tests, and formatting verification pass.

## Manual verification

1. Drive along the curved road, across the stream bridge, and toward the town.
   Confirm both edges remain visually continuous around every bend.
2. Confirm trees, rocks, and buildings block the car while bushes do not.
   Confirm building roofs have visible shape and surface details, and scenery
   and props have subtle shadows that remain visually attached to them.
3. Zigzag through the two checkpoint barriers, then push or blast the town
   entrance guardrail aside, and
   confirm the remaining wall has no other opening.
4. Alternatively, take the dirt branch after the bridge and follow it around
   the checkpoint until it rejoins the main road before town.
5. Explode near rural props and confirm trees fall and rocks scatter throughout
   the impact radius.
6. Explode near town buildings and confirm close buildings break into varied
   charred rubble while buildings farther away show cracks but remain standing.
   Confirm some close fragments burn and smoke briefly.
7. Confirm a crater remains at the blast point, including when on a road.
8. Press R and confirm the world and vehicle return to their initial state.
9. Scroll both mouse-wheel directions and confirm the view zooms smoothly,
   remains centered, and stops at its minimum and maximum limits.
10. Drive into each reachable perimeter edge and confirm the visible boundary
   stops the vehicle.
11. Leave the bridge and drive into deep stream water; confirm the vehicle
    sinks, stops responding, cannot explode with Space, and is restored by R.
12. Confirm bridge guardrails line both water-facing edges and stop cars from
    leaving the bridge, while traffic cones move readily when struck.
13. Inspect the main road, bridge, dirt bypass, and town streets and confirm no
    bush overlaps a road surface or shoulder.

## Non-goals

- Generated or hand-authored image assets, procedural infinite terrain,
  interiors, pedestrians, traffic, fire, smoke, structural stress propagation,
  deformable terrain physics, or physically accurate three-dimensional collapse.

## Open decisions

None for this prototype.
