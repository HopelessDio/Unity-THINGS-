"""Validate the real exported rig, sockets, cape and independent weapon files."""
import bpy
import os
import math
from mathutils import Vector

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
MODELS=os.path.join(ROOT,'Assets','Models','Player')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS,'VeilWalker_Model01.fbx'))
rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
body=next(obj for obj in bpy.context.scene.objects if obj.type=='MESH')
assert len(rig.data.bones)==23
assert len([obj for obj in bpy.context.scene.objects if obj.type=='MESH'])==1, 'Character includes weapon/presentation geometry'
assert all(abs(sum(g.weight for g in vertex.groups)-1)<.0001 for vertex in body.data.vertices)
assert all(group.name in rig.data.bones for group in body.vertex_groups)
bpy.context.view_layer.update()
support=bpy.data.objects.get('SwordOffhandGrip')
assert support is not None and support.parent==bpy.data.objects['SwordSocket_R']
left_wrist=rig.matrix_world @ rig.pose.bones['LeftHand'].head
right_wrist=rig.matrix_world @ rig.pose.bones['RightHand'].head
assert (support.matrix_world.translation-left_wrist).length<.085, 'Supporting hand is off the sword grip'
assert (bpy.data.objects['SwordSocket_R'].matrix_world.translation-right_wrist).length<.085
assert .095 < (support.matrix_world.translation-bpy.data.objects['SwordSocket_R'].matrix_world.translation).length < .11
print('DEFAULT_TWO_HANDED_GRIP_PASS: one sword socket, supporting left hand on lower grip, no gun geometry')

def positions(indices):
    evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    return [evaluated.matrix_world @ evaluated.data.vertices[index].co for index in indices]

for side,socket_name in [('Right','SwordSocket_R'),('Left','GunSocket_L')]:
    socket=bpy.data.objects[socket_name]
    assert socket.parent==rig and socket.parent_bone==side+'Hand'
    bpy.context.view_layer.update()
    hand_origin=rig.matrix_world @ rig.pose.bones[side+'Hand'].head
    assert (socket.matrix_world.translation-hand_origin).length<.12
    group=body.vertex_groups[side+'Hand'].index
    indices=[v.index for v in body.data.vertices if any(g.group==group and g.weight>.9 for g in v.groups)]
    assert indices
    before=positions(indices); socket_before=socket.matrix_world.translation.copy()
    bone=rig.pose.bones[side+'LowerArm']; bone.rotation_mode='XYZ'; bone.rotation_euler.x=math.radians(50)
    bpy.context.view_layer.update()
    travel=max((a-b).length for a,b in zip(positions(indices),before))
    assert travel>.03 and (socket.matrix_world.translation-socket_before).length>.03
    bone.rotation_euler.x=0; bpy.context.view_layer.update()
    print(side+' HAND_AND_SOCKET_PASS travel='+str(round(travel,3)))

group=body.vertex_groups['CapeLower'].index
indices=[v.index for v in body.data.vertices if any(g.group==group and g.weight>.9 for g in v.groups)]
assert indices
before=positions(indices)
bone=rig.pose.bones['CapeMiddle']; bone.rotation_mode='XYZ'; bone.rotation_euler.x=math.radians(15)
bpy.context.view_layer.update()
assert max((a-b).length for a,b in zip(positions(indices),before))>.025
bone.rotation_euler.x=0; bpy.context.view_layer.update()
print('CAPE_DEFORMATION_PASS')

bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS,'VeilWalker_Pistol01.fbx'))
gun=bpy.data.objects['VeilWalker_Pistol']; muzzle=bpy.data.objects['Muzzle']
assert gun.location.length<.001 and not gun.vertex_groups
assert muzzle.parent==gun
bpy.context.view_layer.update()
direction=muzzle.matrix_world.to_quaternion() @ Vector((0,0,1))
assert direction.dot(Vector((0,-1,0)))>.99, 'Muzzle does not face down barrel'
assert muzzle.matrix_world.translation.y<-.32
print('INDEPENDENT_GUN_AND_MUZZLE_PASS')
bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS,'LeanKnight_Longsword01.fbx'))
sword=bpy.data.objects['LeanKnight_Longsword']
assert sword.location.length<.001 and not sword.vertex_groups
print('UNCHANGED_INDEPENDENT_SWORD_PASS')
print('VEIL_WALKER_FBX_ROUND_TRIP_PASS')
