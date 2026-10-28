# Game over, scoring, and run statistics

## Purpose

Track the player's destructive actions and driving run, then present a clear
summary after the player vehicle is disabled or deliberately exploded.

## Player-visible behavior

- A run begins when the game opens and begins again when the player presses R.
- The bottom bar's left region shows only the elapsed run time. Score is
  withheld until the game-over summary.
- The run ends immediately when the player vehicle reaches 100% damage or its
  detonator is activated. The timer and top-speed record freeze at that moment.
- Three seconds after the run ends, a full-screen game-over overlay begins a
  one-second fade-in over the still-visible game world. It does not darken the
  full screen, draw a border, or place a panel behind the summary. The table
  text floats directly over the game world with a subtle dark shadow for
  contrast.
- Gameplay HUD elements begin fading out immediately when the run ends and are
  fully hidden after 0.35 seconds. This is independent of the delayed results
  table. The exploded state does not show a reset prompt on the gameplay HUD;
  reset is prompted by the results table.
- The overlay shows a table with event name, count, point value, and subtotal.
  Civilian and soldier outcomes are separate, as are vehicle, structure, and
  other-scenery outcomes. It also shows the total score, damage taken, top speed,
  final run time, game-over reason, and the R reset instruction.
- After detonation, WASD and arrow keys have no effect. The camera remains fixed
  at the final vehicle/explosion position; mouse-wheel zoom remains available.
- Reset clears the score and statistics, dismisses the overlay, restarts the
  timer, and restores normal vehicle-follow camera behavior.

## Scoring

Each qualifying outcome is awarded once:

- Injure a civilian or soldier with a nonlethal blast: 5 points.
- Kill a civilian or soldier with a lethal blast without limb breakup: 10 points.
- Explode a civilian or soldier into limbs at close blast range: 15 points.
- Run over a living civilian or soldier with the player vehicle: count it as a
  kill worth 10 points, without a separate run-over category.
- Disable another active vehicle with an outer-band blast: 10 points.
- Destroy another active or parked vehicle: 20 points.
- Damage an intact building or shack with an outer-band blast: 10 points.
- Destroy an intact building or shack: 50 points.
- Destroy other scenery, including vegetation and rocks: 5 points.

Traffic vehicles striking people do not award player points. Moving a physics
prop without destroying it does not award points.

## Functional requirements

- The full category breakdown, total score, elapsed time, top speed, terminal
  reason, and time since game over are simulation-owned state; rendering only
  reads them.
- Run time advances from the fixed simulation timestep rather than wall-clock
  frame time.
- Top speed is the greatest finite player speed observed during the current run
  and is reported in kilometres per hour.
- Damage taken is the player vehicle's clamped damage percentage at game over.
- Explosion scoring is calculated before the run is ended so outcomes from the
  terminal detonation are included in the displayed total.
- Score awards are based on state transitions so an already dead person or an
  already damaged/destroyed scenery object cannot score twice.
- The game-over overlay delay is three simulated seconds and its fade duration is
  one simulated second.
- Overlay opacity applies to the results table text and its shadow, with no
  backdrop. The HUD has an independent 0.35-second fade duration beginning at
  the instant the run ends.

## Acceptance criteria

- The timer advances deterministically during play and freezes on disable or
  detonation.
- The score uses the configured values for each supported person, vehicle,
  structure, and scenery outcome, and the terminal explosion's awards appear in
  the total.
- Player run-overs score once; traffic-caused deaths do not score.
- Top speed never decreases during a run and freezes at game over.
- The bottom bar shows time at left but no score. The overlay is invisible before
  three seconds of game-over time, fades to full visibility over the following
  second, and reports every score category in a table plus total score, damage
  taken, top speed, run time, and the terminal reason. The gameplay HUD begins
  fading immediately after either disable or detonation and is hidden well
  before the overlay appears.
- WASD and arrow input cannot move the camera after detonation.
- Reset clears all run state and allows a new run.
- Tests, build, and formatting verification pass.

## Manual verification

1. Drive and confirm the bottom-left run timer advances without displaying score.
2. Run over a person and confirm the score increases by 150 exactly once.
3. Detonate near people and scenery and confirm the score reflects the resulting
   civilian and soldier injuries, deaths, limb explosions, vehicle outcomes,
   and environmental destruction.
4. After detonation, hold WASD and arrow keys and confirm the camera does not pan.
5. Confirm the timer stops and the gameplay HUD begins fading immediately, no
   overlay is visible for three seconds, and the game-over summary then fades in
   with the total score and statistics. Confirm the world remains unobstructed
   behind shadowed text, without a panel, dark full-screen fade, or border.
6. Reach 100% vehicle damage and confirm the disabled reason produces the same
   delayed summary without an explosion.
7. Press R and confirm score, timer, top speed, damage summary, and overlay reset.

## Non-goals

- Persistent high scores, online leaderboards, score multipliers, combos, ranks,
  bonuses for traffic damage, or scoring movable props that are merely pushed.
- Pausing the aftermath simulation when the run ends.

## Open decisions

None for this increment. Point values and overlay timing can be retuned later.
