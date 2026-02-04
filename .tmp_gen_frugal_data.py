import json
from pathlib import Path
root = Path('data/Anniversary')
prof_dir = root / 'professions'
profs = []
for path in sorted(prof_dir.glob('*.json')):
    data = json.loads(path.read_text())
    profs.append({
        'professionId': data['professionId'],
        'name': data['professionName'],
        'recipes': data['recipes'],
    })
items = json.loads((root / 'items.json').read_text())
item_map = {int(i['itemId']): i['name'] for i in items}
producers = json.loads((root / 'producers.json').read_text()).get('producers', [])
smelts = [p for p in producers if p.get('kind') == 'Smelt']
lines = []
lines.append('FrugalForgeData_Anniversary = {')
lines.append('  professions = {')
for prof in profs:
    lines.append('    {')
    lines.append(f"      professionId = {prof['professionId']},")
    lines.append(f"      name = {json.dumps(prof['name'])},")
    lines.append('      recipes = {')
    for r in prof['recipes']:
        lines.append('        {')
        lines.append(f"          recipeId = {json.dumps(r['recipeId'])},")
        lines.append(f"          professionId = {r['professionId']},")
        lines.append(f"          name = {json.dumps(r['name'])},")
        lines.append(f"          createsItemId = {r['createsItemId']},")
        lines.append(f"          createsQuantity = {r['createsQuantity']},")
        lines.append(f"          learnedByTrainer = {str(r['learnedByTrainer']).lower()},")
        if 'cooldownSeconds' in r and r['cooldownSeconds']:
            lines.append(f"          cooldownSeconds = {r['cooldownSeconds']},")
        lines.append(f"          minSkill = {r['minSkill']},")
        lines.append(f"          orangeUntil = {r['orangeUntil']},")
        lines.append(f"          yellowUntil = {r['yellowUntil']},")
        lines.append(f"          greenUntil = {r['greenUntil']},")
        lines.append(f"          grayAt = {r['grayAt']},")
        lines.append('          reagents = {')
        for reg in r['reagents']:
            lines.append(f"            {{ itemId = {reg['itemId']}, qty = {reg['qty']} }},")
        lines.append('          },')
        lines.append('        },')
    lines.append('      },')
    lines.append('    },')
lines.append('  },')
lines.append('  smelts = {')
for s in smelts:
    out = s['output']
    lines.append(f"    [{out['itemId']}] = {{")
    lines.append(f"      name = {json.dumps(s['name'])},")
    lines.append(f"      outputQty = {out.get('qty', 1)},")
    lines.append('      reagents = {')
    for reg in s.get('reagents', []):
        lines.append(f"        {{ itemId = {reg['itemId']}, qty = {reg['qty']} }},")
    lines.append('      },')
    lines.append('    },')
lines.append('  },')
lines.append('  items = {')
for item_id, name in sorted(item_map.items()):
    lines.append(f"    [{item_id}] = {json.dumps(name)},")
lines.append('  }')
lines.append('}')
Path('addon/FrugalForge/FrugalForge_Data_Anniversary.lua').write_text("\n".join(lines))
print('wrote', len(profs), 'professions,', len(item_map), 'items,', len(smelts), 'smelts')
