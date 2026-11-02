#!/usr/bin/env python3
"""Erzeugt server/data/world/cor.json aus dem Original (kod/object/active/holder/room/crnthrm). Werte siehe docs/cor.md.

Aufruf (im Ordner game): python3 tools/make_cor.py
"""
from kodarea import exits, locked, npc, write_area

NICE = 164
DEFAULT_LOCKED = "Die Tür ist verschlossen."

# Bäume (Tree, Krone/Stamm) und ein Strauch in Cor Noth (cornoth.kod:120-148)
TREES = [(4, 43), (7, 48), (5, 58), (20, 57), (26, 65), (36, 44), (30, 53), (28, 48), (21, 41), (31, 42), (40, 44),
         (21, 30), (27, 28), (16, 5)]

rooms = [
    {"id": 150, "constant": "RID_CORNOTH", "kodClass": "CorNoth", "name": "Cor Noth", "roo": "cornoth.roo",
     "music": "city04.mid", "baseLight": NICE, "outsideFactor": 8, "outdoors": True,
     "teleport": {"row": 20, "col": 20},
     "exits": exits([11, 60, 153, 4, 2, "NONE"], [23, 32, 152, 3, 3, "NONE"], [24, 37, 152, 3, 6, "NONE"],
                    [25, 37, 152, 3, 6, "NONE"], [38, 31, 701, 20, 13, "270"], [39, 31, 701, 20, 13, "270"],
                    [37, 58, 711, 11, 10, "225"], [24, 20, 151, 9, 7, "180"], [24, 19, 151, 9, 7, "180"],
                    [1, 39, 574, 54, 24, "NONE"], [1, 40, 574, 54, 25, "NONE"], [30, 69, 575, 6, 19, "NONE"],
                    [31, 69, 575, 7, 19, "NONE"], [41, 12, 155, 4, 3, "NONE"], [11, 33, 156, 10, 25, "NONE"],
                    [10, 8, 154, 7, 6, "NONE"]),
     "lockedDoors": locked(DEFAULT_LOCKED, (40, 22), (44, 9), (27, 12), (27, 13), (28, 12), (28, 13), (21, 16), (11, 17)),
     "edgeExits": [{"edge": "WEST", "to": 574, "toConstant": "RID_G4", "toRow": 48, "toCol": 24, "rotate": "NONE"}],
     "objects": [{"type": "TREE", "row": r, "col": c, "fineRow": 32, "fineCol": 32, "angle": 0} for r, c in TREES]
                + [{"type": "SHRUB", "row": 38, "col": 15, "fineRow": 32, "fineCol": 32, "angle": 0}]},
    {"id": 151, "constant": "RID_COR_GROCER", "kodClass": "CorGrocer", "name": "Solomon's Edibles", "roo": "corgroc.roo",
     "music": "smithy.mid", "baseLight": NICE, "outsideFactor": 3, "teleport": {"row": 5, "col": 5}, "noCombat": True,
     "exits": exits([10, 7, 150, 23, 20, "180"]),
     "npcs": [npc("CornothGrocer", "Solomon", "cngrocer.bgf", "Krämer", 3, 5, angle=1024)]},
    {"id": 152, "constant": "RID_COR_HALL", "kodClass": "CorHall", "name": "The Adventurer's Hall of Cor Noth",
     "roo": "corhall.roo", "music": "main.mid", "baseLight": NICE, "outsideFactor": 3, "teleport": {"row": 3, "col": 3},
     "noCombat": True,
     "exits": exits([2, 3, 150, 21, 32, "NONE"], [3, 3, 150, 21, 32, "NONE"], [2, 7, 150, 24, 39, "NONE"],
                    [3, 7, 150, 24, 39, "NONE"], [2, 8, 150, 24, 39, "NONE"], [3, 8, 150, 24, 39, "NONE"])},
    {"id": 153, "constant": "RID_COR_INN", "kodClass": "CorInn", "name": "Cibilo Creek Inn", "roo": "corinn.roo",
     "music": "bar2.mid", "baseLight": NICE, "outsideFactor": 3, "teleport": {"row": 5, "col": 5}, "noCombat": True,
     "exits": exits([3, 1, 150, 11, 59, "NONE"]),
     "lockedDoors": locked(DEFAULT_LOCKED, (10, 7), (10, 4)),
     "npcs": [npc("CorNothInnKeeper", "D'Franco", "cninnk.bgf", "Wirt", 3, 11, angle=2048)]},
    {"id": 154, "constant": "RID_COR_WEAPONSMASTER", "kodClass": "CorWeaponsMaster", "name": "The Weapon Master's Abode",
     "roo": "weaponsm.roo", "music": "smithy.mid", "baseLight": NICE, "outsideFactor": 3, "teleport": {"row": 5, "col": 5},
     "noCombat": True,
     "exits": exits([5, 6, 154, 5, 8, "NONE"], [5, 7, 154, 5, 5, "NONE"], [8, 6, 150, 11, 8, "NONE"]),
     "lockedDoors": locked("Rook lässt dich nicht in seine Privaträume.", (1, 7)),
     "npcs": [npc("CorNothSergeant", "Rook", "cnsarge.bgf", "Waffenmeister", 4, 3, angle=1024, fine_row=16, fine_col=48)]},
    {"id": 155, "constant": "RID_COR_TAILOR", "kodClass": "CorTailor", "name": "The Spindle and the Spinster",
     "roo": "cortail.roo", "music": "city04.mid", "baseLight": NICE, "outsideFactor": 3, "teleport": {"row": 6, "col": 3},
     "noCombat": True,
     "exits": exits([3, 3, 150, 40, 12, "NONE"]),
     "lockedDoors": locked(DEFAULT_LOCKED, (11, 7), (11, 13)),
     "npcs": [npc("CorNothTailor", "Hester Gilk", "cntailor.bgf", "Schneiderin", 4, 13)]},
    {"id": 156, "constant": "RID_COR_UNIV", "kodClass": "CornothUniversity", "name": "The Hall of Genealogy",
     "roo": "coruniv.roo", "music": "main.mid", "baseLight": NICE, "outsideFactor": 3, "teleport": {"row": 3, "col": 25},
     "noCombat": True,
     "exits": exits([11, 25, 150, 12, 33, "NONE"])},
]

write_area("cor", rooms, source="kod/object/active/holder/room/crnthrm (Meridian 59), erzeugt mit tools/make_cor.py")
