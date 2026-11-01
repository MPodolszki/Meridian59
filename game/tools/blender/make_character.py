"""Erzeugt Spielfiguren mit MPFB2 (MakeHuman für Blender) und exportiert sie für Godot.

Presets: dwarf (Spielerrasse Zwerg), zombie, skeleton (Monster vom Friedhof in Tos).

Voraussetzung: Blender 5.x mit Erweiterung „MPFB“ (extensions.blender.org, ID `mpfb`) und dem
MakeHuman-System-Asset-Paket (CC0, makehuman_system_assets_cc0.zip, in MPFB über „Load pack“ installiert).
Aufruf im Hintergrund (im Repo-Root):

    blender -b --python game/tools/blender/make_character.py -- \
        game/client/models/dwarf.glb [vorschau.png] [--preset dwarf|zombie|skeleton]

Ergebnis: Mesh mit Skelett „game_engine“ und Animationen Idle, Walk, Run, Attack, Die.
Die mit MPFB erzeugten Körper sind CC0 (MakeHuman-Lizenz für Ausgaben), also eigene Assets.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.targetservice import TargetService
from bl_ext.blender_org.mpfb.services.locationservice import LocationService

TARGET_HEIGHT_M = 1.35

# Makro-Werte 0..1 (MakeHuman): männlich, mittleres Alter, sehr muskulös und schwer, kleinste Größe,
# wenig „ideale“ Proportionen (gedrungen).
MACRO = {
    "gender": 1.0,
    "age": 0.78,
    "muscle": 0.95,
    "weight": 0.85,
    "proportions": 0.15,
    "height": 0.0,
    "cupsize": 0.5,
    "firmness": 0.5,
    "race": {"caucasian": 0.8, "african": 0.1, "asian": 0.1},
}

# Zwergen-Proportionen: (Ordner, Target, Gewicht). Seitenpaare l-/r- werden automatisch ergänzt.
TARGETS = [
    ("legs", "upperlegs-height-decr", 1.0),
    ("legs", "lowerlegs-height-decr", 1.0),
    ("torso", "torso-scale-vert-incr", 0.4),          # langer Rumpf
    ("legs", "{s}-upperleg-muscle-incr", 0.5),
    ("legs", "{s}-lowerleg-muscle-incr", 0.5),
    ("torso", "torso-scale-horiz-incr", 0.6),
    ("torso", "torso-scale-depth-incr", 0.6),
    ("torso", "measure-shoulder-dist-incr", 0.8),
    ("torso", "torso-muscle-pectoral-incr", 0.5),
    ("stomach", "stomach-pregnant-incr", 0.25),      # leichter Bauch
    ("neck", "neck-scale-vert-decr", 0.7),            # kurzer Hals
    ("neck", "neck-scale-horiz-incr", 0.6),
    ("head", "head-scale-horiz-incr", 0.3),
    ("head", "head-age-incr", 0.7),
    ("arms", "{s}-upperarm-muscle-incr", 0.7),
    ("arms", "{s}-lowerarm-muscle-incr", 0.6),
    ("arms", "{s}-upperarm-shoulder-muscle-incr", 0.6),
    ("hands", "{s}-hand-scale-incr", 0.6),
    ("hands", "{s}-hand-fingers-diameter-incr", 0.5),
    ("feet", "{s}-foot-scale-incr", 0.5),
]


# Ausstattung aus dem MakeHuman-System-Asset-Paket (alles CC0): (Typ, Unterordner, Name)
ASSETS = [
    ("Eyes", "eyes", "high-poly"),
    ("Eyebrows", "eyebrows", "eyebrow010"),
    ("Eyelashes", "eyelashes", "eyelashes02"),
    ("Hair", "hair", "short02"),
    ("Clothes", "clothes", "male_worksuit01"),
    ("Clothes", "clothes", "shoes06"),
]
SKIN = ("skins", "middleage_caucasian_male")


BEARD = True
STYLE = "dwarf"          # Animationsstil: dwarf, zombie, skeleton

# Abweichungen je Preset von den Zwergen-Werten oben.
PRESETS = {
    "dwarf": {},
    "zombie": {
        "TARGET_HEIGHT_M": 1.72,
        "MACRO": {"gender": 1.0, "age": 0.55, "muscle": 0.25, "weight": 0.2, "proportions": 0.4,
                  "height": 0.6, "cupsize": 0.5, "firmness": 0.5,
                  "race": {"caucasian": 0.8, "african": 0.1, "asian": 0.1}},
        "TARGETS": [("head", "head-age-incr", 1.0), ("stomach", "stomach-pregnant-decr", 0.6),
                    ("neck", "neck-scale-horiz-decr", 0.5)],
        "ASSETS": [("Eyes", "eyes", "high-poly"), ("Eyebrows", "eyebrows", "eyebrow001"),
                   ("Hair", "hair", "short04"), ("Clothes", "clothes", "male_worksuit01")],
        # Haut graugrün-faulig, Kleidung verdreckt grau-braun, Haare stumpf grau
        "RECOLOR": [("lightskinned", (0.42, 0.50, 0.38), (0.42, 0.50, 0.38)),
                    ("worksuit", (0.26, 0.24, 0.20), (0.42, 0.40, 0.34)),
                    ("short04", (0.20, 0.20, 0.18), (0.20, 0.20, 0.18))],
        "BEARD": False,
        "STYLE": "zombie",
    },
    "skeleton": {
        "TARGET_HEIGHT_M": 1.75,
        "MACRO": {"gender": 1.0, "age": 0.5, "muscle": 0.3, "weight": 0.1, "proportions": 0.6,
                  "height": 0.6, "cupsize": 0.5, "firmness": 0.5,
                  "race": {"caucasian": 0.34, "african": 0.33, "asian": 0.33}},
        "TARGETS": [],
        "ASSETS": [],
        "RECOLOR": [],
        "BEARD": False,
        "STYLE": "skeleton",
    },
}


def asset_path(folder, name, ext):
    return os.path.join(LocationService.get_user_data(folder), name, name + ext)


def reset_scene():
    bpy.ops.wm.read_homefile(use_empty=True)


def apply_targets(basemesh):
    root = LocationService.get_mpfb_data("targets")
    for folder, name, weight in TARGETS:
        for side in (["l", "r"] if "{s}" in name else [None]):
            target = name.format(s=side) if side else name
            path = os.path.join(root, folder, target + ".target.gz")
            if not os.path.exists(path):
                print(f"Target fehlt: {path}")
                continue
            TargetService.load_target(basemesh, path, weight=weight, name=target)


def height_of(obj):
    zs = [(obj.matrix_world @ Vector(c)).z for c in obj.bound_box]
    return max(zs) - min(zs)


def make_skin(basemesh):
    """Einfaches Hautmaterial (Platzhalter, bis Skin-Texturen aus dem MPFB-Asset-Paket genutzt werden)."""
    mat = bpy.data.materials.new("DwarfSkin")
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.80, 0.56, 0.44, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.55
    basemesh.data.materials.clear()
    basemesh.data.materials.append(mat)


def dress(basemesh):
    """Haut, Augen, Brauen, Haare, Kleidung aus dem Asset-Paket; ohne Paket nur Hautfarbe."""
    skin = asset_path(*SKIN, ".mhmat")
    if not os.path.exists(skin):
        print("Asset-Paket fehlt – nur einfache Hautfarbe")
        make_skin(basemesh)
        return
    # MAKESKIN: Principled-BSDF mit Texturen, exportiert sauber nach glTF (SSS-Varianten nicht).
    HumanService.set_character_skin(skin, basemesh, skin_type="MAKESKIN")
    for asset_type, folder, name in ASSETS:
        path = asset_path(folder, name, ".mhclo")
        if not os.path.exists(path):
            print(f"Asset fehlt: {path}")
            continue
        HumanService.add_mhclo_asset(path, basemesh, asset_type=asset_type, subdiv_levels=0,
                                     material_type="MAKESKIN")
        print(f"Asset: {asset_type} {name}")


# Umfärben moderner Kleidung in mittelalterliche Töne: (Bildname-Teil, Farbe für blaue Bereiche, Farbe für helle Bereiche)
RECOLOR = [
    ("worksuit", (0.36, 0.25, 0.15), (0.80, 0.72, 0.58)),   # Jeans → braune Wolle, weißes Shirt → Leinen
    ("shoes", (0.28, 0.18, 0.11), (0.35, 0.24, 0.15)),       # Turnschuhe → Leder
    ("short02", (0.30, 0.14, 0.07), (0.30, 0.14, 0.07)),     # Haare → kupferbraun wie der Bart
]
BEARD_COLOR = (0.30, 0.14, 0.07)
MAX_TEXTURE = 1024


def process_textures():
    """Texturen verkleinern und Kleidung umfärben (Pixel werden direkt geändert, damit glTF sie exportiert)."""
    import numpy as np
    for img in list(bpy.data.images):
        if img.size[0] == 0:
            continue
        if max(img.size) > MAX_TEXTURE:
            f = MAX_TEXTURE / max(img.size)
            img.scale(int(img.size[0] * f), int(img.size[1] * f))
        rule = next((r for r in RECOLOR if r[0] in img.name.lower() and "diffuse" in img.name.lower()), None)
        if rule is None:
            continue
        px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)
        rgb = px[:, :3]
        lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
        blueish = (rgb[:, 2] - rgb[:, 0]) > 0.08
        dark, light = np.array(rule[1], np.float32), np.array(rule[2], np.float32)
        # Helligkeit relativ zur mittleren Helligkeit des Bereichs, damit Falten/Nähte erhalten bleiben.
        for mask, tint in ((blueish, dark), (~blueish, light)):
            if mask.any():
                rel = np.minimum(lum[mask] / max(float(lum[mask].mean()), 1e-3), 1.15)
                rgb[mask] = np.clip(rel[:, None] * tint[None, :], 0, 1)
        px[:, :3] = rgb
        img.pixels[:] = px.ravel()
        img.pack()
        print(f"Umgefärbt: {img.name}")


def beard_texture():
    """Strähnen-Textur mit Alpha: viele dünne, unten auslaufende Haarsträhnen (prozedural, eigenes Asset)."""
    import numpy as np
    w, h = 128, 256
    rng = np.random.default_rng(7)
    alpha = np.zeros((h, w), np.float32)
    shade = np.ones((h, w), np.float32)
    ys = np.arange(h)[:, None] / h
    for _ in range(90):
        x0 = rng.uniform(0, w)
        length = rng.uniform(0.6, 1.0)
        width = rng.uniform(0.8, 2.2)
        wave = rng.uniform(-4, 4)
        xs = x0 + wave * np.sin(ys[:, 0] * rng.uniform(2, 5))
        dist = np.abs(np.arange(w)[None, :] - xs[:, None])
        strand = np.clip(1 - dist / width, 0, 1) * (ys < length)
        strand *= np.clip((length - ys) / 0.25, 0, 1)            # Spitze läuft aus
        alpha = np.maximum(alpha, strand)
        shade = np.where(strand > 0.3, rng.uniform(0.7, 1.25), shade)
    img = bpy.data.images.new("dwarf_beard", w, h, alpha=True)
    rgba = np.zeros((h, w, 4), np.float32)
    for c in range(3):
        rgba[:, :, c] = np.clip(BEARD_COLOR[c] * shade, 0, 1)
    rgba[:, :, 3] = alpha
    img.pixels[:] = rgba[::-1].ravel()                           # Blender-Bilder beginnen unten
    img.pack()
    return img


def beard_material():
    mat = bpy.data.materials.new("DwarfBeard")
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = beard_texture()
    clip = nt.nodes.new("ShaderNodeMath")                        # → glTF alphaMode MASK
    clip.operation = 'GREATER_THAN'
    clip.inputs[1].default_value = 0.35
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], clip.inputs[0])
    nt.links.new(clip.outputs[0], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.8
    mat.use_backface_culling = False
    return mat


def add_beard(basemesh, rig):
    """Vollbart und Schnurrbart als Haarkarten (schmale Flächen mit Strähnen-Textur) am Kopfknochen."""
    import random
    import bmesh
    rnd = random.Random(42)
    head_bone = rig.data.bones["head"]
    head_z = (rig.matrix_world @ head_bone.head_local).z
    body = basemesh.vertex_groups["body"].index
    verts = [v for v in basemesh.data.vertices if any(g.group == body and g.weight > 0.5 for g in v.groups)]
    # Orientierungspunkte am Kopf: Nasenspitze = vorderster Punkt oberhalb des Halses.
    head_verts = [v for v in verts if v.co.z > head_z]
    nose = min(head_verts, key=lambda v: v.co.y)
    nose_z, face_front = nose.co.z, nose.co.y
    mouth_z = nose_z - 0.032
    print(f"Kopf: Hals {head_z:.3f}, Nase {nose_z:.3f}, Mund ~{mouth_z:.3f}")

    def beard_zone(v):
        if v.normal.y > 0.35 or v.co.y > face_front + 0.10 or abs(v.co.x) > 0.088:
            return False
        if v.co.z < nose_z - 0.125:                              # nicht auf Brust/Hals (Kinn ca. 9 cm unter Nase)
            return False
        if abs(v.co.x) > 0.06:
            cheek_top = nose_z + 0.025                           # Koteletten bis zu den Ohren
        elif abs(v.co.x) > 0.032:
            cheek_top = nose_z - 0.012
        else:
            cheek_top = mouth_z - 0.014
        return v.co.z <= cheek_top and v.co.y < face_front + 0.10

    beard = [v for v in verts if beard_zone(v)]
    mustache = [v for v in verts if abs(v.co.x) < 0.03 and mouth_z + 0.004 < v.co.z < nose_z - 0.012
                and v.normal.y < -0.4 and v.co.y < face_front + 0.035]

    bm = bmesh.new()
    uv = bm.loops.layers.uv.new()

    def card(origin, normal, length, width, droop):
        down = Vector((0, 0, -1))
        side = normal.cross(down).normalized() if normal.cross(down).length > 1e-3 else Vector((1, 0, 0))
        u0 = rnd.uniform(0, 0.75)
        prev = None
        p = origin + normal * 0.004
        segments = 3
        for i in range(segments + 1):
            t = i / segments
            if i > 0:
                direction = (down * (1 - droop) + normal * droop).normalized()
                p = p + direction * (length / segments)
                p += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)) * 0.004
            taper = 1 - 0.6 * t
            a = bm.verts.new(p - side * width * taper / 2)
            b = bm.verts.new(p + side * width * taper / 2)
            if prev:
                f = bm.faces.new((prev[0], prev[1], b, a))
                for loop, (uu, vv) in zip(f.loops, ((u0, 1 - prev[2]), (u0 + 0.25, 1 - prev[2]), (u0 + 0.25, 1 - t), (u0, 1 - t))):
                    loop[uv].uv = (uu, vv)
            prev = (a, b, t)

    for v in rnd.sample(beard, min(len(beard), 420)):
        center = 1 - min(abs(v.co.x) / 0.075, 1)                # zur Kinnmitte hin länger
        length = (0.07 + 0.16 * center) * rnd.uniform(0.8, 1.15)
        card(v.co.copy(), v.normal.copy(), length, 0.022, rnd.uniform(0.1, 0.3))
    for v in mustache:
        card(v.co.copy(), v.normal.copy(), rnd.uniform(0.04, 0.06), 0.014, 0.25)

    mesh = bpy.data.meshes.new("DwarfBeard")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("DwarfBeard", mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(beard_material())
    obj.parent = rig
    group = obj.vertex_groups.new(name="head")
    group.add(range(len(mesh.vertices)), 1.0, 'REPLACE')
    obj.modifiers.new("Armature", 'ARMATURE').object = rig
    print(f"Bart: {len(beard)} + {len(mustache)} Ansatzpunkte, {len(mesh.polygons)} Flächen")


FPS = 24


def add_animations(rig, style="dwarf"):
    """Prozedurale Animationen „Idle“, „Walk“, „Run“ (Schleifen) am game_engine-Skelett.

    Rotationen werden als Achse im Armature-Raum angegeben (Figur schaut nach -Y, +X = linke Seite,
    +Z oben) und in den lokalen Raum des Knochens umgerechnet. Positiver Winkel um X schwenkt ein
    nach unten zeigendes Glied nach hinten (+Y)."""
    from mathutils import Quaternion
    scene = bpy.context.scene
    scene.render.fps = FPS
    bones = rig.pose.bones
    for pb in bones:
        pb.rotation_mode = 'QUATERNION'
    rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}

    def local(name, axis, degrees):
        a = (rest[name].inverted() @ Vector(axis)).normalized()
        return Quaternion(a, math.radians(degrees))

    # Arme aus der A-Pose nach unten: aktuellen Winkel zur Horizontalen messen.
    def arm_down_angle(side):
        up = rig.data.bones[f"upperarm_{side}"]
        v = up.tail_local - up.head_local
        current = math.degrees(math.atan2(-v.z, abs(v.x)))
        return 78.0 - current                                   # Ziel: fast senkrecht, Zwerge sind breit

    down_deg = {"l": arm_down_angle("l"), "r": -arm_down_angle("r")}
    down = {s: local(f"upperarm_{s}", (0, 1, 0), d) for s, d in down_deg.items()}
    # Ellbogen-Achse: die seitliche Achse, wie sie nach dem Senken der Oberarme liegt.
    # Kinder erben die Drehung des Elternknochens, also die Achse in dessen Ruhelage zurückdrehen.
    elbow_axis = {s: Quaternion((0, 1, 0), -math.radians(d)) @ Vector((1, 0, 0)) for s, d in down_deg.items()}

    def elbow(side, degrees):
        return local(f"lowerarm_{side}", tuple(elbow_axis[side]), degrees)

    zombie = style == "zombie"

    def pose(t, kind):
        """Rotationen (Knochen → Quaternion) und Beckenhöhe für Phase t (0..1)."""
        w = 2 * math.pi * t
        rot = {}
        if kind in ("Attack", "Die"):
            return special_pose(t, kind)
        if kind == "Idle":
            breath = math.sin(w)
            amp_leg, amp_arm, knee, bob, lean = 0, 0, 0, 0.004 * breath, 2
            rot["spine_02"] = local("spine_02", (1, 0, 0), 1.5 * breath)
            rot["head"] = local("head", (0, 0, 1), 3 * math.sin(w + 1))
            arm_sway = 2 * breath
        else:
            run = kind == "Run"
            amp_leg, amp_arm = (38, 26) if run else (24, 13)
            knee = 70 if run else 40
            lean = 10 if run else 3
            if zombie:                                            # schlurfend, kleine Schritte
                amp_leg, amp_arm, knee = amp_leg * 0.6, 4, knee * 0.5
            bob = (0.035 if run else 0.018) * abs(math.cos(w))
            arm_sway = 0
            rot["spine_01"] = local("spine_01", (0, 0, 1), (6 if run else 4) * math.sin(w))
        swing = math.sin(w)
        for side, sign in (("l", 1), ("r", -1)):
            phase = swing * sign                                  # Beine gegengleich
            rot[f"thigh_{side}"] = local(f"thigh_{side}", (1, 0, 0), -amp_leg * phase)
            # Knie beugt beim Zurückschwingen und in der Schwungphase
            bend = knee * max(0.0, math.sin(w * 1 + (0 if sign > 0 else math.pi) + math.pi / 2)) if amp_leg else 0
            rot[f"calf_{side}"] = local(f"calf_{side}", (1, 0, 0), bend)
            rot[f"foot_{side}"] = local(f"foot_{side}", (1, 0, 0), -0.4 * bend)
            reach = -70 if zombie else 0                          # Zombies strecken die Arme nach vorn
            arm = local(f"upperarm_{side}", (1, 0, 0), reach + amp_arm * phase + arm_sway)   # Arme gegen die Beine
            rot[f"upperarm_{side}"] = arm @ down[side]
            rot[f"lowerarm_{side}"] = elbow(side, -(12 + (25 if kind == "Run" else 4)) * (0.3 if zombie else 1))
        # Positiver Winkel kippt den nach oben zeigenden Rumpf nach vorn (-Y).
        rot["spine_03"] = local("spine_03", (1, 0, 0), lean + (18 if zombie else 0))
        if zombie:
            rot["head"] = local("head", (0, 1, 0), 12)           # Kopf schief
        return rot, bob

    def base_arms():
        return {f"upperarm_{s}": down[s] for s in ("l", "r")}

    def special_pose(t, kind):
        rot = base_arms()
        if kind == "Attack":
            # Rechter Arm: ausholen (nach oben/hinten), zuschlagen (nach vorn unten), zurück.
            if t < 0.35:
                k = t / 0.35
                swing = -150 * k
            elif t < 0.55:
                k = (t - 0.35) / 0.2
                swing = -150 + 120 * k
            else:
                k = (t - 0.55) / 0.45
                swing = -30 * (1 - k)
            rot["upperarm_r"] = local("upperarm_r", (1, 0, 0), swing) @ down["r"]
            rot["lowerarm_r"] = elbow("r", -20)
            rot["spine_02"] = local("spine_02", (0, 0, 1), 15 * math.sin(math.pi * t))
            rot["spine_03"] = local("spine_03", (1, 0, 0), 12 * math.sin(math.pi * t))
            return rot, 0.0
        # Die: in die Knie gehen und nach hinten umkippen, letzte Pose bleibt liegen.
        k = min(1.0, t * 1.3)
        ease = k * k * (3 - 2 * k)
        rot["pelvis"] = local("pelvis", (1, 0, 0), 80 * ease)
        for s in ("l", "r"):
            rot[f"thigh_{s}"] = local(f"thigh_{s}", (1, 0, 0), -50 * ease)
            rot[f"calf_{s}"] = local(f"calf_{s}", (1, 0, 0), 70 * ease)
            rot[f"upperarm_{s}"] = local(f"upperarm_{s}", (1, 0, 0), -40 * ease) @ down[s]
        rot["head"] = local("head", (1, 0, 0), 20 * ease)
        return rot, -hip_height * 0.82 * ease

    hip_height = (rig.matrix_world @ rig.data.bones["pelvis"].head_local).z

    clips = [("Idle", 3.0), ("Walk", 1.4 if zombie else 1.0), ("Run", 0.66), ("Attack", 0.6), ("Die", 1.2)]
    for kind, seconds in clips:
        action = bpy.data.actions.new(kind)
        action.use_fake_user = True
        rig.animation_data_create()
        rig.animation_data.action = action
        frames = round(seconds * FPS)
        pelvis_rest = bones["pelvis"].location.copy()
        for f in range(frames + 1):
            rot, bob = pose(f / frames, kind)
            for pb in bones:
                pb.rotation_quaternion = rot.get(pb.name, Quaternion())
                pb.keyframe_insert("rotation_quaternion", frame=f)
            # Becken auf und ab (im lokalen Raum des Beckens entlang Armature-Z)
            bones["pelvis"].location = pelvis_rest + rest["pelvis"].inverted() @ Vector((0, 0, bob))
            bones["pelvis"].keyframe_insert("location", frame=f)
        track = rig.animation_data.nla_tracks.new()
        track.name = kind
        track.strips.new(kind, 1, action)
        rig.animation_data.action = None
        print(f"Animation {kind}: {frames} Frames")


def apply_preset(name):
    g = globals()
    for key, value in PRESETS[name].items():
        g[key] = value


def main(out_glb, preview_png=None, preset="dwarf"):
    apply_preset(preset)
    reset_scene()
    basemesh = HumanService.create_human(macro_detail_dict=MACRO, feet_on_ground=True)
    apply_targets(basemesh)
    TargetService.bake_targets(basemesh)

    # Auf Zielgröße skalieren und Füße auf den Boden setzen.
    bpy.context.view_layer.update()
    factor = TARGET_HEIGHT_M / height_of(basemesh)
    basemesh.scale = (factor, factor, factor)
    bpy.context.view_layer.objects.active = basemesh
    basemesh.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.context.view_layer.update()
    lowest = min((basemesh.matrix_world @ Vector(c)).z for c in basemesh.bound_box)
    basemesh.location.z -= lowest
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    rig = HumanService.add_builtin_rig(basemesh, "game_engine", import_weights=True)
    if STYLE == "skeleton":
        body = build_skeleton_mesh(rig)
        bpy.data.objects.remove(basemesh, do_unlink=True)        # nur die Knochen exportieren
        basemesh = body
    else:
        dress(basemesh)
        process_textures()
        if BEARD:
            add_beard(basemesh, rig)
        basemesh.name = preset.capitalize()
    add_animations(rig, STYLE)
    rig.name = preset.capitalize() + "Rig"
    print(f"Höhe: {height_of(basemesh):.2f} m, Rig: {rig.name}")

    os.makedirs(os.path.dirname(os.path.abspath(out_glb)), exist_ok=True)
    # Alles exportieren, was zur Figur gehört (Körper, Kleidung, Haare – hängen am Rig).
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        if obj.type in {'MESH', 'ARMATURE'}:
            obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(out_glb), export_format='GLB',
                              use_selection=True, export_apply=True, export_yup=True,
                              export_animations=True, export_animation_mode='ACTIONS')
    print(f"Exportiert: {out_glb}")

    if preview_png:
        for track in rig.animation_data.nla_tracks:             # nur die gewählte Animation auswerten
            track.mute = True
        rig.animation_data.action = bpy.data.actions["Walk"]    # Vorschau in Lauf-Pose
        bpy.context.scene.frame_set(6)
        render_preview(basemesh, preview_png)


def render_preview(obj, path):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 40
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    top = max((obj.matrix_world @ Vector(c)).z for c in obj.bound_box)
    cam.location = (1.6, -2.4, top * 0.75)
    direction = Vector((0, 0, top * 0.5)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    for loc, energy in (((2, -3, 3), 400), ((-3, -1, 2), 150), ((0, 3, 3), 200)):
        light = bpy.data.objects.new("L", bpy.data.lights.new("L", 'POINT'))
        light.data.energy = energy
        light.location = loc
        scene.collection.objects.link(light)
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 600, 800
    scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)
    print(f"Vorschau: {path}")
    # Nahaufnahme vom Kopf
    head_z = max((obj.matrix_world @ Vector(c)).z for c in obj.bound_box) - 0.12
    cam.location = (0.25, -0.75, head_z + 0.03)
    cam.rotation_euler = (Vector((0, 0, head_z - 0.03)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.abspath(path.replace(".png", "_head.png"))
    bpy.ops.render.render(write_still=True)


def build_skeleton_mesh(rig):
    """Knochen-Geometrie aus dem Rig: verjüngte Röhren je Knochen, Gelenkkugeln, Schädel, Rippen, Becken.
    Jedes Teil ist zu 100 % an seinen Knochen gebunden."""
    import bmesh
    from mathutils import Matrix
    bm = bmesh.new()
    deform = bm.verts.layers.deform.verify()                     # Gewichte direkt an den Punkten
    bone_index = {}                                              # Knochenname → Vertex-Gruppen-Index

    def add(geom_verts, bone):
        idx = bone_index.setdefault(bone, len(bone_index))
        for v in geom_verts:
            v[deform][idx] = 1.0

    def tube(a, b, r1, r2, bone, segments=8):
        d = b - a
        length = d.length
        if length < 1e-4:
            return
        res = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=r1, radius2=r2, depth=length)
        rot = d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=res["verts"])
        add(res["verts"], bone)

    def ball(center, radius, bone, scale=(1, 1, 1)):
        res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=radius)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1)), verts=res["verts"])
        add(res["verts"], bone)

    mw = rig.matrix_world
    skip = ("Root", "ball_", "pelvis", "spine_", "neck_01", "head", "clavicle_")
    for bone in rig.data.bones:
        a, b = mw @ bone.head_local, mw @ bone.tail_local
        if any(bone.name.startswith(s) for s in skip):
            continue
        finger = any(bone.name.startswith(f) for f in ("thumb", "index", "middle", "ring", "pinky"))
        r = 0.006 if finger else max(0.012, bone.length * 0.07)
        tube(a, b, r, r * 0.75, bone.name)
        ball(a, r * 1.4, bone.name)

    B = {b.name: (mw @ b.head_local, mw @ b.tail_local) for b in rig.data.bones}
    # Wirbelsäule aus Wirbeln
    for name in ("spine_01", "spine_02", "spine_03", "neck_01"):
        a, b = B[name]
        for i in range(3):
            p = a.lerp(b, (i + 0.5) / 3)
            ball(p + Vector((0, 0.03, 0)), 0.018, name, (1.2, 1.0, 0.7))
    # Brustkorb: Rippenbögen an spine_02/spine_03
    for name, count in (("spine_02", 3), ("spine_03", 4)):
        a, b = B[name]
        for i in range(count):
            p = a.lerp(b, (i + 0.5) / count)
            width = 0.13 - 0.012 * abs(i - count / 2)
            # Rippenbogen als Kette kurzer Röhren auf einer Ellipse (vorne offen, Brustbein separat)
            pts = [p + Vector((width * math.sin(a), 0.09 * math.cos(a) - 0.01, -0.02 * (1 - math.cos(a))))
                   for a in [math.radians(x) for x in range(25, 336, 31)]]
            for q0, q1 in zip(pts, pts[1:]):
                tube(q0, q1, 0.007, 0.007, name, segments=6)
        tube(a + Vector((0, -0.1, 0)), b + Vector((0, -0.1, 0)), 0.012, 0.012, name)   # Brustbein
    bm.verts.index_update()
    # Becken, Schulterblätter, Schädel, Kiefer
    pa, pb_ = B["pelvis"]
    ball(pa + Vector((0, 0, 0.02)), 0.11, "pelvis", (1.25, 0.7, 0.55))
    for s, sign in (("l", 1), ("r", -1)):
        ca, cb = B[f"clavicle_{s}"]
        tube(ca, cb, 0.012, 0.01, f"clavicle_{s}")
    ha, hb = B["head"]
    ball(ha + Vector((0, -0.01, 0.10)), 0.095, "head", (0.85, 1.05, 1.05))
    ball(ha + Vector((0, -0.06, 0.02)), 0.05, "head", (1.1, 0.8, 0.6))      # Oberkiefer
    ball(ha + Vector((0, -0.06, -0.025)), 0.045, "head", (1.0, 0.8, 0.45))  # Unterkiefer
    bm.verts.index_update()
    first_eye = len(bm.verts)
    bone_index.setdefault("head", len(bone_index))
    for x in (-0.032, 0.032):
        ball(ha + Vector((x, -0.085, 0.10)), 0.022, "head")               # Augenhöhlen (dunkel)
    bm.verts.index_update()

    mesh = bpy.data.meshes.new("Skeleton")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Skeleton", mesh)
    bpy.context.scene.collection.objects.link(obj)
    # Vertex-Gruppen in derselben Reihenfolge wie die Indizes der Gewichtsebene anlegen.
    for bone, _ in sorted(bone_index.items(), key=lambda kv: kv[1]):
        obj.vertex_groups.new(name=bone)

    bone_mat = bpy.data.materials.new("Bone")
    bsdf = bone_mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.78, 0.73, 0.60, 1)
    bsdf.inputs["Roughness"].default_value = 0.6
    hole_mat = bpy.data.materials.new("EyeSocket")
    hole_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.03, 0.02, 0.02, 1)
    mesh.materials.append(bone_mat)
    mesh.materials.append(hole_mat)
    for poly in mesh.polygons:
        if all(v >= first_eye for v in poly.vertices):
            poly.material_index = 1
    for poly in mesh.polygons:
        poly.use_smooth = True

    obj.parent = rig
    obj.modifiers.new("Armature", 'ARMATURE').object = rig
    return obj


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preset = "dwarf"
    if "--preset" in argv:
        i = argv.index("--preset")
        preset = argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
    main(argv[0] if argv else f"{preset}.glb", argv[1] if len(argv) > 1 else None, preset)
