# Vehicle damage and HUD

## Purpose

Add a deterministic player-car damage system that rewards clean driving and
avoiding guard fire, then present the essential driving state in a compact HUD.

## Player-visible behavior

- The intact player car has damage from 0% to 100%. Reset restores 0% damage.
- Colliding with an intact building, shack, parked vehicle, or traffic vehicle
  adds damage based on the collision approach speed. Impacts at or below 4 m/s
  cause no damage; faster impacts progress from minor through moderate to severe.
- Traffic cones, guardrails, people, trees, rocks, debris, town walls, stream
  boundaries, and the playable perimeter do not damage the player car.
- Each guard round that reaches the player car adds 8% damage.
- At 100% damage the car is disabled and no longer responds to drive input. It
  remains physical and can still be deliberately detonated with Space.
- At 100% damage, dark smoke rises from the engine and the windshield shows
  visible cracks. The HUD does not add a disabled instruction message.
- A translucent full-width bar at the bottom keeps speed fixed in the center.
  Smaller RPM and Gear readouts are stacked at the left, and a right-side HP bar
  provides the damage indicator without showing a numeric damage percentage.
  A separate translucent sidebar lists WASD / Arrows Drive,
  Mouse-wheel Zoom, Space Detonator, and R Reset.
- Exploded and sunk states remain visible in the bottom status bar.

## Functional requirements

- Collision damage consumes Box2D hit events after the fixed physics step and
  uses the reported positive approach speed in metres per second.
- Only contacts between the player chassis and an eligible car or intact
  structure can cause collision damage.
- Collision damage is a deterministic piecewise-linear function: 0 damage at
  4 m/s or below, up to 8 damage at 10 m/s, up to 35 damage at 18 m/s, and up
  to 70 damage at 28 m/s or above.
- Damage is clamped to the inclusive range 0 to 100.
- A guard round distinguishes a vehicle impact from an obstacle impact and
  applies bullet damage exactly once while its impact spark remains visible.
- Simulation owns damage state; rendering only reads the resulting values.
- HUD fields use fixed regions so changing RPM digits or gear does not shift the
  speed readout or neighboring fields.

## Acceptance criteria

- A collision at 4 m/s or below adds no damage, while 10, 18, and 28 m/s
  impacts calculate approximately 8, 35, and 70 damage respectively.
- Eligible structures and cars cause collision damage; cones and guardrails do
  not.
- Repeated confirmed guard hits add 8% each without applying more than once per
  round.
- Damage never exceeds 100%, reaching 100% prevents propulsion, and Space can
  still detonate the disabled car.
- Reset clears damage and restores drive control.
- The bottom HUD contains only the centered primary status fields plus an
  exploded or sunk status when applicable. Controls appear in the sidebar.
- Speed remains visually fixed as RPM and gear change; RPM appears above Gear on
  the left, and the right-side HP bar shrinks from full to empty with damage.
- A disabled intact car has engine smoke and windshield cracks but no disabled
  instruction text.
- Tests, build, and formatting verification pass.

## Manual verification

1. Nudge a building below roughly 14 km/h and confirm Damage remains 0%.
2. Strike a building and a moving or parked car at increasing speeds and confirm
   progressively larger damage increases.
3. Hit cones and guardrails at speed and confirm Damage does not change.
4. Let guards hit the car and confirm Damage rises by 8% per visible hit.
5. Reach 100% damage, confirm drive controls stop propelling the car, then press
   Space and confirm the detonator still works.
6. Press R and confirm the car returns with 0% damage and working controls.
7. Confirm the bottom status bar is translucent and centered, and the control
   sidebar contains all four requested control descriptions.
8. Change speed and gears and confirm the HUD regions do not shift. Disable the
   car and confirm the HP bar is empty, engine smoke and windshield cracks are
   visible, and no disabled instruction text appears.

## Non-goals

- Component-specific damage, repair, armor, deformation, damage-dependent
  handling before 100%, automatic explosion, game-over screens, or persistence.

## Open decisions

None for this increment.
