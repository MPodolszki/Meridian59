#!/usr/bin/env python3
"""Überträgt Ausgänge und verschlossene Türen aus der Weltdatei des Servers in die Raumdaten des Clients.

Aufruf (im Ordner game): python3 tools/export_exits.py
Liest alle Gebiete in server/data/world/*.json und ergänzt in client/rooms/<roo>.json die Felder
  "exits":  [[zeile, spalte, "Zielraum"], ...]   (KOD-Felder, 1-basiert)
  "locked": [[zeile, spalte], ...]
  "terrain", "trees": [[zeile, spalte, feinzeile, feinspalte, "tree"|"shrub"], ...] (falls vorhanden)
Der Client zeichnet dort Türen (Linksklick: hinausgehen).
"""
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
rooms = [r for f in sorted((root / "server/data/world").glob("*.json")) for r in json.loads(f.read_text())["rooms"]]
names = {r["id"]: r["name"] for r in rooms}

for room in rooms:
    path = root / "client/rooms" / (Path(room["roo"]).stem.lower() + ".json")
    if not path.exists():
        continue
    layout = json.loads(path.read_text())
    layout["exits"] = [[e["row"], e["col"], names.get(e["to"], "")] for e in room.get("exits", [])]
    layout["locked"] = [[d["row"], d["col"]] for d in room.get("lockedDoors", [])]
    # Gelände ("village": Wiese statt Pflaster) und Bäume/Sträucher aus den Zierobjekten (OO_*TREE*, OO_*SHRUB*).
    if "terrain" in room:
        layout["terrain"] = room["terrain"]
    layout["outdoors"] = bool(room.get("outdoors", False))   # Beleuchtung im Client (Sonne vs. Decke)
    # Ränder, über die man den Raum verlassen darf (plEdge_Exits); sonst endet der Raum am Raster.
    edges = sorted({e["edge"] for e in room.get("edgeExits", [])})
    if edges:
        layout["edges"] = edges
    else:
        layout.pop("edges", None)
    trees = [[o["row"], o["col"], o["fineRow"], o["fineCol"], "shrub" if "SHRUB" in o["type"] else "tree"]
             for o in room.get("objects", []) if "TREE" in o["type"] or "SHRUB" in o["type"]]
    if trees:
        layout["trees"] = trees
    # Kompakt wie die übrigen Layoutdateien: eine Zeile je Rasterzeile.
    floor = layout.pop("floor")
    head = json.dumps(layout, ensure_ascii=False)[:-1]
    rows = ",\n".join("    " + json.dumps(r, separators=(",", ":")) for r in floor)
    path.write_text(f'{head}, "floor": [\n{rows}\n]}}\n')
    print(f"{path.name}: {len(layout['exits'])} Ausgänge, {len(layout['locked'])} verschlossen")
