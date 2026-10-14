"""Production Veil Walker with a two-handed sword loadout; gun is optional."""
import bpy
import os
import math
import json
import sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
ART=os.path.join(ROOT,'ArtSource')
MODELS=os.path.join(ROOT,'Assets','Models','Player')
INCLUDE_GUN='--include-gun' in sys.argv
os.makedirs(MODELS,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(ART,'VeilWalker_Preview09_HoodCollar.blend'))
preview_root=bpy.data.objects['VeilWalker_Preview_Front']
parts=list(preview_root.children)
assert all(obj.type=='MESH' for obj in parts)
for obj in parts:
    local=obj.matrix_basis.copy()
    obj.parent=None
    obj.matrix_basis=local
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
# Strip all presentation duplicates before exporting the approved geometry.
for obj in list(bpy.data.objects):
    if obj not in parts:
        bpy.data.objects.remove(obj,do_unlink=True)

specs=[('Root',(0,0,0),(0,0,.12),None),
    ('Hips',(0,0,1.02),(0,0,1.16),'Root'),
    ('Spine',(0,0,1.16),(0,0,1.35),'Hips'),
    ('Chest',(0,0,1.35),(0,0,1.56),'Spine'),
    ('Neck',(0,0,1.56),(0,0,1.68),'Chest'),
    ('Head',(0,0,1.68),(0,0,1.985),'Neck')]
for side,s in [('Left',-1),('Right',1)]:
    specs.extend([(side+'Shoulder',(s*.04,0,1.53),(s*.196,0,1.51),'Chest'),
        (side+'UpperArm',(s*.196,0,1.51),(s*.284,0,1.215),side+'Shoulder'),
        (side+'LowerArm',(s*.284,0,1.215),(s*.346,-.007,.985),side+'UpperArm'),
        (side+'Hand',(s*.346,-.007,.985),(s*.355,-.021,.882),side+'LowerArm'),
        (side+'UpperLeg',(s*.104,0,1.045),(s*.127,-.014,.59),'Hips'),
        (side+'LowerLeg',(s*.127,-.014,.59),(s*.15,.003,.13),side+'UpperLeg'),
        (side+'Foot',(s*.15,.003,.13),(s*.15,-.13,.045),side+'LowerLeg')])
specs.extend([('CapeUpper',(0,.17,1.46),(0,.2,1.10),'Chest'),
    ('CapeMiddle',(0,.2,1.10),(0,.22,.78),'CapeUpper'),
    ('CapeLower',(0,.22,.78),(0,.24,.45),'CapeMiddle')])
data=bpy.data.armatures.new('VeilWalkerSkeleton')
rig=bpy.data.objects.new('VeilWalkerRig',data)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
rig.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in specs:
    bone=data.edit_bones.new(name); bone.head=head; bone.tail=tail
    if parent: bone.parent=data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT'); rig.select_set(False)
rig.show_in_front=True

def blend(a,b,t):
    t=max(0,min(1,t))
    t=t*t*(3-2*t)
    return {a:1-t,b:t}

def weights(name,p):
    x,y,z=p
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
            if name.endswith(('Mitten','Thumb')): return {side+'Hand':1}
            if name.endswith(('Forearm','ElbowJoint')): return {side+'LowerArm':1}
            if name.endswith('WristCuff'): return {side+'Hand':1}
            if name.endswith('UpperArm'): return {side+'UpperArm':1}
            if name.endswith(('Boot','BootTrim')): return {side+'Foot':1}
    raise ValueError('No skinning rule for '+name)

for obj in parts:
    if obj.name=='ContinuousTrousers':
        bpy.context.view_layer.objects.active=obj
        decimate=obj.modifiers.new('Game mesh reduction','DECIMATE'); decimate.ratio=.65
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    obj.vertex_groups.clear()
    groups={}
    for vertex in obj.data.vertices:
        distribution=weights(obj.name,vertex.co)
        assert abs(sum(distribution.values())-1)<.0001
        for bone_name,weight in distribution.items():
            if weight<.00001: continue
            if bone_name not in groups: groups[bone_name]=obj.vertex_groups.new(name=bone_name)
            groups[bone_name].add([vertex.index],weight,'REPLACE')

bpy.ops.object.select_all(action='DESELECT')
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
body=bpy.context.object; body.name='VeilWalker_SkinnedCharacter'
bpy.context.scene.cursor.location=(0,0,0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
body.parent=rig
mod=body.modifiers.new('Veil Walker skeleton','ARMATURE'); mod.object=rig
bpy.ops.object.select_all(action='DESELECT')

def socket(name,side,rotation):
    obj=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(obj)
    s=-1 if side=='Left' else 1
    obj.location=(s*.352,-.02,.928)
    obj.rotation_euler=rotation
    bpy.context.view_layer.update()
    matrix=obj.matrix_world.copy()
    obj.parent=rig; obj.parent_type='BONE'; obj.parent_bone=side+'Hand'
    obj.matrix_world=matrix; obj.empty_display_size=.045
    return obj

sword_socket=socket('SwordSocket_R','Right',(0,math.radians(125),0))
gun_socket=socket('GunSocket_L','Left',(0,0,0))
swap_sword_socket=socket('SwordSocket_L','Left',(0,math.radians(-125),0))
swap_gun_socket=socket('GunSocket_R','Right',(0,0,0))

if not INCLUDE_GUN:
    # Solve a relaxed two-hand grip, then bake it as the default bind pose so
    # the imported character holds the sword without an animation controller.
    sword_rotation=Vector((math.radians(8),math.radians(20),0))
    from mathutils import Euler
    sword_orientation=Euler(sword_rotation,'XYZ').to_quaternion()
    sword_direction=sword_orientation @ Vector((0,0,1))
    right_palm=Vector((.045,-.25,1.155))
    left_palm=right_palm-sword_direction*.102
    finger_direction=Vector((0,-1,0))
    hand_orientation=finger_direction.to_track_quat('Y','Z')
    for side,s,palm in [('Right',1,right_palm),('Left',-1,left_palm)]:
        upper=rig.pose.bones[side+'UpperArm']
        lower=rig.pose.bones[side+'LowerArm']
        hand=rig.pose.bones[side+'Hand']
        shoulder=upper.head.copy()
        wrist=palm-finger_direction*.057
        delta=wrist-shoulder; distance=delta.length
        l1=upper.bone.length; l2=lower.bone.length
        assert abs(l1-l2)<distance<l1+l2, 'Grip target outside arm reach'
        direction=delta.normalized()
        along=(l1*l1-l2*l2+distance*distance)/(2*distance)
        height=math.sqrt(max(0,l1*l1-along*along))
        bend=Vector((s,-.3,-.15))
        bend=(bend-direction*bend.dot(direction)).normalized()
        elbow=shoulder+direction*along+bend*height
        upper.matrix=Matrix.Translation(shoulder) @ (elbow-shoulder).to_track_quat('Y','Z').to_matrix().to_4x4()
        bpy.context.view_layer.update()
        lower.matrix=Matrix.Translation(elbow) @ (wrist-elbow).to_track_quat('Y','Z').to_matrix().to_4x4()
        bpy.context.view_layer.update()
        hand.matrix=Matrix.Translation(wrist) @ hand_orientation.to_matrix().to_4x4()
        bpy.context.view_layer.update()
    # Preserve the posed vertices and weights before applying pose as rest.
    bpy.context.view_layer.objects.active=body
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Raising the arms changes sleeve clearance. Refit only the mantle's
    # shoulder surface to this held pose so sleeves cannot poke through it.
    cape_materials={i for i,mat in enumerate(body.data.materials) if mat is not None and mat.name=='Veil_Burgundy'}
    cape_faces=[tuple(p.vertices) for p in body.data.polygons
        if p.material_index in cape_materials and min(body.data.vertices[i].co.z for i in p.vertices)<1.6]
    cape_vertices=set(i for face in cape_faces for i in face)
    for side in ['Left','Right']:
        group=body.vertex_groups[side+'UpperArm'].index
        arm_vertices={v.index for v in body.data.vertices if any(g.group==group and g.weight>.9 for g in v.groups)}
        arm_faces=[tuple(p.vertices) for p in body.data.polygons if all(i in arm_vertices for i in p.vertices)]
        tree=BVHTree.FromPolygons([v.co for v in body.data.vertices],arm_faces)
        for index in cape_vertices:
            vertex=body.data.vertices[index]
            if abs(vertex.co.x)<.14 or vertex.co.z<1.35: continue
            point,normal,face,distance=tree.find_nearest(vertex.co)
            if point is None: continue
            signed=(vertex.co-point).dot(normal)
            if signed<.012 and distance<.08:
                vertex.co=point+normal*.012
        body.data.update()
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True); bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT'); rig.select_set(False)
    mod=body.modifiers.new('Veil Walker skeleton','ARMATURE'); mod.object=rig
    bpy.context.view_layer.update()
    sword_socket.matrix_world=Matrix.Translation(right_palm) @ sword_orientation.to_matrix().to_4x4()
    # Retain optional gun sockets at the new hand positions, unequipped.
    gun_socket.matrix_world=Matrix.Translation(left_palm)
    swap_gun_socket.matrix_world=Matrix.Translation(right_palm)
    swap_sword_socket.matrix_world=Matrix.Translation(left_palm) @ sword_orientation.to_matrix().to_4x4()
    support=bpy.data.objects.new('SwordOffhandGrip',None)
    bpy.context.collection.objects.link(support)
    support.parent=sword_socket; support.location=(0,0,-.102)
    support.empty_display_size=.025
    bpy.context.view_layer.update()
    assert (support.matrix_world.translation-left_palm).length<.001
else:
    support=None

def export(path,objects,types):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types=types,
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,
        use_armature_deform_only=False,apply_unit_scale=True)

knight_path=os.path.join(MODELS,'VeilWalker_Model01.fbx')
export(knight_path,[rig,body,sword_socket,gun_socket,swap_sword_socket,swap_gun_socket]+([support] if support else []),{'MESH','ARMATURE','EMPTY'})

# A compact dark-steel pistol, independently exported with its origin in the grip.
gun_parts=[]
def mat(name,color,metal,rough):
    material=bpy.data.materials.new(name); material.use_nodes=True
    material.diffuse_color=(*color,1)
    shader=material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(*color,1)
    shader.inputs['Metallic'].default_value=metal; shader.inputs['Roughness'].default_value=rough
    return material
metal=mat('VeilGun_Steel',(.055,.062,.075),.75,.4)
trim=mat('VeilGun_Bronze',(.32,.22,.105),.65,.43)
grip_mat=mat('VeilGun_Grip',(.02,.013,.015),0,.8)
black=mat('VeilGun_MuzzleDark',(.001,.002,.003),0,1)
def finish_gun(obj,material,bevel=.003):
    obj.data.materials.append(material)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Machined edges','BEVEL'); mod.width=bevel; mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    gun_parts.append(obj)
    return obj
def box(name,location,size,material,rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location,rotation=rotation)
    obj=bpy.context.object; obj.name=name; obj.scale=size
    return finish_gun(obj,material)
def cylinder(name,location,radius,depth,material):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=radius,depth=depth,
        location=location,rotation=(math.pi/2,0,0))
    obj=bpy.context.object; obj.name=name
    return finish_gun(obj,material,.0015)
gun_path=os.path.join(MODELS,'VeilWalker_Pistol01.fbx')
gun=None
if INCLUDE_GUN:
    box('PistolFrame',(0,-.035,.1),(.066,.2,.07),metal)
    box('PistolSlide',(0,-.063,.141),(.06,.225,.034),metal)
    box('PistolGrip',(0,.02,.016),(.046,.068,.155),grip_mat,(math.radians(-12),0,0))
    box('GripButt',(0,.034,-.064),(.05,.074,.013),trim)
    for s in [-1,1]:
        box('ReceiverAccent'+str(s),(s*.034,-.018,.107),(.005,.115,.018),trim)
        for i in range(4):
            box('SlideVent'+str(s)+'_'+str(i),(s*.031,.006-i*.014,.15),(.004,.005,.016),black)
    cylinder('PistolBarrel',(0,-.211,.115),.027,.22,metal)
    cylinder('MuzzleRing',(0,-.323,.115),.032,.018,trim)
    cylinder('MuzzleBore',(0,-.334,.115),.021,.004,black)
    box('FrontSight',(0,-.237,.162),(.012,.017,.018),trim)
    box('RearSight',(0,.022,.168),(.041,.012,.019),metal)
    box('TriggerGuardBottom',(0,-.057,.018),(.014,.094,.012),metal)
    box('TriggerGuardFront',(0,-.101,.048),(.014,.012,.064),metal)
    box('Trigger',(0,-.042,.052),(.01,.018,.039),trim,(math.radians(12),0,0))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in gun_parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=gun_parts[0]
    bpy.ops.object.join()
    gun=bpy.context.object; gun.name='VeilWalker_Pistol'
    bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    muzzle=bpy.data.objects.new('Muzzle',None); bpy.context.collection.objects.link(muzzle)
    muzzle.location=(0,-.338,.115); muzzle.rotation_euler.x=math.pi/2
    muzzle.empty_display_size=.025; muzzle.parent=gun
    export(gun_path,[gun,muzzle],{'MESH','EMPTY'})

# Import the existing approved sword unchanged, then equip only presentation
# copies. Neither weapon geometry is included in the character FBX.
existing=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS,'LeanKnight_Longsword01.fbx'))
sword=next(obj for obj in bpy.data.objects if obj not in existing and obj.type=='MESH')
sword.name='ApprovedLongsword'
def attach(obj,target):
    obj.parent=target; obj.matrix_parent_inverse=Matrix.Identity(4)
    obj.matrix_basis=Matrix.Identity(4)
attach(sword,sword_socket)
if gun: attach(gun,gun_socket)
bpy.context.view_layer.update()

# A real equipped render: front and back views of the production rig.
present=bpy.data.collections.new('Presentation'); bpy.context.scene.collection.children.link(present)
rear_rig=rig.copy(); rear_rig.data=rig.data.copy()
bpy.context.collection.objects.link(rear_rig)
rear_rig.name='Preview_Back_Rig'
rear_rig.location=(.78,.06,0); rear_rig.rotation_euler.z=math.radians(166)
rear_body=body.copy(); rear_body.data=body.data.copy()
bpy.context.collection.objects.link(rear_body); rear_body.parent=rear_rig
rear_body.modifiers[0].object=rear_rig
rig.location=(-.62,0,0); rig.rotation_euler.z=math.radians(-8)
floor_mat=mat('StudioFloor',(.22,.20,.18),0,.92)
box('PreviewGround',(0,0,-.034),(200,200,.04),floor_mat)
world=bpy.context.scene.world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.17,.16,.15,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,location,energy,color,size in [
    ('Key',(-3,-4,5),430,(.87,.93,1),4),('Fill',(3,-3,3),220,(1,.88,.76),4),('Rim',(0,3,4),420,(1,.83,.72),3)]:
    light=bpy.data.lights.new(name,'AREA'); light.energy=energy; light.color=color; light.size=size
    obj=bpy.data.objects.new(name,light); bpy.context.collection.objects.link(obj); obj.location=location; aim(obj,(0,0,1.1))
camera_data=bpy.data.cameras.new('VeilWalker_EquippedPreview')
camera=bpy.data.objects.new('VeilWalker_EquippedPreview',camera_data)
bpy.context.collection.objects.link(camera)
camera.location=(2.2,-8,2.85); camera_data.type='ORTHO'; camera_data.ortho_scale=3.9
aim(camera,(.18,0,1.16))
scene=bpy.context.scene; scene.camera=camera
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=16; scene.cycles.use_denoising=True
scene.render.resolution_x=1536; scene.render.resolution_y=1024; scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'; scene.render.image_settings.file_format='PNG'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART,'VeilWalker_Model01.blend'))
scene.render.filepath=os.path.join(ART,'Previews','VeilWalker_DualWeapon_Model01.png' if INCLUDE_GUN else 'VeilWalker_TwoHandSword_Model01.png')
bpy.ops.render.render(write_still=True)
assert len(data.bones)==23
assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in body.data.vertices)
print('VEIL_WALKER_VALIDATION='+json.dumps({'bones':len(data.bones),'vertices':len(body.data.vertices),
    'polygons':len(body.data.polygons),'all_weights_normalized':True,'weapon_sockets':4,
    'sword_is_existing_asset':True,'gun_equipped':INCLUDE_GUN,'default_two_hand_grip':not INCLUDE_GUN,
    'character_fbx':knight_path,'optional_gun_fbx':gun_path}))
