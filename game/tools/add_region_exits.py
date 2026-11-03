#!/usr/bin/env python3
"""Ergänzt in allen Gebietsdateien (server/data/world/*.json) die Zonen-Übergänge aus SomethingMoved der
KOD-Raumklassen (z. B. Marion → Wälder C4/C5). Aufruf (im Ordner game): python3 tools/add_region_exits.py"""
import json
from pathlib import Path

from kodarea import dumps
from kodroom import classes, region_exits

root = Path(__file__).resolve().parent.parent
kod = classes()
for f in sorted((root / "server/data/world").glob("*.json")):
    world = json.loads(f.read_text())
    changed = 0
    for room in world["rooms"]:
        entry = kod.get(room["kodClass"].lower())
        if not entry or "rows" not in room:
            continue
        found = region_exits(entry[1], room["rows"], room["cols"])
        if found or room.get("regionExits"):
            room["regionExits"] = found
            changed += len(found)
    f.write_text(dumps(world) + "\n")
    print(f"{f.name}: {changed} Zonen-Übergänge")
