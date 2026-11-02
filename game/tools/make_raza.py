#!/usr/bin/env python3
"""Erzeugt server/data/world/raza.json aus dem Original (kod/object/active/holder/room/guest*, newb*).

Aufruf (im Ordner game): python3 tools/make_raza.py
Räume, Ausgänge, Türen, Licht und Bewohner stehen unten als Daten (Werte aus KOD, siehe docs/raza.md);
die Zierobjekte (Bäume, Heu, Lampen …) des Dorfs werden aus guest2.kod gelesen. Größe (rows/cols) kommt aus
den Raumlayouts client/rooms/guestN.json (tools/roo2layout.py).
"""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
kod = root.parent / "kod/object/active/holder/room"

ANGLE = {"ANGLE_EAST": 0, "ANGLE_SOUTH_EAST": 512, "ANGLE_SOUTH": 1024, "ANGLE_SOUTH_WEST": 1536,
         "ANGLE_WEST": 2048, "ANGLE_NORTH_WEST": 2560, "ANGLE_NORTH": 3072, "ANGLE_NORTH_EAST": 3584}


def ornaments(path):
    """Create(&OrnamentalObject,#type=X) bzw. Create(&Lamp) mit #new_row/#new_col/#fine_*/#new_angle."""
    text = path.read_text(encoding="latin-1")
    out = []
    pattern = re.compile(r"Create\(&(OrnamentalObject,#type=(\w+)|Lamp)\)\s*,\s*(#[^)]*?)\)", re.S)
    for m in pattern.finditer(text):
        kind = m.group(2) or "Lamp"
        args = dict(re.findall(r"#(\w+)\s*=\s*(\w+)", m.group(3)))
        out.append({"type": kind, "row": int(args["new_row"]), "col": int(args["new_col"]),
                    "fineRow": int(args.get("fine_row", 32)), "fineCol": int(args.get("fine_col", 32)),
                    "angle": ANGLE.get(args.get("new_angle", ""), 0)})
    return out


def exits(*entries):
    """[zeile, spalte, ziel, zielzeile, zielspalte, winkel?]"""
    return [{"row": r, "col": c, "to": to, "toRow": tr, "toCol": tc, "rotate": rot}
            for r, c, to, tr, tc, rot in entries]


def locked(text, *cells):
    return [{"row": r, "col": c, "message": text} for r, c in cells]


def sign(name, text, row, col, fine_row=32, fine_col=32):
    """Schild (sign.kod, #newbie=TRUE → razsign1.bgf): Name und Text beim Anschauen."""
    return {"kodClass": "Sign", "name": name, "icon": "razsign1.bgf", "role": "Schild", "description": text,
            "row": row, "col": col, "fineRow": fine_row, "fineCol": fine_col}


def npc(kod_class, name, icon, role, row, col, angle=0, fine_row=32, fine_col=32):
    return {"kodClass": kod_class, "name": name, "icon": icon, "role": role, "row": row, "col": col,
            "fineRow": fine_row, "fineCol": fine_col, "angle": angle}


SIGNS = {
    "welcome": "Willkommen im Spiel! Wir hoffen, dass dir diese einzigartige Online-Welt gefällt.\n\n"
               "Zuerst lernst du zu gehen: Mit W und S gehst du vor und zurück, mit den Pfeiltasten drehst du dich.\n\n"
               "Viele Dinge in der Welt kannst du mit der rechten Maustaste anschauen – diese Schilder zum Beispiel, "
               "oder den Wirt.\n\nDurch Türen gehst du mit der Leertaste.\n-------------------\n"
               "Wenn du bereit bist, geh zur Ausgangstür (vorne links), drück die Leertaste und lies das nächste Schild, "
               "das du gleich draußen siehst.",
    "outside": "Jetzt, da du draußen bist, kannst du das Laufen richtig üben. Mit Shift gehst du langsamer, mit A und D "
               "trittst du zur Seite. Mit Ende drehst du dich schnell um.\n\nAuf der Karte kannst du mit der rechten "
               "Maustaste eigene Notizen eintragen.\n-------------------\n"
               "Danach geh zur Schmiede im Nordflügel des Steinhauses am Westende des Teichs.",
    "buying": "Du bist fast mit leeren Händen in diese Welt gekommen, aber ein wenig Geld hast du. Geh zum Schmied, "
              "drück Enter und tippe BUY: Er zeigt dir, was er verkauft. Kauf zuerst eine Waffe, dann eine Rüstung.\n\n"
              "Unbewaffnet bist du trotzdem noch: Schau dir die Waffe im Inventar an und benutze sie (USE). Ein gelber "
              "Schein zeigt, dass du sie führst. Genauso mit der Rüstung. Mit UNUSE legst du etwas ab; ein Doppelklick "
              "im Inventar geht auch.\n-------------\n"
              "Danach geh in die Schenke, das große braune Haus am Ostende des Dorfs.",
    "selling": "Manchmal möchte ein Händler oder Dorfbewohner, dass du ihm etwas bringst. Noch öfter findest du nach "
               "einem Kampf Dinge, die du selbst nicht brauchst, aber verkaufen kannst – etwa Entwurzelbeeren von den "
               "Mumien. Mit OFFER (Strg+O) bietest du sie Ravi, dem Apotheker, an.\n\nNicht jeder will alles: Ravi "
               "kauft keine Waffen. Sag „buy“ oder „sell“, dann erfährst du, was ein Händler nimmt.\n"
               "--------------------\nHier endet deine Einführung. Schau dich um und probier Neues aus – vielleicht im "
               "Großen Museum, das Ausstellungen über die Wunder Meridians zeigt.",
    "communication": "Sich zu verständigen ist in Meridian wichtig. Am häufigsten nutzt man SAY: Tippst du SAY HALLO, "
                     "antwortet dir der Älteste in dieser Hütte.\n\nJeder Spieler im selben Gebiet hört, was du sagst. "
                     "Händler und Älteste hören allerdings schlecht – geh nahe heran.\n\nMit BROADCAST erreichst du alle, "
                     "die gerade spielen. Füll diesen Kanal nicht mit Unsinn; jede Rundnachricht kostet die Hälfte "
                     "deines Manas.\n-------------------------\nSag QUEST, um zu erfahren, was du als Nächstes tun sollst.",
    "health": "Oben siehst du drei Werte: Lebenspunkte, Mana und Ausdauer. Lebenspunkte verlierst du, wenn du Schaden "
              "nimmst; bei null stirbst du. Mana bestimmt, wie viele Zauber du wirken kannst. Beides kommt mit der "
              "Zeit zurück.\n\nAm wichtigsten ist die Ausdauer: Von ihr hängt ab, wie schnell Lebenspunkte und Mana "
              "zurückkehren, und Rennen kostet Ausdauer. Du gewinnst sie durch Essen (im Inventar benutzen) und durch "
              "Rasten. In Gasthäusern und Schenken geht das doppelt so schnell, und dort gibt es auch Proviant.\n"
              "-----------------------\nGeh von hier zum Haus des Ältesten, der kleinen Hütte südwestlich von hier.",
    "lookingaround": "Wenn dich gerade kein Monster jagt, lern noch etwas: Mit Bild auf und Bild ab schaust du nach oben "
                     "und nach unten in die Grube voller Mumien. Wink ihnen ruhig mit WAVE – sie winken nicht zurück.\n\n"
                     "Bild ab hilft sehr gegen kleinere Gegner, und die meisten frühen Gegner sind kleiner als du. Mit "
                     "Pos1 richtest du den Blick wieder gerade aus.",
    "realm": "Das Portal neben diesem Schild bringt dich in eine sichere Stadt, damit du die Welt von Meridian 59 "
             "entdecken kannst.\n\nJetzt lohnt es sich, weitere Hilfe zu lesen: Dein Schutzengel hat dir Post mit "
             "Hinweisen geschickt (MAIL).\n\nUnd ein letzter Rat: Such dir Freunde. Meridian 59 ist eine Gemeinschaft, "
             "und wer mächtige Verbündete hat, kommt weiter als ein Einzelgänger.\n\nWir sehen uns im Spiel!",
    "bones": "Dieser Knochenhaufen ist zweifellos von einem Leichnam übrig, der seit Dutzenden Meridian-Jahren verwest.",
}

BASE = 1010          # RID_NEWB_BASE
SOME, NICE, DARK = 5, 164, 124

rooms = [
    {"id": 1011, "constant": "RID_RAZA_INN", "kodClass": "Newb1", "name": "Raza Inn", "roo": "guest1.roo",
     "music": "login.mid", "baseLight": 255, "outsideFactor": 10, "teleport": {"row": 7, "col": 4},
     "noCombat": True, "respawnRoom": 1011, "improveCap": 25,
     "exits": exits([1, 6, BASE + 2, 25, 41, "NONE"]),
     "lockedDoors": locked("Die Hintertür ist verschlossen.", (8, 9)),
     "npcs": [npc("HazarInnkeeper", "Marcus", "jsmerch.bgf", "Wirt (verkauft nichts)", 3, 9),
              sign("Willkommen", SIGNS["welcome"], 6, 7)]},
    {"id": 1012, "constant": "RID_RAZA", "kodClass": "Newb2", "name": "Raza", "roo": "guest2.roo",
     "music": "login.mid", "baseLight": 255, "outsideFactor": 10, "outdoors": True, "terrain": "village",
     "teleport": {"row": 23, "col": 41},
     "noCombat": True, "respawnRoom": 1011, "improveCap": 25,
     "lockedDoors": locked("Die Tür des Bauernhauses ist von innen fest verriegelt.", (18, 8), (19, 8))
                    + locked("Die Tür des kleinen Schuppens ist verschlossen.", (27, 6), (28, 6))
                    + locked("Die Tore des Tempels bleiben verschlossen, außer bei Begräbnissen.", (12, 36))
                    + locked("Die Türen des Rathauses sind von innen fest verriegelt.", (19, 18), (20, 18), (21, 18))
                    + locked("Die Tür zu Eric d'Jorns Wohnung ist verschlossen.", (23, 57), (22, 57)),
     "exits": exits([26, 41, BASE + 1, 3, 6, "NONE"],
                    [16, 20, BASE + 3, 6, 8, "NONE"], [17, 19, BASE + 3, 6, 8, "NONE"], [16, 19, BASE + 3, 6, 8, "NONE"],
                    [23, 19, BASE + 4, 3, 5, "NONE"], [24, 19, BASE + 4, 3, 5, "NONE"], [24, 20, BASE + 4, 3, 5, "NONE"],
                    [26, 51, BASE + 5, 2, 2, "NONE"], [26, 52, BASE + 5, 2, 2, "NONE"],
                    [7, 44, BASE + 6, 11, 16, "WEST"], [8, 44, BASE + 6, 11, 16, "WEST"],
                    [18, 57, BASE + 7, 5, 3, "NONE"], [19, 57, BASE + 7, 5, 3, "NONE"],
                    [8, 53, BASE + 8, 12, 11, "NONE"], [8, 54, BASE + 8, 12, 11, "NONE"]),
     "npcs": [sign("Unterwegs", SIGNS["outside"], 20, 41)],
     # Kühe aus newb2.kod (31,56), (35,59), (30,59); die Karte hat nur 30 Zeilen, daher die ersten beiden
     # auf die nächsten Felder am Südrand gesetzt (docs/raza.md).
     "fixedMonsters": [{"kind": "cow", "row": 29, "col": 56}, {"kind": "cow", "row": 28, "col": 60},
                       {"kind": "cow", "row": 30, "col": 59}],
     "objects": ornaments(kod / "guest2.kod")},
    {"id": 1013, "constant": "RID_NEWB3", "kodClass": "Newb3", "name": "Raza Blacksmith", "roo": "guest3.roo",
     "music": "smithy.mid", "baseLight": NICE, "outsideFactor": SOME, "teleport": {"row": 6, "col": 8},
     "noCombat": True, "respawnRoom": 1011, "improveCap": 25,
     "exits": exits([7, 8, BASE + 2, 17, 21, "NONE"], [7, 9, BASE + 2, 17, 21, "NONE"]),
     "npcs": [npc("HazarBlacksmith", "Tomas", "bqsmith.bgf", "Schmied (Waffen, Rüstungen)", 4, 5, angle=1024),
              sign("Einkaufen", SIGNS["buying"], 4, 7)]},
    {"id": 1014, "constant": "RID_NEWB4", "kodClass": "Newb4", "name": "Ravi's Magicks of Raza", "roo": "guest4.roo",
     "music": "login.mid", "baseLight": NICE, "outsideFactor": SOME, "teleport": {"row": 3, "col": 5},
     "noCombat": True, "respawnRoom": 1011, "improveCap": 25,
     "exits": exits([2, 5, BASE + 2, 23, 21, "NONE"], [2, 6, BASE + 2, 23, 21, "NONE"], [3, 6, BASE + 2, 23, 21, "NONE"]),
     "npcs": [npc("HazarApothecary", "Ravi", "kcshopk.bgf", "Apotheker (Reagenzien, Edelsteine)", 6, 4),
              sign("Verkaufen", SIGNS["selling"], 4, 6, 48, 32)]},
    {"id": 1015, "constant": "RID_NEWB5", "kodClass": "Newb5", "name": "Raza Hut", "roo": "guest5.roo",
     "music": "login.mid", "baseLight": NICE, "outsideFactor": SOME, "teleport": {"row": 2, "col": 2},
     "noCombat": True, "respawnRoom": 1011, "improveCap": 25,
     "exits": exits([1, 2, BASE + 2, 25, 52, "NONE"], [1, 3, BASE + 2, 25, 52, "NONE"], [1, 4, BASE + 2, 25, 52, "NONE"]),
     "npcs": [npc("HazarElder", "Rodric d'Stane", "mrelder.bgf", "Ältester", 5, 3),
              sign("Verständigung", SIGNS["communication"], 3, 2, 32, 0)]},
    {"id": 1016, "constant": "RID_RAZA_MAUSOLEUM", "kodClass": "Newb6", "name": "Mausoleum", "roo": "guest6.roo",
     "music": "nec03.mid", "baseLight": DARK, "outsideFactor": 0, "teleport": {"row": 11, "col": 23},
     "respawnRoom": 1011, "improveCap": 25,
     "exits": exits([11, 17, BASE + 2, 7, 43, "WEST"]),
     "generators": [{"row": r, "col": c} for r, c in
                    [(11, 4), (5, 21), (5, 36), (9, 28), (17, 35), (21, 23), (25, 11), (29, 18), (37, 35), (38, 19)]],
     "monsters": [{"kind": "mummy", "weight": 50}, {"kind": "mummynotreasure", "weight": 50}],
     "maxMonsters": 20,
     "npcs": [sign("Umschauen", SIGNS["lookingaround"], 11, 2, 32, 48)]
             + [dict(sign("Knochenhaufen", SIGNS["bones"], r, c, fr, fc), icon="bonepile.bgf") for r, c, fr, fc in
                [(3, 18, 0, 8), (5, 14, 32, 40), (19, 18, 0, 40), (22, 11, 24, 56), (23, 8, 40, 16), (25, 14, 0, 40)]]},
    {"id": 1017, "constant": "RID_RAZA_BAR", "kodClass": "Newb7", "name": "Raza Pub", "roo": "guest7.roo",
     "music": "login.mid", "baseLight": NICE, "outsideFactor": SOME, "teleport": {"row": 5, "col": 4},
     "noCombat": True, "respawnRoom": 1011, "improveCap": 25,
     "exits": exits([5, 2, BASE + 2, 18, 56, "NONE"]),
     "npcs": [npc("HazarBartender", "Eric d'Jorn", "tsinnk.bgf", "Wirt der Schenke (Essen, Kleidung)", 8, 7, angle=1536),
              sign("Leben, Mana und Ausdauer", SIGNS["health"], 4, 7)]},
    {"id": 1018, "constant": "RID_RAZA_MUSEUM", "kodClass": "Newb8", "name": "The Grand Museum of Raza",
     "roo": "guest8.roo", "music": "login.mid", "baseLight": NICE, "outsideFactor": SOME, "teleport": {"row": 9, "col": 11},
     "noCombat": True, "respawnRoom": 1011, "improveCap": 25,
     "exits": exits([13, 10, BASE + 2, 9, 54, "NONE"], [13, 11, BASE + 2, 9, 54, "NONE"], [13, 12, BASE + 2, 9, 54, "NONE"]),
     "portal": {"row": 2, "col": 11, "toRoom": 52,
                "warning": "Achtung: Wenn du dieses Portal betrittst, kannst du nicht nach Raza zurückkehren. Betritt es "
                           "nur, wenn du deinen Fähigkeiten vertraust. Mehr steht auf dem Schild rechts neben dem Portal.",
                "text": "Du betrittst das Portal und beginnst dein Abenteuer in Meridian 59!"},
     "npcs": [npc("NewbPortal", "Portal", "portal.bgf", "Portal aufs Festland", 2, 11, angle=1024),
              sign("Hinaus in die Welt", SIGNS["realm"], 2, 12, 16, 16)]},
]

for room in rooms:
    layout = root / "client/rooms" / (Path(room["roo"]).stem + ".json")
    if layout.exists():
        data = json.loads(layout.read_text())
        room["rows"], room["cols"] = data["rows"], data["cols"]

world = {"source": "kod/object/active/holder/room/guest*/newb* (Meridian 59), erzeugt mit tools/make_raza.py",
         "startRoom": 1011, "start": True, "rooms": rooms}
out = root / "server/data/world/raza.json"
out.write_text(json.dumps(world, ensure_ascii=False, indent=2) + "\n")
print(f"{out}: {len(rooms)} Räume, {sum(len(r.get('objects', [])) for r in rooms)} Zierobjekte")
