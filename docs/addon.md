# WowAhPlannerScan addon (legacy AH API)

Your client reports:
- `C_AuctionHouse=no`
- `QueryAuctionItems=yes`

So this addon uses the legacy Auction House query API (`QueryAuctionItems`) and scans a target list of reagent itemIds instead of scanning the entire AH.

## Install

1) Copy `addon/WowAhPlannerScan` into your WoW AddOns folder:
- `_anniversary_/Interface/AddOns/WowAhPlannerScan`

2) Ensure the `Interface` number in `addon/WowAhPlannerScan/WowAhPlannerScan.toc` matches your client build if needed.

## Load scan targets (recommended: recipe targets)

The web app can generate a Lua file containing all recipes for a profession (minSkill/grayAt + reagent itemIds). The addon then automatically limits the scan to your current skill up to `maxSkillDelta` higher (default 100), clamped to **Expansion cap skill** (default 350).

- `GET /api/scans/recipeTargets.lua?version=Anniversary&professionId=197`

Save the response as:
- `_anniversary_/Interface/AddOns/WowAhPlannerScan/WowAhPlannerScan_Targets.lua`

If you're running the web app on the same machine as WoW, use the app's `/targets` page and click **Install targets** to write this file automatically.

It should define:
- `WowAhPlannerScan_TargetProfessionId = 197`
- `WowAhPlannerScan_TargetProfessionName = "Tailoring"` (used if your client's profession IDs differ)
- `WowAhPlannerScan_TargetItemIds = { ... }` (full pack reagent list, used as fallback)
- `WowAhPlannerScan_RecipeTargets = { ... }`

## Configure UI + scan settings

In-game:
- `/wahpscan options`

Options include:
- Show scan panel when Auction House opens
- Max skill delta (default 100)
- Expansion cap skill (default 350)
- Max pages per item
- Min query interval (seconds)
- Query timeout (seconds)
- Max timeout retries (per page)

If you see repeated `Query timeout ... Retrying`, try:
- staying on the Browse tab
- increasing Min query interval (e.g. 4–5 seconds)
- lowering Max pages per item (e.g. 1–3)
- lowering Max timeout retries

## Scan + export

1) Go to an Auction House and open the AH window.
2) Use the small **WowAhPlannerScan** panel next to the AH window (or run `/wahpscan start`).
3) When it finishes: `/wahpscan export`
4) Copy the JSON and paste it into the web app at `/upload`.

Troubleshooting:
- `/wahpscan debug` prints what the addon sees (profession info + settings).
- If `GetProfessions()` is all `nil`, the addon falls back to reading the Skills list (`GetSkillLineInfo`).

## Legacy fallback (direct itemId list)

If you don't want recipe targets, you can still generate a direct itemId list:
- `GET /api/scans/targets.lua?version=Anniversary&professionId=197&currentSkill=150&maxSkillDelta=100`

