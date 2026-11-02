#!/usr/bin/env python3
"""Überträgt Ausgänge und verschlossene Türen aus der Weltdatei des Servers in die Raumdaten des Clients.

Aufruf (im Ordner game): python3 tools/export_exits.py
Liest server/data/world/tos.json und ergänzt in client/rooms/<roo>.json die Felder
  "exits":  [[zeile, spalte, "Zielraum"], ...]   (KOD-Felder, 1-basiert)
  "locked": [[zeile, spalte], ...]
Der Client zeichnet dort Türen und zeigt „Leertaste: hinausgehen“.
"""
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
world = json.loads((root / "server/data/world/tos.json").read_text())
names = {r["id"]: r["name"] for r in world["rooms"]}

for room in world["rooms"]:
    path = root / "client/rooms" / (Path(room["roo"]).stem.lower() + ".json")
    if not path.exists():
        continue
    layout = json.loads(path.read_text())
    layout["exits"] = [[e["row"], e["col"], names.get(e["to"], "")] for e in room.get("exits", [])]
    layout["locked"] = [[d["row"], d["col"]] for d in room.get("lockedDoors", [])]
    # Kompakt wie die übrigen Layoutdateien: eine Zeile je Rasterzeile.
    floor = layout.pop("floor")
    head = json.dumps(layout, ensure_ascii=False)[:-1]
    rows = ",\n".join("    " + json.dumps(r, separators=(",", ":")) for r in floor)
    path.write_text(f'{head}, "floor": [\n{rows}\n]}}\n')
    print(f"{path.name}: {len(layout['exits'])} Ausgänge, {len(layout['locked'])} verschlossen")
