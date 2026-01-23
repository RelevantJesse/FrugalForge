# Phase 2 Plan: “Full Featured” (starting with Anniversary / TBC)

This app is designed around **versioned data packs** + **pluggable, failure-tolerant price providers**. To reach “all professions / all recipes” and “real-ish time AH pricing”, Phase 2 focuses on:

1) a repeatable **recipe data pipeline** for Anniversary (TBC-oriented)
2) at least one **real price ingestion** path that can refresh ~hourly and safely fall back to cached data

## Goals (Phase 2)

### Recipe data (Anniversary first)
- Generate complete profession recipe packs for Anniversary (TBC prepatch → TBC).
- Keep packs versioned and reproducible: include metadata like build number/date and generator version.
- Enforce validation (no unknown reagent itemIds, no missing required fields, no duplicates).

### Auction pricing (real-ish time, hourly OK)
- Refresh prices on a schedule (hourly by default).
- Providers may fail: the app must keep running, show **stale/unavailable** status, and block planning if required item prices are missing and there is no cache.
- Support multiple providers with fallback + user selection later.

## Work breakdown

### 1) Anniversary data pack pipeline (recommended approach)
Build a `DataPackBuilder` tool (console app) that produces:
- `data/Anniversary/items.json`
- `data/Anniversary/professions/*.json`

Inputs should be **client-derived exports** for the target build (preferred because it is complete and consistent). The tool should accept exported tables (CSV/JSON) and perform mapping:
- recipes/spells → profession
- spell reagents → reagent itemIds/qty
- required skill and difficulty thresholds

Deliverables:
- `tools/WowAhPlanner.DataPackBuilder` (new project)
- `docs/datapacks.md` describing: required inputs, how to run the builder, and how packs map to Classic/TBC builds
- CI validation: builder can validate packs without needing the game installed

### 2) Real-ish time pricing ingestion
Pick a “real” ingestion path and implement it behind `IPriceProvider`:

Option A (recommended for reliability): **User-uploaded snapshots**
- Provide an endpoint/UI to upload a JSON snapshot per realm
- Store snapshot into SQLite (so refresh doesn’t depend on the provider always being up)
- Easy to refresh hourly with an in-game scan + small converter script

Option B: **TSM API** (requires API key, may not be realm-specific depending on endpoint)
- Implement as optional provider with explicit configuration
- Keep StubJson available for deterministic/dev use

Deliverables:
- one real provider implementation + wiring
- background worker enabled via config to refresh realms hourly
- UI clearly shows provider + snapshot timestamp + stale/unavailable

### 3) Planner + UX upgrades (from Plan.md V2 list)
- Owned mats input (subtract from shopping list)
- Export shopping list (CSV/text)
- Alternative objectives toggles (gold vs crafts vs “safer”)

## Current status (end of MVP)
- Versioned data packs load from `data/{version}/...` (currently `Era` sample).
- StubJson provider + SQLite cache works and tolerates provider failure.
- Planner works end-to-end and fails with missing itemIds when required prices are unavailable.

