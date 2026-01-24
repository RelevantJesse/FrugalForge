# Repository Guidelines

## Project Structure & Module Organization

- `src/WowAhPlanner.Core`: Domain models + planning algorithms + port interfaces (no EF Core / ASP.NET references).
- `src/WowAhPlanner.Infrastructure`: EF Core (SQLite) caching, price providers, data pack loader, background workers.
- `src/WowAhPlanner.Web`: Blazor Server UI + minimal API endpoints (wires DI and configuration).
- `tests/WowAhPlanner.Tests`: Unit tests (xUnit).
- `data/{GameVersion}/`: Versioned data packs (e.g. `data/Anniversary/items.json`, `data/Anniversary/professions/*.json`, `data/Anniversary/producers.json`).
- `addon/WowAhPlannerScan`: In‑game scan addon (legacy AH API).
- `docs/`: User-facing documentation and status notes.
- `tools/`: One-off scripts/utilities.

## Build, Test, and Development Commands

- `dotnet build` — builds the full solution.
- `dotnet test` — runs all unit tests.
- `dotnet run --project src/WowAhPlanner.Web` — runs the web app locally.

Tip: if Debug builds fail due to locked DLLs, stop the running `WowAhPlanner.Web` process or build `-c Release`.

## Coding Style & Naming Conventions

- C#: 4-space indentation, idiomatic .NET naming (`PascalCase` types/methods, `camelCase` locals/params).
- Keep clean boundaries: Core must not depend on Infrastructure/Web; use ports in `WowAhPlanner.Core.Ports`.
- JSON packs: stable IDs (e.g. `recipeId`, `producerId`), consistent casing, keep files reasonably small and version-scoped.

## Testing Guidelines

- Framework: xUnit (`[Fact]`).
- Keep tests deterministic: use in-memory repositories/providers (see `tests/WowAhPlanner.Tests/PlannerServiceTests.cs`).
- Naming: `*Tests.cs` with method names describing behavior (e.g. `Excludes_cooldown_recipes_from_planning`).

## Commit & Pull Request Guidelines

- Commit messages in this repo are short and imperative (e.g. “Fix exact search”, “Vendor item support”). Keep them focused and scoped.
- PRs should include:
  - what changed + why
  - any data pack/addon changes and how to validate
  - screenshots for UI changes (Plan/Targets/Upload) when relevant

## Configuration & Safety Notes

- Do not commit secrets/API keys. Use `src/WowAhPlanner.Web/appsettings.json` + user secrets/environment overrides.
- Treat uploaded snapshots as untrusted input: validate schema and realm/version metadata; prefer safe defaults and explicit overrides.
