#!/usr/bin/env python3
"""Überträgt die Beutetabellen des Originals (kod/object/passive/trestype/*.kod, plTreasure) nach
server/data/items/items.json ("treasure"). Aufruf (im Ordner game):  python3 tools/make_treasure.py

Tabellenname = TID-Konstante ohne „TID_“ in Kleinbuchstaben (TID_MEDIUM_TOUGH → medium_tough, TID_NEWBIE_AREA →
newbie). Gegenstände = KOD-Klasse in Kleinbuchstaben; Gegenstände, die es in items.json noch nicht gibt, werden
weggelassen und am Ende aufgelistet (ihr Gewicht verfällt, wie eine Niete).
"""
import collections
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
path = root / "server/data/items/items.json"
data = json.loads(path.read_text())
known = {i["key"] for i in data["items"]}
missing = collections.Counter()
for f in sorted((root.parent / "kod/object/passive/trestype").glob("*.kod")):
    text = re.sub(r"%[^\n]*", "", f.read_text(encoding="latin-1"))
    num = re.search(r"piTreasure_num\s*=\s*TID_(\w+)", text)
    lst = re.search(r"plTreasure\s*=\s*\[(.*?)\]\s*;", text, re.S)
    if not num or not lst:
        continue
    name = {"newbie_area": "newbie"}.get(num.group(1).lower(), num.group(1).lower())
    entries = []
    for cls, w in re.findall(r"\[\s*&(\w+)\s*,\s*(\d+)\s*\]", lst.group(1)):
        key = cls.lower()
        if key in known:
            entries.append({"item": key, "weight": int(w)})
        else:
            missing[key] += 1
    data["treasure"][name] = entries
    print(f"{name}: {len(entries)} Einträge")
path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
print("Fehlende Gegenstände:", ", ".join(f"{k}({n})" for k, n in missing.most_common()))
