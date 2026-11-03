#!/usr/bin/env python3
"""Erzeugt weitere Gebiete des Festlands vollständig aus den KOD-Raumklassen (tools/kodroom.py): je Gebiet eine
Weltdatei server/data/world/<gebiet>.json und die Raumlayouts client/rooms/<roo>.json. Werte und Abweichungen je
Gebiet in docs/areas.md.

Aufruf (im Ordner game): python3 tools/make_areas.py [gebiet …] && python3 tools/export_exits.py
"""
import sys
from pathlib import Path

import roo2layout
from kodarea import write_area
from kodroom import ANGLES, CONST, missing_kinds, mob_classes, prop, resource, room_of, roomdir, symbol

root = Path(__file__).resolve().parent.parent
M = roomdir / "monsroom"

AREAS = {
    # Kanalisationen von Barloque und Jasper mit dem Versteck des Lupogg-Königs (sewking.kod) dazwischen
    "sewers": [M / "barlsew.kod", M / "barlsew2.kod", M / "barlsew3.kod", M / "jassew1.kod", M / "jassew2.kod",
               M / "jassew3.kod", M / "bossroom/sewking.kod", roomdir / "barlqrm/asshq.kod"],
    # Burgen: Blackstone Keep des Herzogs (duke1–5, über Tos), Castle Victoria der Prinzessin (castle1, 1b, 1c,
    # Thronsaal, Kerker) und die Bourgeois-Burg (castle2a–e)
    "castles": [roomdir / "duke1.kod", roomdir / "duke2.kod", roomdir / "duke3.kod", M / "objroom/duke4.kod",
                M / "objroom/duke5.kod", M / "objroom/castle1.kod", M / "castle1b.kod", roomdir / "castle1c.kod",
                M / "throne1.kod", roomdir / "dungeon.kod", roomdir / "castle2a.kod", M / "castle2b.kod",
                M / "castle2c.kod", roomdir / "castle2d.kod", roomdir / "castle2e.kod"],
}


def home_npcs(rooms):
    """Fraktionsführer (factions.kod GotoHomeroom): viHomeroom, viMyRow/Col/FR/FC, viMyAngle."""
    by_id = {r["id"]: r for r in rooms}
    for real, text in mob_classes().values():
        home = symbol(text, "viHomeroom")
        row = prop(text, "viMyRow")
        if not home or row is None or CONST.get(home.upper()) not in by_id:
            continue
        name_sym, icon_sym = symbol(text, "vrName"), symbol(text, "vrIcon")
        angle = symbol(text, "viMyAngle") or ""
        by_id[CONST[home.upper()]].setdefault("npcs", []).append({
            "kodClass": real, "name": resource(text, name_sym) or real, "icon": (resource(text, icon_sym) or "").lower(),
            "role": "Bewohner", "row": row, "col": prop(text, "viMyCol") or 1, "fineRow": prop(text, "viMyFR") or 32,
            "fineCol": prop(text, "viMyFC") or 32, "angle": ANGLES.get(angle[6:], 0) if angle.startswith("ANGLE_") else 0})


def build(name):
    rooms = sorted((room_of(p) for p in AREAS[name]), key=lambda r: r["id"])
    write_area(name, rooms, source=", ".join(str(p.relative_to(roomdir)) for p in AREAS[name]))
    world = root / f"server/data/world/{name}.json"
    roo2layout.main(world, root.parent / "resource/rooms", root / "client/rooms")
    # zweiter Durchgang: Größen aus den Layouts (Ränder, Zonen-Übergänge)
    rooms = sorted((room_of(p) for p in AREAS[name]), key=lambda r: r["id"])
    home_npcs(rooms)
    write_area(name, rooms, source=", ".join(str(p.relative_to(roomdir)) for p in AREAS[name]))


for area in sys.argv[1:] or list(AREAS):
    build(area)
if missing_kinds:
    print("Monsterklassen ohne Eintrag in monsters.json:", ", ".join(sorted(missing_kinds)))
