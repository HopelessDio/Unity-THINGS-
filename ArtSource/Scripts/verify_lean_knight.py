"""Round-trip FBX check: skin deformation, right-hand socket and independent weapon."""
import bpy
import os
import math
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MODELS = os.path.join(ROOT, 'Assets', 'Models', 'Player')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, 'LeanKnight_Model01.fbx'))
rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
skin = next(obj for obj in bpy.context.scene.objects if obj.type == 'MESH')
socket = bpy.data.objects['SwordSocket_R']
assert len(rig.data.bones) == 20
assert len(skin.modifiers) > 0 and any(mod.type == 'ARMATURE' for mod in skin.modifiers)
assert all(len(vertex.groups) > 0 for vertex in skin.data.vertices)
assert socket.parent == rig and socket.parent_bone == 'RightHand'
bpy.context.view_layer.update()
hand_origin = rig.matrix_world @ rig.pose.bones['RightHand'].head
assert (socket.matrix_world.translation - hand_origin).length < .15, 'Socket is detached from hand'
hand_group = skin.vertex_groups['RightHand'].index
hand_vertices = [v.index for v in skin.data.vertices if any(g.group == hand_group for g in v.groups)]
depsgraph = bpy.context.evaluated_depsgraph_get()
evaluated = skin.evaluated_get(depsgraph)
before = [evaluated.matrix_world @ evaluated.data.vertices[i].co for i in hand_vertices]
socket_before = socket.matrix_world.translation.copy()
rig.pose.bones['RightLowerArm'].rotation_mode = 'XYZ'
rig.pose.bones['RightLowerArm'].rotation_euler.x = math.radians(60)
bpy.context.view_layer.update()
evaluated = skin.evaluated_get(bpy.context.evaluated_depsgraph_get())
after = [evaluated.matrix_world @ evaluated.data.vertices[i].co for i in hand_vertices]
movement = max((a-b).length for a,b in zip(after,before))
assert movement > .04, 'Hand mesh does not follow forearm rotation'
assert (socket.matrix_world.translation-socket_before).length > .04, 'Socket does not follow forearm'
rig.pose.bones['RightLowerArm'].rotation_euler.x = 0
bpy.context.view_layer.update()
knight_mesh_count = len([obj for obj in bpy.context.scene.objects if obj.type=='MESH'])
bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, 'LeanKnight_Longsword01.fbx'))
sword = bpy.data.objects['LeanKnight_Longsword']
assert sword.parent is None
assert not sword.vertex_groups
assert len([obj for obj in bpy.context.scene.objects if obj.type=='MESH']) == knight_mesh_count+1
assert sword.location.length < .001
print('FBX_ROUND_TRIP_PASS: 20 bones; all knight vertices weighted; forearm rotates hand and socket; sword imports independently with grip pivot. Max hand travel: '+str(round(movement,3)))
