"""Liest Raumklassen aus dem KOD-Original (kod/object/active/holder/room/**.kod): Name, .roo, Musik, Licht,
Ankunftspunkt, Ausgänge (plExits, plEdge_Exits), Zonen-Übergänge (SomethingMoved → UtilGoNearSquare), Monster
(plMonsters, plGenerators, piGen_*, piMonster_count_max) und Bäume. Benutzt von make_wilderness.py und
add_region_exits.py."""
import json
import re
from pathlib import Path

from roo2layout import read_roo

root = Path(__file__).resolve().parent.parent
repo = root.parent
roomdir = repo / "kod/object/active/holder/room"
khd = (repo / "kod/include/blakston.khd").read_text(encoding="latin-1")

CONST = {m.group(1).upper(): int(m.group(2), 0) for m in re.finditer(r"^\s*(\w+)\s*=\s*(0x[0-9A-Fa-f]+|-?\d+)", khd, re.M)}
EDGE = {"LEAVE_NORTH": "NORTH", "LEAVE_SOUTH": "SOUTH", "LEAVE_EAST": "EAST", "LEAVE_WEST": "WEST"}
ROTATE = {"ROTATE_NONE": "NONE", "ROTATE_90": "90", "ROTATE_180": "180", "ROTATE_270": "270", "ROTATE_LEFT": "270",
          "ROTATE_RIGHT": "90"}


def classes():
    """Klassenname (klein) → (Pfad, Text) aller Raumklassen."""
    out = {}
    for f in roomdir.rglob("*.kod"):
        text = f.read_text(encoding="latin-1")
        m = re.search(r"^(\w+)\s+is\s+(\w+)", text, re.M)
        if m:
            out[m.group(1).lower()] = (f, text)
    return out


LOCAL = {}   # Konstanten der gerade gelesenen Datei (constants:-Abschnitt), von room_of gesetzt


def local_constants(text):
    sec = re.search(r"^constants:(.*?)^(resources|classvars|properties|messages):", text, re.S | re.M)
    return {m.group(1).upper(): int(m.group(2), 0)
            for m in re.finditer(r"^\s*(\w+)\s*=\s*(0x[0-9A-Fa-f]+|-?\d+)\s*$", sec.group(1) if sec else "", re.M)}


def value(token):
    token = token.strip()
    if re.fullmatch(r"-?\d+", token):
        return int(token)
    return LOCAL.get(token.upper(), CONST.get(token.upper()))


def resource(text, name):
    m = re.search(rf"^\s*{re.escape(name)}\s*=\s*(.+?)\s*$", text, re.M | re.I)
    if not m:
        return None
    v = m.group(1)
    return v[1:v.rindex('"')] if v.startswith('"') else v


def prop(text, name):
    """Eigenschaft/Klassenvariable; Ausdrücke aus Konstanten und + - * / werden ausgerechnet."""
    m = re.search(rf"^\s*{name}\s*=\s*([^%\n]+)", text, re.M | re.I)
    if not m:
        return None
    raw = m.group(1).strip()
    if raw == "$":
        return None
    expr = re.sub(r"[A-Za-z_]\w*", lambda t: str(CONST.get(t.group(0).upper(), "None")), raw)
    try:
        v = eval(expr.replace("/", "//"), {})
        return int(v) if v is not None else None
    except Exception:
        return None


def symbol(text, name):
    m = re.search(rf"^\s*{name}\s*=\s*(\w+)", text, re.M | re.I)
    return m.group(1) if m else None


def exits(text):
    out = []
    for m in re.finditer(r"plExits\s*=\s*Cons\(\[\s*([^\]]*)\]", text, re.I):
        parts = [p.strip() for p in m.group(1).split(",")]
        if parts[2] == "ROOM_LOCKED_DOOR":
            continue
        r, c, to, tr, tc = (value(p) for p in parts[:5])
        rot = ROTATE.get(parts[5], "NONE") if len(parts) > 5 else "NONE"
        out.append({"row": r, "col": c, "to": to, "toConstant": parts[2], "toRow": tr, "toCol": tc, "rotate": rot})
    return out


def edge_exits(text):
    out = []
    for m in re.finditer(r"plEdge_Exits\s*=\s*Cons\(\[\s*([^\]]*)\]", text, re.I):
        parts = [p.strip() for p in m.group(1).split(",")]
        e = {"edge": EDGE[parts[0]], "to": value(parts[1]), "toConstant": parts[1], "toRow": value(parts[2]),
             "toCol": value(parts[3]), "rotate": ROTATE.get(parts[4], "NONE") if len(parts) > 4 else "NONE"}
        # Bedingter Rand (room.kod): [ …, ROW_IS_GREATER_THAN|… , Wert ]
        if len(parts) > 6 and parts[5] in COND:
            e[COND[parts[5]]] = value(parts[6])
        out.append(e)
    return out


COND = {"ROW_IS_GREATER_THAN": "rowAbove", "ROW_IS_LESS_THAN": "rowBelow", "COL_IS_GREATER_THAN": "colAbove",
        "COL_IS_LESS_THAN": "colBelow"}


def _condition(cond):
    """KOD-Bedingung über new_row/new_col → Python-Ausdruck."""
    expr = re.sub(r"(?<![<>!=])=(?!=)", "==", cond)
    expr = re.sub(r"<>", "!=", expr)
    expr = re.sub(r"\bAND\b|\band\b", " and ", expr, flags=re.I)
    expr = re.sub(r"\bOR\b|\bor\b", " or ", expr, flags=re.I)
    return expr


def region_exits(text, rows, cols):
    """SomethingMoved: if (Bedingung) { … UtilGoNearSquare … #num=RID_X … #new_row=…, #new_col=… [#new_angle=ANGLE_Y] }."""
    m = re.search(r"\n\s*SomethingMoved\(.*?\n\s*\{(.*?)\n\s*propagate;", text, re.S)
    if not m:
        return []
    body = m.group(1)
    out = []
    for blk in re.finditer(r"if\s*(\(.*?\)|[^{]+?)\s*\{(.*?)return;", body, re.S):
        cond, act = blk.group(1), blk.group(2)
        if "UtilGoNearSquare" not in act:
            continue
        rid = re.search(r"#num\s*=\s*(\w+)", act).group(1)
        tr = value(re.search(r"#new_row\s*=\s*(\w+)", act).group(1))
        tc = value(re.search(r"#new_col\s*=\s*(\w+)", act).group(1))
        ang = re.search(r"#new_angle\s*=\s*ANGLE_(\w+)", act)
        try:
            expr = compile(_condition(cond), "<kod>", "eval")
        except SyntaxError:
            continue
        cells = [[r, c] for r in range(1, rows + 1) for c in range(1, cols + 1)
                 if eval(expr, {}, {"new_row": r, "new_col": c})]
        if cells:
            e = {"rects": rects(cells), "to": value(rid), "toConstant": rid, "toRow": tr, "toCol": tc}
            if ang:
                e["angle"] = ang.group(1)
            out.append(e)
    return out


def rects(cells):
    """Felder → Rechtecke [zeile1, spalte1, zeile2, spalte2] (Zeilenläufe, gleiche Läufe untereinander verschmolzen)."""
    runs = {}
    for r, c in sorted(cells):
        cur = runs.setdefault(r, [])
        if cur and cur[-1][1] == c - 1:
            cur[-1][1] = c
        else:
            cur.append([c, c])
    out = []
    for r in sorted(runs):
        for c1, c2 in runs[r]:
            for o in out:
                if o[2] == r - 1 and o[1] == c1 and o[3] == c2:
                    o[2] = r
                    break
            else:
                out.append([r, c1, r, c2])
    return out


def monsters(text):
    m = re.search(r"plMonsters\s*=\s*\[(.*?)\]\s*;", text, re.S)
    table = re.findall(r"\[\s*&(\w+)\s*,\s*(\d+)\s*\]", m.group(1)) if m else []
    g = re.search(r"plGenerators\s*=\s*\[(.*?)\]\s*;", text, re.S)
    gens = [[int(a), int(b)] for a, b in re.findall(r"\[\s*(\d+)\s*,\s*(\d+)\s*\]", g.group(1))] if g else []
    return [(cls, int(w)) for cls, w in table], gens


def trees(text):
    """Create(&Tree,#top=TREE_X,…) bzw. &SingleTree mit #new_row/#new_col (+fine)."""
    out = []
    for m in re.finditer(r"Create\(&(Tree|SingleTree)([^)]*)\)\s*,\s*(#[^;]*?)\)\s*;", text, re.S):
        args = dict(re.findall(r"#(\w+)\s*=\s*(\w+)", m.group(3)))
        if "new_row" not in args:
            continue
        top = re.search(r"#top\s*=\s*TREE_(\w+)", m.group(2))
        out.append({"type": f"TREE_{top.group(1) if top else 'OAK'}", "row": value(args["new_row"]),
                    "col": value(args["new_col"]), "fineRow": value(args.get("fine_row", "32")),
                    "fineCol": value(args.get("fine_col", "32")), "angle": 0})
    return out


def locked_doors(text):
    """plExits-Einträge mit ROOM_LOCKED_DOOR (optional mit Nachricht)."""
    out = []
    for m in re.finditer(r"plExits\s*=\s*Cons\(\[\s*([^\]]*)\]", text, re.I):
        parts = [p.strip() for p in m.group(1).split(",")]
        if parts[2] == "ROOM_LOCKED_DOOR":
            d = {"row": value(parts[0]), "col": value(parts[1])}
            if len(parts) > 3 and resource(text, parts[3]):
                d["message"] = resource(text, parts[3])
            out.append(d)
    return out


TERRAIN = {"FOREST": "forest", "GRASS": "forest", "FIELD": "forest", "PLAINS": "forest", "SWAMP": "forest",
           "MOUNTAIN": "mountain", "BADLANDS": "badlands", "DESERT": "badlands", "CAVES": "cave", "SNOW": "snow"}
kinds = {v["class"].lower(): k for k, v in json.loads((root / "server/data/monsters/monsters.json").read_text())["monsters"].items()}
missing_kinds = set()


def room_of(path):
    text = path.read_text(encoding="latin-1")
    LOCAL.clear()
    LOCAL.update(local_constants(text))
    cls = re.search(r"^(\w+)\s+is\s+(\w+)", text, re.M).group(1)
    roo = resource(text, f"room_{cls}") or resource(text, symbol(text, "prRoom") or "")
    name_sym = symbol(text, "vrName")
    name = (resource(text, name_sym) if name_sym else None) or cls
    music_sym = symbol(text, "prMusic")
    music = resource(text, music_sym) if music_sym else None
    rid_sym = symbol(text, "piRoom_num")
    room = {"id": CONST[rid_sym.upper()], "constant": rid_sym.upper(), "kodClass": cls, "name": name, "roo": roo.lower()}
    if music:
        room["music"] = music
    light = prop(text, "piBaseLight")
    room["baseLight"] = 164 if light is None else light
    outside = prop(text, "piOutside_factor")
    room["outsideFactor"] = 8 if outside is None else outside
    room["outdoors"] = room["outsideFactor"] > 0
    terrain = symbol(text, "viTerrain_type")
    if terrain and terrain.startswith("TERRAIN_"):
        room["terrain"] = TERRAIN.get(terrain[8:], terrain[8:].lower())
    room["teleport"] = {"row": prop(text, "viTeleport_row") or 1, "col": prop(text, "viTeleport_col") or 1}
    room["exits"] = exits(text)
    room["edgeExits"] = edge_exits(text)
    room["lockedDoors"] = locked_doors(text)
    src = repo / "resource/rooms" / roo
    if src.exists():
        cols, rows, _, _ = read_roo(src)
        room["regionExits"] = region_exits(text, rows, cols)
    table, gens = monsters(text)
    mons = []
    for kod_class, weight in table:
        key = kinds.get(kod_class.lower())
        if key is None:
            missing_kinds.add(kod_class)
            continue
        mons.append({"kind": key, "weight": weight})
    if mons and gens:
        room["monsters"] = mons
        room["generators"] = [{"row": r, "col": c} for r, c in gens]
        room["maxMonsters"] = prop(text, "piMonster_count_max") or 10
        room["genTime"] = prop(text, "piGen_time") or 20000
        room["genPercent"] = prop(text, "piGen_percent") or 100
        lo, hi = prop(text, "piInit_count_min"), prop(text, "piInit_count_max")
        if lo is not None:
            room["initCountMin"] = lo
        if hi is not None:
            room["initCountMax"] = hi
    # Bossräume (bossroom.kod): Boss und Gefolge einmal fest (plBossTemplate, plHenchmenTemplate)
    fixed = []
    for lst in ("plBossTemplate", "plHenchmenTemplate"):
        b = re.search(rf"{lst}\s*=\s*\[(.*?)\]\s*;", text, re.S)
        for cls, r, c in re.findall(r"\[\s*&(\w+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]", b.group(1)) if b else []:
            key = kinds.get(cls.lower())
            if key is None:
                missing_kinds.add(cls)
            else:
                fixed.append({"kind": key, "row": int(r), "col": int(c)})
    found = npcs(text)
    fixed += [{"kind": n["fixed"], "row": n["row"], "col": n["col"]} for n in found if "fixed" in n]
    found = [n for n in found if "fixed" not in n]
    if found:
        room["npcs"] = found
    if fixed:
        room["fixedMonsters"] = fixed
        room["boss"] = True
    room["objects"] = trees(text)
    return room




_mobs = None


def mob_classes():
    """Klassenname (klein) → Text aller Monster/NPC-Klassen unter nomoveon/battler/monster."""
    global _mobs
    if _mobs is None:
        _mobs = {}
        for f in (repo / "kod/object/active/holder/nomoveon/battler/monster").rglob("*.kod"):
            t = f.read_text(encoding="latin-1")
            m = re.search(r"^(\w+)\s+is\s+(\w+)", t, re.M)
            if m:
                _mobs[m.group(1).lower()] = (m.group(1), t)
    return _mobs


PEACEFUL = {"cow"}   # Monsterklassen, die als Bewohner gelten (Kühe in Raza stehen schon in raza.json)
ANGLES = {"EAST": 0, "SOUTH_EAST": 512, "SOUTH": 1024, "SOUTH_WEST": 1536, "WEST": 2048, "NORTH_WEST": 2560,
          "NORTH": 3072, "NORTH_EAST": 3584}


def npcs(text):
    """Friedliche Bewohner (MOB_NOFIGHT) und Schilder aus Create(&Klasse …) mit #new_row/#new_col."""
    out = []
    for m in re.finditer(r"Create\(\s*&(\w+)([^)]*)\)\s*,\s*(#[^;]*?)\)\s*;", text, re.S | re.I):
        cls, inner, place = m.group(1), m.group(2), m.group(3)
        args = dict(re.findall(r"#(\w+)\s*=\s*(\w+)", place))
        if "new_row" not in args or value(args["new_row"]) is None:
            continue
        pos = {"row": value(args["new_row"]), "col": value(args["new_col"]),
               "fineRow": value(args.get("fine_row", "32")), "fineCol": value(args.get("fine_col", "32")),
               "angle": ANGLES.get(args.get("new_angle", "")[6:], 0) if args.get("new_angle", "").startswith("ANGLE_") else 0}
        if cls.lower() == "sign":
            iargs = dict(re.findall(r"#(\w+)\s*=\s*(\w+)", inner))
            name = resource(text, iargs.get("name", "")) or "Schild"
            desc = resource(text, iargs.get("desc", "")) or ""
            out.append(dict(kodClass="Sign", name=name, icon="razsign1.bgf", role="Schild", description=desc, **pos))
            continue
        if cls.lower() in kinds and cls.lower() not in PEACEFUL:
            out.append(dict(fixed=kinds[cls.lower()], **pos))
            continue
        mob = mob_classes().get(cls.lower())
        if not mob:
            continue
        real, mt = mob
        attrs = re.search(r"viAttributes\s*=\s*([^\n]+)", mt)
        if not attrs or "MOB_NOFIGHT" not in attrs.group(1):
            continue
        name_sym = symbol(mt, "vrName")
        icon_sym = symbol(mt, "vrIcon")
        name = resource(mt, name_sym) if name_sym else real
        icon = resource(mt, icon_sym) if icon_sym else None
        out.append(dict(kodClass=real, name=name or real, icon=(icon or "").lower() or None, role="Bewohner", **pos))
    return out
