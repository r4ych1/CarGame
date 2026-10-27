# Ambient characters

## Purpose

Give the rural road and town the appearance of an inhabited place using
code-drawn people with lightweight movement and collision behavior.

## Player-visible behavior

- People stand near rural homes and along the road shoulder, with subtle idle
  motion and occasional changes in facing.
- Many people occupy town streets and sidewalks. Some walk short repeating
  paths while others stand, talk in pairs, or perform a small work-like idle
  motion.
- People never spawn in the stream and cannot enter its visible water area;
  they may cross only over the bridge.
- Standing figures read clearly beside cars, with a drawn head-to-foot length
  of about 2 metres, roughly half a traffic car's length, and top-down shading
  that gives their upright pose more depth.
- The living top-down sprite uses a compact rectangular torso with its head
  centered over its forward end. Arms and legs swing forward and backward along
  the facing direction with opposing walking phases.
- A guard's circular head is centered directly on the center of the rectangular
  torso so the soldier reads as a compact top-down unit.
- A compact, soft shadow is cast slightly down and right from each living person
  by the same implied upper-left light source used for cars and scenery. Its
  short offset keeps the figure visually grounded rather than floating.
- People have lightweight physics and can be pushed by vehicles and props.
- A direct car impact transfers a small, capped amount of the vehicle's momentum
  and spin to the person for 0.22 seconds before removing their collision
  response. This gives the impact physical weight without letting the body stay
  pinned to the bumper. The visual fall, flattened body, and persistent blood
  pool remain.
- People within the 20-metre deadly zone are killed and thrown outward.
- People within 10 metres of ground zero burst into separate physics-driven
  body parts. When each part settles, a clearly visible blood pool spreads
  beneath it for about 0.8 seconds and then persists until reset.
- Separated body parts inherit the person's pre-blast motion but receive only
  5 to 14 m/s of additional outward speed, keeping the remains substantially
  closer to the blast than an intact fatal victim.
- People between 20 and 32 metres are knocked away with physics but survive and
  are thrown prone without blood. They lie sprawled for about 2.8 seconds, then
  get up and recover their authored activity. Their prone appearance is about
  60% of their standing visual size.
- Fatal blast launch speed is about 18 m/s at the 32-metre outer edge and up to
  105 m/s near the center. Nonfatal damage-zone launches are stronger: about
  28 m/s near the outer edge and up to 54 m/s near the 20-metre death boundary.
  Both use nonlinear distance falloff.
- Pressing R restores the same people, paths, and living state.
- Walkers move at roughly 1.5 to 2.3 m/s. Panicked people run at about 8 m/s.
- A resident facing a corpse within 8 metres or any living person facing an
  approaching vehicle within 10 metres runs away briefly, whether idle or
  walking. Guards ignore corpses. Vehicle-triggered panic
  chooses a lateral route out of the lane instead of running ahead of the car.
- Living people within 44 metres of an explosion panic and run away. People
  inside the 32-metre impact radius also receive its physical force.
- Panicked people look ahead for town walls, stream banks, and intact buildings
  or shacks, then steer along a clear side route instead of continuing to run
  directly into an obstacle.

## Functional requirements

- People and paths are handcrafted and deterministic.
- Each person is a lightweight dynamic Box2D body; living walkers steer toward
  their authored paths while idle people hold their positions.
- Vehicle, explosion, and corpse panic temporarily override authored movement
  with a flee direction; characters return to their authored activity afterward.
- Panic steering tests the intended route against expanded town-wall, stream-bank,
  and intact-building bounds and deterministically selects a clear angled route
  when the direct route is blocked. Destroyed buildings no longer block a route.
- Stream-bank segments participate in panic route avoidance while leaving the
  bridge opening clear. A post-physics position constraint returns any living
  person displaced into the stream to the nearest bank without affecting travel
  across the bridge.
- Nearby corpses can trigger resident panic when in the character's forward
  view. Guards ignore corpses. Idle residents can enter the same flee state as
  walkers.
- Vehicle impact and blast deaths disable walking behavior. Vehicle-impact
  bodies receive a short momentum transfer and stop colliding after 0.22
  seconds; blast bodies settle through physics and stop colliding after the
  longer fall reaction. Both remain visible until reset.
- Close-range blast victims split into independently simulated limb and torso
  bodies; each leaves a persistent blood pool after settling. Pools are not
  drawn while fragments are airborne, grow for about 0.8 seconds after landing,
  and vary in size with the body part. Fragment bodies stop colliding after the
  brief reaction.
- Draw living poses, fall animations, corpses, and blood pools with Raylib
  primitives and no image assets.
- Update movement, physics response, and animation on the fixed simulation
  timestep; rendering only draws the resulting pose.
- Keep path-following and death state separate from renderer calls.

## Acceptance criteria

- At least 12 people appear along the rural road and near neighboring homes.
- At least 30 people appear within town walls, distributed among multiple
  blocks and streets.
- The population includes standing, talking, working, and walking behaviors.
- A standing person's visible head-to-foot length is about 2 metres, roughly
  half of a 4.2-metre traffic car.
- The head is centered across the torso rectangle, and walking limb swings
  follow the character's direction of travel.
- Every living guard's head circle is centered on its torso rectangle.
- The living-person shadow follows the shared scene-light direction with a
  shorter offset than vehicle and building shadows.
- People remain within their authored paths while alive and stay inside the
  playable area.
- Every authored spawn and walking path lies outside the visible stream, unless
  it is on the bridge. Living people fleeing or displaced by physics remain out
  of the stream.
- Explosions within 20 metres kill people; impacts from 20 to 32 metres throw
  them back with physics and leave them sprawled on the ground without blood
  for about 2.8 seconds before they recover. The prone drawing is about 60% of
  standing size. Nonfatal impact speed ranges from about 28 m/s at the outer
  edge to 54 m/s near the death boundary. Fatal impacts retain their existing
  18 to 105 m/s distance profile.
- People killed within 10 metres split into physics-driven body pieces, each
  leaving a visible, persistent blood pool where it comes to rest. Added
  fragment speed stays between 5 and 14 m/s rather than inheriting the full
  fatal blast launch.
- Residents facing a corpse within 8 metres and people facing an approaching
  vehicle within 10 metres flee, while guards ignore corpses. People within 44
  metres of an explosion panic. Fleeing
  works for idle and walking people. Vehicle-triggered flee direction is lateral
  to the vehicle's travel rather than ahead of it.
- A panicked person's selected direction does not intersect a town wall, stream
  bank, or intact building within the five-metre look-ahead distance when a clear
  angled route is available. Destroying a building removes its navigation bound.
- Walk speed is about 1.5 to 2.3 m/s and panic speed is about 8 m/s.
- Car impacts trigger a brief physical knock, then disable collision after 0.22
  seconds and leave a flattened body with a persistent blood pool so the car can
  finish driving over the person.
- Reset deterministically restores every person, path, and animation state.
- Build succeeds and formatting verification passes.

## Manual verification

1. Inspect the rural road and confirm people stand or walk near the shoulder and
   neighboring homes without covering the drivable lane or standing in water.
2. Inspect the town and confirm a dense population across the streets and
   blocks, with visible idle gestures and short walking routes.
3. Watch for several seconds and confirm limbs, heads, and bodies animate while
   walkers progress along their routes.
4. Hit a person with the car; confirm they receive a brief physical knock before
   the car passes over them without carrying them ahead of the bumper. Confirm
   they fall, remain flattened, and leave a blood pool.
5. Approach a person with the car and confirm they flee sideways out of its path
   rather than running ahead of it.
6. Explode near a group and confirm nearby survivors run away at a clear sprint;
   people nearing town walls or buildings should turn and continue along a clear
   route. Confirm a destroyed building no longer causes people to detour around
   its former footprint.
   Confirm anyone fleeing near the stream follows its bank or crosses the bridge
   rather than entering the water.
7. Confirm people within 10 metres break apart, but their limbs settle much
   closer to ground zero than intact fatal victims. Confirm a blood pool grows
   beneath every settled fragment and remains visible.
8. Confirm people thrown prone look smaller than their standing pose without
   becoming difficult to identify.
9. Leave a corpse near idle and walking residents and confirm those who face it
   flee, while guards remain unaffected by the corpse.
10. Press R and confirm the population returns to its initial living state.

## Non-goals

- Dialogue, schedules, tasks, general-purpose pathfinding beyond short-range
  panic obstacle avoidance and authored loops, or persistent person state
  between resets.

## Open decisions

None for this prototype.
