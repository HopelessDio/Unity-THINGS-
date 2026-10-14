"""Lean concept knight with rigid armor skinning and an independent sword."""
import bpy
import math
import os
import json
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MODEL = os.path.join(ROOT, 'Assets', 'Models', 'Player')
ART = os.path.join(ROOT, 'ArtSource')
os.makedirs(MODEL, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, metal=0, rough=0.7):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metal
    shader.inputs['Roughness'].default_value = rough
    noise = mat.node_tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 95
    noise.inputs['Detail'].default_value = 3
    bump = mat.node_tree.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.12
    bump.inputs['Distance'].default_value = 0.002
    mat.node_tree.links.new(noise.outputs['Fac'], bump.inputs['Height'])
    mat.node_tree.links.new(bump.outputs['Normal'], shader.inputs['Normal'])
    return mat

steel = material('LeanKnight_SlateSteel', (0.062, 0.073, 0.09), 0.68, 0.52)
bronze = material('LeanKnight_AgedBronze', (0.3, 0.23, 0.13), 0.7, 0.45)
cloth = material('LeanKnight_CharcoalCloth', (0.026, 0.023, 0.026), 0, 0.95)
cape = material('LeanKnight_CrimsonCape', (0.19, 0.042, 0.039), 0, 0.9)
black = material('LeanKnight_Visor', (0.003, 0.004, 0.006), 0.1, 0.6)
blade_mat = material('LeanKnight_BladeSteel', (0.42, 0.46, 0.5), 0.83, 0.3)
parts = []
sword_parts = []

def finish(obj, mat, bone=None, bevel=0.004, sword=False):
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Armor edge bevel', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if bone:
        obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    (sword_parts if sword else parts).append(obj)
    obj.select_set(False)
    return obj

def mesh(name, vertices, faces, mat, bone=None, bevel=0.003, sword=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, bone, bevel, sword)

def box(name, pos, size, mat, bone=None, bevel=0.004, sword=False):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    return finish(obj, mat, bone, bevel, sword)

def volume(name, rings, mat, bone, sides=10):
    vertices = []
    for x, y, z, rx, ry in rings:
        for i in range(sides):
            angle = 2 * math.pi * i / sides
            vertices.append((x + rx * math.cos(angle), y + ry * math.sin(angle), z))
    faces = [tuple(reversed(range(sides)))]
    for row in range(len(rings) - 1):
        for i in range(sides):
            a = row * sides + i
            b = row * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range((len(rings) - 1) * sides, len(rings) * sides)))
    return mesh(name, vertices, faces, mat, bone)

def panel(name, outline, mat, bone, depth=0.012, trim=False):
    # Outline lies in world coordinates; thickness goes along Y.
    vertices = [(x, y-depth/2, z) for x,y,z in outline] + [(x,y+depth/2,z) for x,y,z in outline]
    n = len(outline)
    faces = [tuple(reversed(range(n))), tuple(range(n, n*2))]
    faces += [(i, (i+1)%n, (i+1)%n+n, i+n) for i in range(n)]
    obj = mesh(name, vertices, faces, mat, bone)
    if trim:
        for i in range(n):
            a, b = Vector(outline[i]), Vector(outline[(i+1)%n])
            rail(name+'Trim'+str(i), a+Vector((0,-depth,0)), b+Vector((0,-depth,0)), 0.004, bronze, bone)
    return obj

def rail(name, start, end, radius, mat, bone=None, sword=False):
    a, b = Vector(start), Vector(end)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=(b-a).length, location=(a+b)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (b-a).to_track_quat('Z','Y').to_euler()
    return finish(obj, mat, bone, 0.001, sword)

bone_specs = [('Root',(0,0,0),(0,0,0.12),None),
    ('Hips',(0,0,0.98),(0,0,1.15),'Root'), ('Spine',(0,0,1.15),(0,0,1.36),'Hips'),
    ('Chest',(0,0,1.36),(0,0,1.58),'Spine'), ('Neck',(0,0,1.58),(0,0,1.7),'Chest'),
    ('Head',(0,0,1.7),(0,0,1.99),'Neck')]
for side,s in [('Left',-1),('Right',1)]:
    bone_specs += [(side+'UpperLeg',(s*.12,0,1.03),(s*.135,0,.58),'Hips'),
        (side+'LowerLeg',(s*.135,0,.58),(s*.15,0,.13),side+'UpperLeg'),
        (side+'Foot',(s*.15,0,.13),(s*.15,-.17,.055),side+'LowerLeg'),
        (side+'Shoulder',(s*.04,0,1.55),(s*.22,0,1.54),'Chest'),
        (side+'UpperArm',(s*.22,0,1.54),(s*.34,0,1.26),side+'Shoulder'),
        (side+'LowerArm',(s*.34,0,1.26),(s*.4,-.015,1.02),side+'UpperArm'),
        (side+'Hand',(s*.4,-.015,1.02),(s*.41,-.025,.9),side+'LowerArm')]
arm = bpy.data.armatures.new('LeanKnightSkeleton')
rig = bpy.data.objects.new('LeanKnightRig', arm)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in bone_specs:
    b=arm.edit_bones.new(name)
    b.head=head
    b.tail=tail
    if parent:
        b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
rig.select_set(False)
rig.show_in_front=True

# Lean tapered torso, angular chest plates and a narrow belt.
volume('BodyCloth',[(0,0,1.08,.145,.092),(0,0,1.32,.16,.095),(0,0,1.56,.21,.1)],cloth,'Chest')
volume('Cuirass',[(0,0,1.16,.135,.09),(0,0,1.32,.165,.112),(0,0,1.51,.21,.118),(0,0,1.58,.16,.09)],steel,'Chest',12)
panel('Breastplate', [(-.185,-.101,1.5),(-.13,-.139,1.31),(0,-.151,1.2),(.13,-.139,1.31),(.185,-.101,1.5),(0,-.135,1.56)],steel,'Chest',trim=True)
rail('ChestRidge',(0,-.153,1.23),(0,-.141,1.53),.005,bronze,'Chest')
volume('Belt',[(0,0,1.09,.151,.108),(0,0,1.14,.15,.107)],cloth,'Hips',12)
box('Buckle',(0,-.119,1.12),(.055,.013,.033),bronze,'Hips')
volume('Gorget',[(0,0,1.57,.11,.085),(0,0,1.65,.086,.07)],steel,'Neck')

for side,s in [('Left',-1),('Right',1)]:
    upper=side+'UpperLeg'; lower=side+'LowerLeg'; foot=side+'Foot'
    volume(side+'ThighCloth',[(s*.13,0,.62,.068,.072),(s*.12,0,1.04,.091,.084)],cloth,upper)
    panel(side+'HipGuard',[(s*.06,-.112,1.07),(s*.22,-.1,1.04),(s*.24,-.117,.81),(s*.11,-.135,.76)],steel,upper,trim=True)
    panel(side+'HipGuardLower',[(s*.1,-.145,.94),(s*.22,-.132,.95),(s*.24,-.135,.79),(s*.12,-.149,.73)],steel,upper,trim=True)
    volume(side+'Shin',[(s*.15,0,.16,.047,.062),(s*.142,0,.36,.068,.069),(s*.135,0,.55,.06,.071)],steel,lower)
    panel(side+'Knee',[(s*.135-.055,-.08,.65),(s*.135+.055,-.08,.65),(s*.135+.048,-.1,.56),(s*.135,-.125,.515),(s*.135-.048,-.1,.56)],steel,lower,trim=True)
    rail(side+'ShinRidge',(s*.15,-.072,.18),(s*.14,-.077,.5),.0035,bronze,lower)
    volume(side+'Ankle',[(s*.15,0,.08,.054,.069),(s*.15,0,.19,.052,.068)],cloth,foot)
    volume(side+'Boot',[(s*.15,-.074,.035,.064,.155),(s*.15,-.075,.085,.061,.14),(s*.15,-.026,.145,.049,.075)],steel,foot,8)
    volume(side+'Sole',[(s*.15,-.074,.01,.065,.154),(s*.15,-.074,.035,.065,.154)],black,foot,8)
    # Sloping simple angular pauldrons and separated elbow/wrist segments.
    upperarm=side+'UpperArm'; fore=side+'LowerArm'; hand=side+'Hand'
    volume(side+'ShoulderDome',[(s*.255,0,1.445,.08,.1),(s*.255,0,1.54,.092,.116),(s*.245,0,1.605,.047,.06)],steel,side+'Shoulder',10)
    panel(side+'Pauldron',[(s*.14,-.116,1.57),(s*.24,-.13,1.61),(s*.33,-.121,1.54),(s*.34,-.14,1.455),(s*.185,-.143,1.49)],steel,side+'Shoulder',depth=.025,trim=True)
    rail(side+'Sleeve',(s*.23,0,1.49),(s*.335,0,1.27),.059,cloth,upperarm)
    panel(side+'UpperArmPlate',[(s*.245-.045,-.064,1.48),(s*.245+.045,-.064,1.48),(s*.335+.04,-.072,1.29),(s*.335-.04,-.072,1.29)],steel,upperarm)
    box(side+'Elbow',(s*.34,0,1.255),(.115,.115,.08),steel,fore,.018)
    rail(side+'Forearm',(s*.35,0,1.23),(s*.4,-.015,1.025),.044,cloth,fore)
    panel(side+'Bracer',[(s*.355-.051,-.056,1.23),(s*.355+.051,-.056,1.23),(s*.4+.038,-.062,1.05),(s*.4,-.08,1.025),(s*.4-.038,-.062,1.05)],steel,fore,trim=True)
    box(side+'Gauntlet',(s*.406,-.018,.985),(.078,.064,.094),steel,hand,.012)
    for finger in range(4):
        x=s*.406+(finger-1.5)*.017
        box(side+'Finger'+str(finger),(x,-.023,.921),(.013,.045,.056),steel,hand,.005)
    box(side+'Thumb',(s*.406-s*.051,-.046,.964),(.025,.041,.059),steel,hand,.006)

# Distinctive closed angular helmet, dark T visor and bronze framing.
volume('HelmetShell',[(0,0,1.68,.076,.085),(0,0,1.8,.097,.1),(0,0,1.94,.091,.098),(0,.005,2.015,.034,.056)],steel,'Head',12)
panel('FaceShield',[(-.086,-.09,1.86),(-.075,-.105,1.71),(0,-.132,1.67),(.075,-.105,1.71),(.086,-.09,1.86),(0,-.126,1.88)],steel,'Head',trim=True)
box('EyeVisor',(0,-.119,1.887),(.167,.009,.025),black,'Head',.002)
rail('BrowTrim',(-.091,-.124,1.909),(.091,-.124,1.909),.005,bronze,'Head')
rail('VisorBaseTrim',(-.083,-.127,1.87),(.083,-.127,1.87),.004,bronze,'Head')
rail('FaceCenterTrim',(0,-.141,1.683),(0,-.14,1.87),.004,bronze,'Head')
mesh('HelmetCrest',[(-.009,-.06,1.985),(-.009,-.012,2.078),(-.009,.06,2.04),(-.009,.095,1.945),
    (.009,-.06,1.985),(.009,-.012,2.078),(.009,.06,2.04),(.009,.095,1.945)],
    [(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],steel,'Head')

# Ragged cloth with broad modeled folds; cape kept short for readable legs.
for s in [-1,1]:
    panel('FrontTabard'+str(s),[(s*.015,-.119,1.09),(s*.105,-.109,1.09),(s*.13,-.139,.73),(s*.08,-.151,.70),(s*.045,-.153,.74),(s*.012,-.146,.68)],cloth,'Hips',depth=.008)
for i in range(3):
    left=-.23+i*.15
    vertices=[]
    rows=10; columns=6
    for row in range(rows):
        t=row/(rows-1)
        for col in range(columns):
            u=col/(columns-1)
            x=left + (u-.5)*.015*t + .157*u
            y=.119+.10*t+.014*math.sin(u*math.pi*2+.6*i)*(.3+t)
            z=1.605-.48*t
            if row==rows-1:
                z-= .027*(col%2)+ .022*math.sin(col*3.1+i)
            vertices.append((x,y,z))
    faces=[]
    for row in range(rows-1):
        for col in range(columns-1):
            a=row*columns+col
            faces.append((a,a+1,a+columns+1,a+columns))
    obj=mesh('CapePanel'+str(i),vertices,faces,cape,'Chest',bevel=0)
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Cloth thickness','SOLIDIFY'); mod.thickness=.006
    bpy.ops.object.modifier_apply(modifier=mod.name)
panel('ShoulderScarf',[(-.22,-.026,1.59),(-.12,-.125,1.615),(.15,-.12,1.6),(.24,-.035,1.54),(.2,-.12,1.47),(.03,-.143,1.54),(-.17,-.129,1.55)],cape,'Chest',depth=.018)
box('CapeClasp',(-.17,-.145,1.577),(.045,.017,.045),bronze,'Chest',.014)

def join(objects,name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=name
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    obj.select_set(False)
    return obj

knight=join(parts,'LeanKnight_ArmorAndCloth')
knight.parent=rig
mod=knight.modifiers.new('Knight skeleton','ARMATURE')
mod.object=rig
socket=bpy.data.objects.new('SwordSocket_R',None)
bpy.context.collection.objects.link(socket)
socket.location=(.405,-.025,.965)
socket.rotation_euler=(math.pi/2,0,0)
bpy.context.view_layer.update()
matrix=socket.matrix_world.copy()
socket.parent=rig
socket.parent_type='BONE'
socket.parent_bone='RightHand'
socket.matrix_world=matrix
socket.empty_display_size=.075

# Sword origin is the center of the grip. Blade extends +Z in Blender / +Y in Unity.
rail('SwordGrip',(0,0,-.12),(0,0,.12),.024,cloth,sword=True)
for i in range(9):
    bpy.ops.mesh.primitive_torus_add(major_segments=12,minor_segments=4,major_radius=.024,minor_radius=.002,location=(0,0,-.1+i*.025))
    finish(bpy.context.object,black,sword=True,bevel=0)
box('SwordGuard',(0,0,.145),(.32,.034,.033),bronze,sword=True)
vertices=[]
for z,w in [(.165,.045),(1.09,.028),(1.25,0)]:
    vertices += [(-w,0,z),(0,-.012,z),(w,0,z),(0,.012,z)]
faces=[(0,3,2,1)]
for row in range(2):
    for i in range(4): faces.append((row*4+i,row*4+(i+1)%4,(row+1)*4+(i+1)%4,(row+1)*4+i))
mesh('SwordBlade',vertices,faces,blade_mat,sword=True,bevel=.0008)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.047,location=(0,0,-.16))
finish(bpy.context.object,bronze,sword=True,bevel=.001)
sword=join(sword_parts,'LeanKnight_Longsword')

def export(path,objects,types):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types=types,
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,
        use_armature_deform_only=False,apply_unit_scale=True)

knight_path=os.path.join(MODEL,'LeanKnight_Model01.fbx')
sword_path=os.path.join(MODEL,'LeanKnight_Longsword01.fbx')
export(knight_path,[rig,knight,socket],{'ARMATURE','MESH','EMPTY'})
export(sword_path,[sword],{'MESH'})

# Front/rear presentation duplicates are added only after exports.
sword.location=(1.07,0,.32)
rear=knight.copy()
rear.data=knight.data.copy()
rear.parent=None
rear.modifiers.clear()
bpy.context.collection.objects.link(rear)
rear.location=(.48,.27,0)
rear.rotation_euler.z=math.pi
rig.location=(-.55,0,0)
floor_mat=material('StudioFloor',(.19,.175,.16),0,.9)
box('StudioGround',(0,0,-.04),(200,200,.04),floor_mat)
world=bpy.context.scene.world
world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.16,.15,.14,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65

def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,pos,energy,color,size in [
    ('Key',(-3,-4,5),420,(.82,.89,1),4),
    ('Fill',(3,-2,3),210,(1,.9,.79),3),
    ('Rim',(0,3,4),420,(1,.8,.62),3)]:
    data=bpy.data.lights.new(name,'AREA'); data.energy=energy; data.color=color; data.size=size
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj); obj.location=pos; aim(obj,(0,0,1))
data=bpy.data.cameras.new('KnightPreview')
camera=bpy.data.objects.new('KnightPreview',data)
bpy.context.collection.objects.link(camera)
camera.location=(3.0,-8,3.05)
data.type='ORTHO'; data.ortho_scale=3.8
aim(camera,(.1,0,1.06))
scene=bpy.context.scene; scene.camera=camera
scene.render.engine='BLENDER_EEVEE'
scene.eevee.taa_render_samples=32
scene.render.resolution_x=1536; scene.render.resolution_y=1024
scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
scene.render.image_settings.file_format='PNG'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART,'LeanKnight_Model01.blend'))
scene.render.filepath=os.path.join(ART,'Previews','LeanKnight_Model01.png')
bpy.ops.render.render(write_still=True)
assert set(vg.name for vg in knight.vertex_groups).issubset(set(arm.bones.keys()))
assert all(len(v.groups)>0 for v in knight.data.vertices), 'Unweighted knight vertices'
report={'knight_vertices':len(knight.data.vertices),'knight_polygons':len(knight.data.polygons),
    'sword_vertices':len(sword.data.vertices),'bones':len(arm.bones),'all_vertices_weighted':True,
    'sword_is_separate':True,'knight_fbx':knight_path,'sword_fbx':sword_path}
print('VALIDATION='+json.dumps(report))
