"""Liest Raumklassen aus dem KOD-Original (kod/object/active/holder/room/**.kod): Name, .roo, Musik, Licht,
Ankunftspunkt, Ausgänge (plExits, plEdge_Exits), Zonen-Übergänge (SomethingMoved → UtilGoNearSquare), Monster
(plMonsters, plGenerators, piGen_*, piMonster_count_max) und Bäume. Benutzt von make_wilderness.py und
add_region_exits.py."""
import re
from pathlib import Path

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


def value(token):
    token = token.strip()
    if re.fullmatch(r"-?\d+", token):
        return int(token)
    return CONST.get(token.upper())


def resource(text, name):
    m = re.search(rf"^\s*{re.escape(name)}\s*=\s*(.+?)\s*$", text, re.M)
    if not m:
        return None
    v = m.group(1)
    return v[1:v.rindex('"')] if v.startswith('"') else v


def prop(text, name):
    m = re.search(rf"^\s*{name}\s*=\s*([\w$]+)", text, re.M | re.I)
    return value(m.group(1)) if m else None


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
        out.append({"edge": EDGE[parts[0]], "to": value(parts[1]), "toConstant": parts[1], "toRow": value(parts[2]),
                    "toCol": value(parts[3]), "rotate": ROTATE.get(parts[4], "NONE") if len(parts) > 4 else "NONE"})
    return out


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
