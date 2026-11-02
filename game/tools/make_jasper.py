#!/usr/bin/env python3
"""Erzeugt server/data/world/jasper.json aus dem Original (kod/object/active/holder/room/jasperrm). Werte: docs/jasper.md.

Aufruf (im Ordner game): python3 tools/make_jasper.py
"""
from kodarea import exits, locked, npc, sign, write_area

NICE, MUSIC = 164, "city03.mid"   # JasperRoom: prMusic city03.mid für alle Räume ohne eigene Musik
DEFAULT_LOCKED = "Diese Tür ist verschlossen."


def indoor(rid, const, cls, name, roo, teleport, out, music=MUSIC, no_combat=True, **extra):
    room = {"id": rid, "constant": const, "kodClass": cls, "name": name, "roo": roo, "music": music,
            "baseLight": NICE, "outsideFactor": 3, "teleport": {"row": teleport[0], "col": teleport[1]},
            "exits": exits(*out)}
    if no_combat:
        room["noCombat"] = True
    room.update(extra)
    return room


# Verlassene Häuser (jasab1–14): Name, Ankunft, Ausgänge zurück nach West-Jasper (382)
ABANDONED = [
    (351, "Old Granary", (11, 13), [[12, 13, 382, 35, 25, "NONE"]]),
    (352, "Icehouse", (7, 7), [[8, 1, 382, 35, 25, "NONE"]]),
    (353, "Old Schoolhouse", (15, 5), [[17, 15, 382, 44, 38, "NONE"]]),
    (354, "Deserted Eating House", (8, 4), [[9, 13, 382, 35, 25, "NONE"], [14, 7, 382, 35, 25, "NONE"]]),
    (355, "Abandoned Building", (10, 9), [[13, 6, 382, 35, 25, "NONE"]]),
    (356, "Abandoned Building", (6, 12), [[5, 8, 382, 35, 25, "NONE"]]),
    (357, "Abandoned Building", (9, 7), [[8, 10, 382, 35, 25, "NONE"]]),
    (358, "Abandoned Building", (6, 7), [[8, 10, 382, 35, 25, "NONE"]]),
    (359, "Abandoned Building", (7, 6), [[5, 9, 382, 35, 25, "NONE"]]),
    (360, "Disused Shop", (8, 5), [[6, 2, 382, 35, 25, "NONE"]]),
    (361, "Abandoned Counting Room", (5, 4), [[6, 2, 382, 52, 20, "NONE"]]),
    (362, "Old Barracks", (3, 5), [[1, 5, 382, 35, 25, "NONE"]]),
    (363, "Dusty Chamber", (12, 10), [[5, 8, 382, 35, 25, "NONE"]]),
    (364, "Warm Haven", (9, 13), [[8, 20, 382, 35, 25, "NONE"]]),
]

rooms = [
    {"id": 350, "constant": "RID_JASPER", "kodClass": "Jasper", "name": "East Jasper", "roo": "jas-east.roo",
     "music": MUSIC, "baseLight": NICE, "outsideFactor": 8, "outdoors": True, "teleport": {"row": 40, "col": 54},
     "exits": exits([36, 34, 382, 43, 37, "NONE"], [37, 34, 382, 44, 37, "NONE"], [38, 34, 382, 45, 37, "NONE"],
                    [44, 32, 382, 51, 35, "NONE"], [45, 32, 382, 52, 35, "NONE"], [52, 26, 382, 60, 32, "NONE"],
                    [52, 27, 382, 60, 32, "NONE"], [41, 57, 374, 6, 6, "NONE"], [42, 58, 374, 6, 6, "NONE"],
                    [49, 39, 373, 19, 3, "NONE"], [59, 31, 372, 13, 7, "NONE"], [59, 32, 372, 13, 7, "NONE"],
                    [59, 33, 372, 13, 7, "NONE"], [29, 61, 710, 19, 11, "NONE"], [29, 62, 710, 19, 11, "NONE"]),
     "lockedDoors": locked("Die Hütte des alten Schürfers ist wohl zu baufällig, um sie zu betreten.", (39, 49))
                    + locked("Fest verschlossen trotzt das Arsenal der Stadt jedem Eindringling.", (48, 34))
                    + locked("Zum Kornspeicher hast du keinen Zutritt.", (54, 39)),
     "edgeExits": [{"edge": "EAST", "to": 568, "toConstant": "RID_F8", "toRow": 14, "toCol": 7, "rotate": "315"},
                   {"edge": "SOUTH", "to": 704, "toConstant": "RID_GUILDH4", "toRow": 50, "toCol": 27, "rotate": "180"}]},
    {"id": 382, "constant": "RID_JAS_WEST", "kodClass": "JasperWest", "name": "West Jasper", "roo": "jas-west.roo",
     "music": MUSIC, "baseLight": NICE, "outsideFactor": 8, "outdoors": True, "teleport": {"row": 37, "col": 25},
     "exits": exits([43, 39, 350, 36, 34, "NONE"], [44, 39, 350, 37, 34, "NONE"], [45, 39, 350, 38, 34, "NONE"],
                    [51, 37, 350, 44, 33, "NONE"], [52, 37, 350, 44, 33, "NONE"], [59, 32, 350, 51, 28, "NONE"],
                    [59, 33, 350, 51, 28, "NONE"], [44, 25, 370, 3, 8, "NONE"], [44, 26, 370, 3, 8, "NONE"],
                    [64, 20, 371, 3, 6, "NONE"], [64, 21, 371, 3, 6, "NONE"], [31, 27, 375, 10, 6, "NONE"],
                    [32, 28, 375, 10, 6, "NONE"], [52, 20, 376, 7, 7, "NONE"], [53, 20, 376, 7, 7, "NONE"],
                    [35, 29, 377, 29, 6, "NONE"], [43, 37, 353, 16, 15, "NONE"], [43, 38, 353, 16, 15, "NONE"]),
     "lockedDoors": locked(DEFAULT_LOCKED, (49, 36), (51, 33), (43, 32), (30, 22), (34, 21), (43, 19), (42, 15), (38, 15),
                           (40, 19), (63, 18), (45, 34), (39, 19), (42, 19), (64, 22), (63, 30)),
     "edgeExits": [{"edge": "WEST", "to": 547, "toConstant": "RID_D7", "toRow": 40, "toCol": 48, "rotate": "WEST"},
                   {"edge": "NORTH", "to": 557, "toConstant": "RID_E7", "toRow": 48, "toCol": 32, "rotate": "NORTH"}]},
    indoor(376, "RID_JAS_BANK", "JasBank", "The Royal Bank of Jasper", "jasbank.roo", (7, 7),
           [[6, 9, 382, 53, 21, "NONE"], [7, 9, 382, 53, 21, "NONE"]], music="bank.mid",
           npcs=[npc("JasperBanker", "Yevitan", "jsbanker.bgf", "Bankier", 8, 5, angle=512, fine_row=44, fine_col=36),
                 sign("Bank", "Mit deposit, withdraw (Betrag) und balance verwaltest du dein Bankkonto.", 5, 8, 24, 16)]),
    indoor(375, "RID_JAS_ELDER_HUT", "JasperElderHut", "The Home of the Wise Man", "jaselder.roo", (9, 7),
           [[11, 5, 382, 32, 26, "NONE"], [12, 6, 382, 32, 26, "NONE"]], music="bar2.mid",
           npcs=[npc("JasperElder", "Afiera D'xor", "jselder.bgf", "Weiser", 8, 13, angle=1536)]),
    indoor(372, "RID_JAS_HALL", "JasperHall", "Adventurer's Hall of Jasper", "jashall.roo", (13, 7),
           [[14, 7, 350, 60, 32, "NONE"]], music="main.mid"),
    indoor(370, "RID_JAS_INN", "JasperInn", "Yonder Inn of Jasper", "jasinn.roo", (4, 10),
           [[2, 8, 382, 42, 25, "NONE"]], music="bar2.mid",
           npcs=[npc("JasperInnkeeper", "Widow Qesino", "jsinnk.bgf", "Wirtin", 6, 6, angle=3584)]),
    indoor(374, "RID_JAS_SMITHY", "JasperSmithy", "Quintor's Smithy", "jassmith.roo", (9, 9),
           [[5, 5, 350, 40, 56, "NONE"]], music="smithy.mid",
           npcs=[npc("JasperBlacksmith", "Quintor", "jssmith.bgf", "Waffenschmied", 13, 13, angle=2560)]),
    indoor(373, "RID_JAS_STORE", "JasperStore", "General Store of Jasper", "jasstore.roo", (19, 5),
           [[19, 1, 350, 49, 38, "NONE"], [19, 2, 350, 49, 38, "NONE"]], music="bank.mid",
           npcs=[npc("JasperMerchant", "Zhieu B'hob", "jsmerch.bgf", "Händler", 18, 8, angle=2048)]),
    indoor(371, "RID_JAS_TAVERN", "JasperTavern", "Pietro's Wicked Brews", "jastavrn.roo", (8, 6),
           [[2, 6, 382, 63, 20, "NONE"]], music="bar.mid",
           npcs=[npc("JasperBartender", "Pietro", "jsbart.bgf", "Schankwirt", 15, 6, angle=3072)]),
] + [indoor(rid, f"RID_JAS_AB{rid - 350}", f"JasperAB{rid - 350}", name, f"jasab{rid - 350}.roo", tp, out,
            no_combat=False) for rid, name, tp, out in ABANDONED]
rooms[-12]["lockedDoors"] = locked(DEFAULT_LOCKED, (7, 2))   # Old Schoolhouse (353)

write_area("jasper", rooms, source="kod/object/active/holder/room/jasperrm (Meridian 59), erzeugt mit tools/make_jasper.py")
