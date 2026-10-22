# PNG asset workflow

This workflow keeps generated artwork reproducible, reviewable, and safe to replace without losing accepted work.

## 1. Define the asset contract

Before generation, record:

- Gameplay purpose and asset name.
- Exact canvas size and intended in-game size.
- Camera angle and orientation.
- Visual style, palette, and outline treatment.
- Transparency requirement.
- Anchor or pivot point.
- Whether animation frames or variants are required.

Put this contract in the relevant feature spec. Reusable art-direction language belongs in the prompt sidecar.

## 2. Generate working variants

Generate PNG candidates into `assets/work/`, which is intentionally ignored by Git. For a rotatable vehicle sprite, request:

- Orthographic, directly overhead view.
- Vehicle facing straight upward.
- Centered object with padding around its full silhouette.
- Transparent background.
- No text, border, scenery, perspective, or baked directional shadow.
- Consistent lighting and proportions with the established style.

Generate a small number of purposeful variants rather than many nearly identical files.

## 3. Review in context

Review candidates both at full resolution and at their intended display size. Check:

- Silhouette readability.
- Clean alpha edges and absence of background residue.
- Correct orientation, scale, and center.
- Consistency with accepted assets.
- Readability over representative road and terrain colors.
- Rotation without wobble or visible off-center movement.

Do not accept an image solely because it looks good in isolation.

## 4. Prepare the runtime PNG

- Crop or pad to the contracted canvas without changing the intended pivot.
- Preserve full alpha transparency.
- Use a descriptive lowercase kebab-case filename, such as `player-car-blue.png`.
- Place the accepted image under the appropriate `assets/game/` subfolder.
- Add a same-base-name Markdown sidecar under `assets/prompts/`, such as `player-car-blue.md`.

The sidecar records the final prompt, generation date, tool and model when known, canvas size, runtime size, pivot, edits performed, and related feature spec.

## 5. Integrate and verify

Load assets through a centralized asset owner. Unload textures deterministically. Verify the asset in a running build at multiple angles and against representative backgrounds. Record the manual result in the feature handoff.

Accepted PNGs and prompt sidecars are committed together. Rejected candidates and temporary editing files remain in `assets/work/`.

## Prompt sidecar template

```markdown
# Asset name

- Runtime file: `assets/game/...`
- Related spec: `specs/...`
- Generated: YYYY-MM-DD
- Tool/model: ...
- Canvas: ... px
- Runtime size: ... px or world units
- Pivot: center or coordinates
- Post-processing: none or details

## Prompt

Exact final prompt.

## Review notes

Why this variant was accepted and any known limitations.
```
