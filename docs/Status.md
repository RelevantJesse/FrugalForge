# Project Status (Jan 2026)

This repo contains **FrugalForge** (in-game addon) to generate profession leveling plans using real-ish time auction pricing.

## Where we're at

### Addon (FrugalForge + FrugalScan)
- Targets are generated in-game via **Build Targets** in `/frugal`.
- Uses legacy AH APIs (`QueryAuctionItems`) and UI-driven search because `C_AuctionHouse` is unavailable on this client.
- Uses **quoted searches** (`"Item Name"`) to force exact name matching on the browse UI.
- Uses **buyout only** (ignores bid-only auctions).
- Uses configurable **nth-cheapest** (default: 3rd) instead of cheapest to reduce outliers.
- Has a small AH panel UI + Settings entry.
- Supports:
  - quick one-off scans: `/frugalscan item <itemId|itemLink>`
  - owned export (Bagnon/BagBrother): `/frugalscan owned`

## Things we learned (and baked in)
- **Legacy AH querying is rate-limited** and can be slow; `CanSendAuctionQuery` may flip false for long periods. The addon must be patient and back off.
- **Exact matching matters.** Without quotes/exact-match UI, searches return partial-name items ("Thick Leather Ammo Pouch").
- **Bid-only auctions are misleading for reagent pricing.** Using buyout-only avoids underpricing.
- **Cheapest listing can be an outlier.** The nth-cheapest strategy (default 3rd) is a good "cheap but not insane" heuristic.
- **Intermediates shouldn't be purchased blindly.** Expanding craftables/smelts produces more realistic shopping lists.
- **Time-gated crafts must be excluded.** Cooldowns (Mooncloth etc.) can otherwise dominate a "cheapest plan" incorrectly.

## Data workflow (current)

1) In-game: open `/frugal`, choose profession, and **Build Targets**.
2) At the AH: click **Scan** or `/frugalscan start`, or scan one item with `/frugalscan item ...`.
3) If you want owned materials, run `/frugalscan owned` (requires BagBrother).

## Protecting against bad uploads (current)
- Targets embed `(region/version/realmSlug)` and the addon includes these fields in exported snapshots.
- Upload UI validates snapshot metadata against current selection by default and requires an explicit override to accept mismatches.
- Uploads are aggregated across the last few uploads (median per item) to reduce outliers/trolling.

If this is hosted publicly later: add rate limits, stronger auth, and/or signed uploads; JSON metadata validation alone cannot stop intentional spoofing.

## Enhancement ideas (next)

### Near-term
- Better Intermediates presentation: group by kind and show underlying reagent expansion ("you will need X ore to smelt Y bars").
- Recipe availability sources (trainer/vendor/drop/quest) and UI hints for acquiring missing recipes.
- Smarter pricing heuristics: min/median/trimmed mean, "ignore 1c troll listings", configurable percentiles.

### Medium-term
- Snapshot history + trend charts per item and per realm.
- Multi-character owned snapshots (merge, show who/when).
- Robust data pack pipeline tooling (repeatable build + validation + provenance).

### Longer-term
- More providers (TSM/Wowhead/other) as optional integrations where licensing/ToS allow.
