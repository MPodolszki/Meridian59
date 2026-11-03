#!/usr/bin/env python3
"""Erzeugt die Tempel des Festlands: Shal'ille (temple.kod, bei Marion über C4), Kraanan (tempkra.kod, Westrand von
G9) und Qor (tempqor.kod, Zugang von I8/H9) mit ihren Priesterinnen; dazu Läden (Sprüche lehren) und Dialoge.
Die Faren-Priesterin steht in den Badlands (badland1, tools/make_wilderness.py), ihr Laden und Dialog stehen hier.
Der Riija-Tempel liegt auf Ko'catan und kommt mit Ko'catan. Werte und Abweichungen: docs/temples.md.

Aufruf (im Ordner game): python3 tools/make_temples.py   (vorher make_wilderness.py für die Layouts)
"""
import json
import re
from pathlib import Path

from kodarea import dumps, npc, write_area
from kodroom import CONST, room_of, roomdir

root = Path(__file__).resolve().parent.parent
monster = root.parent / "kod/object/active/holder/nomoveon/battler/monster/temples"
spell_keys = {s["sid"]: s["key"] for s in json.loads((root / "server/data/spells/spells.json").read_text())["spells"]}

PRIESTS = {
    # Klasse: (Datei, Name, Raumklasse oder None, Zeile, Spalte, Feinzeile, Feinspalte, Winkel)
    "ShalillePriestess": ("shalprst.kod", "Priesterin Xiana", "Temple", 17, 2, 24, 24, 0),
    "KraananPriestess": ("kranprst.kod", "Priesterin Qerti'nya", "TempleKraanan", 13, 20, 24, 32, 1024),
    "QorPriestess": ("qorprst.kod", "Priesterin Zuxana", "TempleQor", 16, 15, 32, 0, 1024),
    "FarenPriestess": ("farnprst.kod", "Priesterin Tenuv'vyal", None, 44, 59, 32, 32, 2048),
}

DIALOG = {
    "ShalillePriestess": {
        "title": "Priesterin der Shal'ille",
        "description": "Sauber und ordentlich, diese Priesterin des friedvollen Gleichmuts spricht nur freundliche Worte. "
                       "Sie wünscht sogar denen von Qor Frieden und betet für ihre Erlösung.",
        "greeting": "Friede sei mit dir. Shal'ille heilt, die sie darum bitten.",
        "topics": [{"keywords": ["lernen", "zauber", "spruch", "heilen", "learn"],
                    "reply": "Ich lehre die Gaben Shal'illes: Heilung, Schutz und das Licht gegen die Finsternis. Sieh, was ich dir beibringen kann (B)."},
                   {"keywords": ["qor"], "reply": "Auch die Diener Qors können Erlösung finden. Ich bete für sie."}],
    },
    "KraananPriestess": {
        "title": "Priesterin des Kraanan",
        "description": "Eine kräftige Frau in schlichter Rüstung. Die Priesterin des Kriegsgottes mustert jeden Besucher, "
                       "als schätze sie ab, wie lange er sich im Kampf halten würde.",
        "greeting": "Kraanan ehrt die Tapferen. Was führt dich in sein Haus?",
        "topics": [{"keywords": ["lernen", "zauber", "spruch", "kampf", "learn"],
                    "reply": "Stärke, Segen für die Waffe, Schutz vor Magie – die Gaben des Kriegsgottes. Wähle (B)."},
                   {"keywords": ["waffen", "rüstung"], "reply": "Die Waffen an den Wänden sind Opfergaben der Tapferen. Rühr sie nicht an."}],
    },
    "QorPriestess": {
        "title": "Priesterin der Qor",
        "description": "Die Priesterin der Finsternis lächelt kalt. In ihren Augen spiegelt sich das Glimmen der Kohlebecken.",
        "greeting": "Du hast also den Weg in Qors Haus gefunden. Was suchst du in der Dunkelheit?",
        "topics": [{"keywords": ["lernen", "zauber", "spruch", "dunkel", "learn"],
                    "reply": "Flüche, Gift, Schwäche und Dunkelheit – Qor lehrt, wie man Feinde zerbricht. Wähle (B)."},
                   {"keywords": ["shal'ille", "shalille"], "reply": "Shal'ille? Ihre Schwäche nennt sie Gnade."}],
    },
    "FarenPriestess": {
        "title": "Priesterin des Faren",
        "description": "Wind zerrt an der Robe der Priesterin, die hier draußen in den Badlands den Elementen dient. "
                       "Um sie herum scheint die Luft zu knistern.",
        "greeting": "Die Elemente grüßen dich, Wanderer.",
        "topics": [{"keywords": ["lernen", "zauber", "spruch", "feuer", "learn"],
                    "reply": "Feuer, Eis, Blitz und Erde – die Kräfte Farens. Wähle, was du lernen willst (B)."},
                   {"keywords": ["badlands", "tempel"], "reply": "Farens Tempel ist der offene Himmel. Hier draußen hört er mich am besten."}],
    },
}


def priest_spells(text):
    """plFor_sale = [$,$,[SID_…],$] → Sprüche, die es im Spiel schon gibt (Schlüssel aus spells.json), und die übrigen."""
    m = re.search(r"plFor_sale\s*=\s*\[\s*\$\s*,\s*\$\s*,\s*\[(.*?)\]", text, re.S)
    sids = re.findall(r"SID_\w+", m.group(1))
    known = [spell_keys[CONST[s]] for s in sids if CONST.get(s) in spell_keys]
    missing = [s for s in sids if CONST.get(s) not in spell_keys]
    return known, missing


def kod_npc(cls):
    file, name, room_class, row, col, frow, fcol, angle = PRIESTS[cls]
    text = (monster / file).read_text(encoding="latin-1")
    icon = re.search(r"_icon_rsc\s*=\s*(\w+\.bgf)", text).group(1)
    return text, npc(cls, name, icon, DIALOG[cls]["title"], row, col, angle=angle, fine_row=frow, fine_col=fcol)


rooms = [room_of(roomdir / f) for f in ("temple.kod", "tempkra.kod", "tempqor.kod")]
by_class = {r["kodClass"]: r for r in rooms}
# Qor: der Ausgang im Süden führt im Original abwechselnd nach I8 oder H9 (ExitsTimer); hier immer nach I8.
by_class["TempleQor"]["edgeExits"] = [{"edge": "SOUTH", "to": CONST["RID_I8"], "toConstant": "RID_I8", "toRow": 32,
                                       "toCol": 25, "rotate": "NORTH_WEST"}]
shops, dialogs, report = {}, {}, {}
for cls, (file, name, room_class, row, col, *_rest) in PRIESTS.items():
    text, entry = kod_npc(cls)
    if room_class:
        room = by_class[room_class]
        room.setdefault("npcs", []).append(entry)
        room["noCombat"] = True
    known, missing = priest_spells(text)
    shops[cls] = {"markup": 3, "spells": known}
    dialogs[cls] = DIALOG[cls]
    report[cls] = missing
write_area("temples", rooms, source="kod/object/active/holder/room/temple.kod, tempkra.kod, tempqor.kod")
(root / "server/data/shops/temples.json").write_text(dumps({
    "_hinweis": "Priesterinnen der Tempel (kod/.../monster/temples): plFor_sale Liste 3 = lehrbare Sprüche; nur Sprüche, "
                "die es im Spiel schon gibt (spells.json). Preis wie spell.kod GetValue.",
    "shops": shops}) + "\n")
(root / "server/data/dialog/temples.json").write_text(dumps({
    "_hinweis": "Priesterinnen der Tempel, Texte sinngemäß nach dem Original.", "npcs": dialogs}) + "\n")
for cls, missing in report.items():
    print(f"{cls}: noch nicht im Spiel: {', '.join(missing)}")
