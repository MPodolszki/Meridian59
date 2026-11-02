#!/usr/bin/env python3
"""Überträgt die Einrichtung der Räume aus dem Original in die Gebietsdateien (server/data/world/*.json, Feld "objects").

Aufruf (im Ordner game, nach den make_*.py-Skripten, vor export_exits.py):  python3 tools/add_furniture.py

Zu jeder Raumklasse ("kodClass") wird die KOD-Datei unter kod/object/active/holder/room gesucht, samt Elternklassen
(Raza „Newb2 is Guest2“). Gelesen werden alle Platzierungen der Form
  Send(self,@NewHold,#what=Create(&Klasse,…),#new_row=…,#new_col=…[,#fine_row=…][,#fine_col=…][,#new_angle=…])
für Möbel, Lichter und Zierobjekte. Platzierungen mit berechneten Koordinaten (Schleifen, Konstanten) werden
übersprungen. Ergebnis je Objekt: {"type", "row", "col", "fineRow", "fineCol", "angle"[, "intensity"]};
OrnamentalObject liefert seinen OO_-Typ, Bäume "TREE", Sträucher "SHRUB".
"""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
kod_rooms = root.parent / "kod/object/active/holder/room"

ANGLE = {"ANGLE_EAST": 0, "ANGLE_SOUTH_EAST": 512, "ANGLE_SOUTH": 1024, "ANGLE_SOUTH_WEST": 1536,
         "ANGLE_WEST": 2048, "ANGLE_NORTH_WEST": 2560, "ANGLE_NORTH": 3072, "ANGLE_NORTH_EAST": 3584}
# Klassen, die als Einrichtung dargestellt werden (Kleinschreibung → Typname im Client)
FURNITURE = {
    "table": "Table", "barstool": "BarStool", "stool": "Stool", "brazier": "Brazier", "candelabra": "Candelabra",
    "candle": "Candle", "firepit": "Firepit", "chest": "Chest", "woodenbox": "Chest", "bookpedestal": "BookPedestal",
    "pillar": "Pillar", "lamp": "Lamp", "tablelamp": "Candle", "kocatanlamp": "Lamp", "throne": "Throne",
    "skull": "Skull", "tombstone": "Tombstone", "fountain": "Fountain", "dynamiclight": "Light",
    "tree": "TREE", "feytree": "TREE", "glowtree": "TREE", "shrub": "SHRUB", "tallbush": "SHRUB",
    "statue": "Statue", "hotplate": "Firepit", "quillpen": "Quill",
}
BASES = {"room", "monsterroom", "objectroom"}

classes = {}
for f in kod_rooms.rglob("*.kod"):
    text = f.read_text(encoding="latin-1")
    for m in re.finditer(r"^(\w+)\s+is\s+(\w+)", text, re.M):
        classes[m.group(1).lower()] = (m.group(2).lower(), f)


def placements(path):
    """Alle NewHold(Create(...)) mit festen Koordinaten in einer KOD-Datei."""
    text = re.sub(r"#\s+", "#", path.read_text(encoding="latin-1"))
    text = re.sub(r"%[^\n]*", "", text)   # Kommentare
    out = []
    for m in re.finditer(r"#what\s*=\s*Create\(\s*&(\w+)", text, re.I):
        cls = m.group(1).lower()
        if cls not in FURNITURE and cls != "ornamentalobject":
            continue
        # Argumente von Create(...) und danach die Parameter des NewHold bis zur schließenden Klammer
        depth, i = 1, text.index("(", m.start()) + 1
        while depth and i < len(text):
            depth += {"(": 1, ")": -1}.get(text[i], 0)
            i += 1
        create_args = text[m.end():i - 1]
        j = text.find(")", i)
        hold_args = dict(re.findall(r"#(\w+)\s*=\s*([\w-]+)", text[i:j]))
        try:
            row, col = int(hold_args["new_row"]), int(hold_args["new_col"])
        except (KeyError, ValueError):
            continue
        def num(key, default):
            v = hold_args.get(key)
            return int(v) if v is not None and v.lstrip("-").isdigit() else default
        kind = FURNITURE.get(cls)
        if cls == "ornamentalobject":
            t = re.search(r"#type\s*=\s*(\w+)", create_args)
            if not t:
                continue
            kind = t.group(1)
        obj = {"type": kind, "row": row, "col": col, "fineRow": num("fine_row", 32), "fineCol": num("fine_col", 32),
               "angle": ANGLE.get(hold_args.get("new_angle", ""), 0)}
        if cls == "dynamiclight":
            inten = re.search(r"#iIntensity\s*=\s*(\d+)", create_args)
            obj["intensity"] = int(inten.group(1)) if inten else 30
        out.append(obj)
    return out


def furniture(kod_class):
    """Einrichtung einer Raumklasse inklusive Elternklassen (ohne Duplikate)."""
    seen, result, cls = set(), [], (kod_class or "").lower()
    while cls in classes and cls not in BASES and cls not in seen:
        seen.add(cls)
        parent, path = classes[cls]
        result += placements(path)
        cls = parent
    unique = {(o["type"], o["row"], o["col"], o["fineRow"], o["fineCol"]): o for o in result}
    return list(unique.values())


for area in sorted((root / "server/data/world").glob("*.json")):
    data = json.loads(area.read_text())
    count = 0
    for room in data["rooms"]:
        objs = furniture(room.get("kodClass"))
        if objs:
            room["objects"] = objs
            count += len(objs)
    area.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    print(f"{area.name}: {count} Objekte")
