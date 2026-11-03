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

from kodarea import npc, write_area
from kodroom import CONST, missing_kinds, room_of, roomdir

root = Path(__file__).resolve().parent.parent

FILES = sorted(
    [p for p in (roomdir / "monsroom").glob("[a-k][0-9].kod")]
    + [p for p in (roomdir / "monsroom/objroom").glob("[a-k][0-9].kod")]
    + list((roomdir / "monsroom").glob("forest*.kod")) + list((roomdir / "monsroom/objroom").glob("forest*.kod"))
    + list((roomdir / "monsroom/objroom").glob("canyon*.kod"))
    + [roomdir / f for f in ("field1.kod", "badland2.kod", "cave3.kod", "monsroom/badland1.kod",
                             "monsroom/objroom/icecave1.kod", "monsroom/objroom/cave2.kod")])


# StandardLeaveDir-Sonderfälle, die echte Wege sind (K5: Westrand → Badlands).
EXTRA_EDGES = {"OutdoorsK5": [{"edge": "WEST", "to": CONST["RID_BADLAND1"], "toConstant": "RID_BADLAND1", "toRow": 3,
                               "toCol": 80, "rotate": "NONE"}]}

# Eingänge zum Qor-Tempel (SomethingMoved → TempleEntrance, nicht über UtilGoNearSquare).
QOR = {"to": CONST["RID_TEMPLE_QOR"], "toConstant": "RID_TEMPLE_QOR", "toRow": 33, "toCol": 14, "angle": "NORTH"}
EXTRA_REGIONS = {"OutdoorsI8": [dict(QOR, rects=[[38, 26, 38, 26]])], "OutdoorsH9": [dict(QOR, rects=[[26, 12, 26, 12]])]}
# Faren-Priesterin in den Badlands (badland1.kod CreateStandardObjects); Laden und Dialog in make_temples.py.
EXTRA_NPCS = {"Badland1": [npc("FarenPriestess", "Priesterin Tenuv'vyal", "farnprst.bgf", "Priesterin des Faren", 44, 59,
                               angle=2048)]}

rooms = sorted((room_of(p) for p in FILES), key=lambda r: r["id"])
for r in rooms:
    r["edgeExits"] += EXTRA_EDGES.get(r["kodClass"], [])
    r["regionExits"] = r.get("regionExits", []) + EXTRA_REGIONS.get(r["kodClass"], [])
    if r["kodClass"] in EXTRA_NPCS:
        r["npcs"] = EXTRA_NPCS[r["kodClass"]]
write_area("wilderness", rooms, source="kod/object/active/holder/room/monsroom (Wildnis)")
if missing_kinds:
    print("Monsterklassen ohne Eintrag in monsters.json:", ", ".join(sorted(missing_kinds)))
