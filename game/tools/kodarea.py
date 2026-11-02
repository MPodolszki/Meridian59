"""Gemeinsame Bausteine für die Gebietsdateien (tools/make_raza.py, make_marion.py …): Räume, Ausgänge, Türen,
NPCs, Schilder und Zierobjekte im Format von server/data/world/*.json, Werte aus dem KOD-Original."""
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



def write_area(name, rooms, start=False, source="", respawn_room=None, start_room=None):
    """Größen aus client/rooms/*.json ergänzen und server/data/world/<name>.json schreiben."""
    for room in rooms:
        layout = root / "client/rooms" / (Path(room["roo"]).stem.lower() + ".json")
        if layout.exists():
            data = json.loads(layout.read_text())
            room["rows"], room["cols"] = data["rows"], data["cols"]
    world = {"source": source, "startRoom": start_room if start_room is not None else rooms[0]["id"]}
    if start:
        world["start"] = True
    if respawn_room is not None:
        world["respawnRoom"] = respawn_room
    world["rooms"] = rooms
    out = root / "server/data/world" / f"{name}.json"
    out.write_text(json.dumps(world, ensure_ascii=False, indent=2) + "\n")
    print(f"{out}: {len(rooms)} Räume, {sum(len(r.get('objects', [])) for r in rooms)} Zierobjekte")
