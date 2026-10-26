# Destructible driving environment

## Purpose

Replace the empty test grid with a compact code-drawn rural road and town that
provides scenery, collision, and deterministic blast destruction.

## Player-visible behavior

- The car begins on a curved two-lane road that approaches a small town.
- Rural space contains a winding stream, two road bridges, trees, bushes,
  rocks, and sparse buildings. Town buildings form rows around streets.
- A solid wall encloses every town building and its rows. The wall has one
  8-metre-wide entrance aligned with the approaching main road on its southern
  side, blocked by one movable guardrail section.
- Shortly before the town entrance, a checkpoint places two movable guardrails
  across opposite lanes at staggered distances. Their alternating openings form
  a driveable zigzag. A small destructible checkpoint shack stands beside the
  road without blocking that path.
- Intact trees, rocks, and buildings block the car. Bushes are visual foliage
  and do not block it. Roads and bridges remain driveable.
- The car explosion affects objects within 35 metres. Trees become dynamic
  fallen trunks, rocks are thrown, and buildings split into rigid debris that
  settles as rubble.
- Each explosion leaves a persistent crater centered on the car. Roads can be
  visually cratered but remain driveable in this prototype.
- R restores the vehicle and the entire undamaged environment.
- A visible solid perimeter encloses the complete environment and prevents the
  vehicle or dynamic debris from leaving the playable area.
- The mouse wheel zooms the camera in and out while preserving its current
  target. Zoom is clamped between 0.35x and 2.5x.

## Functional requirements

- World layout is handcrafted and deterministic, measured in metres, and
  contains no image assets.
- Static colliders exist for intact solid props. Destruction removes each
  affected collider before creating dynamic debris bodies.
- Town walls are permanent static colliders and are immune to explosions.
- Blast impulse decreases linearly with distance and points away from the
  explosion center. Dynamic debris receives explicit ground resistance.
- Environment ownership, reset, and Box2D cleanup are separate from vehicle
  dynamics and rendering.
- The playable rectangle spans X -220 to 220 metres and Y -380 to 70 metres.
  Four static 4-metre-thick boundary walls sit along its edges and are immune
  to explosions and environment reset.
- Camera zoom changes in multiplicative 12% steps per wheel notch and remains
  a rendering concern; it does not affect simulation or input forces.

## Acceptance criteria

- The map includes a curved main road, town streets, stream, two bridges,
  rural vegetation and rocks, sparse outskirts buildings, and at least two
  rows of town buildings.
- Every town-row building lies inside the wall, whose only opening contains one
  physical guardrail spanning the road entrance.
- The checkpoint has exactly two staggered lane barriers and one roadside shack;
  the intact car can zigzag between the barriers without leaving the road.
- Intact solid objects collide with the vehicle.
- Objects inside 35 metres transition exactly once to destroyed state; objects
  outside remain intact.
- Destroyed buildings produce multiple independently simulated pieces, trees
  produce a knocked-over trunk, rocks become dynamic, and a crater is recorded.
- Debris motion is finite, outward, slows over time, and reset restores the
  original deterministic world.
- Mouse-wheel input changes camera zoom within the specified limits without
  changing the camera target.
- The vehicle and dynamic debris collide with every perimeter edge and cannot
  travel indefinitely beyond the rendered environment.
- Build, tests, and formatting verification pass.

## Manual verification

1. Drive along the curved road, across a bridge, and toward the town.
2. Confirm trees, rocks, and buildings block the car while bushes do not.
3. Zigzag through the two checkpoint barriers, then push or blast the town
   entrance guardrail aside, and
   confirm the remaining wall has no other opening.
4. Explode near rural props and confirm trees fall and rocks scatter.
5. Explode beside town buildings and confirm walls separate and settle as
   rubble while distant buildings remain intact.
6. Confirm a crater remains at the blast point, including when on a road.
7. Press R and confirm the world and vehicle return to their initial state.
8. Scroll both mouse-wheel directions and confirm the view zooms smoothly,
   remains centered, and stops at its minimum and maximum limits.
9. Drive into each reachable perimeter edge and confirm the visible boundary
   stops the vehicle.

## Non-goals

- Generated or hand-authored image assets, procedural infinite terrain,
  interiors, pedestrians, traffic, fire, smoke, structural stress propagation,
  deformable terrain physics, or physically accurate three-dimensional collapse.

## Open decisions

None for this prototype.
