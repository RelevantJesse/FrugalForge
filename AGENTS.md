# Repository Guidelines

## Project Structure & Module Organization

- `src/WowAhPlanner.Core`: Domain models, planner algorithm, and port interfaces (no EF Core / ASP.NET dependencies).
- `src/WowAhPlanner.Infrastructure`: SQLite EF Core persistence, caching, price providers, data pack loader, background workers.
- `src/WowAhPlanner.WinForms`: WinForms desktop UI (composition root/DI, local state, page controls).
- `tests/WowAhPlanner.Tests`: Unit tests (xUnit).
- `data/{GameVersion}/`: Versioned data packs (e.g. `data/Anniversary/items.json`, `data/Anniversary/professions/tailoring.json`, `data/Anniversary/producers.json`).
- `addon/ProfessionLevelerScan`: In-game scan addon that exports price snapshots.
- `docs/`: Notes, UX decisions, and status docs.
- `tools/`: Utilities/scripts.

## Build, Test, and Development Commands

- `dotnet build WowAhPlanner.slnx` - build the solution.
- `dotnet test` - run all unit tests.
- `dotnet run --project src/WowAhPlanner.WinForms` - run the WinForms app locally.

If Debug builds fail due to locked DLLs, stop the running web process or build with `-c Release`.

## Coding Style & Naming Conventions

- C#: 4-space indentation; use standard .NET naming (`PascalCase` types/methods, `camelCase` locals/params).
- Keep boundaries strict: Core must not reference UI/Infrastructure. Add dependencies via ports in `WowAhPlanner.Core.Ports` and implement them in Infrastructure. UIs (WinForms/Web) reference Core + Infrastructure.
- JSON data packs: keep small and version-scoped; prefer stable identifiers (`recipeId`, `producerId`, `itemId`) and consistent casing.

## Planner Object Model & Cost Semantics (Do Not Guess)

Define these terms explicitly in code and tests; do not rely on implied meanings.

- Item: uniquely identified by `itemId` (stable across data packs); may have zero or more recipes that produce it.
- Base mat: an item that is not craftable in-plan (no recipe available, or crafting disabled by user/settings).
- Intermediate: an item that is craftable and appears as an input (reagent) to at least one recipe in the plan graph.
- Recipe: produces an output `itemId` with an `outputCount` per craft; has reagents (`itemId`, `quantityPerCraft`) and optional skill range metadata.
- Recipe graph: directed edges `outputItemId -> reagentItemId` (with quantities + optional skill range).
- Market price: per-unit buy price; `null`/missing means "unknown price" (not zero).
- Craft cost: cost to obtain 1 unit of an item via a chosen recipe. Compute per craft, then divide by `outputCount`.

Hard rule: selection-time cost vs shopping-list accounting are separate concerns.

- Selection-time effective cost: marginal cost used only to choose buy vs craft for each needed item/quantity (and which recipe to use if multiple). This may optionally apply an "owned discount" depending on settings.
- Shopping list accounting: always reflects real quantities (`needed`, `owned`, `toCraft`, `toBuy`). Owned quantities must remain truthful even if selection treated owned as a discount.

UI checkbox semantics (orthogonal and deterministic):

- Ignore owned mats for selection:
  - Only affects selection-time effective cost calculations (treat owned as 0 for decision-making).
  - Must not change owned quantities in the shopping list; list still shows owned.
- Use current character only:
  - Only affects which owned counts are available (aggregation scope).
  - Must not change market prices or craft-cost computations.
- If both are enabled: selection ignores owned; shopping list uses owned from current character only.

Missing price behavior (must be visible; never silently treated as 0):

- For selection: treat missing buy price as invalid/Infinity.
- If an item has missing buy price but the required quantity can be fully satisfied by owned (under the current owned-scope setting), selection may allow that requirement to be satisfied via owned; outputs must still flag the item as "missing price".
- If "Ignore owned mats for selection" is enabled, owned must not be used to bypass missing-price invalidation during selection.

Recursion/cycles:

- Intermediates may be recursive. Detect cycles in the recipe graph.
- If a cycle is detected for an item, treat that item as non-craftable for planning (fallback to buy if priced; otherwise mark as unknown/unplannable).
- Include a max recursion depth / expansion safeguard to avoid pathological graphs.

Quantities and rounding:

- Crafts occur in whole numbers. `craftCount = ceil(neededUnits / outputCount)`.
- Reagent requirements expand from `craftCount * quantityPerCraft`.
- Ensure rounding is applied before expanding reagents so shopping lists and costs match.

Price snapshots / scanning "includes intermediates":

- Price snapshots should track items that are intermediates even if not directly in the final reagent list.
- Planner must request prices for intermediates encountered during recursive expansion (and their reagents), and surface missing prices clearly.

## Testing Guidelines

- Framework: xUnit (`[Fact]`).
- Prefer deterministic tests: stub providers/repositories; avoid time and filesystem dependencies unless the test is explicitly for loaders.
- Naming: `*Tests.cs` with behavior-focused method names.

## Commit & Pull Request Guidelines

- Commit messages are short and imperative (examples from history: `Fix exact search`, `Vendor item support`, `Docs update`).
- PRs should include: what/why, how to validate (especially for `data/` and `addon/` changes), and screenshots for UI changes when applicable.

## Configuration & Safety Notes

- Do not commit secrets or API keys.
- WinForms stores local app data under `%LOCALAPPDATA%\WowAhPlanner` (SQLite + JSON state).
- Treat uploaded snapshots as untrusted input: validate schema and realm/version metadata; fail closed with clear errors.
