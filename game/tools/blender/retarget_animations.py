"""Überträgt Animationen der Universal Animation Library (Quaternius, CC0) auf unsere Figuren.

Aufruf (Blender im Hintergrund):
  blender -b -P game/tools/blender/retarget_animations.py -- QUELLE.gltf MODELL.glb [ZIEL.glb]

QUELLE: AnimationLibrary_Godot_Standard.gltf (Rigify-Knochen DEF-*), MODELL: unsere Figur mit Skelett
„game_engine“ (Unreal-Namen). Die vorhandenen Animationen des Modells werden ersetzt.

Verfahren: Rumpf, Hals und Kopf übernehmen die Rotation des Quellknochens relativ zu seiner Ruhelage im
Armature-Raum (Zielrotation = Quellpose · Quellruhe⁻¹ · Zielruhe) – so bleiben Drehungen um die eigene Achse
erhalten. Gliedmaßen (Schlüsselbein, Arme, Hände, Finger, Beine, Füße) werden dagegen so gedreht, dass ihre
Knochenrichtung der gepositionierten Quellrichtung entspricht (kürzeste Drehung von der Zielruhe aus). Das ist
nötig, weil Quelle (A-Pose) und Ziel (T-Pose) verschiedene Grundhaltungen haben: Die reine Relativübertragung ließ
die Arme um den Unterschied verdreht abstehen. Die Hüfte wird zusätzlich verschoben, skaliert mit dem Verhältnis
der Hüfthöhen. Blickrichtungen werden angeglichen.
"""
import math
import sys

import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SOURCE, MODEL = argv[0], argv[1]
TARGET = argv[2] if len(argv) > 2 else MODEL

# Quelle (Rigify) → Ziel (Unreal-Namen)
MAP = {"DEF-hips": "pelvis", "DEF-spine.001": "spine_01", "DEF-spine.002": "spine_02", "DEF-spine.003": "spine_03",
       "DEF-neck": "neck_01", "DEF-head": "head"}
for side, s in (("L", "l"), ("R", "r")):
    MAP.update({
        f"DEF-shoulder.{side}": f"clavicle_{s}", f"DEF-upper_arm.{side}": f"upperarm_{s}",
        f"DEF-forearm.{side}": f"lowerarm_{s}", f"DEF-hand.{side}": f"hand_{s}",
        f"DEF-thigh.{side}": f"thigh_{s}", f"DEF-shin.{side}": f"calf_{s}",
        f"DEF-foot.{side}": f"foot_{s}", f"DEF-toe.{side}": f"ball_{s}",
    })
    for finger, ours in (("f_index", "index"), ("f_middle", "middle"), ("f_pinky", "pinky"), ("f_ring", "ring"), ("thumb", "thumb")):
        for i in (1, 2, 3):
            MAP[f"DEF-{finger}.0{i}.{side}"] = f"{ours}_0{i}_{s}"

# Unsere Clipnamen ← Quaternius-Clips (Schleifen bleiben Schleifen)
CLIPS = {
    "Idle": "Idle_Loop", "Walk": "Walk_Loop", "Run": "Jog_Fwd_Loop", "Sprint": "Sprint_Loop",
    "Attack": "Sword_Attack", "Punch": "Punch_Jab", "Punch2": "Punch_Cross", "Die": "Death01",
    "Hit": "Hit_Chest", "HitHead": "Hit_Head",
    "Cast": "Spell_Simple_Shoot", "CastIdle": "Spell_Simple_Idle_Loop",
    "Jump_Start": "Jump_Start", "Jump_Loop": "Jump_Loop", "Jump_Land": "Jump_Land",
    "Interact": "Interact", "PickUp": "PickUp_Table", "Idle_Torch": "Idle_Torch_Loop",
    "CombatIdle": "Sword_Idle", "Talk": "Idle_Talking_Loop", "Dance": "Dance_Loop",
    "SitDown": "Sitting_Enter", "Sit": "Sitting_Idle_Loop", "StandUp": "Sitting_Exit",
    "Crouch": "Crouch_Idle_Loop", "CrouchWalk": "Crouch_Fwd_Loop", "Roll": "Roll",
    "Swim": "Swim_Fwd_Loop", "SwimIdle": "Swim_Idle_Loop",
}

# Gliedmaßen: Richtung angleichen statt Relativdrehung (siehe Kopfkommentar)
LIMB_PREFIXES = ("clavicle", "upperarm", "lowerarm", "hand", "thigh", "calf", "foot", "ball",
                 "index", "middle", "ring", "pinky", "thumb")

bpy.ops.wm.read_factory_settings(use_empty=True)


def import_armature(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    rig = next(o for o in new if o.type == "ARMATURE")
    return rig, new


def forward_sign(rig, foot, toe):
    """+1, wenn die Zehen in +Y zeigen, sonst −1 (Blickrichtung der Figur)."""
    b = rig.data.bones
    return 1 if (b[toe].head_local - b[foot].head_local).y > 0 else -1


target, target_objects = import_armature(MODEL)
for action in list(bpy.data.actions):          # alte, prozedurale Animationen entfernen
    bpy.data.actions.remove(action)
source, source_objects = import_armature(SOURCE)
source_actions = {a.name: a for a in bpy.data.actions}

# Blickrichtung angleichen: Quelle ggf. um 180° um Z drehen.
flip = Matrix.Rotation(math.pi, 4, "Z") if forward_sign(source, "DEF-foot.L", "DEF-toe.L") != forward_sign(target, "foot_l", "ball_l") else Matrix.Identity(4)

src_world = source.matrix_world.copy()
tgt_world_inv = target.matrix_world.inverted()
to_target = tgt_world_inv @ flip @ src_world          # Quell-Armature-Raum → Ziel-Armature-Raum (ohne Skalierung)
to_target_rot = to_target.to_quaternion()

src_rest = {b.name: b.matrix_local.copy() for b in source.data.bones}
tgt_rest = {b.name: b.matrix_local.copy() for b in target.data.bones}
inverse = {v: k for k, v in MAP.items()}

src_hip_h = (src_world @ src_rest["DEF-hips"].to_translation()).z
tgt_hip_h = (target.matrix_world @ tgt_rest["pelvis"].to_translation()).z
hip_scale = tgt_hip_h / src_hip_h if src_hip_h > 1e-6 else 1.0

order = []                                             # Zielknochen, Eltern zuerst
def walk(bone):
    order.append(bone)
    for c in bone.children:
        walk(c)
for b in target.data.bones:
    if b.parent is None:
        walk(b)

scene = bpy.context.scene
target.animation_data_create()
source.animation_data_create()
for pb in target.pose.bones:
    pb.rotation_mode = "QUATERNION"

for clip, src_name in CLIPS.items():
    src_action = source_actions.get(src_name)
    if src_action is None:
        print(f"Quelle {src_name} fehlt – {clip} übersprungen")
        continue
    source.animation_data.action = src_action
    start, end = (int(round(x)) for x in src_action.frame_range)
    action = bpy.data.actions.new(clip)
    action.use_fake_user = True
    target.animation_data.action = action
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        posed = {}
        for bone in order:
            name = bone.name
            rest = tgt_rest[name]
            src = inverse.get(name)
            if src is not None and name.startswith(LIMB_PREFIXES):
                sp = source.pose.bones[src].matrix          # gepost im Quell-Armature-Raum
                src_dir = to_target_rot @ (sp.to_quaternion() @ Vector((0, 1, 0)))
                rest_dir = rest.to_quaternion() @ Vector((0, 1, 0))
                rot = rest_dir.rotation_difference(src_dir) @ rest.to_quaternion()
            elif src is not None:
                sp = source.pose.bones[src].matrix          # gepost im Quell-Armature-Raum
                delta = sp.to_quaternion() @ src_rest[src].to_quaternion().inverted()
                rot = to_target_rot @ delta @ to_target_rot.inverted() @ rest.to_quaternion()
            else:
                rot = (posed[bone.parent.name] @ (tgt_rest[bone.parent.name].inverted() @ rest)).to_quaternion() if bone.parent else rest.to_quaternion()
            if bone.parent:
                parent_rest = tgt_rest[bone.parent.name]
                head = (posed[bone.parent.name] @ (parent_rest.inverted() @ rest)).to_translation()
            else:
                head = rest.to_translation()
            if name == "pelvis" and "DEF-hips" in source.pose.bones:
                moved = source.pose.bones["DEF-hips"].matrix.to_translation() - src_rest["DEF-hips"].to_translation()
                head = rest.to_translation() + (to_target.to_3x3() @ moved) * hip_scale
            m = Matrix.Translation(head) @ rot.to_matrix().to_4x4()
            posed[name] = m
            parent_posed = (posed[bone.parent.name] @ (tgt_rest[bone.parent.name].inverted() @ rest)) if bone.parent else rest
            basis = parent_posed.inverted() @ m
            pb = target.pose.bones[name]
            pb.matrix_basis = basis
            pb.keyframe_insert("rotation_quaternion", frame=frame - start)
            if name == "pelvis":
                pb.keyframe_insert("location", frame=frame - start)
    track = target.animation_data.nla_tracks.new()
    track.name = clip
    track.strips.new(clip, 0, action)
    target.animation_data.action = None
    print(f"{clip} ← {src_name}: {end - start + 1} Bilder")

# Nur unsere Figur exportieren.
for o in source_objects:
    bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a.name in source_actions and a.name not in CLIPS:
        bpy.data.actions.remove(a)
bpy.ops.object.select_all(action="DESELECT")
for o in target_objects:
    o.select_set(True)
bpy.ops.export_scene.gltf(filepath=TARGET, export_format="GLB", use_selection=True,
                          export_animations=True, export_animation_mode="ACTIONS")
print(f"geschrieben: {TARGET}")
