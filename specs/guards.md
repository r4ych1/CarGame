# Armed guards

## Purpose

Add a small armed guard presence around the checkpoint and town entrance. Guards
threaten the player's car, make nearby residents react to gunfire, and respond
differently from civilians when an explosion happens outside its damage radius.

## Player-visible behavior

- Three guards stand around the roadside checkpoint and three stand just inside
  the town entrance.
- At their posts, guards are deterministically varied between standing watch and
  walking a small perimeter so the group does not remain uniformly still.
- Living, upright guards within 36 metres turn toward the intact player car and
  fire a rifle about once per second only when no solid scenery, prop, traffic
  vehicle, or town wall blocks sight. A short muzzle flash and a fast tracer make
  every shot readable; a round that reaches the car produces a brief impact spark.
- Seeing the car within 36 metres makes a guard aggressive. The guard remains
  focused on the car through temporary loss of sight and only disengages when the
  car leaves that range, explodes, or sinks. A disengaging guard walks back to
  their authored post before resuming their standing or perimeter activity.
- Rounds collide with intervening solid objects and walls instead of travelling
  through them if something moves into the firing line.
- Gunshots do not add vehicle health or a game-over state in this increment.
- Non-guard people within 22 metres of a shot run away from the guard who fired.
- Guards use the same physical body and death behavior as other people, so the
  player and traffic can run them over.
- Guards inside the 32-metre explosion damage radius use the existing person
  response: they die within the expanded 20-metre deadly zone or are thrown down
  in the outer damage band, and living guards flee away after recovering.
- Guards outside the damage radius run toward the blast to investigate, stopping
  about 8 metres from its center, then continuously walk a small perimeter around
  ground zero. Investigation overrides their original post until reset.
- A guard that sees an approaching car within 10 metres stops firing and flees
  laterally out of its path. Walls and solid objects block this sight check.
- Pressing R restores guard positions, life state, investigation state, and all
  rounds.

## Functional requirements

- A character has an explicit resident or guard role; shared collision, blast,
  stream, death, and rendering state remains in the character type.
- Guard placement and fire timing are handcrafted and deterministic.
- The stand-or-patrol choice is pseudo-random but deterministic for each authored
  guard, and includes both behaviors in the six-guard group.
- Guard targeting and projectile updates run on the fixed simulation timestep.
- Aggression is acquired only from unobstructed sight. Once acquired, obstruction
  prevents firing but does not clear aggression while the car remains in range.
- Rounds use deterministic two-dimensional segment-versus-oriented-car collision
  plus segment-versus-obstacle collision, and are removed after impact or after
  travelling beyond their useful lifetime.
- A dead, knocked-down, panicking, or investigating guard cannot fire.
- Gunfire panic temporarily overrides a resident's authored activity and uses the
  existing wall-aware flee steering.
- Investigation uses the existing wall-aware steering and does not enter the
  visible stream outside the bridge. After reaching ground zero, the guard keeps
  walking around it rather than stopping.
- Rendering uses Raylib primitives for uniforms, rifles, muzzle flashes, tracers,
  and impact sparks; no image assets are added.

## Acceptance criteria

- Exactly six guards spawn: three in the checkpoint area and three inside the
  town entrance. At least one stands watch and at least one walks a small post
  perimeter.
- A guard within 36 metres of an intact player car faces it and produces rounds;
  no rounds are produced when the car is exploded, sunk, out of range, or hidden
  by a solid object or wall.
- After first seeing the car, a guard remains aggressive while it stays within
  36 metres even if cover temporarily blocks sight. Leaving the range clears
  aggression, and a displaced guard walks back to the original post.
- A fired round stops at the first solid obstruction or at the player's car,
  whichever it reaches first.
- A shot causes living residents within 22 metres to panic away from its source.
- A round crossing the player's oriented chassis is marked as an impact and does
  not continue through the car.
- Guards can be killed by a moving vehicle and use the same persistent corpse and
  blood behavior as residents.
- A surviving guard inside 32 metres flees an explosion; a guard outside
  32 metres investigates it instead and patrols around ground zero after arriving.
- A guard with unobstructed sight of an approaching car within 10 metres flees;
  a wall-hidden car does not trigger that response.
- Reset restores all six guards and clears active rounds.
- Tests, build, and formatting verification pass.

## Manual verification

1. Drive north to the checkpoint and confirm three armed guards are visible there,
   followed by three more just inside the town entrance.
2. Enter a guard's range and confirm the guard turns, fires readable tracers, and
   produces sparks when rounds reach the car.
3. Put a building, prop, traffic vehicle, and town wall between the car and a
   guard; confirm the guard does not fire through them and moving cover catches
   an already-fired round.
4. Approach a guard directly with a clear path and confirm they flee sideways
   when the car gets close; repeat with a wall between them and confirm they do
   not react through it.
5. Let a guard see the car, briefly move behind cover while remaining within
   36 metres, and confirm the guard stays alert but does not shoot through cover.
   Leave the range and confirm the guard walks back to their original post.
6. Observe nearby residents when firing starts and confirm they scatter using
   clear routes around walls.
7. Run over a guard and confirm the same brief physical knock, flattened corpse,
   and blood pool used for other people.
8. Explode with a guard in the outer damage band and confirm the survivor is
   thrown down and then flees. Observe a more distant guard and confirm they run
   toward the blast, stop short of its center, and continue walking around it.
9. Press R and confirm all guards return to their posts and all rounds disappear.

## Non-goals

- Player vehicle health, bullet damage, game-over behavior, guard cover tactics,
  patrol routes, reloading, ammunition, return fire, sound, or general pathfinding.

## Open decisions

None for this increment.
