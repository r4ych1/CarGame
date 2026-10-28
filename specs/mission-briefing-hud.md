# Mission briefing HUD

## Purpose

Give the player an immediate objective and route warning without interrupting
driving or requiring an external image asset.

## Player-visible behavior

- A compact mission briefing appears in the upper-left gameplay HUD.
- A drawn handheld walkie-talkie visually identifies the briefing as a radio
  message.
- The briefing reads: "Advance up the main road to the enemy base and complete
  the mission. Watch out for the checkpoint."
- The briefing remains fully visible while the car is stationary. Once the car
  first exceeds 0.5 metres per second, it fades away over 0.75 seconds and stays
  hidden for the rest of that run.
- Reset restores the briefing at full opacity. While visible, it also respects
  the gameplay HUD's game-over fade.
- The panel does not overlap the upper-right controls panel or bottom driving
  instruments at the standard 1280 by 720 window size.

## Functional requirements

- Render the walkie-talkie and briefing entirely with Raylib drawing primitives
  and text. Do not add image assets.
- Keep this presentation in the renderer; it does not create simulation or
  mission-completion state.
- Keep the one-run dismissal state in the renderer and reset it with the other
  visual effects.
- Split the instruction into short fixed lines that remain inside the panel.

## Acceptance criteria

- During active play, the upper-left HUD clearly shows a walkie-talkie,
  a `MISSION` heading, the route objective, and the checkpoint warning.
- The briefing is readable at the standard window size and does not overlap
  existing HUD regions.
- Moving the player car dismisses the briefing smoothly, and resetting the run
  restores it.
- Build and formatting verification pass.

## Manual verification

1. Start a run and confirm the mission briefing is immediately visible in the
   upper-left corner.
2. Confirm the walkie-talkie silhouette and its antenna, speaker, display, and
   controls are recognizable.
3. Read every instruction line and confirm none extends outside the panel.
4. Begin driving and confirm the complete briefing fades away, remains hidden
   after stopping, and returns after reset.

## Non-goals

- Mission progression, objective markers, voice playback, radio animation,
  dismissing the briefing, or responsive layouts for other window sizes.
