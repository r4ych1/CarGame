# CarGame

A spec-driven 2D top-down driving game proof of concept.

## Technology

- C# and .NET 8
- Raylib-cs 8.1.0 for rendering, input, audio, and window management
- Box2D.NET 3.1.654 for 2D collision and rigid-body physics
- Generated transparent PNG assets following [`docs/asset-workflow.md`](docs/asset-workflow.md)

## Getting started

```powershell
dotnet restore
dotnet build CarGame.sln
dotnet run --project src/CarGame/CarGame.csproj
```

Open the repository folder or `CarGame.sln` in Visual Studio Code. Feature work begins with a focused specification under `specs/`.

## Repository layout

```text
assets/
  game/       Accepted runtime assets
  prompts/    Reproducible image prompts and provenance
docs/         Project workflows and technical notes
specs/        Feature behavior and acceptance criteria
src/CarGame/  Game executable
```

## Technical documentation

- [`docs/vehicle-dynamics.md`](docs/vehicle-dynamics.md) explains the reusable
  vehicle definition, axle-load, slip-angle, and friction-circle model.
- [`docs/asset-workflow.md`](docs/asset-workflow.md) defines generated PNG asset
  intake and provenance requirements.
