#!/usr/bin/env python3
"""Erzeugt server/data/world/marion.json aus dem Original (kod/object/active/holder/room/marnrm, monsroom/objroom/mar*,
monsroom/marcryp1/2, marcry3a). Werte siehe docs/marion.md.

Aufruf (im Ordner game): python3 tools/make_marion.py
"""
from kodarea import exits, locked, npc, sign, write_area

SOME, NICE, DARK, VERY_DARK = 5, 164, 124, 84

SIGNS = {
    "mimosa": "Dieser Baum erinnert an einen gefallenen Freund. Am Boden ist eine kleine Metallplakette angebracht, "
              "daneben liegen ein paar getrocknete Rosenblätter.\n\n"
              "„Die jungen Mädchen schnitten sich das Haar, als du gingst. An jenem Tag wurde kein Brot gebacken, und "
              "der Wein schmeckte sauer. Wir schleppten uns im strömenden Regen den Hügel hinauf, um dich auf den Weg "
              "zu schicken – ein Stück weiter in die Ewigkeit als deine regennassen Gefährten. Diana behielt dein "
              "Schwert, Aspasia deinen schlammigen Umhang aus feiner roter Wolle. Ich behielt die Erinnerung an dich – "
              "und die dunkle Tunika, mit der wir dir das Gesicht abwischten. Mehr hätte ich nicht getan.“",
    "corpse": "Die Gespenstermumien, die diese Gruft heimsuchen, waren einst einfache mumifizierte Leichen wie diese.",
    "warning": "Auf Befehl König Far'Nohls darf niemand diesen Punkt überschreiten. Dieser Ort ist der Zivilisation "
               "verloren, und wer ihn betritt, wird wahrscheinlich sterben. Wer diesen Befehl missachtet, findet "
               "kein Erbarmen.",
}

rooms = [
    {"id": 200, "constant": "RID_MARION", "kodClass": "Marion", "name": "Marion", "roo": "marion.roo",
     "music": "city02.mid", "baseLight": NICE, "outsideFactor": 10, "outdoors": True, "terrain": "village",
     "teleport": {"row": 56, "col": 39}, "noCombat": True,
     "exits": exits([32, 16, 205, 4, 10, "NONE"], [56, 54, 202, 4, 3, "NONE"], [62, 58, 202, 12, 5, "NONE"],
                    [50, 21, 204, 6, 20, "NONE"], [31, 43, 201, 11, 4, "NONE"], [80, 15, 2600, 11, 17, "NONE"]),
     "lockedDoors": locked("Dieses Tor wurde errichtet, um die Bürger vor den Gefahren der Gruft zu schützen.",
                           (70, 13), (70, 14), (71, 13), (71, 14)),
     "npcs": [npc("MarionElder", "Ran er'Hoth", "mrelder.bgf", "Ältester", 80, 37, angle=3072),
              dict(sign("Der Mimosenbaum", SIGNS["mimosa"], 45, 7), icon="treebot.bgf")],
     # Bäume (Tree, Eiche über Ulme) – für den Godot-Client als Zierobjekte
     "objects": [{"type": "TREE", "row": r, "col": c, "fineRow": 32, "fineCol": fc, "angle": 0}
                 for r, c, fc in [(5, 17, 32), (5, 24, 32), (2, 21, 0)]]},
    {"id": 201, "constant": "RID_MAR_SMITHY", "kodClass": "MarionSmithy", "name": "Ye Olde Slasher Salesman",
     "roo": "marsmith.roo", "music": "smithy.mid", "baseLight": NICE, "outsideFactor": SOME,
     "teleport": {"row": 10, "col": 4}, "noCombat": True,
     "exits": exits([11, 4, 200, 32, 43, "NONE"], [12, 4, 200, 32, 43, "NONE"]),
     "npcs": [npc("MarionBlacksmith", "Colhorr", "mrsmith.bgf", "Schmied", 7, 4, angle=1024)]},
    {"id": 202, "constant": "RID_MAR_INN", "kodClass": "MarionInn", "name": "The Limping Toad Inn and Tavern",
     "roo": "marinn.roo", "music": "bar.mid", "baseLight": NICE, "outsideFactor": SOME,
     "teleport": {"row": 11, "col": 6}, "noCombat": True,
     "exits": exits([4, 1, 200, 56, 53, "NONE"], [12, 5, 200, 62, 57, "NONE"], [12, 4, 200, 62, 57, "NONE"]),
     "lockedDoors": locked("Die Tür rührt sich nicht. Seltsam, sie scheint sich gar nicht öffnen zu lassen. "
                           "Es gibt ein kleines Schlüsselloch …",
                           (5, 9), (5, 12), (5, 15), (5, 16), (7, 11), (7, 12), (7, 15), (7, 16)),
     "npcs": [npc("MarionInnkeeper", "Morrigan", "mrinnk.bgf", "Wirt", 2, 4, angle=1024, fine_row=48),
              npc("MarionBartender", "Tova", "mrbart.bgf", "Schankwirt", 14, 9, angle=3072, fine_row=48, fine_col=0)]},
    {"id": 203, "constant": "RID_MAR_ELDER_HUT", "kodClass": "MarionElderHut", "name": "The home of the elder",
     "roo": "marelder.roo", "music": "innmusic.mid", "baseLight": NICE, "outsideFactor": SOME,
     "teleport": {"row": 8, "col": 3},
     "exits": exits([11, 4, 200, 55, 28, "45"])},
    {"id": 204, "constant": "RID_MAR_HALL", "kodClass": "MarHall", "name": "The Adventurer's Hall of Marion",
     "roo": "marhall.roo", "music": "main.mid", "baseLight": NICE, "outsideFactor": 3,
     "teleport": {"row": 5, "col": 8}, "noCombat": True,
     "exits": exits([6, 21, 200, 50, 22, "NONE"], [7, 21, 200, 50, 22, "NONE"])},
    {"id": 205, "constant": "RID_MAR_HEALER_SHOP", "kodClass": "MarionHealerShop", "name": "Lady Aftyn's Sanctuary",
     "roo": "marheal.roo", "music": "main.mid", "baseLight": NICE, "outsideFactor": 3,
     "teleport": {"row": 4, "col": 7}, "noCombat": True,
     "exits": exits([4, 10, 200, 32, 17, "NONE"], [5, 10, 200, 32, 17, "NONE"]),
     "npcs": [npc("MarionHealer", "Lady Aftyn", "mrhealer.bgf", "Heilerin", 5, 3, fine_row=14)]},
    {"id": 2600, "constant": "RID_MAR_CRYPT1", "kodClass": "MarionCrypt1", "name": "The crypt in Marion",
     "roo": "MarDun01.roo", "music": "nec01.mid", "baseLight": DARK, "outsideFactor": 0,
     "teleport": {"row": 11, "col": 23},
     "exits": exits([11, 17, 200, 80, 15, "NONE"], [38, 6, 2601, 3, 26, "NONE"], [38, 7, 2601, 3, 27, "NONE"]),
     "generators": [{"row": r, "col": c} for r, c in
                    [(5, 21), (5, 36), (9, 28), (17, 35), (21, 23), (25, 11), (29, 18), (37, 35), (38, 19)]],
     "monsters": [{"kind": "spectralmummy", "weight": 100}], "maxMonsters": 10,
     # Hebelrätsel (marcryp1.kod CreateLevers, LeverChanged, CheckAllDeadMonsters, OpenDoorDown)
     "puzzle": "marioncrypt",
     "npcs": [dict(sign("Mumifizierte Leiche", SIGNS["corpse"], r, c), icon="mummyX.bgf") for r, c in
              [(19, 18), (23, 8), (25, 14), (32, 14), (30, 40), (27, 33), (19, 32), (17, 40), (13, 40),
               (9, 34), (4, 28), (2, 26), (2, 40)]]
             + [dict(npc("GuestLever", "Hebel", "lever.bgf", "Hebel", 27, c),
                     description="Dieser Hebel sieht alt und abgenutzt aus, muss aber noch etwas bewegen.") for c in (4, 9)]},
    {"id": 2601, "constant": "RID_MAR_CRYPT2", "kodClass": "MarionCrypt2", "name": "Resting place of Marion's ancestors",
     "roo": "MarDun02.roo", "music": "Song.mid", "baseLight": DARK, "outsideFactor": 0,
     "puzzle": "marioncrypt2",   # marcryp2.kod: lebende Statuen bewachen die Tür zu Gruft 3a
     "teleport": {"row": 17, "col": 18},
     "exits": exits([2, 26, 2600, 37, 6, "NONE"], [2, 27, 2600, 37, 7, "NONE"],
                    [38, 26, 2602, 31, 17, "NONE"], [38, 27, 2602, 31, 17, "NONE"]),
     "generators": [{"row": r, "col": c} for r, c in
                    [(7, 16), (11, 8), (14, 6), (17, 2), (18, 3), (24, 7), (23, 9), (26, 8), (27, 5), (31, 7), (32, 3),
                     (28, 11), (29, 15), (36, 14), (37, 20), (31, 25), (32, 27), (33, 31), (34, 35), (31, 37), (31, 43),
                     (27, 45), (24, 46), (22, 45), (21, 48), (19, 42), (23, 41), (12, 50), (13, 45), (16, 45), (11, 42),
                     (8, 39), (3, 41), (2, 38), (5, 37)]],
     "monsters": [{"kind": "skeleton", "weight": 80}, {"kind": "batteredskeleton", "weight": 20}], "maxMonsters": 25,
     "npcs": [sign("Warnung!", SIGNS["warning"], 38, 27)]},
    {"id": 2602, "constant": "RID_MAR_CRYPT3A", "kodClass": "MarionCrypt3a", "name": "Affirmation of the Forsaken",
     "roo": "res00001.roo", "music": "song10.mid", "baseLight": VERY_DARK, "outsideFactor": 0,
     "teleport": {"row": 18, "col": 21},
     "exits": exits([30, 17, 2601, 37, 27, "NONE"], [30, 18, 2601, 37, 27, "NONE"]),
     "lockedDoors": locked("Deine Mühe ist vergebens, es hält stand.",
                           (22, 26), (23, 26), (14, 8), (14, 9), (15, 8), (15, 9), (31, 27), (31, 28), (32, 27), (32, 28)),
     "generators": [{"row": r, "col": c} for r, c in [(17, 5), (17, 19), (21, 16), (24, 9), (13, 17), (24, 28), (27, 31)]],
     "monsters": [{"kind": "thrasher", "weight": 100}], "maxMonsters": 15},
]

write_area("marion", rooms, source="kod/object/active/holder/room/marnrm, monsroom/objroom/mar*, monsroom/marcry* "
           "(Meridian 59), erzeugt mit tools/make_marion.py")
