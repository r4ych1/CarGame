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
- Standing figures read clearly beside cars, with a drawn head-to-foot length
  of about 2 metres, roughly half a traffic car's length, and top-down shading
  that gives their upright pose more depth.
- The living top-down sprite uses a compact rectangular torso with its head
  centered over its forward end. Arms and legs swing forward and backward along
  the facing direction with opposing walking phases.
- People have lightweight physics and can be pushed by vehicles and props.
- A direct car impact starts a brief fall animation, then leaves a flattened
  body and a persistent blood pool.
- After the fall reaction, the body stops blocking vehicles so cars can pass
  over it.
- People within the 16-metre deadly zone are killed and thrown outward.
- People within 10 metres of ground zero burst into separate physics-driven
  body parts. When each part settles, a clearly visible blood pool spreads
  beneath it for about 0.8 seconds and then persists until reset.
- Separated body parts inherit the person's pre-blast motion but receive only
  5 to 14 m/s of additional outward speed, keeping the remains substantially
  closer to the blast than an intact fatal victim.
- People between 16 and 32 metres are knocked away with physics but survive and
  are thrown prone without blood. They lie sprawled for about 2.8 seconds, then
  get up and recover their authored activity. Their prone appearance is about
  60% of their standing visual size.
- Fatal blast launch speed is about 18 m/s at the 32-metre outer edge and up to
  105 m/s near the center. Nonfatal damage-zone launches are stronger: about
  28 m/s near the outer edge and up to 68 m/s near the 16-metre death boundary.
  Both use nonlinear distance falloff.
- Pressing R restores the same people, paths, and living state.
- Walkers move at roughly 1.5 to 2.3 m/s. Panicked people run at about 8 m/s.
- A person facing a corpse within 8 metres or an approaching vehicle within
  10 metres runs away briefly, whether idle or walking.
- Living people within 44 metres of an explosion panic and run away. People
  inside the 32-metre impact radius also receive its physical force.

## Functional requirements

- People and paths are handcrafted and deterministic.
- Each person is a lightweight dynamic Box2D body; living walkers steer toward
  their authored paths while idle people hold their positions.
- Vehicle approach and blast panic temporarily override authored movement with
  a flee direction; characters return to their authored activity afterward.
- Nearby corpses can trigger panic when in the character's forward view. Idle
  characters can enter the same flee state as walkers.
- Vehicle impact and blast deaths disable walking behavior. Dead bodies settle
  through physics, stop colliding after the fall reaction, and remain visible
  until reset.
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
- People remain within their authored paths while alive and stay inside the
  playable area.
- Explosions within 16 metres kill people; impacts from 16 to 32 metres throw
  them back with physics and leave them sprawled on the ground without blood
  for about 2.8 seconds before they recover. The prone drawing is about 60% of
  standing size. Nonfatal impact speed ranges from about 28 m/s at the outer
  edge to 68 m/s near the death boundary. Fatal impacts retain their existing
  18 to 105 m/s distance profile.
- People killed within 10 metres split into physics-driven body pieces, each
  leaving a visible, persistent blood pool where it comes to rest. Added
  fragment speed stays between 5 and 14 m/s rather than inheriting the full
  fatal blast launch.
- People facing a corpse within 8 metres or an approaching vehicle within
  10 metres flee, and people within 44 metres of an explosion panic. Fleeing
  works for idle and walking people.
- Walk speed is about 1.5 to 2.3 m/s and panic speed is about 8 m/s.
- Car impacts trigger the fall animation and leave a flattened body with a
  persistent blood pool.
- A fallen body no longer blocks a car after the fall reaction completes.
- Reset deterministically restores every person, path, and animation state.
- Build succeeds and formatting verification passes.

## Manual verification

1. Inspect the rural road and confirm people stand or walk near the shoulder and
   neighboring homes without covering the drivable lane.
2. Inspect the town and confirm a dense population across the streets and
   blocks, with visible idle gestures and short walking routes.
3. Watch for several seconds and confirm limbs, heads, and bodies animate while
   walkers progress along their routes.
4. Hit a person with the car; confirm they fall, remain flattened, and leave a
   blood pool.
5. Approach a person from the direction they are facing; confirm they run away.
6. Explode near a group and confirm nearby survivors run away at a clear sprint.
7. Confirm people within 10 metres break apart, but their limbs settle much
   closer to ground zero than intact fatal victims. Confirm a blood pool grows
   beneath every settled fragment and remains visible.
8. Confirm people thrown prone look smaller than their standing pose without
   becoming difficult to identify.
9. Leave a corpse near idle and walking people; confirm those who face it flee.
10. Press R and confirm the population returns to its initial living state.

## Non-goals

- Dialogue, schedules, tasks, pathfinding beyond authored loops, or persistent
  person state between resets.

## Open decisions

None for this prototype.
