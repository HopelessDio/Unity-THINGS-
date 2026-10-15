"""Create a separate T-pose/Humanoid character; never overwrite the gameplay rig."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Models/Player/Mixamo'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'ArtSource/VeilWalker_Preview09_HoodCollar.blend'))
parts = list(bpy.data.objects['VeilWalker_Preview_Front'].children)
assert all(obj.type == 'MESH' for obj in parts)
for obj in parts:
    local = obj.matrix_basis.copy()
    obj.parent = None
    obj.matrix_basis = local
    obj.animation_data_clear()
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
for obj in list(bpy.data.objects):
    if obj not in parts:
        bpy.data.objects.remove(obj, do_unlink=True)

PREFIX = 'mixamorig:'
# The character faces Blender -Y. Anatomical left is +X, not the old part label.
mapping = {'Hips': 'Hips', 'Spine': 'Spine', 'Chest': 'Spine2', 'Neck': 'Neck', 'Head': 'Head',
           'CapeUpper': 'CapeUpper', 'CapeMiddle': 'CapeMiddle', 'CapeLower': 'CapeLower'}
for old_side, side in [('Left', 'Right'), ('Right', 'Left')]:
    for old, new in [('Shoulder', 'Shoulder'), ('UpperArm', 'Arm'), ('LowerArm', 'ForeArm'),
                     ('Hand', 'Hand'), ('UpperLeg', 'UpLeg'), ('LowerLeg', 'Leg'), ('Foot', 'Foot')]:
        mapping[old_side + old] = side + new

specs = [('Hips', (0,0,1.02), (0,0,1.16), None),
         ('Spine', (0,0,1.16), (0,0,1.35), 'Hips'),
         ('Spine1', (0,0,1.35), (0,0,1.45), 'Spine'),
         ('Spine2', (0,0,1.45), (0,0,1.56), 'Spine1'),
         ('Neck', (0,0,1.56), (0,0,1.68), 'Spine2'),
         ('Head', (0,0,1.68), (0,0,1.985), 'Neck')]
for side, sign in [('Left', 1), ('Right', -1)]:
    specs += [(side+'Shoulder', (sign*.04,0,1.53), (sign*.196,0,1.51), 'Spine2'),
              (side+'Arm', (sign*.196,0,1.51), (sign*.284,0,1.215), side+'Shoulder'),
              (side+'ForeArm', (sign*.284,0,1.215), (sign*.346,-.007,.985), side+'Arm'),
              (side+'Hand', (sign*.346,-.007,.985), (sign*.355,-.021,.882), side+'ForeArm'),
              (side+'UpLeg', (sign*.104,0,1.045), (sign*.127,-.014,.59), 'Hips'),
              (side+'Leg', (sign*.127,-.014,.59), (sign*.15,.003,.13), side+'UpLeg'),
              (side+'Foot', (sign*.15,.003,.13), (sign*.15,-.13,.045), side+'Leg'),
              (side+'ToeBase', (sign*.15,-.13,.045), (sign*.15,-.21,.045), side+'Foot')]
specs += [('CapeUpper',(0,.17,1.46),(0,.2,1.10),'Spine2'),
          ('CapeMiddle',(0,.2,1.10),(0,.22,.78),'CapeUpper'),
          ('CapeLower',(0,.22,.78),(0,.24,.45),'CapeMiddle')]
data = bpy.data.armatures.new('VeilWalkerMixamoSkeleton')
rig = bpy.data.objects.new('VeilWalkerMixamoRig', data)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, head, tail, parent in specs:
    bone = data.edit_bones.new(PREFIX+name)
    bone.head, bone.tail = head, tail
    if parent:
        bone.parent = data.edit_bones[PREFIX+parent]
bpy.ops.object.mode_set(mode='OBJECT')
rig.select_set(False)
rig.show_in_front = True

def blend(a,b,t):
    t = max(0,min(1,t)); t=t*t*(3-2*t)
    return {a:1-t,b:t}

def weights(name,p):
    x,y,z = p
    if name=='ContinuousTrousers':
        side='Left' if x<0 else 'Right'
        if z>.96: return blend(side+'UpperLeg','Hips',(z-.96)/.12)
        return blend(side+'LowerLeg',side+'UpperLeg',(z-.54)/.11)
    if name=='Torso':
        if z<1.29: return blend('Hips','Spine',(z-1.12)/.17)
        return blend('Spine','Chest',(z-1.29)/.15)
    if name=='ContinuousSoftCape':
        if y<.035 or z>1.38: return {'Chest':1}
        if z>1.2: return blend('CapeUpper','Chest',(z-1.2)/.18)
        if z>.99: return blend('CapeMiddle','CapeUpper',(z-.99)/.21)
        if z>.70: return blend('CapeLower','CapeMiddle',(z-.70)/.29)
        return {'CapeLower':1}
    if name=='HoodDrapedCollar': return blend('Chest','Head',(z-1.56)/.16)
    if name=='HoodShell': return blend('Neck','Head',(z-1.585)/.13)
    if name=='ShapedNeck': return blend('Chest','Neck',(z-1.56)/.08)
    if name in ('IvoryMask','MaskEyeLining','MaskShapedFace'): return {'Head':1}
    if name in ('Belt','BeltCenter'): return {'Hips':1}
    if name=='ChestDiamond': return {'Chest':1}
    for side in ['Left','Right']:
        if name.startswith(side):
            if name.endswith(('Mitten','Thumb','WristCuff')): return {side+'Hand':1}
            if name.endswith(('Forearm','ElbowJoint')): return {side+'LowerArm':1}
            if name.endswith('UpperArm'): return {side+'UpperArm':1}
            if name.endswith(('Boot','BootTrim')): return {side+'Foot':1}
    raise ValueError('Missing weights for '+name)

for obj in parts:
    if obj.name=='ContinuousTrousers':
        bpy.context.view_layer.objects.active=obj
        reduction=obj.modifiers.new('Game mesh reduction','DECIMATE'); reduction.ratio=.65
        bpy.ops.object.modifier_apply(modifier=reduction.name)
    obj.vertex_groups.clear()
    groups={}
    for vertex in obj.data.vertices:
        for old,weight in weights(obj.name,vertex.co).items():
            if weight<.00001: continue
            name=PREFIX+mapping[old]
            if name not in groups: groups[name]=obj.vertex_groups.new(name=name)
            groups[name].add([vertex.index],weight,'REPLACE')
bpy.ops.object.select_all(action='DESELECT')
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
body=bpy.context.object; body.name='VeilWalker_MixamoBody'
bpy.context.scene.cursor.location=(0,0,0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
body.parent=rig
modifier=body.modifiers.new('Mixamo skin','ARMATURE'); modifier.object=rig

# Skin into a true horizontal-arm T pose, then make that the bind pose.
for side, sign in [('Left',1),('Right',-1)]:
    origin=rig.pose.bones[PREFIX+side+'Arm'].head.copy()
    for suffix in ['Arm','ForeArm','Hand']:
        bone=rig.pose.bones[PREFIX+side+suffix]
        rest=bone.bone.matrix_local
        direction=(bone.bone.tail_local-bone.bone.head_local).normalized()
        orientation=direction.rotation_difference(Vector((sign,0,0))) @ rest.to_quaternion()
        bone.matrix=Matrix.Translation(origin) @ orientation.to_matrix().to_4x4()
        bpy.context.view_layer.update()
        origin=bone.tail.copy()
    origin=rig.pose.bones[PREFIX+side+'UpLeg'].head.copy()
    for suffix in ['UpLeg','Leg']:
        bone=rig.pose.bones[PREFIX+side+suffix]
        direction=(bone.bone.tail_local-bone.bone.head_local).normalized()
        orientation=direction.rotation_difference(Vector((0,0,-1))) @ bone.bone.matrix_local.to_quaternion()
        bone.matrix=Matrix.Translation(origin) @ orientation.to_matrix().to_4x4()
        bpy.context.view_layer.update()
        origin=bone.tail.copy()
    foot=rig.pose.bones[PREFIX+side+'Foot']
    foot.matrix=Matrix.Translation(origin) @ foot.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
    bpy.context.view_layer.update()
    toe=rig.pose.bones[PREFIX+side+'ToeBase']
    toe.matrix=Matrix.Translation(foot.tail) @ toe.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
    bpy.context.view_layer.update()
bpy.context.view_layer.objects.active=body
bpy.ops.object.modifier_apply(modifier=modifier.name)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
modifier=body.modifiers.new('Mixamo skin','ARMATURE'); modifier.object=rig
rig.select_set(False)

# Independent attachment points; no sword or gun mesh is baked into the character.
sockets=[]
for side in ['Left','Right']:
    hand=rig.data.bones[PREFIX+side+'Hand']
    socket=bpy.data.objects.new('WeaponSocket_'+side,None)
    bpy.context.collection.objects.link(socket)
    socket.parent=rig; socket.parent_type='BONE'; socket.parent_bone=hand.name
    socket.matrix_world=Matrix.Translation((hand.head_local+hand.tail_local)*.5)
    socket.empty_display_size=.035
    sockets.append(socket)
scene=bpy.context.scene
scene.render.fps=30
bpy.context.view_layer.update()
assert len(data.bones)==25
assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in body.data.vertices)
for side in ['Left','Right']:
    for suffix in ['Arm','ForeArm','Hand']:
        bone=data.bones[PREFIX+side+suffix]
        assert abs((bone.tail_local-bone.head_local).normalized().z)<.001

def export(path,objects,types):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types=types,
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,
        use_armature_deform_only=False,apply_unit_scale=True)

export(OUT/'VeilWalker_Mixamo.fbx',[rig,body]+sockets,{'MESH','ARMATURE','EMPTY'})
upload=body.copy(); upload.data=body.data.copy(); bpy.context.collection.objects.link(upload)
upload.name='VeilWalker_MixamoUpload'; upload.parent=None; upload.modifiers.clear(); upload.vertex_groups.clear()
export(OUT/'VeilWalker_MixamoUpload.fbx',[upload],{'MESH'})
bpy.data.objects.remove(upload,do_unlink=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/VeilWalker_Mixamo.blend'))

def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,location,power in [('Key',(-3,-4,5),600),('Fill',(4,-2,3),400),('Rim',(0,3,4),500)]:
    light=bpy.data.lights.new(name,'AREA'); light.energy=power; light.shape='DISK'; light.size=4
    obj=bpy.data.objects.new(name,light); bpy.context.collection.objects.link(obj); obj.location=location; aim(obj,(0,0,1.1))
if scene.world is None: scene.world=bpy.data.worlds.new('Studio')
scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
camera=bpy.data.objects.new('MixamoRigPreview',bpy.data.cameras.new('MixamoRigPreview'))
bpy.context.collection.objects.link(camera); camera.location=(1.2,-7,2.7); aim(camera,(0,0,1.08))
camera.data.type='ORTHO'; camera.data.ortho_scale=2.7; scene.camera=camera
scene.render.engine='CYCLES'; scene.cycles.samples=12; scene.cycles.use_denoising=True
scene.render.resolution_x=1200; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.render.filepath=str(ROOT/'ArtSource/Previews/VeilWalker_Mixamo_TPose.png')
bpy.ops.render.render(write_still=True)
print('MIXAMO_RIG_VALIDATION='+json.dumps({'bones':len(data.bones),'normalized_weights':True,
      't_pose':True,'vertices':len(body.data.vertices),'anatomical_left_positive_blender_x':True,
      'fingers':'mitten mesh; no separate finger chains','original_gameplay_assets_unchanged':True}))
