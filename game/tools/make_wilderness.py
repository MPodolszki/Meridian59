#!/usr/bin/env python3
"""Erzeugt server/data/world/wilderness.json: die Wildnis des Festlands zwischen den Städten (Kartenquadrate A5–K5,
Wälder, Schluchten, Badlands, Höhlen) aus den KOD-Raumklassen, alles automatisch gelesen (tools/kodroom.py).
Werte und Abweichungen: docs/wilderness.md.

Aufruf (im Ordner game):
    python3 tools/make_wilderness.py                      # Weltdatei schreiben
    python -I tools/roo2layout.py server/data/world/wilderness.json ../resource/rooms client/rooms
    python3 tools/make_wilderness.py                      # noch einmal: Größen aus den Layouts
"""
import json
import re
from pathlib import Path

from kodarea import write_area
from kodroom import CONST, classes, edge_exits, exits, locked_doors, monsters, prop, region_exits, resource, roomdir, symbol, trees
from roo2layout import read_roo

root = Path(__file__).resolve().parent.parent
RESOURCES = root.parent / "resource/rooms"

FILES = sorted(
    [p for p in (roomdir / "monsroom").glob("[a-k][0-9].kod")]
    + [p for p in (roomdir / "monsroom/objroom").glob("[a-k][0-9].kod")]
    + list((roomdir / "monsroom").glob("forest*.kod")) + list((roomdir / "monsroom/objroom").glob("forest*.kod"))
    + list((roomdir / "monsroom/objroom").glob("canyon*.kod"))
    + [roomdir / f for f in ("field1.kod", "badland2.kod", "cave3.kod", "monsroom/badland1.kod",
                             "monsroom/objroom/icecave1.kod", "monsroom/objroom/cave2.kod")])

TERRAIN = {"FOREST": "forest", "GRASS": "forest", "FIELD": "forest", "PLAINS": "forest", "SWAMP": "forest",
           "MOUNTAIN": "mountain", "BADLANDS": "badlands", "DESERT": "badlands", "CAVES": "cave", "SNOW": "snow"}

kinds = {v["class"].lower(): k for k, v in json.loads((root / "server/data/monsters/monsters.json").read_text())["monsters"].items()}
missing_kinds = set()


def room_of(path):
    text = path.read_text(encoding="latin-1")
    cls = re.search(r"^(\w+)\s+is\s+(\w+)", text, re.M).group(1)
    roo = resource(text, f"room_{cls}") or resource(text, symbol(text, "prRoom") or "")
    name_sym = symbol(text, "vrName")
    name = resource(text, name_sym) if name_sym else cls
    music_sym = symbol(text, "prMusic")
    music = resource(text, music_sym) if music_sym else None
    rid_sym = symbol(text, "piRoom_num")
    room = {"id": CONST[rid_sym.upper()], "constant": rid_sym.upper(), "kodClass": cls, "name": name, "roo": roo.lower()}
    if music:
        room["music"] = music
    light = symbol(text, "piBaseLight")
    room["baseLight"] = CONST.get(light.upper(), 164) if light else 164
    outside = prop(text, "piOutside_factor")
    room["outsideFactor"] = 8 if outside is None else outside
    room["outdoors"] = room["outsideFactor"] > 0
    terrain = symbol(text, "viTerrain_type")
    if terrain and terrain.startswith("TERRAIN_"):
        room["terrain"] = TERRAIN.get(terrain[8:], terrain[8:].lower())
    room["teleport"] = {"row": prop(text, "viTeleport_row") or 1, "col": prop(text, "viTeleport_col") or 1}
    room["exits"] = exits(text)
    room["edgeExits"] = edge_exits(text)
    room["lockedDoors"] = locked_doors(text)
    src = RESOURCES / roo
    if src.exists():
        cols, rows, _, _ = read_roo(src)
        room["regionExits"] = region_exits(text, rows, cols)
    table, gens = monsters(text)
    mons = []
    for kod_class, weight in table:
        key = kinds.get(kod_class.lower())
        if key is None:
            missing_kinds.add(kod_class)
            continue
        mons.append({"kind": key, "weight": weight})
    if mons and gens:
        room["monsters"] = mons
        room["generators"] = [{"row": r, "col": c} for r, c in gens]
        room["maxMonsters"] = prop(text, "piMonster_count_max") or 10
        room["genTime"] = prop(text, "piGen_time") or 20000
        room["genPercent"] = prop(text, "piGen_percent") or 100
        lo, hi = prop(text, "piInit_count_min"), prop(text, "piInit_count_max")
        if lo is not None:
            room["initCountMin"] = lo
        if hi is not None:
            room["initCountMax"] = hi
    room["objects"] = trees(text)
    return room


# StandardLeaveDir-Sonderfälle, die echte Wege sind (K5: Westrand → Badlands).
EXTRA_EDGES = {"OutdoorsK5": [{"edge": "WEST", "to": CONST["RID_BADLAND1"], "toConstant": "RID_BADLAND1", "toRow": 3,
                               "toCol": 80, "rotate": "NONE"}]}

rooms = sorted((room_of(p) for p in FILES), key=lambda r: r["id"])
for r in rooms:
    r["edgeExits"] += EXTRA_EDGES.get(r["kodClass"], [])
write_area("wilderness", rooms, source="kod/object/active/holder/room/monsroom (Wildnis)")
if missing_kinds:
    print("Monsterklassen ohne Eintrag in monsters.json:", ", ".join(sorted(missing_kinds)))
