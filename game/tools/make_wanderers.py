#!/usr/bin/env python3
"""Erzeugt die wandernden NPCs (kod/.../monster/towns/wanderer/*.kod, Wanderer: alle 30–90 min zu einem anderen Ziel
aus plDestinations, nicht solange Spieler im Raum sind): server/data/world/wanderers.json (nur "wanderers", keine
Räume) mit Läden (lehrbare Sprüche/Fertigkeiten, die es schon gibt) und Dialogen daneben. Werte: docs/wanderers.md.

Aufruf (im Ordner game): python3 tools/make_wanderers.py
"""
import json
import re
from pathlib import Path

from kodarea import dumps
from kodroom import CONST, resource, symbol

root = Path(__file__).resolve().parent.parent
kod = root.parent / "kod/object/active/holder/nomoveon/battler/monster/towns/wanderer"
spell_keys = {s["sid"]: s["key"] for s in json.loads((root / "server/data/spells/spells.json").read_text())["spells"]}
skill_keys = {s["skid"]: s["key"] for s in json.loads((root / "server/data/skills/skills.json").read_text())["skills"]}

DIALOG = {
    "DarkWizard": {
        "title": "Hexenmeister",
        "description": "Du spürst die Gegenwart eines mächtigen Hexenmeisters. Aufdringliche Magie lastet schwer in der "
                       "Luft um diesen wandernden Zauberer.",
        "greeting": "Du suchst Macht? Dann bist du bei mir richtig – wenn du sie zu tragen vermagst.",
        "topics": [{"keywords": ["lernen", "zauber", "spruch", "learn"],
                    "reply": "Feuer, Frost, Säure und der Durst nach fremdem Leben – ich lehre, was die Tempel verschweigen (B)."}],
    },
    "Heretic": {
        "title": "Ketzerin",
        "description": "Wegen ihrer gotteslästerlichen Ansichten aus der Gemeinschaft der Gläubigen verstoßen, zieht diese "
                       "Frau allein durch die Wildnis.",
        "greeting": "Die Götter? Ach, die Götter. Was willst du?",
        "topics": [],
    },
    "HunterGhost": {
        "title": "Geist eines Druiden",
        "description": "Dieser druidische Magier wirkt so ruhig und friedlich, dass man sich fragt, warum sein geisterhafter "
                       "Schatten noch auf dieser Ebene wandelt.",
        "greeting": "Friede, Wanderer. Die Jagd ist noch nicht zu Ende.",
        "topics": [],
    },
    "Izzio": {
        "title": "Kräutersammler",
        "description": "Izzio trägt seinen riesigen Sack und seinen prallen Bauch mit erstaunlicher Leichtigkeit. Ein seliges "
                       "Lächeln zeugt von seiner schlichten Freude an der Natur.",
        "greeting": "Ah, was für ein herrlicher Tag, um durch die Wälder zu streifen!",
        "topics": [],
    },
    "JealousGeneral": {
        "title": "General",
        "description": "Erst einfacher Soldat an vorderster Front, später ein weiser General: Jonas D'Accor hat jedes Übel "
                       "bekämpft, das die Städte von Meridian bedrängt hat.",
        "greeting": "Steh gerade, wenn du mit mir sprichst!",
        "topics": [{"keywords": ["lernen", "zauber", "blocken", "learn"],
                    "reply": "Schnelligkeit und ein guter Schild retten mehr Leben als jedes Gebet. Ich zeige es dir (B)."}],
    },
    "Minstrel": {
        "title": "Spielmann",
        "description": "Parrin ist im ganzen Land bekannt, doch selten im Guten. So begabt er mit der Leier ist, so "
                       "boshaft sind seine Verse.",
        "greeting": "Ein Lied gefällig? Nein? Dann eben ein Spottvers über dich!",
        "topics": [],
    },
}


def destinations(text):
    m = re.search(r"plDestinations\s*=\s*\[(.*?)\]\s*;", text, re.S)
    out = []
    for d in re.findall(r"\[([^\[\]]+)\]", m.group(1)):
        parts = [p.strip() for p in d.split(",")]
        rid = CONST.get(parts[0].upper())
        if rid is None:
            continue
        out.append([rid] + [int(p) for p in parts[1:]])
    return out


wanderers, shops, dialogs = [], {}, {}
for f in sorted(kod.glob("*.kod")):
    text = f.read_text(encoding="latin-1")
    cls = re.search(r"^(\w+)\s+is\s+Wanderer", text, re.M)
    if not cls:
        continue
    cls = cls.group(1)
    name = resource(text, symbol(text, "vrName"))
    icon = resource(text, symbol(text, "vrIcon"))
    wanderers.append({"kodClass": cls, "name": name, "icon": icon.lower(), "destinations": destinations(text)})
    sale = re.search(r"plFor_Sale\s*=\s*\[\s*\$\s*,\s*(\$|\[[^\]]*\])\s*,\s*\[([^\]]*)\]", text, re.S | re.I)
    if sale:
        spells = [spell_keys[CONST[s]] for s in re.findall(r"SID_\w+", sale.group(2)) if CONST.get(s) in spell_keys]
        skills = [skill_keys[CONST[s]] for s in re.findall(r"SKID_\w+", sale.group(1)) if CONST.get(s) in skill_keys]
        if spells or skills:
            shops[cls] = {"markup": 3, **({"spells": spells} if spells else {}), **({"skills": skills} if skills else {})}
    dialogs[cls] = DIALOG[cls]

out = root / "server/data/world/wanderers.json"
out.write_text(dumps({"source": "kod/object/active/holder/nomoveon/battler/monster/towns/wanderer", "rooms": [],
                      "wanderers": wanderers}) + "\n")
(root / "server/data/shops/wanderers.json").write_text(dumps({
    "_hinweis": "Wandernde NPCs (towns/wanderer): plFor_sale Liste 2/3 = lehrbare Fertigkeiten/Sprüche, nur vorhandene.",
    "shops": shops}) + "\n")
(root / "server/data/dialog/wanderers.json").write_text(dumps({
    "_hinweis": "Wandernde NPCs, Texte sinngemäß nach dem Original.", "npcs": dialogs}) + "\n")
for w in wanderers:
    print(w["kodClass"], w["name"], len(w["destinations"]), "Ziele", shops.get(w["kodClass"], {}))
