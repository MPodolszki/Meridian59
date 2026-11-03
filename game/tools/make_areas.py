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
from kodroom import missing_kinds, room_of, roomdir

root = Path(__file__).resolve().parent.parent
M = roomdir / "monsroom"

AREAS = {
    # Kanalisationen von Barloque und Jasper mit dem Versteck des Lupogg-Königs (sewking.kod) dazwischen
    "sewers": [M / "barlsew.kod", M / "barlsew2.kod", M / "barlsew3.kod", M / "jassew1.kod", M / "jassew2.kod",
               M / "jassew3.kod", M / "bossroom/sewking.kod", roomdir / "barlqrm/asshq.kod"],
}


def build(name):
    rooms = sorted((room_of(p) for p in AREAS[name]), key=lambda r: r["id"])
    write_area(name, rooms, source=", ".join(str(p.relative_to(roomdir)) for p in AREAS[name]))
    world = root / f"server/data/world/{name}.json"
    roo2layout.main(world, root.parent / "resource/rooms", root / "client/rooms")
    # zweiter Durchgang: Größen aus den Layouts (Ränder, Zonen-Übergänge)
    rooms = sorted((room_of(p) for p in AREAS[name]), key=lambda r: r["id"])
    write_area(name, rooms, source=", ".join(str(p.relative_to(roomdir)) for p in AREAS[name]))


for area in sys.argv[1:] or list(AREAS):
    build(area)
if missing_kinds:
    print("Monsterklassen ohne Eintrag in monsters.json:", ", ".join(sorted(missing_kinds)))
