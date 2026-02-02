# ProfessionLeveler Addon: In-Game Planner Plan (draft)

## Goals
- Let players generate and view profession leveling plans fully in-game using data the addon already captures (price + owned snapshots), with no import/export steps.
- Keep scans, targets, owned capture, and planning self-contained while remaining lightweight enough for the WoW client (avoid UI hitches and excessive memory).

## Functional Scope
- **Targets & scans**
  - Maintain the existing target selection and scan flow; continue saving price snapshots into SavedVariables.
  - Allow re-use of the most recent snapshot on login without re-scanning.
- **Owned materials**
  - Keep owned snapshot capture and per-character breakdown in SavedVariables.
- **Plan generation**
  - Lua-side planner that:
    - Uses latest price snapshot + owned data.
    - Filters to items with either price data or owned counts; gracefully skips missing data and shows a message if insufficient.
    - Supports current profession/realm only (no cross-realm planning).
  - Outputs:
    - Step list (skill ranges, recipe, rank, reagent counts).
    - Shopping list (missing quantities with price per item and total).
    - Owned summary (who has what).
- **Persistence**
  - Save last generated plan in SavedVariables so it survives reload/login.
  - Store metadata (snapshot timestamp, realm, profession) to detect staleness or mismatched realms.
- **Performance safeguards**
  - Limit scope to current profession and a bounded skill window (e.g., current skill → current+150 or up to cap).
  - Use precomputed lookup tables built once per login (items → prices, owned → counts).
  - Avoid iterating entire recipe catalog every frame; compute on-demand and cache results.

## UI / Screens
- **Home panel**
  - Shows snapshot timestamp, realm, profession, price coverage %, owned coverage %.
  - Buttons: “Generate Plan”, “Re-scan”, “Refresh Owned”.
- **Plan panel**
  - Steps list (scrollable): skill range, recipe name, reagents with (owned/need/price).
  - Shopping list: aggregated missing reagents, total cost, per-item tooltip with price source and last-seen time.
  - Badges for data quality: “Using stale snapshot (age hh:mm)”, “Missing prices for 3 items (skipped)”.
- **Targets panel (existing)**
  - Minor copy updates to reflect the new name; optionally show current target set summary.
- **Settings panel**
  - Price rank selection (min/median), skill window size, include intermediates toggle, “warn on stale data” toggle, verbose debug toggle.

## Data & Storage
- **SavedVariables**
  - `ProfessionLevelerScanDB`:
    - `prices`: latest price snapshot per item (bid/buyout/median, seenAt).
    - `owned`: per-character owned counts with realm metadata.
    - `plan`: last generated plan (steps, shopping list, totals, metadata).
    - `settings`: user options (price rank, skill window, intermediates, showPanelOnAuctionHouse, verboseDebug).
    - `debugLog`: existing log buffer (retain).
  - Maintain bridge fields to legacy `WowAhPlannerScan*` names for compatibility.
- **Data inputs**
  - Targets file: `ProfessionLevelerScan_Targets.lua` (already installed by app; keep same format with dual naming).
  - Recipe data: embed a trimmed table for the current game version; keep IDs stable and small to reduce memory.

## Algorithms (in brief)
- Build item → price map from snapshot (respect rank selection).
- Build item → owned count map (aggregate per realm/character; keep per-character details for display).
- For each recipe in scope:
  - Compute reagent need, apply owned offsets, compute cost using available prices only.
  - Skip or flag reagents without price/owned coverage.
- Choose cheapest recipe per skill bracket (reuse app logic, but limit candidates to reduce CPU).
- Aggregate shopping list over chosen recipes; compute totals.

## Edge Cases & Fallbacks
- No snapshot loaded: show “Scan required” and disable Generate Plan.
- Stale snapshot beyond threshold: allow plan, but show warning badge.
- Missing prices for key reagents: skip recipe or mark step incomplete; surface a clear warning.
- Realm/profession mismatch between snapshot and current character: prompt to re-scan or switch targets.

## Full Implementation Plan (waterfall)
1. **Data model & storage**
   - Define SavedVariables schema for FrugalForge (`plan`, `planItems`, `settings`, `meta`), plus compatibility bridges to read existing scanner snapshots/owned.
   - Embed/derive reagent quantities: either (a) consume full recipe data from an embedded compressed table, or (b) extend Targets generator to include reagent quantities per recipe. Choose (a) to avoid desktop dependency in-game.
2. **Data ingest**
   - Snapshot ingest: read latest price snapshot from ProfessionLevelerScanDB/WowAhPlannerScanDB; build item→price map with selected rank.
   - Owned ingest: read owned snapshot; build item→owned map plus per-character breakdown.
   - Targets ingest: read recipe targets with skill bands and reagent quantities.
3. **Planner algorithm (Lua)**
   - For each recipe target:
     - Compute per-reagent need, owned offset, missing quantity, and cost from price map.
     - Mark missing-price reagents; skip recipe if critical data missing.
   - Build cheapest-sequence plan:
     - Sort by minSkill; choose cheapest viable recipe per skill step; respect grayAt.
     - Aggregate shopping list (missing reagents), totals, and owned contributions.
   - Outputs: steps, shopping list, totals, quality flags (stale snapshot, missing prices, partial coverage).
4. **UI/UX**
   - Panels: Snapshot/Owned status, Plan (steps + collapsible reagent details), Shopping list, Warnings.
   - Actions: Generate Plan, Refresh Snapshot/Owned indicators, Copy Plan (optional), Collapse/Expand all.
   - Badges: stale snapshot, missing prices count, owned coverage %, price coverage %.
5. **Performance & safety**
   - Scope to current profession/realm; cap recipes evaluated to target list only.
   - Precompute maps once per generation; avoid per-frame work; keep memory small.
6. **Settings**
   - Price rank (min/median/recent), skill window cap, include intermediates toggle (if target data carries it), stale-hours threshold, verbose debug.
7. **Testing & validation (manual)**
   - Cases: no snapshot, no owned, partial prices, full prices, stale data, missing reagents; ensure UI degrades gracefully.
8. **Docs & versioning**
   - Update README/addon sections with FrugalForge usage and limitations.
   - Bump addon version to 0.2.0 when planner is live; note beta status.
