# WoW Classic Auction House Profession Planner

Windows desktop (WinForms) app that loads versioned profession recipe data packs, ingests auction pricing snapshots, caches price summaries in SQLite, and generates a cheapest-expected-cost profession leveling plan + shopping list.

## Quick start (recommended)

1) Go to the repo's **Releases** page
2) Click the latest version and download the `.zip`
3) Extract the zip somewhere (like your Desktop) and open the extracted folder
4) Double-click `WowAhPlanner.WinForms.exe` (or `run.cmd`)

Data is stored under `%LOCALAPPDATA%\WowAhPlanner` (SQLite + JSON state). The `addon/WowAhPlannerScan` folder is bundled inside the zip.

### Install the in-game addon

1) Copy `addon/WowAhPlannerScan/` into your WoW Anniversary AddOns folder, for example:
   - `...\World of Warcraft\_anniversary_\Interface\AddOns\`
2) In the app, open **Targets** and click **Install targets** (writes `WowAhPlannerScan_Targets.lua` into the add-on folder).
3) In WoW, `/reload` so the addon picks up the targets.

### Scan + upload prices (no copy/paste)

1) In-game at the Auction House: run a scan with the addon.
2) `/reload` so SavedVariables are written.
3) In the app, open **Upload**:
   - Point to your `WTF\...\SavedVariables\WowAhPlannerScan.lua`
   - Click **Load + Upload** (or load into the textarea, then Upload)

### Owned materials

1) In-game: `/wahpscan owned` then `/reload`.
2) In the app, open **Owned**:
   - Point to the same SavedVariables file
   - Click **Load + Save** to store owned mats (per realm) and per-character breakdown.

### Build a plan

1) **Home**: pick Game Version, Region, Realm.
2) **Plan**: select profession, current/target skill, price mode (Min/Median), toggle owned materials.
3) Click **Generate plan**. Steps + shopping list will only include items with AH/vendor prices or owned coverage.

## Build

`dotnet build WowAhPlanner.slnx`

## Run (local dev)

`dotnet run --project src/WowAhPlanner.WinForms`

## Download + run (no .NET install)

GitHub Releases include a self-contained Windows build. Download the zip, extract it somewhere writable, then double-click `WowAhPlanner.WinForms.exe` (or `run.cmd`). The in-game addon is included at `addon/WowAhPlannerScan`.

## Test

`dotnet test`

## Sample data

- Profession + items data packs: `data/Era/items.json`, `data/Era/professions/cooking.json`
- Anniversary packs (active development): `data/Anniversary/items.json`, `data/Anniversary/professions/*.json`, `data/Anniversary/producers.json`
- Deterministic stub prices:
  - `data/Era/stub-prices.json`
  - `data/Anniversary/stub-prices.json`

## In-game scanning + upload workflow (WinForms)

- Targets:
  - App **Targets** -> **Install targets** (writes `WowAhPlannerScan_Targets.lua` into the add-on folder).
  - `/reload` in-game.
- Scan:
  - In-game AH addon: scan (default +100 skill window).
  - `/reload` so SavedVariables are written.
- Upload:
  - App **Upload** -> point to `WowAhPlannerScan.lua` -> **Load + Upload**.

Addon docs: `docs/addon.md`

## Owned materials workflow (WinForms)

- In-game: `/wahpscan owned`, then `/reload`.
- App **Owned**:
  - Point to `WowAhPlannerScan.lua`.
  - **Load + Save** (stores per realm; keeps per-character breakdown so Plan can show “owned by”).

## Phase 2

See `Phase2.md` for the plan to scale Anniversary/TBC and beyond (full recipe packs + additional price ingestion options).

## Tests included

- Planner chooses cheapest recipe: `tests/WowAhPlanner.Tests/PlannerServiceTests.cs`
- Shopping list quantity aggregation: `tests/WowAhPlanner.Tests/PlannerServiceTests.cs`
- Data pack loader validation (missing required fields): `tests/WowAhPlanner.Tests/DataPackLoaderTests.cs`
