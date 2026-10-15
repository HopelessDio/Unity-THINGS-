"""Export the animation skeleton's true bind pose, not its first animated frame."""
import bpy
from pathlib import Path
from mathutils import Matrix

root=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/MaximoAnimations/VeilWalker_Model01@Great Sword Run.fbx'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
rig.animation_data_clear()
rig.matrix_world=Matrix.Identity(4)
for bone in rig.pose.bones:
    bone.matrix_basis=Matrix.Identity(4)
for obj in list(bpy.data.objects):
    if obj!=rig: bpy.data.objects.remove(obj,do_unlink=True)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active=rig
path=root/'Assets/Models/Player/Mixamo/VeilWalker_AnimationReference.fbx'
bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE'},
    axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,
    use_armature_deform_only=False,apply_unit_scale=True)
for side in ['Left','Right']:
    arm=rig.data.bones[side+'UpperArm']; hand=rig.data.bones[side+'Hand']
    delta=hand.head_local-arm.head_local
    assert abs(delta.normalized().z)<.05, 'Source reference arm is not horizontal'
print('SOURCE_REFERENCE_PASS: true 23-bone bind skeleton, no sampled animation pose')
