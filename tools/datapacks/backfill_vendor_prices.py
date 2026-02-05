import argparse
import json
import re
import time
import urllib.request
from pathlib import Path
from typing import Dict, List, Optional, Tuple


DEFAULT_USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/120.0.0.0 Safari/537.36"
)


def _http_get_text(url: str, *, user_agent: str, timeout_seconds: int = 45) -> str:
    req = urllib.request.Request(url)
    req.add_header("User-Agent", user_agent)
    req.add_header("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")
    req.add_header("Accept-Language", "en-US,en;q=0.9")
    req.add_header("Cache-Control", "no-cache")
    with urllib.request.urlopen(req, timeout=timeout_seconds) as resp:
        return resp.read().decode("utf-8", errors="replace")


def _find_matching_bracket(text: str, start_index: int, open_char: str, close_char: str) -> int:
    if text[start_index] != open_char:
        raise ValueError(f"Expected '{open_char}' at index {start_index}")

    depth = 0
    in_string = False
    escape = False

    for idx in range(start_index, len(text)):
        ch = text[idx]

        if in_string:
            if escape:
                escape = False
                continue
            if ch == "\\":
                escape = True
                continue
            if ch == '"':
                in_string = False
            continue

        if ch == '"':
            in_string = True
            continue

        if ch == open_char:
            depth += 1
            continue
        if ch == close_char:
            depth -= 1
            if depth == 0:
                return idx

    raise ValueError(f"No matching '{close_char}' found for '{open_char}' at {start_index}")


def _extract_sold_by_listview_data(html: str) -> Optional[List[dict]]:
    idx = html.find("id: 'sold-by'")
    if idx < 0:
        idx = html.find('id: "sold-by"')
    if idx < 0:
        return None

    data_idx = html.find("data:", idx)
    if data_idx < 0:
        return None

    array_start = html.find("[", data_idx)
    if array_start < 0:
        return None

    array_end = _find_matching_bracket(html, array_start, "[", "]")
    raw = html[array_start : array_end + 1]
    try:
        data = json.loads(raw)
    except json.JSONDecodeError:
        return None
    if not isinstance(data, list):
        return None
    return [x for x in data if isinstance(x, dict)]


def _extract_unlimited_vendor_money_costs(sold_by: List[dict]) -> List[int]:
    costs: List[int] = []
    for vendor in sold_by:
        stock = vendor.get("stock")
        if stock != -1:
            continue
        cost = vendor.get("cost")
        if not isinstance(cost, list) or not cost:
            continue
        if not isinstance(cost[0], list) or not cost[0]:
            continue
        money = cost[0][0]
        if isinstance(money, bool):
            continue
        if isinstance(money, int) and money > 0 and len(cost[0]) == 1 and len(cost) == 1:
            costs.append(money)
    return costs


def _load_items(path: Path) -> List[dict]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, list):
        raise SystemExit(f"{path} must be a JSON list")
    items: List[dict] = []
    for item in raw:
        if isinstance(item, dict) and "itemId" in item and "name" in item:
            items.append(item)
    return items


def _write_items(path: Path, items: List[dict]) -> None:
    items_sorted = sorted(items, key=lambda it: int(it.get("itemId") or 0))
    path.write_text(json.dumps(items_sorted, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def _load_item_cache(cache_dir: Path, item_id: int, *, user_agent: str, request_delay_seconds: float) -> str:
    cache_dir.mkdir(parents=True, exist_ok=True)
    cache_path = cache_dir / f"wowhead_tbc_item_{item_id}.html"
    if cache_path.exists():
        return cache_path.read_text(encoding="utf-8", errors="replace")
    url = f"https://www.wowhead.com/tbc/item={item_id}"
    html = _http_get_text(url, user_agent=user_agent)
    cache_path.write_text(html, encoding="utf-8")
    if request_delay_seconds > 0:
        time.sleep(request_delay_seconds)
    return html


def _load_item_xml_cache(cache_dir: Path, item_id: int, *, user_agent: str, request_delay_seconds: float) -> str:
    cache_dir.mkdir(parents=True, exist_ok=True)
    cache_path = cache_dir / f"wowhead_tbc_item_{item_id}.xml"
    if cache_path.exists():
        return cache_path.read_text(encoding="utf-8", errors="replace")
    url = f"https://www.wowhead.com/tbc/item={item_id}?xml"
    xml = _http_get_text(url, user_agent=user_agent)
    cache_path.write_text(xml, encoding="utf-8")
    if request_delay_seconds > 0:
        time.sleep(request_delay_seconds)
    return xml


def _is_vendor_item_from_xml(xml: str) -> bool:
    match = re.search(r"<json><!\[CDATA\[(.*?)\]\]></json>", xml)
    if not match:
        return False
    payload = match.group(1).strip()
    if not payload:
        return False
    try:
        obj = json.loads("{" + payload + "}")
    except json.JSONDecodeError:
        return False
    source = obj.get("source")
    return isinstance(source, list) and 5 in source


def _backfill_vendor_prices(
    items: List[dict],
    *,
    cache_dir: Path,
    user_agent: str,
    request_delay_seconds: float,
    max_items: int,
) -> Tuple[int, int, int]:
    scanned = 0
    updated = 0
    skipped_existing = 0
    vendor_candidates = 0

    for item in items:
        if max_items > 0 and scanned >= max_items:
            break

        try:
            item_id = int(item.get("itemId") or 0)
        except (TypeError, ValueError):
            continue
        if item_id <= 0:
            continue

        scanned += 1

        if isinstance(item.get("vendorPriceCopper"), int) and item["vendorPriceCopper"] > 0:
            skipped_existing += 1
            continue

        xml = _load_item_xml_cache(cache_dir, item_id, user_agent=user_agent, request_delay_seconds=request_delay_seconds)
        if not _is_vendor_item_from_xml(xml):
            continue

        vendor_candidates += 1

        html = _load_item_cache(cache_dir, item_id, user_agent=user_agent, request_delay_seconds=request_delay_seconds)
        sold_by = _extract_sold_by_listview_data(html)
        if not sold_by:
            continue
        costs = _extract_unlimited_vendor_money_costs(sold_by)
        if not costs:
            continue

        item["vendorPriceCopper"] = min(costs)
        updated += 1

        if scanned % 100 == 0:
            print(f"Scanned {scanned} items; vendor candidates {vendor_candidates}; updated {updated}")

    return scanned, updated, skipped_existing


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Backfill vendorPriceCopper in items.json for items sold with unlimited stock."
    )
    parser.add_argument("--items-json", type=Path, default=Path("data/Anniversary/items.json"))
    parser.add_argument("--cache-dir", type=Path, default=Path(".wago-cache") / "wowhead-items")
    parser.add_argument("--user-agent", default=DEFAULT_USER_AGENT)
    parser.add_argument("--request-delay-seconds", type=float, default=0.0)
    parser.add_argument("--max-items", type=int, default=0, help="0 means no limit")
    args = parser.parse_args()

    items = _load_items(args.items_json)
    scanned, updated, skipped_existing = _backfill_vendor_prices(
        items,
        cache_dir=args.cache_dir,
        user_agent=args.user_agent,
        request_delay_seconds=args.request_delay_seconds,
        max_items=args.max_items,
    )
    _write_items(args.items_json, items)

    print(f"Scanned {scanned} items")
    print(f"Updated {updated} items with vendorPriceCopper")
    print(f"Skipped {skipped_existing} items (already had vendorPriceCopper)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
