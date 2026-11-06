#!/usr/bin/env python3
"""Ergänzt server/data/items/items.json um Gegenstände ohne Spruch aus den Beutetabellen (Erz, Spinnenei, Schlüssel,
Reliquie des Qor, Neruditschwert, Ring der Lethargie, Jüngerroben, Sonderstäbe) und setzt für alle Gegenstände mit
kodClass den Spruchmodifikator (item.kod GetBaseSpellModifier: viSpell_modifier aus der Klasse oder einer Elternklasse;
Waffen nach Typ und Qualität wie weapon.kod). Schulabhängige Modifikatoren (Fackel, Orkschild, Efeukranz, wahre Laute)
stehen in SCHOOL_MODIFIERS, die Jüngerrobe rechnet der Server selbst (robedisc.kod).

Aufruf (im Ordner game): python3 tools/make_misc_items.py && python3 tools/make_treasure.py
"""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
kod = root.parent / "kod/object/item"

classes = {}
for f in kod.rglob("*.kod"):
    t = re.sub(r"%[^\n]*", "", f.read_text(encoding="latin-1"))
    m = re.search(r"^(\w+)\s+is\s+(\w+)", t, re.M)
    if m:
        classes[m.group(1).lower()] = (m.group(2).lower(), t)


def classvar(cls, name):
    """Wert aus classvars:/properties: der Klasse oder der nächsten Elternklasse."""
    while cls in classes:
        parent, t = classes[cls]
        m = re.search(rf"^\s*{name}\s*=\s*(-?\d+)", t, re.M | re.I)
        if m:
            return int(m.group(1))
        cls = parent
    return None


# weapon.kod: WEAPON_SPELL_HIGH/MID/LOW nach Typ, Qualität −/+5.
WEAPON_TYPE = {"bludgeon": 0, "thrust": -10, "slash": -15}
WEAPON_QUALITY = {"low": 5, "high": -5, "nerudite": 5, "normal": 0, "magic": 0}
# GetBaseSpellModifier(oSpell) mit Schulabhängigkeit: Schule → Wert (sonst 0).
SCHOOL_MODIFIERS = {
    "torch": {"4": 10},                 # torch.kod: Faren +10
    "orcshield": {"1": -15, "2": 15},   # orcshld.kod
    "ivycirclet": {"1": 15, "2": -15},  # ivycircl.kod
    "truelute": {"6": 10},              # truelute.kod: Jala +10
}

SCHOOLS = {1: ("shalille", "Shal'illes"), 2: ("qor", "Qors"), 3: ("kraanan", "Kraanans"), 4: ("faren", "Farens"),
           5: ("riija", "Riijas"), 6: ("jala", "Jalas")}

NEW = [
    {"key": "firesand", "kodClass": "FireSand", "name": "Feuersand", "icon": "firesand.bgf", "value": 30,
     "types": ["reagent"], "number": True, "plural": "Feuersand",
     "description": "Seltsam warm und von ungewöhnlicher Farbe – dieser Sand ist ein begehrtes Reagenz."},
    {"key": "longbow", "kodClass": "Longbow", "name": "Langbogen", "icon": "bow.bgf", "value": 2500,
     "types": ["weapon"], "article": "einen", "slot": "weapon", "weaponType": "ranged", "twoHanded": True,
     "range": 100, "hitBonus": 100, "proficiency": 456, "overlay": "bowbov.bgf", "windowOverlay": "povbow.bgf",
     "description": "Ein hoher Bogen aus elastischem Holz. Er trifft weit und genau."},
    {"key": "battlebow", "kodClass": "BattleBow", "name": "Kampfbogen", "icon": "bow.bgf", "value": 3000,
     "types": ["weapon"], "article": "einen", "slot": "weapon", "weaponType": "ranged", "twoHanded": True,
     "range": 17, "weaponDamage": 2, "proficiency": 456, "overlay": "bowbov.bgf", "windowOverlay": "povbow.bgf",
     "description": "Ein kurzer, schwerer Bogen für den Kampf auf nahe Entfernung. Seine Pfeile schlagen hart ein."},
    {"key": "arrow", "kodClass": "Arrow", "name": "Pfeil", "icon": "arrow.bgf", "value": 10, "types": ["ammo"],
     "number": True, "plural": "Pfeile", "slot": "quiver", "defaultNumber": 20, "damageMin": 1, "damageMax": 5,
     "attackTypes": ["pierce"],
     "description": "Schlank und gerade, mit Federn von Dschungelvögeln – gewöhnliche Munition für einen Bogen."},
    {"key": "neruditearrow", "kodClass": "NeruditeArrow", "name": "Neruditpfeil", "icon": "arrowner.bgf",
     "value": 20, "types": ["ammo"], "number": True, "plural": "Neruditpfeile", "slot": "quiver", "defaultNumber": 5,
     "damageMin": 2, "damageMax": 6, "attackTypes": ["pierce", "nerudite"],
     "description": "Die geschnitzten Spitzen richten etwas mehr Schaden an als gewöhnliche Pfeile – und zersplittert "
                    "der kleine grüne Stein beim Aufprall, kann das tödlich sein."},
    {"key": "silverarrow", "kodClass": "SilverArrow", "name": "Silberpfeil", "icon": "arrowsil.bgf", "value": 30,
     "types": ["ammo"], "number": True, "plural": "Silberpfeile", "slot": "quiver", "defaultNumber": 10,
     "damageMin": 2, "damageMax": 6, "attackTypes": ["pierce", "silver"],
     "description": "Die scharfe, saubere Spitze reißt böse Wunden. Manche Bogenschützen schwören auf sie gegen Untote."},
    {"key": "nodeburstpotion", "kodClass": "NodeBurstPotion", "name": "beißendes Elixier", "icon": "potion01.bgf",
     "value": 120, "types": ["potion"], "article": "ein", "special": "dud",
     "description": "Ein Glasfläschchen mit roter Flüssigkeit und beißendem Geruch. Laut dem großen Warnschild öffnet es "
                    "alle Manaknoten-Verbindungen bis zum Anschlag – und trennt dabei eine für immer."},
    {"key": "illusionaryformpotion", "kodClass": "IllusionaryFormPotion", "name": "brauner Trank", "icon": "flask.bgf",
     "value": 60, "types": ["potion"], "article": "einen", "spell": "illusionaryform", "powerMin": 1, "powerMax": 80,
     "description": "Ein tiefer, dunkler, trüber, stückiger Trank."},
    {"key": "orechunk", "kodClass": "OreChunk", "name": "Erzbrocken", "icon": "ore.bgf", "value": 1000, "types": [],
     "article": "einen", "description": "Ein riesiger, unhandlicher Felsbrocken. Kräftige silberne Adern lassen einen "
                                        "großen verborgenen Wert vermuten."},
    {"key": "neruditeorechunk", "kodClass": "NeruditeOreChunk", "name": "Neruditbrocken", "icon": "neruore.bgf",
     "value": 1500, "types": [], "article": "einen",
     "description": "Ein riesiger, hässlicher grüner Felsbrocken mit leuchtenden Adern. Er muss ein Vermögen wert sein."},
    {"key": "spideregg", "kodClass": "SpiderEgg", "name": "Spinnenei", "icon": "spdregg.bgf", "value": 250,
     "types": [], "article": "ein",
     "description": "Ein Ei, groß wie eine Melone, mit zäher, lederiger Schale. Innen schwappt eine Flüssigkeit."},
    {"key": "key", "kodClass": "Key", "name": "Schlüssel", "icon": "fekey.bgf", "value": 80, "types": [],
     "article": "einen", "description": "Ein kunstvoll geformter Schlüssel mit der Inschrift eines Zauberers."},
    {"key": "scepter", "kodClass": "Scepter", "name": "Reliquie des Qor", "icon": "scepter.bgf", "value": 70,
     "types": [], "article": "eine", "description": "Der Gegenstand trägt die Zeichen Qors. Eine Inschrift lautet: "
                                                   "„Der wahre Diener bringe dies an den öden Ort und spreche meinen Namen.“"},
    {"key": "neruditesword", "kodClass": "NeruditeSword", "name": "Neruditschwert", "icon": "nerswd.bgf",
     "value": 1000, "types": ["weapon"], "article": "ein",
     "description": "Schnell und stark – die Neruditwaffe liegt gut in der Hand.", "slot": "weapon",
     "weaponType": "thrust", "weaponQuality": "nerudite", "windowOverlay": "povnersw.bgf", "overlay": "nerswdov.bgf",
     "proficiency": 451},
    {"key": "ringoflethargy", "kodClass": "RingofLethargy", "name": "Ring der Lethargie", "icon": "ring3.bgf",
     "value": 70, "types": ["ring"], "article": "einen", "slot": "ring", "cursed": True, "vigorRestDrop": 20,
     "tortureExertion": 400000,
     "description": "Der Ring sieht schon von hier gefährlich aus. Vielleicht solltest du ihn nicht tragen.",
     "useText": "Kaum steckt der Ring am Finger, fühlst du dich ausgelaugt und außer Atem.",
     "tortureText": "Als du den Ring abziehen willst, fließt die Kraft aus deinem Körper! Unwillkürlich steckst du ihn "
                    "wieder an – du bist an ihn gebunden und brauchst besondere Hilfe, um ihn loszuwerden.",
     "uncurseText": "Der Ring springt von selbst von deinem Finger. Endlich kannst du wieder frei atmen!"},
    {"key": "disciplerobe", "kodClass": "DiscipleRobe", "name": "Jüngerrobe", "icon": "robe.bgf", "value": 2000,
     "types": ["armor"], "article": "eine", "slot": "armor", "defenseBonus": 20,
     "variants": [f"disciplerobe{k}" for k, _ in SCHOOLS.values()],
     "description": "Schwer und warm. Jüngerroben zeugen von glühender (manche sagen: ungesunder) Hingabe an eine Schule."},
    {"key": "firewand", "kodClass": "FireWand", "name": "Feuerstab", "icon": "wand2.bgf", "value": 150,
     "types": ["wand"], "article": "einen", "special": "fire", "chargesMin": 4, "chargesMax": 12, "damageMin": 4,
     "damageMax": 10, "description": "Ein rußgeschwärzter Stab, dessen Spitze glimmt."},
    {"key": "healwand", "kodClass": "HealWand", "name": "Heilstab", "icon": "healwand.bgf", "value": 200,
     "types": ["wand"], "article": "einen", "special": "heal", "chargesMin": 5, "chargesMax": 5, "damageMin": 2,
     "damageMax": 10, "description": "Ein glatter, heller Stab, der sanft pulsiert."},
    {"key": "vampirewand", "kodClass": "VampireWand", "name": "Vampirstab", "icon": "wand3.bgf", "value": 200,
     "types": ["wand"], "article": "einen", "special": "vampire", "chargesMin": 10, "chargesMax": 18, "damageMin": 2,
     "damageMax": 6, "description": "Ein knochenbleicher Stab, an dem dunkle Adern entlanglaufen."},
    {"key": "staffofjolting", "kodClass": "StaffOfJolting", "name": "Stab der Erschütterung", "icon": "joltstaf.bgf",
     "value": 200, "types": ["wand"], "article": "einen", "special": "jolt", "chargesMin": 25, "chargesMax": 35,
     "description": "Ein schwerer Stab, der in der Hand leise brummt."},
]
for school, (key, gen) in SCHOOLS.items():
    NEW.append({"key": f"disciplerobe{key}", "kodClass": "DiscipleRobe", "name": "Jüngerrobe", "icon": "robe.bgf",
                "value": 2000, "types": ["armor"], "article": "eine", "slot": "armor", "defenseBonus": 20,
                "discipleSchool": school,
                "description": f"Schwer und warm. Die Robe trägt das Zeichen {gen} und stärkt Sprüche dieser Schule."})

path = root / "server/data/items/items.json"
data = json.loads(path.read_text())
have = {i["key"]: i for i in data["items"]}
for entry in NEW:
    if entry["key"] in have:
        have[entry["key"]].update(entry)
    else:
        data["items"].append(entry)
        have[entry["key"]] = entry

changed = []
for item in data["items"]:
    cls = (item.get("kodClass") or "").lower()
    if not cls:
        continue
    if item.get("weaponType") and item["weaponType"] != "ranged":
        mod = WEAPON_TYPE.get(item["weaponType"], 0) + WEAPON_QUALITY.get(item.get("weaponQuality", "normal"), 0)
    else:
        mod = classvar(cls, "viSpell_modifier") or 0
    if item.get("discipleSchool") or item.get("variants"):
        mod = 0   # robedisc.kod: eigene Schule 4 × höchste Stufe (0..20), sonst 5 – rechnet der Server
    item.pop("spellModifier", None)
    item.pop("spellModifierSchools", None)
    if mod:
        item["spellModifier"] = mod
        changed.append(f"{item['key']} {mod:+d}")
    if item["key"] in SCHOOL_MODIFIERS:
        item["spellModifierSchools"] = SCHOOL_MODIFIERS[item["key"]]
        changed.append(f"{item['key']} {SCHOOL_MODIFIERS[item['key']]}")

path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
print("Spruchmodifikatoren:", ", ".join(changed))
