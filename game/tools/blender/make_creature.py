"""Erzeugt Monster-Modelle prozedural aus Grundkörpern (Ellipsoide, Röhren, Kegel) mit Skelett und Animationen.

Jede Art ist ein Eintrag in CREATURES: ein Bauplan (quadruped, insect, spider, scorpion, centipede, worm, flyer,
spirit, tree, flytrap, blob, biped) mit Maßen, Farben und Merkmalen. Jedes Teil hängt zu 100 % an einem Knochen;
die Animationen Idle, Walk, Run, Attack und Die werden aus Gangmustern berechnet (Vierbeiner diagonal, Insekten im
Dreifuß, Spinnen im Viererschritt, Würmer und Tausendfüßer in Wellen, Flieger mit Flügelschlag).
Ergebnis wie bei make_character.py: glTF mit Skelett und Clips, Figur schaut nach -Y (glTF +Z).

Aufruf (im Repo-Root):

    blender -b --python game/tools/blender/make_creature.py -- game/client/models [art …] [--preview ordner]

Ohne Art werden alle Arten erzeugt. Alles eigene Geometrie, keine fremden Assets.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

FPS = 24


# --------------------------------------------------------------------------- Baukasten

class Creature:
    def __init__(self, name):
        self.name = name
        self.bones = []                       # (name, head, tail, parent)
        self.bm = bmesh.new()
        self.deform = self.bm.verts.layers.deform.verify()
        self.group = {}                       # Knochen → Vertex-Gruppe
        self.materials = []                   # (name, farbe, rauheit, leuchten)
        self.anim = None                      # fn(kind, t) → (rotationen, wurzelversatz, skalierung)
        self.clips = [("Idle", 2.4), ("Walk", 1.0), ("Run", 0.6), ("Attack", 0.8), ("Die", 1.3)]

    def bone(self, name, head, tail, parent=None):
        self.bones.append((name, Vector(head), Vector(tail), parent))
        return name

    def mat(self, color, rough=0.75, emit=0.0):
        key = (tuple(round(c, 3) for c in color), rough, emit)
        for i, m in enumerate(self.materials):
            if m[1:] == key:
                return i
        self.materials.append((f"{self.name}_{len(self.materials)}",) + key)
        return len(self.materials) - 1

    def _add(self, geom, bone, material):
        idx = self.group.setdefault(bone, len(self.group))
        verts = [g for g in geom if isinstance(g, bmesh.types.BMVert)]
        for v in verts:
            v[self.deform][idx] = 1.0
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = material
            f.smooth = True

    def ell(self, bone, center, radii, color, rot=(0, 0, 0), seg=16, rough=0.75, emit=0.0):
        """Ellipsoid (Halbachsen radii) um center, gedreht um rot (Grad, XYZ)."""
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=max(6, seg // 2), radius=1.0)
        from mathutils import Euler
        m = (Matrix.Translation(Vector(center)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
             @ Matrix.Diagonal((*radii, 1)))
        bmesh.ops.transform(self.bm, matrix=m, verts=res["verts"])
        self._add(res["verts"], bone, self.mat(color, rough, emit))

    def tube(self, bone, a, b, r1, r2, color, seg=10, rough=0.75, emit=0.0):
        """Röhre/Kegel von a (Radius r1) nach b (Radius r2)."""
        a, b = Vector(a), Vector(b)
        d = b - a
        if d.length < 1e-4:
            return
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=max(r1, 0.001),
                                    radius2=max(r2, 0.0005), depth=d.length)
        rot = d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=res["verts"])
        self._add(res["verts"], bone, self.mat(color, rough, emit))

    def limb(self, bone, a, b, r1, r2, color, joint=True):
        """Glied mit Gelenkkugel am Anfang."""
        self.tube(bone, a, b, r1, r2, color)
        if joint:
            self.ell(bone, a, (r1 * 1.15,) * 3, color, seg=10)

    def eyes(self, bone, center, spread, size, color=(0.05, 0.03, 0.02), glow=0.0, forward=0.0):
        for s in (-1, 1):
            c = Vector(center) + Vector((s * spread, -forward, 0))
            self.ell(bone, c, (size,) * 3, color, seg=10, rough=0.2, emit=glow)


def lighter(c, f=1.25):
    return tuple(min(1.0, x * f) for x in c)


def darker(c, f=0.6):
    return tuple(x * f for x in c)


def wave(t, phase=0.0):
    return math.sin(2 * math.pi * (t + phase))


def ease(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# Achsen im Armature-Raum: Figur schaut nach -Y, +X ist ihre linke Seite, +Z oben.
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)


def die_roll(t, height, side=1):
    """Umkippen auf die Seite: Drehung der Wurzel um die Längsachse und Absenken."""
    e = ease(t / 0.7)
    return [(Y, side * 85 * e)], Vector((0, 0, -height * 0.25 * e))


# --------------------------------------------------------------------------- Vierbeiner

def build_quadruped(c, p):
    L, H, W = p["length"], p["height"], p["width"]
    col, belly, acc = p["color"], p.get("belly", lighter(p["color"])), p.get("accent", darker(p["color"]))
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, L * 0.45, H), (0, -L * 0.45, H), "root")
    neck_top = Vector((0, -L * 0.5 - p.get("neck", 0.15) * L, H + p.get("neck_up", 0.25) * H))
    c.bone("neck", (0, -L * 0.4, H + 0.1 * H), neck_top, "body")
    hs = p.get("head", 0.22) * L
    c.bone("head", neck_top, neck_top + Vector((0, -hs * 1.6, -hs * 0.2)), "neck")
    c.bone("jaw", neck_top + Vector((0, -hs * 0.4, -hs * 0.35)), neck_top + Vector((0, -hs * 1.5, -hs * 0.45)), "head")
    # Rumpf
    c.ell("body", (0, 0, H), (W / 2, L * 0.5, p.get("depth", 0.3) * H + W * 0.2), col)
    c.ell("body", (0, -L * 0.25, H + 0.02), (W * 0.55, L * 0.3, p.get("depth", 0.3) * H + W * 0.25), col)
    c.ell("body", (0, 0, H - W * 0.25), (W * 0.42, L * 0.42, W * 0.22), belly)
    if p.get("hump"):
        c.ell("body", (0, -L * 0.2, H + W * 0.45), (W * 0.45, L * 0.25, W * 0.35), col)
    if p.get("mane"):
        for i in range(6):
            y = -L * 0.45 + i * L * 0.12
            c.tube("body", (0, y, H + W * 0.3), (0, y + L * 0.05, H + W * 0.75), W * 0.12, 0.0, acc, seg=6)
    if p.get("spikes"):
        for i in range(7):
            y = -L * 0.4 + i * L * 0.13
            c.tube("body", (0, y, H + W * 0.45), (0, y + L * 0.06, H + W * 0.9), W * 0.08, 0.0, p["spikes"], seg=6)
    # Hals und Kopf
    c.tube("neck", (0, -L * 0.4, H + 0.1 * H), neck_top, W * 0.32, hs * 0.55, col)
    head_c = neck_top + Vector((0, -hs * 0.5, 0))
    c.ell("head", head_c, (hs * 0.6, hs * 0.75, hs * 0.55), col)
    snout = p.get("snout", 0.9)
    c.ell("head", head_c + Vector((0, -hs * 0.8 * snout, -hs * 0.12)), (hs * 0.35, hs * 0.6 * snout, hs * 0.3), lighter(col, 1.1))
    c.ell("head", head_c + Vector((0, -hs * 1.35 * snout, -hs * 0.05)), (hs * 0.13, hs * 0.1, hs * 0.1), (0.08, 0.05, 0.05))
    c.ell("jaw", head_c + Vector((0, -hs * 0.75 * snout, -hs * 0.38)), (hs * 0.28, hs * 0.55 * snout, hs * 0.12), belly)
    if p.get("teeth"):
        for s in (-1, 1):
            c.tube("head", head_c + Vector((s * hs * 0.15, -hs * 1.15 * snout, -hs * 0.3)),
                   head_c + Vector((s * hs * 0.15, -hs * 1.2 * snout, -hs * 0.6)), hs * 0.06, 0, (0.95, 0.92, 0.8), seg=6)
    c.eyes("head", head_c + Vector((0, -hs * 0.55, hs * 0.22)), hs * 0.32, hs * 0.11,
           p.get("eye", (0.05, 0.03, 0.02)), p.get("eye_glow", 0.0))
    if p.get("ears", True):
        e = p.get("ear_size", 0.5)
        for s in (-1, 1):
            c.tube("head", head_c + Vector((s * hs * 0.35, hs * 0.1, hs * 0.35)),
                   head_c + Vector((s * hs * 0.55, hs * 0.25, hs * (0.35 + e))), hs * 0.18, 0.0, acc, seg=8)
    if p.get("horns"):
        for s in (-1, 1):
            base = head_c + Vector((s * hs * 0.35, 0, hs * 0.4))
            c.tube("head", base, base + Vector((s * hs * 0.6, hs * 0.3, hs * 0.5)), hs * 0.14, 0.0, p["horns"], seg=8)
    # Beine
    legs = []
    for front, y in ((True, -L * 0.36), (False, L * 0.36)):
        for s in (-1, 1):
            nm = ("f" if front else "b") + ("l" if s > 0 else "r")
            hip = Vector((s * W * 0.38, y, H - W * 0.1))
            knee = Vector((s * W * 0.42, y + (0.05 if front else -0.06) * H, H * 0.48))
            foot = Vector((s * W * 0.42, y, 0.04 * H))
            c.bone(f"up_{nm}", hip, knee, "body")
            c.bone(f"lo_{nm}", knee, foot, f"up_{nm}")
            r = p.get("leg", 0.17) * W
            c.limb(f"up_{nm}", hip, knee, r * 1.35, r, col)
            c.limb(f"lo_{nm}", knee, foot, r, r * 0.8, col)
            c.ell(f"lo_{nm}", foot + Vector((0, -r * 0.4, 0)), (r * 1.05, r * 1.4, r * 0.6), acc)
            legs.append((nm, front, s))
    # Schwanz
    tl = p.get("tail", 0.5) * L
    prev, start = "body", Vector((0, L * 0.48, H + 0.05 * H))
    for i in range(3):
        end = start + Vector((0, tl / 3, -tl * 0.15 + p.get("tail_up", 0) * tl / 3))
        c.bone(f"tail{i}", start, end, prev)
        r = p.get("tail_r", 0.08) * W * (1 - i * 0.25)
        c.limb(f"tail{i}", start, end, r, r * 0.75, p.get("tail_color", col))
        prev, start = f"tail{i}", end

    def anim(kind, t):
        rot, off = {}, Vector()
        if kind in ("Walk", "Run"):
            amp = 28 if kind == "Walk" else 40
            for nm, front, s in legs:
                ph = 0.0 if (front and s > 0) or (not front and s < 0) else 0.5
                if kind == "Run":
                    ph = 0.0 if front else 0.5   # Galopp: Vorder- und Hinterbeine paarweise
                    ph += 0.08 * s
                w = wave(t, ph)
                rot[f"up_{nm}"] = [(X, amp * w)]
                rot[f"lo_{nm}"] = [(X, -max(0.0, -wave(t, ph + 0.25)) * (35 if front else -35))]
            rot["body"] = [(X, (6 if kind == "Run" else 2) * wave(t * 2))]
            off.z = abs(wave(t)) * H * (0.06 if kind == "Run" else 0.03)
            for i in range(3):
                rot[f"tail{i}"] = [(Z, 12 * wave(t, i * 0.15))]
            rot["head"] = [(X, 4 * wave(t * 2, 0.2))]
        elif kind == "Idle":
            rot["head"] = [(Z, 18 * wave(t, 0)), (X, 5 * wave(t * 2))]
            for i in range(3):
                rot[f"tail{i}"] = [(Z, 10 * wave(t, i * 0.2))]
            off.z = 0.01 * H * wave(t * 2)
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.7))
            rot["body"] = [(X, -8 * k)]
            rot["neck"] = [(X, -25 * k)]
            rot["head"] = [(X, 15 * k)]
            rot["jaw"] = [(X, 30 * k)]
            for nm, front, s in legs:
                rot[f"up_{nm}"] = [(X, (-25 if front else 15) * k)]
            off.y = -0.25 * L * k
        elif kind == "Die":
            r, off = die_roll(t, H)
            rot["root"] = r
            e = ease(t / 0.9)
            for nm, front, s in legs:
                rot[f"up_{nm}"] = [(X, (25 if front else -25) * e)]
                rot[f"lo_{nm}"] = [(X, (20 if front else -20) * e)]
            rot["neck"] = [(X, 25 * e)]
            rot["jaw"] = [(X, 20 * e)]
        return rot, off, {}
    c.anim = anim


# --------------------------------------------------------------------------- Insekten, Spinnen, Skorpione

def _legs(c, p, pairs, attach, body_bone, H, reach, segs=2, color=None):
    """Seitliche Beine: Oberschenkel schräg nach oben-außen, Unterschenkel nach unten-außen."""
    color = color or darker(p["color"], 0.8)
    r = p.get("leg_r", 0.035) * reach * 3
    legs = []
    for i, y in enumerate(attach):
        for s in (-1, 1):
            nm = f"leg{i}{'l' if s > 0 else 'r'}"
            spread = (i - (pairs - 1) / 2) * 0.35
            out = Vector((s, spread, 0)).normalized()
            hip = Vector((s * p["width"] * 0.35, y, H))
            knee = hip + out * reach * 0.5 + Vector((0, 0, H * 0.6))
            foot = hip + out * reach * 1.1 + Vector((0, 0, -H * 0.95))
            c.bone(f"a_{nm}", hip, knee, body_bone)
            c.limb(f"a_{nm}", hip, knee, r, r * 0.8, color)
            if segs == 3:
                mid = knee + out * reach * 0.45 + Vector((0, 0, -H * 0.9))
                c.bone(f"b_{nm}", knee, mid, f"a_{nm}")
                c.limb(f"b_{nm}", knee, mid, r * 0.8, r * 0.6, color)
                c.bone(f"c_{nm}", mid, foot, f"b_{nm}")
                c.limb(f"c_{nm}", mid, foot, r * 0.6, r * 0.25, color)
            else:
                c.bone(f"b_{nm}", knee, foot, f"a_{nm}")
                c.limb(f"b_{nm}", knee, foot, r * 0.8, r * 0.3, color)
            legs.append((nm, i, s))
    return legs


def _leg_anim(rot, legs, t, amp, groups):
    """Beinschwung um die Hochachse und Anheben während des Vorschwungs; groups(i, s) → Phase."""
    for nm, i, s in legs:
        ph = groups(i, s)
        rot[f"a_{nm}"] = [(Z, amp * wave(t, ph)), (Y, -s * 18 * max(0.0, wave(t, ph + 0.25)))]


def build_insect(c, p):
    L, H, W = p["length"], p["height"], p["width"]
    col = p["color"]
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("thorax", (0, L * 0.05, H), (0, -L * 0.15, H), "root")
    c.bone("abdomen", (0, L * 0.08, H), (0, L * 0.5, H + 0.05 * L), "thorax")
    c.bone("head", (0, -L * 0.15, H), (0, -L * 0.35, H), "thorax")
    c.bone("mand_l", (W * 0.15, -L * 0.36, H - 0.03 * L), (W * 0.05, -L * 0.46, H - 0.03 * L), "head")
    c.bone("mand_r", (-W * 0.15, -L * 0.36, H - 0.03 * L), (-W * 0.05, -L * 0.46, H - 0.03 * L), "head")
    c.ell("thorax", (0, -L * 0.05, H), (W * 0.32, L * 0.14, W * 0.3), col, rough=0.35)
    c.ell("abdomen", (0, L * 0.3, H + 0.04 * L), (W * 0.5, L * 0.24, W * 0.42), p.get("abdomen", col), rough=0.35)
    c.tube("thorax", (0, -L * 0.13, H), (0, L * 0.1, H), W * 0.1, W * 0.1, col)
    c.ell("head", (0, -L * 0.25, H + 0.01 * L), (W * 0.36, L * 0.11, W * 0.3), col, rough=0.35)
    c.eyes("head", (0, -L * 0.29, H + 0.06 * L), W * 0.28, W * 0.12, p.get("eye", (0.05, 0.05, 0.05)), p.get("eye_glow", 0.0))
    for s, b in ((1, "mand_l"), (-1, "mand_r")):
        c.tube(b, (s * W * 0.15, -L * 0.34, H - 0.03 * L), (s * W * 0.03, -L * 0.47, H - 0.04 * L), W * 0.07, 0.0, darker(col, 0.5), seg=6)
        c.tube("head", (s * W * 0.12, -L * 0.3, H + 0.08 * L), (s * W * 0.5, -L * 0.55, H + 0.3 * L), W * 0.025, W * 0.01, darker(col), seg=6)
    legs = _legs(c, p, 3, (-L * 0.12, -L * 0.04, L * 0.04), "thorax", H, p.get("reach", 0.5) * L)

    def anim(kind, t):
        rot, off = {}, Vector()
        if kind in ("Walk", "Run"):
            _leg_anim(rot, legs, t, 22 if kind == "Walk" else 28, lambda i, s: 0.0 if (i % 2 == 0) == (s > 0) else 0.5)
            rot["abdomen"] = [(X, 3 * wave(t * 2))]
            rot["head"] = [(Z, 5 * wave(t))]
            off.z = 0.01 * L * abs(wave(t * 2))
        elif kind == "Idle":
            rot["head"] = [(Z, 10 * wave(t))]
            rot["abdomen"] = [(X, 4 * wave(t * 2))]
            rot["mand_l"] = [(Z, 15 * max(0, wave(t * 3)))]
            rot["mand_r"] = [(Z, -15 * max(0, wave(t * 3)))]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.7))
            rot["thorax"] = [(X, -15 * k)]
            rot["mand_l"] = [(Z, -35 * k)]
            rot["mand_r"] = [(Z, 35 * k)]
            off.y = -0.25 * L * k
        elif kind == "Die":
            e = ease(t / 0.8)
            rot["root"] = [(Y, 175 * e)]
            off.z = H * 1.6 * e   # auf den Rücken, Beine nach oben
            for nm, i, s in legs:
                rot[f"a_{nm}"] = [(Y, s * 40 * e), (Z, 10 * wave(t * 4, i * 0.2) * (1 - e))]
                rot[f"b_{nm}"] = [(Y, s * 50 * e)]
        return rot, off, {}
    c.anim = anim


def build_spider(c, p):
    L, H, W = p["length"], p["height"], p["width"]
    col = p["color"]
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("thorax", (0, L * 0.1, H), (0, -L * 0.2, H), "root")
    c.bone("abdomen", (0, L * 0.1, H + 0.02 * L), (0, L * 0.6, H + 0.15 * L), "thorax")
    c.bone("fang_l", (W * 0.12, -L * 0.3, H - 0.02 * L), (W * 0.1, -L * 0.36, H - 0.12 * L), "thorax")
    c.bone("fang_r", (-W * 0.12, -L * 0.3, H - 0.02 * L), (-W * 0.1, -L * 0.36, H - 0.12 * L), "thorax")
    c.ell("thorax", (0, -L * 0.08, H), (W * 0.35, L * 0.2, W * 0.25), col, rough=0.5)
    c.ell("abdomen", (0, L * 0.38, H + 0.12 * L), (W * 0.55, L * 0.32, W * 0.45), p.get("abdomen", col), rough=0.5)
    if p.get("marking"):
        c.ell("abdomen", (0, L * 0.3, H + 0.12 * L + W * 0.42), (W * 0.2, L * 0.14, W * 0.06), p["marking"], rough=0.4)
    c.eyes("thorax", (0, -L * 0.26, H + 0.06 * L), W * 0.1, W * 0.05, p.get("eye", (0.6, 0.05, 0.05)), p.get("eye_glow", 0.8))
    c.eyes("thorax", (0, -L * 0.24, H + 0.1 * L), W * 0.18, W * 0.04, p.get("eye", (0.6, 0.05, 0.05)), p.get("eye_glow", 0.8))
    for s, b in ((1, "fang_l"), (-1, "fang_r")):
        c.tube(b, (s * W * 0.12, -L * 0.3, H - 0.02 * L), (s * W * 0.1, -L * 0.36, H - 0.14 * L), W * 0.06, 0.0, darker(col, 0.4), seg=6)
    legs = _legs(c, p, 4, (-L * 0.16, -L * 0.1, -L * 0.03, L * 0.03), "thorax", H, p.get("reach", 0.6) * L, segs=3)

    def anim(kind, t):
        rot, off = {}, Vector()
        if kind in ("Walk", "Run"):
            _leg_anim(rot, legs, t, 18 if kind == "Walk" else 24, lambda i, s: 0.0 if (i % 2 == 0) == (s > 0) else 0.5)
            rot["abdomen"] = [(Z, 4 * wave(t))]
            off.z = 0.01 * L * abs(wave(t * 2))
        elif kind == "Idle":
            rot["abdomen"] = [(X, 4 * wave(t * 2))]
            rot["fang_l"] = [(X, 12 * max(0, wave(t * 2)))]
            rot["fang_r"] = [(X, 12 * max(0, wave(t * 2)))]
            for nm, i, s in legs:
                if i == 0:
                    rot[f"a_{nm}"] = [(Y, -s * 6 * max(0, wave(t, 0.3 * s)))]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.7))
            rot["thorax"] = [(X, 14 * k)]   # hinten hoch, vorn runter: Biss
            rot["fang_l"] = [(X, -40 * k)]
            rot["fang_r"] = [(X, -40 * k)]
            for nm, i, s in legs:
                if i == 0:
                    rot[f"a_{nm}"] = [(Y, -s * 50 * k)]
            off.y = -0.2 * L * k
            off.z = 0.1 * H * k
        elif kind == "Die":
            e = ease(t / 0.8)
            rot["root"] = [(Y, 178 * e)]
            off.z = H * 1.7 * e
            for nm, i, s in legs:
                rot[f"a_{nm}"] = [(Y, s * 60 * e)]
                rot[f"b_{nm}"] = [(Y, s * 60 * e)]
        return rot, off, {}
    c.anim = anim


def build_scorpion(c, p):
    L, H, W = p["length"], p["height"], p["width"]
    col = p["color"]
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, L * 0.25, H), (0, -L * 0.2, H), "root")
    c.ell("body", (0, 0, H), (W * 0.45, L * 0.3, W * 0.22), col, rough=0.35)
    for i in range(4):
        c.ell("body", (0, -L * 0.15 + i * L * 0.12, H + W * 0.12), (W * 0.47, L * 0.06, W * 0.15), darker(col, 0.85), rough=0.35)
    c.eyes("body", (0, -L * 0.28, H + W * 0.12), W * 0.08, W * 0.04, (0.05, 0.05, 0.05))
    # Scheren
    for s in (-1, 1):
        nm = "l" if s > 0 else "r"
        a = Vector((s * W * 0.3, -L * 0.25, H))
        b = a + Vector((s * W * 0.4, -L * 0.15, 0.02))
        cc = b + Vector((-s * W * 0.15, -L * 0.25, 0))
        c.bone(f"arm_{nm}", a, b, "body")
        c.bone(f"fore_{nm}", b, cc, f"arm_{nm}")
        c.bone(f"claw_{nm}", cc, cc + Vector((0, -L * 0.15, 0)), f"fore_{nm}")
        c.limb(f"arm_{nm}", a, b, W * 0.09, W * 0.08, col)
        c.limb(f"fore_{nm}", b, cc, W * 0.08, W * 0.1, col)
        c.ell(f"fore_{nm}", cc, (W * 0.17, L * 0.09, W * 0.1), col, rough=0.35)
        c.tube(f"fore_{nm}", cc + Vector((s * W * 0.06, 0, 0)), cc + Vector((s * W * 0.07, -L * 0.17, 0)), W * 0.07, 0.0, darker(col), seg=6)
        c.tube(f"claw_{nm}", cc + Vector((-s * W * 0.06, 0, 0)), cc + Vector((-s * W * 0.03, -L * 0.15, 0)), W * 0.06, 0.0, darker(col), seg=6)
    # Schwanz: 5 Glieder in einem Bogen über den Rücken, Stachel am Ende
    prev, start = "body", Vector((0, L * 0.28, H + W * 0.05))
    n = 5
    for i in range(n):
        ang = math.radians(10 + i * 38)
        end = start + Vector((0, math.cos(ang) * L * 0.13, math.sin(ang) * L * 0.13))
        c.bone(f"tail{i}", start, end, prev)
        r = W * (0.13 - i * 0.012)
        c.limb(f"tail{i}", start, end, r, r * 0.85, col)
        prev, start = f"tail{i}", end
    c.bone("sting", start, start + Vector((0, -L * 0.1, -L * 0.03)), prev)
    c.ell("sting", start + Vector((0, -L * 0.02, 0)), (W * 0.1, L * 0.06, W * 0.08), p.get("sting", lighter(col, 1.4)))
    c.tube("sting", start + Vector((0, -L * 0.06, 0)), start + Vector((0, -L * 0.14, -L * 0.05)), W * 0.04, 0.0, (0.1, 0.08, 0.05), seg=6)
    legs = _legs(c, p, 4, (-L * 0.1, -L * 0.02, L * 0.06, L * 0.14), "body", H, p.get("reach", 0.35) * L)

    def anim(kind, t):
        rot, off = {}, Vector()
        if kind in ("Walk", "Run"):
            _leg_anim(rot, legs, t, 18 if kind == "Walk" else 24, lambda i, s: 0.0 if (i % 2 == 0) == (s > 0) else 0.5)
            for i in range(n):
                rot[f"tail{i}"] = [(X, 4 * wave(t, i * 0.1))]
        elif kind == "Idle":
            for i in range(n):
                rot[f"tail{i}"] = [(X, 5 * wave(t, i * 0.12)), (Z, 4 * wave(t, 0.3))]
            for s in ("l", "r"):
                rot[f"claw_{s}"] = [(Z, (1 if s == "l" else -1) * 15 * max(0, wave(t * 2)))]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.6))
            for i in range(n):
                rot[f"tail{i}"] = [(X, -14 * k)]
            for s in ("l", "r"):
                rot[f"arm_{s}"] = [(Z, (1 if s == "l" else -1) * -20 * k)]
                rot[f"claw_{s}"] = [(Z, (1 if s == "l" else -1) * 30 * k)]
            off.y = -0.1 * L * k
        elif kind == "Die":
            e = ease(t / 0.8)
            for i in range(n):
                rot[f"tail{i}"] = [(X, 30 * e)]
            for nm, i, s in legs:
                rot[f"a_{nm}"] = [(Y, s * 35 * e)]
            off.z = -H * 0.6 * e
        return rot, off, {}
    c.anim = anim


# --------------------------------------------------------------------------- Würmer und Tausendfüßer

def build_worm(c, p, centipede=False):
    L, H, W = p["length"], p["height"], p["width"]
    col = p["color"]
    n = p.get("segments", 8)
    seg_len = L / n
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    prev = "root"
    head_y = -L * 0.5
    names = []
    for i in range(n):
        y0 = head_y + i * seg_len
        nm = c.bone(f"seg{i}", (0, y0 + seg_len, H), (0, y0, H), prev) if i == 0 else \
            c.bone(f"seg{i}", (0, y0, H), (0, y0 + seg_len, H), prev)
        names.append(nm)
        prev = nm
        taper = 1 - max(0, i - n * 0.6) / n * 1.4
        r = W * 0.5 * taper
        band = col if i % 2 == 0 else p.get("band", darker(col, 0.85))
        c.ell(nm, (0, y0 + seg_len * 0.5, H), (r, seg_len * 0.62, r * p.get("flat", 0.85)), band, rough=p.get("rough", 0.45))
        if centipede and i > 0:
            for s in (-1, 1):
                ln = f"leg{i}{'l' if s > 0 else 'r'}"
                a = Vector((s * r * 0.8, y0 + seg_len * 0.5, H))
                b = a + Vector((s * W * 0.55, -seg_len * 0.2, -H * 0.95))
                c.bone(ln, a, b, nm)
                c.limb(ln, a, b, W * 0.06, W * 0.03, darker(col, 0.7))
    # Kopf am vordersten Glied
    hc = Vector((0, head_y + seg_len * 0.2, H + W * 0.05))
    c.ell("seg0", hc, (W * 0.48, seg_len * 0.6, W * 0.42), p.get("head_color", darker(col, 0.9)), rough=0.4)
    if p.get("mouth"):
        c.ell("seg0", hc + Vector((0, -seg_len * 0.55, 0)), (W * 0.3, W * 0.06, W * 0.3), (0.25, 0.05, 0.05))
        for k in range(6):
            a = 2 * math.pi * k / 6
            base = hc + Vector((math.cos(a) * W * 0.3, -seg_len * 0.55, math.sin(a) * W * 0.3))
            c.tube("seg0", base, base + Vector((-math.cos(a) * W * 0.12, -W * 0.12, -math.sin(a) * W * 0.12)), W * 0.05, 0, (0.9, 0.85, 0.7), seg=5)
    else:
        c.eyes("seg0", hc + Vector((0, -seg_len * 0.4, W * 0.15)), W * 0.22, W * 0.08, p.get("eye", (0.05, 0.05, 0.05)), p.get("eye_glow", 0.0))
    if centipede:
        for s in (-1, 1):
            c.tube("seg0", hc + Vector((s * W * 0.15, -seg_len * 0.4, W * 0.2)), hc + Vector((s * W * 0.7, -seg_len * 2, W * 0.6)), W * 0.03, W * 0.01, darker(col), seg=5)
            c.tube("seg0", hc + Vector((s * W * 0.2, -seg_len * 0.45, -W * 0.15)), hc + Vector((s * W * 0.05, -seg_len * 0.9, -W * 0.2)), W * 0.07, 0, darker(col, 0.4), seg=5)
    lateral = p.get("lateral", centipede)

    def anim(kind, t):
        rot, off = {}, Vector()
        axis = Z if lateral else X
        if kind in ("Walk", "Run", "Idle"):
            amp = {"Walk": 10, "Run": 16, "Idle": 4}[kind]
            for i in range(1, n):
                rot[f"seg{i}"] = [(axis, amp * wave(t, -i / n))]
            rot["seg0"] = [(axis, -amp * 0.5 * wave(t, 0.1))]
            if centipede:
                for i in range(1, n):
                    for s in (-1, 1):
                        rot[f"leg{i}{'l' if s > 0 else 'r'}"] = [(Z, (25 if kind != "Idle" else 3) * wave(t * 2, i * 0.15 + (0.5 if s > 0 else 0)))]
            if p.get("rear_up") and kind == "Idle":
                rot["seg1"] = rot.get("seg1", []) + [(X, -20)]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.7))
            rot["seg1"] = [(X, -35 * k)]
            rot["seg2"] = [(X, -15 * k)]
            rot["seg0"] = [(X, 30 * k)]
            off.y = -0.15 * L * k
        elif kind == "Die":
            e = ease(t / 0.9)
            for i in range(1, n):
                rot[f"seg{i}"] = [(Z, 18 * e * (1 if i % 2 else -1) * 0.6), (X, 3 * e)]
            off.z = -H * 0.5 * e
        return rot, off, {}
    c.anim = anim


# --------------------------------------------------------------------------- Flieger, Geister

def build_flyer(c, p):
    L, W = p["length"], p["width"]
    H = p.get("hover", 1.3)
    col = p["color"]
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, L * 0.1, H), (0, -L * 0.25, H), "root")
    c.bone("tail", (0, L * 0.1, H), (0, L * 0.75, H), "body")
    c.ell("body", (0, -L * 0.05, H), (W * 0.32, L * 0.18, W * 0.3), col, rough=0.3)
    c.ell("body", (0, -L * 0.3, H + 0.02), (W * 0.36, L * 0.1, W * 0.33), col, rough=0.3)
    c.eyes("body", (0, -L * 0.33, H + W * 0.12), W * 0.26, W * 0.17, p.get("eye", (0.2, 0.5, 0.25)), p.get("eye_glow", 0.2), forward=0.0)
    for i in range(5):
        y = L * 0.15 + i * L * 0.12
        c.ell("tail", (0, y, H), (W * 0.15 * (1 - i * 0.1),) * 2 + (W * 0.15,), p.get("abdomen", col) if i % 2 else darker(col, 0.8), rough=0.3)
    wing = p.get("wing_color", (0.75, 0.85, 0.9))
    wings = []
    for k, y in enumerate((-L * 0.12, L * 0.0)):
        for s in (-1, 1):
            nm = f"wing{k}{'l' if s > 0 else 'r'}"
            a = Vector((s * W * 0.2, y, H + W * 0.2))
            b = a + Vector((s * L * p.get("span", 0.6), 0, 0))
            c.bone(nm, a, b, "body")
            c.ell(nm, (a + b) / 2, (L * p.get("span", 0.6) * 0.5, L * 0.08, 0.006), wing, rough=0.15, emit=p.get("wing_glow", 0.0))
            wings.append((nm, k, s))
    for i in range(3):
        for s in (-1, 1):
            a = Vector((s * W * 0.15, -L * 0.1 + i * L * 0.07, H - W * 0.2))
            c.tube("body", a, a + Vector((s * W * 0.3, 0, -L * 0.25)), W * 0.03, W * 0.01, darker(col), seg=5)
    if p.get("stinger"):
        c.tube("tail", (0, L * 0.7, H), (0, L * 0.85, H - 0.03), W * 0.08, 0, (0.1, 0.08, 0.05), seg=6)

    def anim(kind, t):
        rot, off = {}, Vector()
        flap = {"Idle": 6, "Walk": 8, "Run": 10, "Attack": 10, "Die": 6}[kind]
        for nm, k, s in wings:
            rot[nm] = [(Y, s * 35 * wave(t * flap, k * 0.25))]
        if kind != "Die":
            off.z = 0.06 * wave(t * 2)
            rot["tail"] = [(X, 6 * wave(t * 2))]
        if kind in ("Walk", "Run"):
            rot["body"] = [(X, 8 if kind == "Walk" else 14)]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.7))
            rot["body"] = [(X, 25 * k)]
            rot["tail"] = [(X, -45 * k)]
            off.y = -0.3 * L * k
            off.z = -0.3 * k
        elif kind == "Die":
            e = ease(t / 0.8)
            for nm, k, s in wings:
                rot[nm] = [(Y, s * 35 * wave(t * flap, k * 0.25) * (1 - e) - s * 20 * e)]
            rot["root"] = [(Y, 160 * e)]
            off.z = -H * e * 0.9   # fällt zu Boden
        return rot, off, {}
    c.anim = anim


def build_spirit(c, p):
    """Schwebende Gestalt ohne Beine (Geist, Fee, Schatten): Rumpf läuft nach unten spitz aus, Arme, Kopf."""
    S = p["height"]
    col = p["color"]
    hover = p.get("hover", 0.3)
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, 0, hover + S * 0.2), (0, 0, hover + S * 0.7), "root")
    c.bone("head", (0, 0, hover + S * 0.75), (0, 0, hover + S * 0.95), "body")
    emit = p.get("glow", 0.0)
    c.tube("body", (0, 0, hover), (0, 0, hover + S * 0.72), S * 0.02, S * 0.17, col, seg=14, emit=emit)
    c.ell("body", (0, 0, hover + S * 0.62), (S * 0.17, S * 0.12, S * 0.12), col, emit=emit)
    c.ell("head", (0, -S * 0.02, hover + S * 0.83), (S * 0.1, S * 0.11, S * 0.12), p.get("head_color", col), emit=emit)
    c.eyes("head", (0, -S * 0.11, hover + S * 0.85), S * 0.04, S * 0.022, p.get("eye", (0.9, 0.9, 0.5)), p.get("eye_glow", 3.0))
    if p.get("hood"):
        c.ell("head", (0, S * 0.01, hover + S * 0.86), (S * 0.13, S * 0.13, S * 0.14), darker(col, 0.7), emit=emit)
    if p.get("crown"):
        for k in range(5):
            a = 2 * math.pi * k / 5
            base = Vector((math.cos(a) * S * 0.08, math.sin(a) * S * 0.08, hover + S * 0.93))
            c.tube("head", base, base + Vector((0, 0, S * 0.07)), S * 0.02, 0, p["crown"], seg=5)
    arms = []
    for s in (-1, 1):
        nm = "l" if s > 0 else "r"
        sh = Vector((s * S * 0.16, 0, hover + S * 0.66))
        el = sh + Vector((s * S * 0.1, -S * 0.05, -S * 0.2))
        ha = el + Vector((s * S * 0.02, -S * 0.12, -S * 0.12))
        c.bone(f"arm_{nm}", sh, el, "body")
        c.bone(f"fore_{nm}", el, ha, f"arm_{nm}")
        c.limb(f"arm_{nm}", sh, el, S * 0.04, S * 0.032, col)
        c.limb(f"fore_{nm}", el, ha, S * 0.032, S * 0.02, col)
        c.ell(f"fore_{nm}", ha, (S * 0.03,) * 3, p.get("head_color", col), emit=emit)
        arms.append((nm, s))
    if p.get("wings"):
        for s in (-1, 1):
            nm = f"wing_{'l' if s > 0 else 'r'}"
            a = Vector((s * S * 0.05, S * 0.08, hover + S * 0.65))
            c.bone(nm, a, a + Vector((s * S * 0.4, S * 0.1, S * 0.2)), "body")
            c.ell(nm, a + Vector((s * S * 0.2, S * 0.06, S * 0.12)), (S * 0.2, S * 0.01, S * 0.13), p["wings"], rough=0.2, emit=1.0)

    def anim(kind, t):
        rot, off = {}, Vector()
        off.z = S * 0.04 * wave(t * (2 if kind == "Idle" else 3))
        for nm, s in arms:
            rot[f"arm_{nm}"] = [(Y, s * 8 * wave(t, 0.2)), (X, 6 * wave(t * 2))]
        if p.get("wings"):
            for s in (-1, 1):
                rot[f"wing_{'l' if s > 0 else 'r'}"] = [(Z, s * 25 * wave(t * 6))]
        if kind in ("Walk", "Run"):
            rot["body"] = [(X, 10 if kind == "Walk" else 18)]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.7))
            for nm, s in arms:
                rot[f"arm_{nm}"] = [(X, -70 * k), (Y, -s * 20 * k)]
            rot["body"] = [(X, 15 * k)]
            off.y = -0.2 * S * k
        elif kind == "Die":
            e = ease(t / 1.0)
            rot["body"] = [(X, -40 * e)]
            off.z = -hover * e - S * 0.1 * e
        return rot, off, ({"body": Vector((1, 1, 1 - 0.5 * ease(t)))} if kind == "Die" else {})
    c.anim = anim


# --------------------------------------------------------------------------- Pflanzen und Schleim

def build_tree(c, p):
    """Baumwesen (Ent): Stamm auf zwei Wurzelbeinen, Astarme, Laubkrone, Gesicht in der Rinde."""
    S = p["height"]
    bark, leaf = p["color"], p.get("leaf", (0.2, 0.42, 0.15))
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, 0, S * 0.3), (0, 0, S * 0.8), "root")
    c.bone("crown", (0, 0, S * 0.8), (0, 0, S * 1.0), "body")
    c.tube("body", (0, 0, S * 0.28), (0, 0, S * 0.85), S * 0.17, S * 0.12, bark, seg=12, rough=0.95)
    c.ell("body", (0, 0, S * 0.32), (S * 0.19, S * 0.17, S * 0.1), bark, rough=0.95)
    for k, (dx, dz, r) in enumerate(((0, 0.95, 0.28), (0.18, 0.88, 0.2), (-0.18, 0.9, 0.22), (0.05, 1.08, 0.2), (-0.1, 0.82, 0.18))):
        c.ell("crown", (dx * S, (k % 2 - 0.5) * 0.1 * S, dz * S), (r * S,) * 3, leaf if k % 2 == 0 else lighter(leaf, 1.15), seg=12, rough=0.9,
              emit=p.get("leaf_glow", 0.0))
    # Gesicht
    c.ell("body", (0, -S * 0.13, S * 0.68), (S * 0.07, S * 0.03, S * 0.035), (0.05, 0.04, 0.02))
    c.eyes("body", (0, -S * 0.14, S * 0.72), S * 0.05, S * 0.025, p.get("eye", (0.9, 0.7, 0.2)), p.get("eye_glow", 1.5))
    legs, arms = [], []
    for s in (-1, 1):
        nm = "l" if s > 0 else "r"
        hip = Vector((s * S * 0.09, 0, S * 0.3))
        knee = Vector((s * S * 0.14, -S * 0.02, S * 0.15))
        foot = Vector((s * S * 0.16, 0, 0.02))
        c.bone(f"thigh_{nm}", hip, knee, "body")
        c.bone(f"shin_{nm}", knee, foot, f"thigh_{nm}")
        c.limb(f"thigh_{nm}", hip, knee, S * 0.07, S * 0.06, bark)
        c.limb(f"shin_{nm}", knee, foot, S * 0.06, S * 0.05, bark)
        for k in range(3):
            a = math.radians(-60 + k * 60)
            c.tube(f"shin_{nm}", foot, foot + Vector((math.sin(a) * S * 0.12, -math.cos(a) * S * 0.12, -0.01)), S * 0.035, S * 0.01, bark, seg=6)
        sh = Vector((s * S * 0.13, 0, S * 0.72))
        el = sh + Vector((s * S * 0.18, -S * 0.03, -S * 0.12))
        ha = el + Vector((s * S * 0.06, -S * 0.08, -S * 0.2))
        c.bone(f"arm_{nm}", sh, el, "body")
        c.bone(f"fore_{nm}", el, ha, f"arm_{nm}")
        c.limb(f"arm_{nm}", sh, el, S * 0.055, S * 0.045, bark)
        c.limb(f"fore_{nm}", el, ha, S * 0.045, S * 0.02, bark)
        for k in range(3):
            c.tube(f"fore_{nm}", ha, ha + Vector((s * S * 0.03 * (k - 1), -S * 0.05, -S * 0.08)), S * 0.015, S * 0.003, bark, seg=5)
        c.ell(f"arm_{nm}", el + Vector((0, 0, S * 0.06)), (S * 0.08,) * 3, leaf, seg=10)
        legs.append((nm, s))
        arms.append((nm, s))
    _biped_anim(c, legs, arms, S, heavy=True)


def build_flytrap(c, p):
    S = p["height"]
    col = p["color"]
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    prev = "root"
    pts = [Vector((0, 0, 0)), Vector((0, 0.02, S * 0.3)), Vector((0, -0.02, S * 0.55)), Vector((0, -S * 0.08, S * 0.75))]
    for i in range(3):
        c.bone(f"stem{i}", pts[i], pts[i + 1], prev)
        c.limb(f"stem{i}", pts[i], pts[i + 1], S * (0.06 - i * 0.01), S * (0.05 - i * 0.01), darker(col, 0.8))
        prev = f"stem{i}"
    top = pts[3]
    c.bone("upper", top, top + Vector((0, -S * 0.3, S * 0.05)), prev)
    c.bone("lower", top, top + Vector((0, -S * 0.3, -S * 0.05)), prev)
    c.ell("upper", top + Vector((0, -S * 0.15, S * 0.04)), (S * 0.16, S * 0.17, S * 0.06), col, rough=0.5)
    c.ell("lower", top + Vector((0, -S * 0.15, -S * 0.04)), (S * 0.16, S * 0.17, S * 0.06), col, rough=0.5)
    inner = p.get("inner", (0.75, 0.15, 0.2))
    c.ell("upper", top + Vector((0, -S * 0.15, S * 0.0)), (S * 0.13, S * 0.14, S * 0.02), inner)
    c.ell("lower", top + Vector((0, -S * 0.15, -S * 0.0)), (S * 0.13, S * 0.14, S * 0.02), inner)
    for k in range(7):
        a = math.radians(-70 + k * 23)
        for jaw, dz in (("upper", -1), ("lower", 1)):
            base = top + Vector((math.sin(a) * S * 0.16, -S * 0.15 - math.cos(a) * S * 0.16, -dz * S * 0.02))
            c.tube(jaw, base, base + Vector((0, 0, dz * S * 0.07)), S * 0.012, 0, (0.95, 0.95, 0.8), seg=5)
    for k in range(5):
        a = 2 * math.pi * k / 5
        c.ell("root", (math.cos(a) * S * 0.18, math.sin(a) * S * 0.18, S * 0.03), (S * 0.17, S * 0.06, S * 0.02), lighter(col, 1.1), rot=(0, 0, math.degrees(a)))

    def anim(kind, t):
        rot, off = {}, Vector()
        sway = {"Idle": 5, "Walk": 8, "Run": 10}.get(kind, 0)
        for i in range(3):
            rot[f"stem{i}"] = [(Z, sway * wave(t, i * 0.1)), (X, sway * 0.5 * wave(t * 2, i * 0.1))]
        rot["upper"] = [(X, -8 * max(0, wave(t)))]
        if kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.6))
            rot["stem1"] = [(X, -25 * k)]
            rot["stem2"] = [(X, -20 * k)]
            o = 1 - k if t > 0.3 else 1
            rot["upper"] = [(X, -35 * o)]
            rot["lower"] = [(X, 25 * o)]
        elif kind == "Die":
            e = ease(t)
            rot["stem0"] = [(X, 40 * e)]
            rot["stem1"] = [(X, 40 * e)]
            rot["stem2"] = [(X, 30 * e)]
        return rot, off, {}
    c.anim = anim


def build_blob(c, p):
    S = p["height"]
    col = p["color"]
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, 0, 0), (0, 0, S), "root")
    c.ell("body", (0, 0, S * 0.4), (S * 0.6, S * 0.6, S * 0.42), col, seg=20, rough=0.1, emit=p.get("glow", 0.1))
    c.ell("body", (0, 0, S * 0.65), (S * 0.35, S * 0.35, S * 0.3), lighter(col, 1.2), seg=16, rough=0.1, emit=p.get("glow", 0.1))
    for k in range(5):
        a = k * 2.1
        c.ell("body", (math.cos(a) * S * 0.25, math.sin(a) * S * 0.25, S * 0.35 + k * 0.03 * S), (S * 0.07,) * 3, darker(col, 0.5), seg=8)
    c.eyes("body", (0, -S * 0.5, S * 0.6), S * 0.15, S * 0.06, (0.05, 0.05, 0.05))
    for k in range(p.get("caps", 0)):
        a = k * 2.4
        base = Vector((math.cos(a) * S * 0.3, math.sin(a) * S * 0.3 + S * 0.1, S * 0.6))
        c.tube("body", base, base + Vector((0, 0, S * 0.3)), S * 0.06, S * 0.05, (0.85, 0.8, 0.7))
        c.ell("body", base + Vector((0, 0, S * 0.32)), (S * 0.2, S * 0.2, S * 0.09), p.get("cap_color", (0.6, 0.3, 0.2)), rough=0.6)

    def anim(kind, t):
        rot, off, scale = {}, Vector(), {}
        f = {"Idle": 1, "Walk": 2, "Run": 3, "Attack": 1, "Die": 1}[kind]
        sq = 0.12 * wave(t * f)
        scale["body"] = Vector((1 + sq * 0.5, 1 + sq * 0.5, 1 - sq))
        if kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.7))
            scale["body"] = Vector((1 - 0.2 * k, 1 + 0.5 * k, 1 + 0.3 * k))
            off.y = -0.3 * S * k
        elif kind == "Die":
            e = ease(t)
            scale["body"] = Vector((1 + 0.6 * e, 1 + 0.6 * e, max(0.05, 1 - 0.9 * e)))
        return rot, off, scale
    c.anim = anim


# --------------------------------------------------------------------------- Zweibeiner (Orks, Trolle, Echsen …)

def _biped_anim(c, legs, arms, S, heavy=False, tail=0, weapon_arm="r"):
    slow = 0.75 if heavy else 1.0

    def anim(kind, t):
        rot, off = {}, Vector()
        if kind in ("Walk", "Run"):
            amp = (28 if kind == "Walk" else 42) * slow
            for nm, s in legs:
                ph = 0.0 if s > 0 else 0.5
                rot[f"thigh_{nm}"] = [(X, -amp * wave(t, ph))]
                rot[f"shin_{nm}"] = [(X, amp * 1.1 * max(0.0, wave(t, ph + 0.3)))]
            for nm, s in arms:
                ph = 0.5 if s > 0 else 0.0
                rot[f"arm_{nm}"] = [(X, -amp * 0.8 * wave(t, ph))]
                rot[f"fore_{nm}"] = [(X, -15 - 10 * max(0, wave(t, ph)))]
            rot["body"] = [(Z, 5 * wave(t)), (X, 4 if kind == "Walk" else 10)]
            off.z = abs(wave(t)) * S * (0.02 if kind == "Walk" else 0.04)
        elif kind == "Idle":
            rot["body"] = [(X, 1.5 * wave(t * 2)), (Z, 2 * wave(t))]
            for nm, s in arms:
                rot[f"arm_{nm}"] = [(Y, s * 3 * wave(t * 2))]
                rot[f"fore_{nm}"] = [(X, -10)]
            if "head" in [b[0] for b in c.bones]:
                rot["head"] = [(Z, 15 * wave(t))]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.75))
            up = ease(t / 0.35) * (1 - ease((t - 0.35) / 0.3))
            down = ease((t - 0.3) / 0.25) * (1 - ease((t - 0.7) / 0.3))
            for nm, s in arms:
                if nm == weapon_arm:
                    rot[f"arm_{nm}"] = [(X, -150 * up - 60 * down)]
                    rot[f"fore_{nm}"] = [(X, -40 * up)]
                else:
                    rot[f"arm_{nm}"] = [(X, -30 * k)]
            rot["body"] = [(Z, (20 if weapon_arm == "r" else -20) * (up - down)), (X, 12 * down)]
            off.y = -0.08 * S * k
        elif kind == "Die":
            e = ease(t / 0.8)
            rot["body"] = [(X, -15 * e)]
            rot["root"] = [(X, -85 * e)]
            off.z = S * 0.05 * e
            for nm, s in arms:
                rot[f"arm_{nm}"] = [(Y, s * 40 * e)]
            for nm, s in legs:
                rot[f"thigh_{nm}"] = [(X, -15 * e)]
        for i in range(tail):
            rot.setdefault(f"tail{i}", []).append((Z, 10 * wave(t, i * 0.15)))
        return rot, off, {}
    c.anim = anim


def build_biped(c, p):
    S = p["height"]
    col = p["color"]
    skin, cloth, acc = p.get("skin", col), p.get("cloth", darker(col, 0.5)), p.get("accent", (0.35, 0.3, 0.25))
    bulk = p.get("bulk", 1.0)
    W = S * 0.2 * bulk
    hip_h = S * p.get("legs", 0.48)
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, 0, hip_h), (0, 0, S * 0.78), "root")
    c.bone("head", (0, 0, S * 0.8), (0, 0, S), "body")
    hunch = p.get("hunch", 0.0) * S
    # Rumpf
    c.ell("body", (0, 0, hip_h + S * 0.04), (W * 0.62, W * 0.42, S * 0.08), cloth)
    c.ell("body", (0, -hunch * 0.3, hip_h + S * 0.17), (W * 0.62, W * 0.45, S * 0.14), skin)
    c.ell("body", (0, -hunch * 0.6, S * 0.68), (W * 0.82, W * 0.5, S * 0.12), skin)
    if p.get("armor"):
        c.ell("body", (0, -hunch * 0.5, S * 0.6), (W * 0.78, W * 0.55, S * 0.16), p["armor"], rough=0.35)
    if p.get("belly"):
        c.ell("body", (0, -W * 0.2, hip_h + S * 0.18), (W * 0.55, W * 0.35, S * 0.11), p["belly"])
    # Kopf
    hc = Vector((0, -hunch, S * 0.88))
    hs = S * p.get("head", 0.075)
    c.tube("body", (0, -hunch * 0.8, S * 0.74), hc + Vector((0, 0, -hs * 0.6)), W * 0.22, W * 0.2, skin)
    c.ell("head", hc, (hs * 1.0, hs * 1.05, hs * 1.15), p.get("head_color", skin))
    snout = p.get("snout", 0.0)
    if snout:
        c.ell("head", hc + Vector((0, -hs * (0.8 + snout * 0.6), -hs * 0.3)), (hs * 0.55, hs * (0.6 + snout * 0.5), hs * 0.45), p.get("head_color", skin))
    c.eyes("head", hc + Vector((0, -hs * 0.85, hs * 0.15)), hs * 0.4, hs * 0.16, p.get("eye", (0.9, 0.85, 0.2)), p.get("eye_glow", 0.6))
    if p.get("beak"):
        c.tube("head", hc + Vector((0, -hs * 0.7, -hs * 0.1)), hc + Vector((0, -hs * 2.0, -hs * 0.5)), hs * 0.4, hs * 0.02, p["beak"], seg=10)
    if p.get("crown"):
        for k in range(6):
            a = 2 * math.pi * k / 6
            base = hc + Vector((math.cos(a) * hs * 0.7, math.sin(a) * hs * 0.7, hs * 0.9))
            c.tube("head", base, base + Vector((0, 0, hs * 0.5)), hs * 0.12, 0, p["crown"], seg=5, rough=0.3)
    if p.get("tusks"):
        for s in (-1, 1):
            c.tube("head", hc + Vector((s * hs * 0.4, -hs * 0.8, -hs * 0.5)), hc + Vector((s * hs * 0.5, -hs * 1.0, hs * 0.0)), hs * 0.12, 0, (0.95, 0.9, 0.75), seg=6)
    if p.get("ears"):
        for s in (-1, 1):
            c.tube("head", hc + Vector((s * hs * 0.9, 0, hs * 0.1)), hc + Vector((s * hs * 1.8, hs * 0.4, hs * 0.6)), hs * 0.25, 0, p.get("head_color", skin), seg=6)
    if p.get("horns"):
        for s in (-1, 1):
            base = hc + Vector((s * hs * 0.6, 0, hs * 0.8))
            c.tube("head", base, base + Vector((s * hs * 0.9, hs * 0.3, hs * 1.0)), hs * 0.22, 0, p["horns"], seg=8)
    if p.get("hair"):
        c.ell("head", hc + Vector((0, hs * 0.15, hs * 0.45)), (hs * 1.05, hs * 1.0, hs * 0.75), p["hair"])
    if p.get("hood"):
        c.ell("head", hc + Vector((0, hs * 0.1, hs * 0.2)), (hs * 1.3, hs * 1.3, hs * 1.4), p["hood"])
    if p.get("crest"):
        for k in range(5):
            c.tube("head", hc + Vector((0, -hs * 0.6 + k * hs * 0.35, hs * 0.9)), hc + Vector((0, -hs * 0.4 + k * hs * 0.4, hs * 1.6)), hs * 0.12, 0, p["crest"], seg=5)
    if p.get("bandages"):
        # Binden der Mumie: schräge, flache Ringe um Rumpf und Glieder
        band = p["bandages"]
        for k in range(9):
            z = hip_h + S * 0.04 + k * S * 0.045
            c.ell("body", (0, -hunch * 0.5 * k / 9, z), (W * 0.86 - abs(k - 5) * W * 0.04, W * 0.55, S * 0.012), band if k % 2 else darker(band, 0.9),
                  rot=(0, (k % 3 - 1) * 8, 0))
    if p.get("robe"):
        c.tube("body", (0, 0, hip_h + S * 0.15), (0, 0, S * 0.03), W * 0.6, W * 0.95, p["robe"], seg=14)
    # Arme
    legs, arms = [], []
    arm_len = p.get("arms", 1.0)
    for s in (-1, 1):
        nm = "l" if s > 0 else "r"
        sh = Vector((s * W * 0.95, -hunch * 0.6, S * 0.74))
        el = sh + Vector((s * W * 0.15, 0, -S * 0.2 * arm_len))
        ha = el + Vector((0, -S * 0.04, -S * 0.19 * arm_len))
        c.bone(f"arm_{nm}", sh, el, "body")
        c.bone(f"fore_{nm}", el, ha, f"arm_{nm}")
        r = W * 0.24 * p.get("arm_r", 1.0)
        c.limb(f"arm_{nm}", sh, el, r * 1.1, r * 0.9, skin)
        c.limb(f"fore_{nm}", el, ha, r * 0.9, r * 0.75, skin)
        c.ell(f"fore_{nm}", ha + Vector((0, 0, -r * 0.6)), (r * 0.8, r * 0.6, r * 1.0), p.get("hand", skin))
        if p.get("claws"):
            for k in range(3):
                c.tube(f"fore_{nm}", ha + Vector(((k - 1) * r * 0.4, -r * 0.3, -r * 1.2)), ha + Vector(((k - 1) * r * 0.4, -r * 0.9, -r * 2.0)), r * 0.15, 0, (0.9, 0.88, 0.8), seg=5)
        if p.get("shoulder"):
            c.ell(f"arm_{nm}", sh + Vector((0, 0, r * 0.4)), (r * 1.7, r * 1.6, r * 1.1), p["shoulder"], rough=0.35)
        arms.append((nm, s))
    weapon = p.get("weapon")
    if weapon:
        ha = Vector((-W * 1.1, -S * 0.04, S * 0.74 - S * 0.39 * arm_len))
        if weapon == "club":
            c.tube("fore_r", ha + Vector((0, 0, S * 0.04)), ha + Vector((0, -S * 0.35, S * 0.15)), S * 0.025, S * 0.06, (0.4, 0.27, 0.15), seg=8)
        elif weapon == "sword":
            c.tube("fore_r", ha + Vector((0, 0.02, 0)), ha + Vector((0, -S * 0.05, 0.02)), S * 0.02, S * 0.02, (0.3, 0.2, 0.1), seg=6)
            c.ell("fore_r", ha + Vector((0, -S * 0.06, 0.02)), (S * 0.07, S * 0.012, S * 0.012), (0.6, 0.5, 0.2), rough=0.3)
            c.tube("fore_r", ha + Vector((0, -S * 0.07, 0.03)), ha + Vector((0, -S * 0.42, S * 0.15)), S * 0.018, S * 0.004, (0.78, 0.78, 0.82), seg=6, rough=0.2)
        elif weapon == "staff":
            c.tube("fore_r", ha + Vector((0, 0, -S * 0.45)), ha + Vector((0, 0, S * 0.55)), S * 0.015, S * 0.015, (0.35, 0.25, 0.15), seg=6)
            c.ell("fore_r", ha + Vector((0, 0, S * 0.58)), (S * 0.04,) * 3, p.get("orb", (0.4, 0.6, 1.0)), emit=3.0)
        elif weapon == "spear":
            c.tube("fore_r", ha + Vector((0, S * 0.3, -S * 0.1)), ha + Vector((0, -S * 0.5, S * 0.18)), S * 0.012, S * 0.012, (0.4, 0.3, 0.2), seg=6)
            c.tube("fore_r", ha + Vector((0, -S * 0.5, S * 0.18)), ha + Vector((0, -S * 0.62, S * 0.22)), S * 0.025, 0, (0.75, 0.75, 0.78), seg=6)
    shield = p.get("shield")
    if shield:
        ha = Vector((W * 1.1, -S * 0.08, S * 0.74 - S * 0.39 * arm_len))
        c.ell("fore_l", ha + Vector((W * 0.25, 0, S * 0.08)), (S * 0.02, S * 0.15, S * 0.17), shield, rough=0.4)
    # Beine
    for s in (-1, 1):
        nm = "l" if s > 0 else "r"
        hip = Vector((s * W * 0.38, 0, hip_h))
        knee = Vector((s * W * 0.42, -S * 0.02 + p.get("digitigrade", 0) * S * 0.08, hip_h * 0.5))
        foot = Vector((s * W * 0.42, 0, S * 0.03))
        c.bone(f"thigh_{nm}", hip, knee, "body")
        c.bone(f"shin_{nm}", knee, foot, f"thigh_{nm}")
        r = W * 0.3 * p.get("leg_r", 1.0)
        c.limb(f"thigh_{nm}", hip, knee, r * 1.1, r * 0.85, p.get("leg_color", skin if not p.get("pants") else p["pants"]))
        c.limb(f"shin_{nm}", knee, foot, r * 0.85, r * 0.7, p.get("leg_color", skin if not p.get("pants") else p["pants"]))
        c.ell(f"shin_{nm}", foot + Vector((0, -r * 0.6, -S * 0.005)), (r * 0.8, r * (1.6 + p.get("webbed", 0)), r * 0.45), p.get("foot", acc))
        legs.append((nm, s))
    tail = 0
    if p.get("tail"):
        prev, start = "body", Vector((0, W * 0.4, hip_h))
        tail = 4
        for i in range(tail):
            end = start + Vector((0, S * p["tail"] / tail, -S * 0.08))
            c.bone(f"tail{i}", start, end, prev)
            r = W * 0.28 * (1 - i * 0.22)
            c.limb(f"tail{i}", start, end, r, r * 0.75, skin)
            prev, start = f"tail{i}", end
    _biped_anim(c, legs, arms, S, heavy=p.get("heavy", False), tail=tail)


def build_mollusk(c, p):
    """Riesenschnecke mit Spiralgehäuse und einer kräftigen Schere."""
    L, H = p["length"], p["height"]
    col, shell = p["color"], p.get("shell", (0.55, 0.45, 0.35))
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    c.bone("body", (0, L * 0.4, H * 0.2), (0, -L * 0.4, H * 0.2), "root")
    c.bone("head", (0, -L * 0.35, H * 0.25), (0, -L * 0.6, H * 0.55), "body")
    c.bone("shell", (0, L * 0.05, H * 0.4), (0, L * 0.05, H), "body")
    c.ell("body", (0, 0, H * 0.15), (L * 0.22, L * 0.5, H * 0.16), col, rough=0.25)
    c.ell("head", (0, -L * 0.48, H * 0.42), (L * 0.13, L * 0.14, H * 0.2), col, rough=0.25)
    for s in (-1, 1):
        c.tube("head", (s * L * 0.05, -L * 0.52, H * 0.55), (s * L * 0.1, -L * 0.6, H * 0.85), L * 0.025, L * 0.015, col, seg=6)
        c.ell("head", (s * L * 0.1, -L * 0.6, H * 0.87), (L * 0.035,) * 3, (0.05, 0.05, 0.05), rough=0.2)
    for k in range(5):
        r = H * (0.42 - k * 0.07)
        c.ell("shell", (0, L * (0.12 - k * 0.04), H * (0.55 + k * 0.1)), (r * 0.75, r, r), shell if k % 2 == 0 else darker(shell, 0.8), rough=0.4)
    a = Vector((L * 0.15, -L * 0.35, H * 0.3))
    b = a + Vector((L * 0.12, -L * 0.25, H * 0.1))
    c.bone("claw", a, b, "body")
    c.limb("claw", a, b, L * 0.05, L * 0.06, darker(col, 0.8))
    c.ell("claw", b, (L * 0.09, L * 0.13, L * 0.06), shell, rough=0.4)
    c.tube("claw", b + Vector((L * 0.03, 0, 0)), b + Vector((L * 0.04, -L * 0.16, 0)), L * 0.04, 0, darker(shell), seg=6)

    def anim(kind, t):
        rot, off = {}, Vector()
        rot["head"] = [(Z, 8 * wave(t)), (X, 4 * wave(t * 2))]
        rot["shell"] = [(Y, 3 * wave(t))]
        if kind in ("Walk", "Run"):
            off.y = 0.0
            rot["body"] = [(Z, 3 * wave(t))]
        elif kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.6))
            rot["claw"] = [(X, -50 * k), (Z, 25 * k)]
            off.y = -0.1 * L * k
        elif kind == "Die":
            e = ease(t)
            rot["head"] = [(X, 50 * e)]
            rot["shell"] = [(Y, 25 * e)]
            off.z = -H * 0.1 * e
        return rot, off, {}
    c.anim = anim


def build_bramble(c, p):
    """Dornengestrüpp: Büschel dorniger Ranken, die sich wiegen und zuschlagen."""
    S = p["height"]
    col = p["color"]
    c.bone("root", (0, 0, 0), (0, 0, 0.1))
    stems = []
    for k in range(7):
        a = 2 * math.pi * k / 7
        base = Vector((math.cos(a) * S * 0.12, math.sin(a) * S * 0.12, 0))
        tip = base + Vector((math.cos(a) * S * 0.4, math.sin(a) * S * 0.4, S * (0.6 + 0.4 * (k % 3) / 2)))
        nm = c.bone(f"stem{k}", base, tip, "root")
        c.tube(nm, base, tip, S * 0.035, S * 0.008, col, seg=6, rough=0.9)
        for j in range(5):
            q = base.lerp(tip, 0.15 + j * 0.17)
            d = Vector((math.cos(a + j), math.sin(a + j), 0.3)).normalized()
            c.tube(nm, q, q + d * S * 0.08, S * 0.012, 0, (0.75, 0.7, 0.55), seg=4)
        c.ell(nm, base.lerp(tip, 0.6), (S * 0.08, S * 0.08, S * 0.06), p.get("leaf", (0.18, 0.32, 0.12)), seg=8, rough=0.9)
        stems.append((nm, a))

    def anim(kind, t):
        rot, off = {}, Vector()
        for nm, a in stems:
            rot[nm] = [(Z, 4 * wave(t, a / 6)), (X, 3 * wave(t * 2, a / 5))]
        if kind == "Attack":
            k = math.sin(math.pi * min(1.0, t / 0.6))
            for nm, a in stems:
                rot[nm] = [(X, -35 * k * max(0, -math.sin(a + math.pi / 2)))]
        elif kind == "Die":
            for nm, a in stems:
                rot[nm] = [(Vector((-math.sin(a), math.cos(a), 0))[:], 50 * ease(t))]
        return rot, off, {}
    c.anim = anim


def add_wings(c, p):
    """Flügel an einem Knochen (Drachenfliege als Echse, Dunkler Engel); schlagen in allen Animationen."""
    w = p["wings"]
    bone = w.get("bone", "body")
    at, span, color = Vector(w["at"]), w["span"], w.get("color", (0.75, 0.85, 0.9))
    names = []
    for s in (-1, 1):
        nm = f"wing_{'l' if s > 0 else 'r'}"
        a = at + Vector((s * 0.02, 0, 0))
        b = a + Vector((s * span, span * 0.25, span * 0.3))
        c.bone(nm, a, b, bone)
        mid = (a + b) / 2
        c.ell(nm, mid, (span * 0.5, span * w.get("depth", 0.22), 0.01), color, rot=(0, -s * 30, 0),
              rough=w.get("rough", 0.25), emit=w.get("glow", 0.0))
        names.append((nm, s))
    inner = c.anim
    rate = w.get("rate", 4)

    def anim(kind, t):
        rot, off, scale = inner(kind, t)
        f = rate * (2 if kind in ("Run", "Attack") else 1)
        for nm, s in names:
            ang = 30 * wave(t * f) if kind != "Die" else 40 * ease(t)
            rot[nm] = [(Y, s * ang)]
        return rot, off, scale
    c.anim = anim


def add_extra_arms(c, p):
    """Zusätzliche, nicht zusammenpassende Glieder (Drescher), die unabhängig peitschen."""
    arms = []
    for k, (side, z, length, col) in enumerate(p["extra_arms"]):
        a = Vector((side * 0.3, 0.05, z))
        b = a + Vector((side * length * 0.5, -length * 0.3, -length * 0.6))
        nm = c.bone(f"extra{k}", a, b, "body")
        c.limb(nm, a, b, 0.07, 0.04, col)
        c.ell(nm, b, (0.08, 0.06, 0.1), darker(col))
        arms.append((nm, side, k))
    inner = c.anim

    def anim(kind, t):
        rot, off, scale = inner(kind, t)
        amp = 60 if kind == "Attack" else 15
        for nm, side, k in arms:
            rot[nm] = [(X, -amp * wave(t * (2 if kind == "Attack" else 1), k * 0.3)), (Y, side * 10 * wave(t, k * 0.2))]
        return rot, off, scale
    c.anim = anim


BUILDERS = {
    "quadruped": build_quadruped, "insect": build_insect, "spider": build_spider, "scorpion": build_scorpion,
    "worm": build_worm, "centipede": lambda c, p: build_worm(c, p, centipede=True), "flyer": build_flyer,
    "spirit": build_spirit, "tree": build_tree, "flytrap": build_flytrap, "blob": build_blob, "biped": build_biped,
    "mollusk": build_mollusk, "bramble": build_bramble,
}

# --------------------------------------------------------------------------- Arten (Maße in Metern)

from creatures import CREATURES  # noqa: E402  (Datei daneben, eigene Liste)


# --------------------------------------------------------------------------- Blender-Teil

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_material(name, color, rough, emit):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = rough
    if emit > 0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
        bsdf.inputs["Emission Strength"].default_value = emit
    return m


def realize(c):
    arm = bpy.data.armatures.new(c.name + "Rig")
    rig = bpy.data.objects.new(c.name + "Rig", arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for name, head, tail, parent in c.bones:
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        if parent:
            eb.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')

    mesh = bpy.data.meshes.new(c.name)
    c.bm.to_mesh(mesh)
    c.bm.free()
    obj = bpy.data.objects.new(c.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for name in sorted(c.group, key=c.group.get):
        obj.vertex_groups.new(name=name)
    for mname, color, rough, emit in c.materials:
        mesh.materials.append(make_material(mname, color, rough, emit))
    obj.parent = rig
    mod = obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig
    return rig, obj


def animate(c, rig):
    scene = bpy.context.scene
    scene.render.fps = FPS
    rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    rig.animation_data_create()
    for kind, seconds in c.clips:
        action = bpy.data.actions.new(kind)
        action.use_fake_user = True
        rig.animation_data.action = action
        frames = round(seconds * FPS)
        for f in range(frames + 1):
            rots, off, scales = c.anim(kind, f / frames)
            for pb in rig.pose.bones:
                q = Quaternion()
                for axis, deg in rots.get(pb.name, []):
                    a = (rest[pb.name].inverted() @ Vector(axis)).normalized()
                    q = q @ Quaternion(a, math.radians(deg))
                pb.rotation_quaternion = q
                pb.keyframe_insert("rotation_quaternion", frame=f)
                pb.scale = scales.get(pb.name, Vector((1, 1, 1)))
                pb.keyframe_insert("scale", frame=f)
            root = rig.pose.bones["root"]
            root.location = rest["root"].inverted() @ off
            root.keyframe_insert("location", frame=f)
        track = rig.animation_data.nla_tracks.new()
        track.name = kind
        track.strips.new(kind, 1, action)
        rig.animation_data.action = None


def render_preview(obj, rig, path, kind="Walk", frame=6):
    scene = bpy.context.scene
    for track in rig.animation_data.nla_tracks:
        track.mute = True
    rig.animation_data.action = bpy.data.actions[kind]
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    pts = [obj.matrix_world @ Vector(cn) for cn in obj.bound_box]
    size = max((max(p[i] for p in pts) - min(p[i] for p in pts)) for i in range(3))
    center = sum(pts, Vector()) / 8
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    cam.location = center + Vector((size * 1.3, -size * 1.6, size * 0.9))
    cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3
    sun.rotation_euler = (math.radians(40), math.radians(10), math.radians(30))
    scene.collection.objects.link(sun)
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.6, 1)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = scene.render.resolution_y = 400
    scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)


def build(key, out_dir, preview_dir=None):
    spec = CREATURES[key]
    reset_scene()
    c = Creature(key.capitalize())
    BUILDERS[spec["body"]](c, spec)
    if isinstance(spec.get("wings"), dict):
        add_wings(c, spec)
    if "extra_arms" in spec:
        add_extra_arms(c, spec)
    rig, obj = realize(c)
    animate(c, rig)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    obj.select_set(True)
    out = os.path.join(out_dir, f"{key}.glb")
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(out), export_format='GLB', use_selection=True,
                              export_apply=True, export_yup=True, export_animations=True,
                              export_animation_mode='ACTIONS')
    print(f"Exportiert: {out}")
    if preview_dir:
        os.makedirs(preview_dir, exist_ok=True)
        render_preview(obj, rig, os.path.join(preview_dir, f"{key}.png"))


def main(argv):
    preview = None
    if "--preview" in argv:
        i = argv.index("--preview")
        preview = argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
    out_dir = argv[0]
    keys = argv[1:] or list(CREATURES)
    for key in keys:
        build(key, out_dir, preview)


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
