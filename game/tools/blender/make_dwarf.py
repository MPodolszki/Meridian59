"""Erzeugt die Spielfigur „Zwerg“ mit MPFB2 (MakeHuman für Blender) und exportiert sie für Godot.

Voraussetzung: Blender 5.x mit Erweiterung „MPFB“ (extensions.blender.org, ID `mpfb`).
Aufruf im Hintergrund (im Repo-Root):

    blender -b --factory-startup-off --python game/tools/blender/make_dwarf.py -- \
        game/client/models/dwarf.glb [vorschau.png]

Ergebnis: Mesh mit Skelett „game_engine“ (für Animationen), ca. 1,35 m groß. Die mit MPFB
erzeugten Körper sind CC0 (MakeHuman-Lizenz für Ausgaben), also ein eigenes Asset.
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
    "age": 0.62,
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
    ("head", "head-age-incr", 0.3),
    ("arms", "{s}-upperarm-muscle-incr", 0.7),
    ("arms", "{s}-lowerarm-muscle-incr", 0.6),
    ("arms", "{s}-upperarm-shoulder-muscle-incr", 0.6),
    ("hands", "{s}-hand-scale-incr", 0.6),
    ("hands", "{s}-hand-fingers-diameter-incr", 0.5),
    ("feet", "{s}-foot-scale-incr", 0.5),
]


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


def main(out_glb, preview_png=None):
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

    make_skin(basemesh)
    rig = HumanService.add_builtin_rig(basemesh, "game_engine", import_weights=True)
    basemesh.name = "Dwarf"
    if rig is not None:
        rig.name = "DwarfRig"
    print(f"Höhe: {height_of(basemesh):.2f} m, Rig: {rig.name if rig else 'keins'}")

    os.makedirs(os.path.dirname(os.path.abspath(out_glb)), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    basemesh.select_set(True)
    if rig:
        rig.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(out_glb), export_format='GLB',
                              use_selection=True, export_apply=True, export_yup=True)
    print(f"Exportiert: {out_glb}")

    if preview_png:
        render_preview(basemesh, preview_png)


def render_preview(obj, path):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.6, -2.6, 1.0)
    direction = Vector((0, 0, 0.7)) - cam.location
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


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    main(argv[0] if argv else "dwarf.glb", argv[1] if len(argv) > 1 else None)
