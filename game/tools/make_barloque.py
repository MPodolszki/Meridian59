#!/usr/bin/env python3
"""Erzeugt server/data/world/barloque.json aus dem Original (kod/object/active/holder/room/barlqrm, objroom/barlbar1).
Werte: docs/barloque.md. Aufruf (im Ordner game): python3 tools/make_barloque.py
"""
from kodarea import exits, kod, locked, npc, ornaments, sign, write_area

NICE, MUSIC = 164, "corloth.mid"
DEFAULT_LOCKED = "Diese Tür ist verschlossen."
RENT_DOOR = ("Die Tür rührt sich nicht. Seltsam, sie scheint sich gar nicht öffnen zu lassen. Es gibt ein kleines "
             "Schlüsselloch – vielleicht passiert etwas, wenn du einen Schlüssel benutzt.")


def indoor(rid, const, cls, name, roo, teleport, out, music, outside=3, **extra):
    room = {"id": rid, "constant": const, "kodClass": cls, "name": name, "roo": roo, "baseLight": NICE,
            "outsideFactor": outside, "teleport": {"row": teleport[0], "col": teleport[1]}, "noCombat": True,
            "exits": exits(*out)}
    if music:
        room["music"] = music
    room.update(extra)
    return room


def guard(row, col, fine_row, fine_col, angle):
    return npc("DukeGuard", "Keep Guard", "dukegard.bgf", "Wache", row, col, angle=angle, fine_row=fine_row, fine_col=fine_col)


rooms = [
    {"id": 101, "constant": "RID_BAR_NORTH", "kodClass": "BarloqueNorth", "name": "North Barloque", "roo": "barln.roo",
     "music": MUSIC, "baseLight": NICE, "outsideFactor": 10, "outdoors": True, "teleport": {"row": 25, "col": 42},
     "exits": exits([2, 48, 107, 27, 27, "NONE"], [2, 49, 107, 27, 27, "NONE"], [47, 41, 108, 56, 9, "NONE"],
                    [48, 41, 108, 56, 9, "NONE"], [13, 16, 104, 5, 4, "NONE"], [16, 36, 105, 25, 8, "NONE"],
                    [16, 37, 105, 25, 8, "NONE"], [19, 26, 106, 16, 12, "180"], [28, 59, 103, 21, 11, "NONE"],
                    [28, 60, 103, 21, 11, "NONE"], [21, 15, 714, 2, 33, "NONE"], [21, 16, 714, 2, 33, "NONE"],
                    [44, 52, 102, 4, 38, "NONE"], [45, 52, 102, 4, 38, "NONE"], [46, 52, 102, 4, 38, "NONE"],
                    [63, 39, 102, 23, 28, "NONE"], [64, 39, 102, 23, 28, "NONE"], [65, 39, 102, 23, 28, "NONE"],
                    [35, 54, 970, 3, 4, "NONE"], [35, 55, 970, 3, 4, "NONE"], [38, 52, 970, 4, 3, "NONE"],
                    [32, 62, 971, 2, 7, "NONE"]),
     "lockedDoors": locked(DEFAULT_LOCKED, (26, 55), (16, 22), (13, 30), (15, 21)),
     "objects": ornaments(kod / "barlqrm/barln.kod")},
    {"id": 102, "constant": "RID_BAR_SOUTH", "kodClass": "BarloqueSouth", "name": "South Barloque", "roo": "barls.roo",
     "music": MUSIC, "baseLight": NICE, "outsideFactor": 10, "outdoors": True, "teleport": {"row": 34, "col": 43},
     "exits": exits([3, 37, 101, 45, 51, "NONE"], [4, 37, 101, 45, 51, "NONE"], [5, 37, 101, 45, 51, "NONE"],
                    [22, 27, 101, 64, 38, "NONE"], [23, 27, 101, 64, 38, "NONE"], [24, 27, 101, 64, 38, "NONE"],
                    [46, 30, 593, 7, 11, "NONE"], [46, 31, 593, 7, 12, "NONE"], [33, 20, 700, 18, 6, "NONE"],
                    [33, 21, 700, 18, 6, "NONE"], [16, 42, 702, 18, 32, "NONE"], [17, 42, 702, 18, 32, "NONE"],
                    [36, 13, 109, 11, 3, "NONE"], [28, 42, 113, 3, 2, "NONE"]),
     "lockedDoors": locked(DEFAULT_LOCKED, (37, 19), (39, 25), (37, 36), (37, 37), (39, 43), (35, 48), (33, 28),
                           (22, 45), (27, 40), (28, 40), (10, 47), (4, 46), (17, 44), (38, 7)),
     "objects": ornaments(kod / "barlqrm/barls.kod")},
    indoor(103, "RID_BAR_TAVERN", "BarlBar1", "The Bhrama & Falcon", "barlbar1.roo", (4, 7),
           [[22, 11, 101, 29, 60, "NONE"]], "bar2.mid", outside=5,
           npcs=[npc("BarloqueBartender", "Meidei", "bqbart.bgf", "Schankwirt", 10, 13)]),
    indoor(104, "RID_BAR_APOTH", "BarApoth", "Joguer's Herbs and Roots", "barapoth.roo", (5, 5),
           [[6, 4, 101, 14, 16, "NONE"], [7, 4, 101, 14, 16, "NONE"]], "smithy.mid",
           npcs=[npc("BarloqueApothecary", "Joguer", "bqapoth.bgf", "Kräuterkundiger", 3, 6, angle=1024)]),
    indoor(105, "RID_BAR_HALL", "BarHall", "Adventurer's Hall of Barloque", "barhall.roo", (5, 5),
           [[26, 8, 101, 17, 36, "NONE"]], "main.mid"),
    indoor(106, "RID_BAR_INN", "BarInn", "Brownestone Inn", "barinn.roo", (5, 6),
           [[17, 12, 101, 18, 26, "180"]], "bar.mid",
           lockedDoors=locked(RENT_DOOR, (1, 5), (2, 5), (2, 7), (3, 7), (7, 5), (11, 6), (11, 7), (4, 12), (4, 13), (4, 17)),
           npcs=[npc("BarloqueInnkeeper", "Pritchett", "bqinnk.bgf", "Wirt", 12, 15, angle=1536)]),
    {"id": 107, "constant": "RID_BAR_PORT", "kodClass": "BarloquePort", "name": "The Ports of Barloque", "roo": "barlport.roo",
     "music": MUSIC, "baseLight": NICE, "outsideFactor": 10, "outdoors": True, "teleport": {"row": 25, "col": 29},
     "exits": exits([29, 27, 101, 4, 49, "NONE"], [29, 26, 101, 4, 49, "NONE"], [22, 5, 709, 9, 23, "NONE"],
                    [11, 55, 114, 3, 11, "NONE"]),
     "lockedDoors": locked(DEFAULT_LOCKED, (15, 4), (23, 13), (24, 13), (10, 10), (10, 11), (9, 15), (9, 16), (9, 19),
                           (17, 26), (18, 26), (20, 26), (14, 32), (14, 33), (13, 32), (13, 33), (12, 34), (12, 35),
                           (13, 34), (13, 25), (24, 41), (24, 45), (25, 45), (22, 52), (19, 57), (19, 58), (10, 47),
                           (10, 48), (10, 50))},
    indoor(109, "RID_BAR_MERCHANT", "BarMerchant", "The Sparkling Stone Shop", "barmerch.roo", (11, 5),
           [[12, 3, 102, 37, 13, "NONE"], [12, 4, 102, 37, 13, "NONE"], [13, 3, 102, 37, 13, "NONE"],
            [13, 4, 102, 37, 13, "NONE"]], "smithy.mid",
           npcs=[npc("BarloqueMerchant", "Herbutte", "bqmerch.bgf", "Edelsteinhändler", 9, 3, angle=1024)]),
    indoor(113, "RID_BAR_SMITHY", "BarloqueSmithy", "The Royal Blacksmith of Barloque", "barsmith.roo", (4, 2),
           [[3, 1, 102, 28, 41, "NONE"]], "smithy.mid", outside=0,
           npcs=[npc("BarloqueBlacksmith", "Fehr'loi Qan", "bqsmith.bgf", "Königlicher Schmied", 4, 7)]),
    indoor(114, "RID_BAR_VAULT", "BarVault", "Office of the Barloque Vaultman", "barvault.roo", (2, 5),
           [[3, 12, 107, 10, 55, "NONE"], [2, 12, 107, 10, 55, "NONE"]], "smithy.mid",
           npcs=[npc("Barloquevaultman", "Obert Cair'bre", "bqvaultm.bgf", "Lagerverwalter", 4, 11, fine_row=48, fine_col=16),
                 sign("Willkommen im Lager von Barloque", "Mit DEPOSIT lagerst du Gegenstände ein, mit WITHDRAW holst du "
                      "sie zurück. Das kostet eine Gebühr; Schillinge werden nicht eingelagert.", 5, 9)]),
    indoor(970, "RID_BAR_COURT", "BarloqueCourt", "Office of the Justicar", "barcourt.roo", (6, 12),
           [[2, 4, 101, 33, 54, "NONE"], [4, 2, 101, 38, 51, "NONE"], [6, 11, 970, 8, 11, "NONE"], [7, 11, 970, 5, 11, "NONE"]],
           None, outside=0,
           npcs=[npc("BarloqueClerk", "Caramo", "bqclerk.bgf", "Gerichtsschreiberin", 12, 12, angle=2048, fine_row=28, fine_col=28),
                 guard(10, 1, 60, 28, 0),
                 dict(sign("Justiziar", "Der Justiziar begnadigt Gesetzlose und Mörder und wird gewählt: Bei Caramo kauft "
                           "man einen Wahlzettel, schreibt einen Namen darauf und gibt ihn zurück. Gesetzlose, Mörder und "
                           "Neulinge dürfen nicht wählen.", 11, 12, 0, 32), icon="newbsign.bgf")]),
    indoor(971, "RID_BAR_JAIL", "BarloqueJail", "Old Jailhouse", "barjail.roo", (6, 12),
           [[2, 7, 101, 31, 62, "NONE"]], None,
           lockedDoors=locked(DEFAULT_LOCKED, (6, 2)),
           npcs=[guard(3, 14, 32, 0, 1024), guard(14, 14, 32, 0, 3072), guard(2, 2, 32, 32, 512)]),
    indoor(700, "RID_GM_HALL", "GMHall", "The Guildmaster's Hall", "gmhall.roo", (8, 7),
           [[19, 6, 102, 34, 20, "NONE"], [19, 7, 102, 34, 21, "NONE"]], "bar.mid",
           npcs=[npc("GuildCreator", "Frular", "gcreator.bgf", "Gildenmeister", 5, 7, angle=1024, fine_row=0, fine_col=0)]),
]

write_area("barloque", rooms, source="kod/object/active/holder/room/barlqrm, monsroom/objroom/barlbar1 (Meridian 59), "
           "erzeugt mit tools/make_barloque.py")
