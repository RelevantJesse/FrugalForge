import argparse
import csv
import json
import re
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Tuple


WAGO_BUILD = "2.5.4.44833"
WAGO_SKILL_LINE_ABILITY_CSV = f"https://wago.tools/db2/SkillLineAbility/csv?build={WAGO_BUILD}"

WOWHEAD_SPELL_URL = "https://www.wowhead.com/tbc/spell="

PROFESSION_ID_TAILORING = 197
PROFESSION_NAME_TAILORING = "Tailoring"


@dataclass(frozen=True)
class Recipe:
    recipe_id: str
    profession_id: int
    name: str
    min_skill: int
    orange_until: int
    yellow_until: int
    green_until: int
    gray_at: int
    reagents: List[Dict[str, int]]


def _slugify(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return slug or "recipe"


def _http_get_text(url: str, *, user_agent: str, timeout_seconds: int = 30) -> str:
    req = urllib.request.Request(url)
    req.add_header("User-Agent", user_agent)
    req.add_header("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")
    req.add_header("Accept-Language", "en-US,en;q=0.9")
    req.add_header("Cache-Control", "no-cache")
    with urllib.request.urlopen(req, timeout=timeout_seconds) as resp:
        data = resp.read()
    return data.decode("utf-8", errors="replace")


def _load_wago_tailoring_spell_ids(cache_dir: Path, *, user_agent: str) -> List[int]:
    cache_path = cache_dir / f"SkillLineAbility.{WAGO_BUILD}.csv"
    if not cache_path.exists():
        cache_path.parent.mkdir(parents=True, exist_ok=True)
        cache_path.write_text(_http_get_text(WAGO_SKILL_LINE_ABILITY_CSV, user_agent=user_agent), encoding="utf-8")

    with cache_path.open("r", newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        spell_ids: List[int] = []
        for row in reader:
            if int(row["SkillLine"]) != PROFESSION_ID_TAILORING:
                continue
            spell_ids.append(int(row["Spell"]))

    return sorted(set(spell_ids))


_RE_WOWHEAD_SPELL_NAME = re.compile(
    r'WH\.Gatherer\.addData\(6,\s*5,\s*\{"(?P<id>\d+)":\{"name_enus":"(?P<name>[^"]+)"',
    re.IGNORECASE,
)

_RE_WOWHEAD_DIFFICULTY = re.compile(
    r"Difficulty:\s*\[color=r1\](?P<r1>\d+)\[\\?/color\].*?"
    r"\[color=r2\](?P<r2>\d+)\[\\?/color\].*?"
    r"\[color=r3\](?P<r3>\d+)\[\\?/color\].*?"
    r"\[color=r4\](?P<r4>\d+)\[\\?/color\]",
    re.IGNORECASE | re.DOTALL,
)

_RE_WOWHEAD_REAGENT_ROW = re.compile(
    r'data-icon-list-quantity="(?P<qty>\d+)".*?item=(?P<item_id>\d+)/',
    re.IGNORECASE,
)

_RE_WOWHEAD_ITEM_NAMES = re.compile(r'"(?P<id>\d+)":\{"name_enus":"(?P<name>[^"]+)"', re.IGNORECASE)


def _parse_wowhead_spell_page(spell_id: int, html: str) -> Tuple[str, Tuple[int, int, int, int], List[Tuple[int, int]], Dict[int, str]]:
    if "Requires Tailoring" not in html:
        raise ValueError("Not a tailoring requirement page")

    m = _RE_WOWHEAD_SPELL_NAME.search(html)
    if not m or int(m.group("id")) != spell_id:
        raise ValueError("Unable to find spell name in page")
    spell_name = m.group("name")

    dm = _RE_WOWHEAD_DIFFICULTY.search(html)
    if not dm:
        raise ValueError("Unable to find difficulty in page")
    r1, r2, r3, r4 = (int(dm.group("r1")), int(dm.group("r2")), int(dm.group("r3")), int(dm.group("r4")))

    reagents_section_start = html.find('id="icon-list-heading-reagents"')
    if reagents_section_start < 0:
        raise ValueError("Unable to find reagents section in page")
    reagents_section_end = html.find("</table>", reagents_section_start)
    if reagents_section_end < 0:
        raise ValueError("Unable to find end of reagents table in page")
    reagents_section = html[reagents_section_start:reagents_section_end]

    reagents: List[Tuple[int, int]] = []
    for rm in _RE_WOWHEAD_REAGENT_ROW.finditer(reagents_section):
        item_id = int(rm.group("item_id"))
        qty = int(rm.group("qty"))
        reagents.append((item_id, qty))

    if not reagents:
        raise ValueError("No reagents parsed")

    item_names: Dict[int, str] = {}
    for im in _RE_WOWHEAD_ITEM_NAMES.finditer(html):
        item_names[int(im.group("id"))] = im.group("name")

    return spell_name, (r1, r2, r3, r4), reagents, item_names


def _difficulty_to_thresholds(r1: int, r2: int, r3: int, r4: int) -> Tuple[int, int, int, int, int]:
    if r1 <= 0 or r2 <= 0 or r3 <= 0 or r4 <= 0:
        raise ValueError("Invalid difficulty values")
    if not (r1 <= r2 <= r3 <= r4):
        raise ValueError("Difficulty values not monotonic")

    min_skill = r1
    orange_until = max(min_skill, r2 - 1)
    yellow_until = max(orange_until, r3 - 1)
    green_until = max(yellow_until, r4 - 1)
    gray_at = max(green_until + 1, r4)

    return min_skill, orange_until, yellow_until, green_until, gray_at


def build_tailoring_pack(
    spell_ids: Iterable[int],
    *,
    cache_dir: Path,
    user_agent: str,
    sleep_seconds: float,
) -> Tuple[Dict[str, object], Dict[int, str]]:
    spell_ids = list(spell_ids)
    recipes: List[Recipe] = []
    reagent_item_names: Dict[int, str] = {}

    used_recipe_ids: Dict[str, int] = {}

    for idx, spell_id in enumerate(spell_ids, start=1):
        cache_path = cache_dir / "wowhead" / f"spell_{spell_id}.html"
        html: str

        if cache_path.exists():
            html = cache_path.read_text(encoding="utf-8", errors="replace")
        else:
            url = f"{WOWHEAD_SPELL_URL}{spell_id}"
            attempts = 0
            while True:
                attempts += 1
                try:
                    html = _http_get_text(url, user_agent=user_agent)
                    break
                except urllib.error.HTTPError as ex:
                    if ex.code in (403, 429):
                        if attempts >= 12:
                            raise
                        retry_after = ex.headers.get("Retry-After")
                        sleep_for = float(retry_after) if retry_after else (15.0 * attempts)
                        print(f"HTTP {ex.code} for spell={spell_id}; sleeping {sleep_for:.1f}s then retrying...")
                        time.sleep(sleep_for)
                        continue

                    if attempts >= 6:
                        raise
                    retry_after = ex.headers.get("Retry-After")
                    time.sleep(float(retry_after) if retry_after else 2.0 * attempts)
                except Exception:
                    if attempts >= 6:
                        raise
                    time.sleep(2.0 * attempts)

            cache_path.parent.mkdir(parents=True, exist_ok=True)
            cache_path.write_text(html, encoding="utf-8")
            time.sleep(sleep_seconds)

        try:
            name, (r1, r2, r3, r4), reagents, item_names = _parse_wowhead_spell_page(spell_id, html)
        except ValueError:
            continue

        min_skill, orange_until, yellow_until, green_until, gray_at = _difficulty_to_thresholds(r1, r2, r3, r4)

        slug = _slugify(name)
        if slug in used_recipe_ids:
            slug = f"{slug}-{spell_id}"
        used_recipe_ids[slug] = spell_id

        reagent_list = [{"itemId": item_id, "qty": qty} for item_id, qty in reagents]

        recipes.append(
            Recipe(
                recipe_id=slug,
                profession_id=PROFESSION_ID_TAILORING,
                name=name,
                min_skill=min_skill,
                orange_until=orange_until,
                yellow_until=yellow_until,
                green_until=green_until,
                gray_at=gray_at,
                reagents=reagent_list,
            )
        )

        for item_id, _qty in reagents:
            item_name = item_names.get(item_id)
            if item_name:
                reagent_item_names[item_id] = item_name

        if idx % 50 == 0:
            print(f"Fetched/parsed {idx}/{len(spell_ids)} pages...")

    pack = {
        "professionId": PROFESSION_ID_TAILORING,
        "professionName": PROFESSION_NAME_TAILORING,
        "recipes": [
            {
                "recipeId": r.recipe_id,
                "professionId": r.profession_id,
                "name": r.name,
                "minSkill": r.min_skill,
                "orangeUntil": r.orange_until,
                "yellowUntil": r.yellow_until,
                "greenUntil": r.green_until,
                "grayAt": r.gray_at,
                "reagents": r.reagents,
            }
            for r in sorted(recipes, key=lambda x: (x.min_skill, x.name))
        ],
    }

    return pack, reagent_item_names


def _load_items_json(path: Path) -> Dict[int, str]:
    if not path.exists():
        return {}
    items = json.loads(path.read_text(encoding="utf-8"))
    return {int(it["itemId"]): it["name"] for it in items}


def _write_items_json(path: Path, items: Dict[int, str]) -> None:
    data = [{"itemId": item_id, "name": items[item_id]} for item_id in sorted(items)]
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Export TBC Classic tailoring recipes into Anniversary datapack JSON.")
    parser.add_argument("--out-profession-json", type=Path, default=Path("data/Anniversary/professions/tailoring.json"))
    parser.add_argument("--out-items-json", type=Path, default=Path("data/Anniversary/items.json"))
    parser.add_argument("--cache-dir", type=Path, default=Path(".wago-cache"))
    parser.add_argument(
        "--user-agent",
        default="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    )
    parser.add_argument("--sleep-seconds", type=float, default=1.0)
    args = parser.parse_args()

    spell_ids = _load_wago_tailoring_spell_ids(args.cache_dir, user_agent=args.user_agent)
    print(f"Tailoring spell ids (from wago.tools): {len(spell_ids)}")

    pack, reagent_item_names = build_tailoring_pack(
        spell_ids,
        cache_dir=args.cache_dir,
        user_agent=args.user_agent,
        sleep_seconds=args.sleep_seconds,
    )

    if not pack["recipes"]:
        raise SystemExit("No recipes were parsed; aborting.")

    args.out_profession_json.parent.mkdir(parents=True, exist_ok=True)
    args.out_profession_json.write_text(json.dumps(pack, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    items = _load_items_json(args.out_items_json)
    missing = 0
    for recipe in pack["recipes"]:
        for reagent in recipe["reagents"]:
            item_id = int(reagent["itemId"])
            if item_id in items:
                continue
            name = reagent_item_names.get(item_id)
            if not name:
                missing += 1
                continue
            items[item_id] = name

    if missing:
        raise SystemExit(f"Missing {missing} reagent item names (wowhead pages didn't include them).")

    _write_items_json(args.out_items_json, items)

    print(f"Wrote {args.out_profession_json} ({len(pack['recipes'])} recipes)")
    print(f"Wrote {args.out_items_json} ({len(items)} items)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
