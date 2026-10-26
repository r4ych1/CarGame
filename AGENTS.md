# CarGame repository instructions

## Project purpose

CarGame is an incrementally developed 2D top-down driving game proof of concept. It uses C# on .NET 8, Raylib-cs for rendering, input, and audio, Box2D.NET for 2D rigid-body physics, and generated PNG artwork. Keep the project small, understandable, and runnable throughout development.

## Sources of truth

- Read this file before making repository changes.
- Read the relevant file under `specs/` before implementing a feature.
- A feature spec owns its intended behavior, acceptance criteria, and explicit non-goals.
- The live code and observed command output own implementation and verification status. Do not claim planned behavior is implemented.

## Spec-driven workflow

For each meaningful feature:

1. Inspect the repository and identify the affected systems.
2. Create or update a focused specification in `specs/` before implementation.
3. Record observable acceptance criteria and non-goals in that specification.
4. Resolve materially ambiguous gameplay or visual decisions with the user.
5. Implement only the agreed scope.
6. Add or update tests for deterministic non-visual behavior where practical.
7. Run focused checks, then the broader verification commands below.
8. Report observed results and remaining limitations; stop before beginning unrelated work.

Do not invent a long-term roadmap or implement anticipated features without an approved specification.

## Architecture boundaries

- Keep game rules and vehicle behavior independent from Raylib drawing calls where practical.
- Keep rendering and input concerns separate from Box2D world and body management.
- Use meters for Box2D simulation and pixels for rendering. Put conversion in one shared boundary; do not scatter scale constants.
- Run physics on a fixed timestep. Rendering may run independently and should not pass variable frame time directly to Box2D.
- A top-down car must model longitudinal drive, steering, lateral tire grip, drag, and drift explicitly. Do not rely on Box2D contact friction as road friction.
- Prefer small, cohesive types over a growing monolithic `Program.cs`.
- Use `System.Numerics.Vector2` at application boundaries unless a library API requires another vector type.

## C# conventions

- Target .NET 8 and keep nullable reference types enabled.
- Follow `.editorconfig`; use file-scoped namespaces and four-space indentation.
- Use clear domain names instead of abbreviations.
- Avoid hidden global mutable state.
- Dispose or unload native graphics resources deterministically.
- Add comments for intent or non-obvious physics reasoning, not line-by-line narration.

## Physics and gameplay verification

- Keep simulation tests deterministic; do not depend on wall-clock timing.
- Prefer unit tests for coordinate conversion, steering math, grip, acceleration, and state transitions.
- Visual or handling changes require a short manual verification checklist in their spec.
- When tuning values, state the units and keep related parameters together.

## Visual assets

- Render project visuals in code. Do not add generated PNG artwork or an image-asset generation workflow.

## Commands

Run from the repository root:

```powershell
dotnet restore
dotnet build CarGame.sln
dotnet run --project src/CarGame/CarGame.csproj
dotnet test CarGame.sln
dotnet format CarGame.sln --verify-no-changes
```

Use the smallest relevant checks during iteration. Before handing off code, run `dotnet build CarGame.sln`; run tests and formatting verification when their inputs changed.

## Version-control boundaries

- Preserve user changes and keep unrelated edits out of the current task.
- Inspect `git status` and the relevant diff before reporting completion.
- Do not commit, push, rewrite history, or create or delete branches unless the user explicitly requests it.
- Never commit credentials, local caches, build output, or rejected generated-asset experiments.
