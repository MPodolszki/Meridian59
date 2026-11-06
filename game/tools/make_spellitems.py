#!/usr/bin/env python3
"""Ergänzt server/data/items/items.json um Tränke, Schriftrollen und Zauberstäbe aus dem Original
(kod/object/item/passitem/spelitem/**: Potion, Scroll, Wand mit viSpellEffect) – nur solche, deren Spruch es im
Spiel schon gibt (spells.json). Schlüssel = Klassenname klein (wie in den Beutetabellen, tools/make_treasure.py).
Spruchstärke wie spelitem.kod (Tränke 20–60, sonst 30–60), Ladungen der Stäbe viHits_init_min/max (Standard 4–9).

Aufruf (im Ordner game): python3 tools/make_spellitems.py && python3 tools/make_treasure.py
"""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
kod = root.parent / "kod/object/item/passitem/spelitem"
khd = (root.parent / "kod/include/blakston.khd").read_text(encoding="latin-1")
SID = {m.group(1): int(m.group(2)) for m in re.finditer(r"^\s*(SID_\w+)\s*=\s*(\d+)", khd, re.M)}
spells = {s["sid"]: s for s in json.loads((root / "server/data/spells/spells.json").read_text())["spells"]}

# Deutsche Namen (Einzahl, Mehrzahl) und Beschreibung; sinngemäß nach dem Original.
GERMAN = {
    "hastepotion": ("Trank der Eile", "Tränke der Eile", "Ein Glasfläschchen mit sandfarbener Flüssigkeit. Es riecht frisch und belebend."),
    "strengthpotion": ("Trank der Stärke", "Tränke der Stärke", "Eine dicke, rötliche Flüssigkeit, die nach Eisen riecht."),
    "nightvisionpotion": ("Katzenaugensaft", "Fläschchen Katzenaugensaft", "Eine schimmernde, grünliche Flüssigkeit. Wer sie trinkt, sieht im Dunkeln wie eine Katze."),
    "karaholscursepotion": ("Kara'hols verfluchtes Elixier", "Kara'hols verfluchte Elixiere", "Ein brodelndes, schwarzrotes Elixier. Schon der Geruch lässt das Blut kochen."),
    "purifypotion": ("Trank der Läuterung", "Tränke der Läuterung", "Eine klare, beinahe leuchtende Flüssigkeit."),
    "resistmagicpotion": ("Elixier der Magieabwehr", "Elixiere der Magieabwehr", "Ein blau schimmerndes Elixier, das leicht nach Gewitter riecht."),
    "resistfirepotion": ("saftige Salbe", "Tiegel saftiger Salbe", "Eine kühle, fettige Salbe gegen Feuer."),
    "resistcoldpotion": ("Trank gegen Kälte", "Tränke gegen Kälte", "Eine wärmende, würzige Flüssigkeit."),
    "resistshockpotion": ("Trank der Nichtleitung", "Tränke der Nichtleitung", "Eine zähe, gummiartige Flüssigkeit, die vor Blitzen schützt."),
    "resistacidpotion": ("Trank gegen Säure", "Tränke gegen Säure", "Eine kalkige, milchige Flüssigkeit."),
    "curepoisonpotion": ("Trank gegen Gift", "Tränke gegen Gift", "Ein bitterer Kräutersud, der Gift aus dem Körper treibt."),
    "rescuepotion": ("Trank der Errettung", "Tränke der Errettung", "Ein goldener Trank. Wer ihn trinkt, wird von Shal'ille in Sicherheit gebracht."),
    "darknessscroll": ("Schriftrolle der Dunkelheit", "Schriftrollen der Dunkelheit", "Eine Schriftrolle mit Qors finsteren Zeichen."),
    "discordscroll": ("Schriftrolle der Zwietracht", "Schriftrollen der Zwietracht", "Eine Schriftrolle, deren Zeichen zu flackern scheinen."),
    "earthquakescroll": ("Schriftrolle des Erdbebens", "Schriftrollen des Erdbebens", "Eine schwere Schriftrolle mit Farens Zeichen der Erde."),
    "finalritesscroll": ("Schriftrolle der Letzten Riten", "Schriftrollen der Letzten Riten", "Eine Schriftrolle mit Shal'illes Gebet für die Toten."),
    "flashscroll": ("Schriftrolle des Blitzlichts", "Schriftrollen des Blitzlichts", "Eine Schriftrolle, die im Licht glitzert."),
    "forcesoflightscroll": ("Schriftrolle der Kräfte des Lichts", "Schriftrollen der Kräfte des Lichts", "Eine Schriftrolle mit goldenen Zeichen Shal'illes."),
    "killingfieldscroll": ("Schriftrolle des Schlachtfelds", "Schriftrollen des Schlachtfelds", "Eine Schriftrolle mit blutroten Zeichen Kraanans."),
    "lightscroll": ("Schriftrolle des Lichts", "Schriftrollen des Lichts", "Eine Schriftrolle, deren Zeichen sanft leuchten."),
    "windscroll": ("Schriftrolle der Winde", "Schriftrollen der Winde", "Eine Schriftrolle, die im Wind flattert, auch wenn keiner weht."),
    "blindwand": ("Zauberstab der Blendung", "Zauberstäbe der Blendung", "Ein schlanker Stab aus dunklem Holz."),
    "enfeeblewand": ("Zauberstab der Schwäche", "Zauberstäbe der Schwäche", "Ein krummer, knorriger Stab."),
    "holdwand": ("Zauberstab der Erstarrung", "Zauberstäbe der Erstarrung", "Ein kalter, steifer Stab."),
    "hospicewand": ("Zauberstab der Erquickung", "Zauberstäbe der Erquickung", "Ein heller Stab, der sich warm anfühlt."),
    "lightningwand": ("Blitzstab", "Blitzstäbe", "Ein Stab, an dessen Spitze kleine Funken knistern."),
    "purifywand": ("Zauberstab der Reinigung", "Zauberstäbe der Reinigung", "Ein weißer Stab, glatt wie Elfenbein."),
    "removecursepotion": ("Trank des Fluchlösens", "Tränke des Fluchlösens", "Ein smaragdgrüner Trank, der nach frischem Regen riecht."),
    "curediseasepotion": ("Trank gegen Krankheit", "Tränke gegen Krankheit", "Ein heller Kräutertrank, der nach Holunder duftet."),
    "resistpoisonpotion": ("Trank der Giftfestigkeit", "Tränke der Giftfestigkeit", "Eine trübe, bittere Flüssigkeit."),
    "freeactionpotion": ("Trank der freien Bewegung", "Tränke der freien Bewegung", "Eine dünne, quirlige Flüssigkeit, die im Fläschchen nie zur Ruhe kommt."),
    "magicshieldpotion": ("Trank des magischen Schilds", "Tränke des magischen Schilds", "Ein silbrig schimmernder Trank."),
    "deflectpotion": ("Trank der Ablenkung", "Tränke der Ablenkung", "Eine spiegelnde Flüssigkeit, in der man sich selbst sieht."),
    "detectinvisibilitypotion": ("Trank des wahren Blicks", "Tränke des wahren Blicks", "Eine glasklare Flüssigkeit, die man kaum sieht."),
    "detectevilpotion": ("Trank der Bosheitserkennung", "Tränke der Bosheitserkennung", "Ein weißer, milchiger Trank."),
    "detectgoodpotion": ("Trank der Güteerkennung", "Tränke der Güteerkennung", "Ein schwarzer, öliger Trank."),
    "eagleeyespotion": ("Trank der Adleraugen", "Tränke der Adleraugen", "Ein goldgelber Trank, der in den Augen brennt."),
    "shadowformpotion": ("Schattentrank", "Schattentränke", "Eine Flüssigkeit, so dunkel, dass sie das Licht schluckt."),
    "cloakpotion": ("Trank der Verhüllung", "Tränke der Verhüllung", "Ein rauchgrauer Trank."),
    "denialpotion": ("Trank der Verleugnung", "Tränke der Verleugnung", "Ein rosaroter Trank, süß wie eine Lüge."),
    "anonymitypotion": ("Trank der Namenlosigkeit", "Tränke der Namenlosigkeit", "Ein farbloser Trank ohne jeden Geruch."),
    "baitpotion": ("Ködertrank", "Ködertränke", "Ein Trank, der verführerisch nach Braten riecht."),
    "trucescroll": ("Schriftrolle der Waffenruhe", "Schriftrollen der Waffenruhe", "Eine Schriftrolle mit Shal'illes Zeichen des Friedens."),
    "heatscroll": ("Schriftrolle der Hitze", "Schriftrollen der Hitze", "Eine Schriftrolle, die sich warm anfühlt."),
    "dispellillusionscroll": ("Schriftrolle der Entzauberung", "Schriftrollen der Entzauberung", "Eine Schriftrolle mit klaren, scharfen Zeichen."),
    "slitherwand": ("Schlitterstab", "Schlitterstäbe", "Ein gewundener Stab, der sich wie eine Schlange anfühlt."),
    "dementwand": ("Zauberstab der Umnachtung", "Zauberstäbe der Umnachtung", "Ein verdrehter, dunkler Stab."),
    "markofdishonorwand": ("Zauberstab der Schande", "Zauberstäbe der Schande", "Ein Stab mit Shal'illes Zeichen, verkehrt herum eingeschnitzt."),
    "identifywand": ("Zauberstab des Erkennens", "Zauberstäbe des Erkennens", "Ein schlichter Stab mit einem eingelassenen Auge."),
    "forgetwand": ("Zauberstab des Vergessens", "Zauberstäbe des Vergessens", "Ein blasser Stab, an den man sich kaum erinnert."),
    "brittlewand": ("Zauberstab der Sprödigkeit", "Zauberstäbe der Sprödigkeit", "Ein rissiger, alter Stab."),
    "shatterwand": ("Zauberstab des Zerschmetterns", "Zauberstäbe des Zerschmetterns", "Ein eiskalter Stab mit Reif an der Spitze."),
    "feigndeathpotion": ("Trank des Scheintods", "Tränke des Scheintods", "Ein grauer, kalter Trank, der nach Grabeserde riecht."),
    "swapwand": ("Zauberstab des Tauschs", "Zauberstäbe des Tauschs", "Ein Stab, der nie ganz dort liegt, wo man ihn hingelegt hat."),
}


def text(t, key):
    m = re.search(rf"^\s*{key}\s*=\s*([^\n%]+)", t, re.M | re.I)
    return m.group(1).strip() if m else None


items_path = root / "server/data/items/items.json"
data = json.loads(items_path.read_text())
have = {i["key"] for i in data["items"]}
added, skipped = [], []
for f in sorted(kod.rglob("*.kod")):
    t = f.read_text(encoding="latin-1")
    m = re.search(r"^(\w+)\s+is\s+(Potion|Scroll|Wand)\b", t, re.M)
    sid = text(t, "viSpellEffect")
    if not m or not sid:
        continue
    cls, kind = m.group(1), m.group(2).lower()
    key = cls.lower()
    spell = spells.get(SID.get(sid))
    if spell is None or key not in GERMAN:
        skipped.append(f"{key} ({sid})")
        continue
    if key in have:
        continue
    name, plural, desc = GERMAN[key]
    icon_sym = text(t, "vrIcon")
    icon = re.search(rf'^\s*{re.escape(icon_sym)}\s*=\s*(\S+)', t, re.M | re.I).group(1) if icon_sym else \
        {"potion": "potion01.bgf", "scroll": "scr07.bgf", "wand": "wand6.bgf"}[kind]
    value = int(text(t, "viValue_average") or {"potion": 60, "scroll": 120, "wand": 120}[kind])
    entry = {"key": key, "kodClass": cls, "name": name, "icon": icon.lower(), "value": value, "types": [kind],
             "plural": plural, "description": desc, "spell": spell["key"],
             "powerMin": 20 if kind == "potion" else 30, "powerMax": 60}
    if kind == "wand":
        entry["chargesMin"] = int(text(t, "viHits_init_min") or 4)
        entry["chargesMax"] = int(text(t, "viHits_init_max") or 9)
    data["items"].append(entry)
    added.append(key)
items_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
print(f"neu: {', '.join(added) or '–'}")
print(f"übersprungen (Spruch fehlt noch): {', '.join(skipped)}")
