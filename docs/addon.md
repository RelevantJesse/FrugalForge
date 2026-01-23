# WowAhPlannerScan addon (legacy AH API)

Your client reports:
- `C_AuctionHouse=no`
- `QueryAuctionItems=yes`

So this addon uses the legacy Auction House query API (`QueryAuctionItems`) and scans a **target list of itemIds** (recipe reagents) instead of scanning the entire AH.

## Install

1) Copy `addon/WowAhPlannerScan` into your WoW AddOns folder:
- `_classic_/Interface/AddOns/WowAhPlannerScan`

2) Ensure the `Interface` number in `addon/WowAhPlannerScan/WowAhPlannerScan.toc` matches your client build if needed.

## Load scan targets (itemIds)

The web app can generate a Lua file containing the itemIds to scan:

- `GET /api/scans/targets.lua?version=Anniversary`
- Optional: `&professionId=185`

Save the response as:
- `_classic_/Interface/AddOns/WowAhPlannerScan/WowAhPlannerScan_Targets.lua`

It should define:
- `WowAhPlannerScan_TargetItemIds = { ... }`

## Scan + export

1) Go to an Auction House and open the AH window.
2) Run: `/wahpscan start`
3) When it finishes: `/wahpscan export`
4) Copy the JSON and paste it into the app at `/upload`.

## Notes / limitations

- Scanning is throttled and can be slow for large target lists (thousands of items).
- This is “min unit buyout” only for now; median/percentiles can be added later.
- For best results, keep the AH window open while scanning.
