"""Build the approved simple zombie, its skin, and two in-place generic clips.

Run with Blender --background --python this_file. Never edits the player or map.
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Models/Enemies/Ashbound'
PREVIEW = ROOT / 'ArtSource/Previews/Ashbound_Zombie_Blender01.png'
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.fps = 30
parts = []


def material(name, color, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Roughness'].default_value = .82
    if emission:
        node.inputs['Emission Color'].default_value = (*color, 1)
        node.inputs['Emission Strength'].default_value = emission
    return mat


skin = material('Ashbound_Skin', (.36, .345, .31))
cloth = material('Ashbound_Tunic', (.045, .043, .047))
trousers = material('Ashbound_Trousers', (.022, .024, .029))
belt = material('Ashbound_Burgundy', (.19, .025, .033))
boots = material('Ashbound_Boots', (.055, .038, .031))
socket = material('Ashbound_Sockets', (.006, .004, .006))
eyes = material('Ashbound_Eyes', (.75, .012, .007), 1.5)

specs = [('Hips', (0, 0, 1.02), (0, 0, 1.19), None),
         ('Spine', (0, 0, 1.19), (0, -.025, 1.42), 'Hips'),
         ('Chest', (0, -.025, 1.42), (0, -.065, 1.64), 'Spine'),
         ('Neck', (0, -.065, 1.64), (0, -.135, 1.76), 'Chest'),
         ('Head', (0, -.135, 1.76), (0, -.155, 2.12), 'Neck')]
for side, sign in [('Left', 1), ('Right', -1)]:
    specs += [(side+'Shoulder', (sign*.055, -.055, 1.60), (sign*.265, -.055, 1.61), 'Chest'),
              (side+'UpperArm', (sign*.265, -.055, 1.61), (sign*.365, -.085, 1.29), side+'Shoulder'),
              (side+'ForeArm', (sign*.365, -.085, 1.29), (sign*.425, -.16, 1.005), side+'UpperArm'),
              (side+'Hand', (sign*.425, -.16, 1.005), (sign*.448, -.18, .865), side+'ForeArm'),
              (side+'Thigh', (sign*.135, 0, 1.02), (sign*.155, -.045, .58), 'Hips'),
              (side+'Shin', (sign*.155, -.045, .58), (sign*.163, .005, .145), side+'Thigh'),
              (side+'Foot', (sign*.163, .005, .145), (sign*.163, -.145, .07), side+'Shin')]
armature = bpy.data.armatures.new('AshboundSkeleton')
rig = bpy.data.objects.new('AshboundRig', armature)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, head, tail, parent in specs:
    bone = armature.edit_bones.new(name)
    bone.head, bone.tail = head, tail
    if parent:
        bone.parent = armature.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
rig.select_set(False)
rig.show_in_front = True


def smoothstep(a, b, x):
    t = max(0, min(1, (x-a)/(b-a)))
    return t*t*(3-2*t)


def blend(a, b, t):
    return {a: 1-t, b: t}


def torso_weights(p):
    if p.z < 1.18:
        return {'Hips': 1}
    if p.z < 1.40:
        return blend('Hips', 'Spine', smoothstep(1.18, 1.40, p.z))
    return blend('Spine', 'Chest', smoothstep(1.40, 1.55, p.z))


def finish(obj, mat, weights, subdiv=0):
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for face in obj.data.polygons:
        face.use_smooth = True
    if subdiv:
        mod = obj.modifiers.new('Soft contours', 'SUBSURF')
        mod.levels = subdiv
        bpy.ops.object.modifier_apply(modifier=mod.name)
    groups = {}
    for vertex in obj.data.vertices:
        influences = weights(vertex.co) if callable(weights) else {weights: 1}
        for name, weight in influences.items():
            if weight <= 0:
                continue
            if name not in groups:
                groups[name] = obj.vertex_groups.new(name=name)
            groups[name].add([vertex.index], weight, 'REPLACE')
    obj.select_set(False)
    parts.append(obj)
    return obj


def ellipsoid(name, center, scale, mat, weights, rotation=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    if rotation:
        obj.rotation_euler = rotation
    return finish(obj, mat, weights)


def rings(name, rows, mat, weights, n=24, subdiv=1, hem=False):
    # Rows are (centre x,y,z; x radius; y radius). Open garment edges get solidify.
    vertices, faces = [], []
    for row, (x, y, z, rx, ry) in enumerate(rows):
        for i in range(n):
            angle = 2*math.pi*i/n
            dz = (-.075 if i % 4 == 0 else 0) if hem and row == 0 else 0
            vertices.append((x+rx*math.cos(angle), y+ry*math.sin(angle), z+dz))
    for row in range(len(rows)-1):
        for i in range(n):
            j = (i+1) % n
            faces.append((row*n+i, row*n+j, (row+1)*n+j, (row+1)*n+i))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, weights, subdiv)


def tube(name, centers, radii, mat, weights, n=16):
    vertices, faces = [], []
    for row, center in enumerate(centers):
        c = Vector(center)
        tangent = Vector(centers[min(row+1, len(centers)-1)]) - Vector(centers[max(0, row-1)])
        tangent.normalize()
        u = Vector((0, 1, 0)).cross(tangent).normalized()
        v = tangent.cross(u).normalized()
        rx, ry = radii[row]
        for i in range(n):
            angle = 2*math.pi*i/n
            vertices.append(c + u*(rx*math.cos(angle)) + v*(ry*math.sin(angle)))
    for row in range(len(centers)-1):
        for i in range(n):
            j = (i+1) % n
            faces.append((row*n+i, row*n+j, (row+1)*n+j, (row+1)*n+i))
    faces += [tuple(reversed(range(n))), tuple((len(centers)-1)*n+i for i in range(n))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, weights, 1)


# The tunic covers the limb joins, but arms/legs are each continuous surfaces.
tunic = rings('SleevelessTunic', [(0,.01,.77,.27,.175), (0,.01,.80,.27,.175),
    (0,0,.96,.235,.15), (0,0,1.10,.205,.135), (0,-.015,1.29,.235,.155),
    (0,-.035,1.48,.275,.17), (0,-.055,1.59,.278,.155),
    (0,-.07,1.64,.235,.135), (0,-.085,1.68,.11,.088),
    (0,-.085,1.685,.108,.086)], cloth, torso_weights, hem=True)
# Solidify retained on source; evaluated on export, avoiding an infinitely thin hem.
solid = tunic.modifiers.new('Cloth edge thickness', 'SOLIDIFY')
solid.thickness = .009
solid.offset = 0
rings('BurgundyBelt', [(0,-.002,1.045,.218,.147), (0,-.002,1.05,.221,.149),
      (0,-.002,1.135,.218,.149), (0,-.002,1.14,.214,.146)], belt, 'Hips')
tube('BeltTail', [(-.075,-.158,1.075),(-.075,-.18,1.02),(-.087,-.19,.89),(-.105,-.205,.78)],
     [(.041,.008),(.041,.008),(.039,.008),(.036,.007)], belt, 'Hips', n=8)
ellipsoid('Neck', (0,-.125,1.68), (.086,.08,.09), skin, 'Neck')
head = rings('ShapedBaldHead', [(0,-.16,1.736,.026,.036), (0,-.18,1.76,.069,.072),
    (0,-.172,1.81,.105,.096), (0,-.152,1.88,.135,.116),
    (0,-.148,1.97,.148,.129), (0,-.135,2.055,.135,.122),
    (0,-.125,2.115,.095,.095), (0,-.125,2.14,.038,.037),
    (0,-.125,2.145,.002,.002)], skin, 'Head', subdiv=2)
for side, sign in [('Left',1),('Right',-1)]:
    ellipsoid(side+'Ear', (sign*.143,-.115,1.917), (.023,.028,.046), skin, 'Head')
    ellipsoid(side+'EyeSocket', (sign*.063,-.265,1.948), (.044,.009,.025), socket, 'Head', (0,sign*.20,0))
    ellipsoid(side+'Eye', (sign*.064,-.275,1.946), (.008,.003,.008), eyes, 'Head')
    ellipsoid(side+'Brow', (sign*.065,-.248,1.979), (.059,.016,.017), skin, 'Head', (0,sign*.15,0))
ellipsoid('NoseBridge', (0,-.265,1.919), (.019,.023,.049), skin, 'Head')
ellipsoid('NoseTip', (0,-.286,1.881), (.023,.018,.015), skin, 'Head')
ellipsoid('MouthCrease', (0,-.267,1.831), (.047,.007,.007), socket, 'Head')
for part in parts:
    if part.vertex_groups.get('Head'):
        # Tuck the skull into the short neck and slightly forward, not perched on a stalk.
        for vertex in part.data.vertices:
            vertex.co.y -= .025
            vertex.co.z -= .045

for side, sign in [('Left',1),('Right',-1)]:
    def arm_weights(p, side=side):
        if p.z > 1.42:
            return {side+'UpperArm':1}
        if p.z > 1.17:
            return blend(side+'ForeArm',side+'UpperArm',smoothstep(1.235,1.345,p.z))
        return blend(side+'Hand',side+'ForeArm',smoothstep(.99,1.06,p.z))
    tube(side+'ContinuousArm', [(sign*.20,-.05,1.57),(sign*.265,-.055,1.565),
        (sign*.31,-.063,1.50),(sign*.348,-.078,1.36),(sign*.365,-.085,1.29),
        (sign*.386,-.11,1.20),(sign*.415,-.148,1.065),(sign*.425,-.16,1.005),
        (sign*.438,-.174,.947),(sign*.448,-.18,.90)],
        [(.075,.075),(.11,.105),(.095,.09),(.068,.065),(.059,.061),
         (.072,.066),(.047,.043),(.038,.032),(.06,.032),(.049,.028)], skin, arm_weights)
    # Simple fingers, individually rounded and gently curled, not detailed nails.
    for finger in range(4):
        x = sign*(.414+finger*.022)
        length = [.064,.078,.072,.057][finger]
        tube(side+'Finger'+str(finger), [(x,-.18,.924),(x+sign*.002,-.186,.895),
             (x+sign*.004,-.215,.895-length*.72),(x+sign*.004,-.243,.895-length)],
             [(.013,.014),(.013,.014),(.012,.012),(.007,.007)], skin, side+'Hand', n=10)
    tube(side+'Thumb', [(sign*.404,-.18,.97),(sign*.379,-.19,.94),
         (sign*.376,-.22,.904),(sign*.389,-.235,.895)],
         [(.02,.02),(.019,.019),(.015,.015),(.009,.009)], skin, side+'Hand', n=12)

    def leg_weights(p, side=side):
        if p.z > .91:
            return blend(side+'Thigh','Hips',smoothstep(.94,1.055,p.z))
        return blend(side+'Shin',side+'Thigh',smoothstep(.53,.65,p.z))
    rings(side+'ContinuousLeg', [(sign*.163,.005,.16,.054,.057),
        (sign*.163,.003,.24,.059,.062),(sign*.161,-.005,.36,.081,.078),
        (sign*.158,-.03,.49,.077,.075),(sign*.155,-.045,.58,.071,.073),
        (sign*.15,-.03,.69,.095,.09),(sign*.142,-.01,.86,.108,.10),
        (sign*.135,0,1.025,.106,.10),(sign*.13,0,1.08,.092,.085)], trousers, leg_weights)
    boot = rings(side+'Boot', [(sign*.163,-.053,.008,.079,.16), (sign*.163,-.054,.018,.09,.175),
        (sign*.163,-.054,.065,.09,.175),(sign*.163,-.045,.115,.081,.15),
        (sign*.163,-.008,.17,.064,.085),(sign*.163,.002,.255,.064,.065),
        (sign*.163,.002,.267,.064,.065)], boots, side+'Foot')
    for vertex in boot.data.vertices:
        if vertex.co.z < .035:
            vertex.co.z = .015

bpy.ops.object.select_all(action='DESELECT')
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
body = bpy.context.object
body.name = 'AshboundBody'
bpy.context.scene.cursor.location = (0,0,0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
# Solidify from the active garment applies to the joined mesh; remove and use
# double-sided game material instead so it cannot thicken eyes and fingers.
body.modifiers.clear()
body.parent = rig
mod = body.modifiers.new('Ashbound Skin', 'ARMATURE')
mod.object = rig
assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in body.data.vertices)


def orient(name, head, tail):
    bone = rig.pose.bones[name]
    rest = bone.bone
    direction = (Vector(tail)-Vector(head)).normalized()
    rotation = (rest.tail_local-rest.head_local).normalized().rotation_difference(direction)
    bone.matrix = Matrix.Translation(Vector(head)) @ (rotation @ rest.matrix_local.to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()


def leg_pose(side, target):
    upper = rig.pose.bones[side+'Thigh']
    lower = rig.pose.bones[side+'Shin']
    hip = upper.head.copy()
    target = Vector(target)
    delta = target-hip
    distance = min(delta.length, upper.length+lower.length-.001)
    direction = delta.normalized()
    bend = Vector((0,-1,0))
    bend = (bend-direction*bend.dot(direction)).normalized()
    along = (upper.length**2-lower.length**2+distance**2)/(2*distance)
    height = math.sqrt(max(0,upper.length**2-along**2))
    knee = hip+direction*along+bend*height
    orient(side+'Thigh', hip, knee)
    orient(side+'Shin', knee, target)
    foot = rig.pose.bones[side+'Foot']
    foot.matrix = Matrix.Translation(target) @ foot.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
    bpy.context.view_layer.update()


for clip in ['Ashbound_Idle', 'Ashbound_Shuffle']:
    action = bpy.data.actions.new(clip)
    rig.animation_data_create()
    rig.animation_data.action = action
    for frame in range(1, 61 if clip.endswith('Idle') else 33):
        phase = (frame-1)/(60 if clip.endswith('Idle') else 31)
        for bone in rig.pose.bones:
            bone.rotation_mode = 'QUATERNION'
            bone.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        moving = clip.endswith('Shuffle')
        bob = .012*math.sin(phase*4*math.pi) if moving else .005*math.sin(phase*2*math.pi)
        hips = rig.pose.bones['Hips']
        hips.matrix = Matrix.Translation((0,0,bob)) @ hips.bone.matrix_local
        bpy.context.view_layer.update()
        for side, sign, offset in [('Left',1,0),('Right',-1,.5)]:
            t = (phase+offset) % 1
            foot_y, lift = .005, 0
            if moving:
                if t < .62:
                    foot_y = -.235 + .48*t/.62
                else:
                    u = (t-.62)/.38
                    foot_y = .245 - .48*(u*u*(3-2*u))
                    lift = .08*math.sin(math.pi*u)
            leg_pose(side, (sign*.163, foot_y, .145+lift))
            # Arms remain low and loosely reaching; no frantic human sprint.
            arm = rig.pose.bones[side+'UpperArm']
            rest = arm.bone
            angle = math.radians(7*math.sin(phase*2*math.pi+offset*2*math.pi) if moving else 1.2*math.sin(phase*2*math.pi))
            world_rot = Matrix.Rotation(angle,4,'X')
            arm.matrix = Matrix.Translation(arm.head) @ world_rot @ rest.matrix_local.to_quaternion().to_matrix().to_4x4()
            bpy.context.view_layer.update()
        for bone in rig.pose.bones:
            bone.keyframe_insert('location', frame=frame, group=bone.name)
            bone.keyframe_insert('rotation_quaternion', frame=frame, group=bone.name)
            bone.keyframe_insert('scale', frame=frame, group=bone.name)
    action.use_fake_user = True
    action['description'] = 'In-place, no gameplay root motion; grounded feet; generic skeleton.'

rig.animation_data.action = bpy.data.actions['Ashbound_Idle']
scene.frame_set(1)
scene.frame_start, scene.frame_end = 1, 60
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'AshboundZombie.fbx'), use_selection=True,
    object_types={'MESH','ARMATURE'}, axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
    use_armature_deform_only=True, apply_unit_scale=True)

metadata = {'height': round(max(v.co.z for v in body.data.vertices),3),
            'vertices': len(body.data.vertices), 'triangles': sum(len(p.vertices)-2 for p in body.data.polygons),
            'bones': len(armature.bones), 'normalizedWeights': True,
            'clips': ['Ashbound_Idle','Ashbound_Shuffle'], 'forward': 'Blender -Y',
            'nominalWalkSpeed': .9}
(OUT/'AshboundZombie.json').write_text(json.dumps(metadata,indent=2))

# Actual Blender studio preview, not an AI picture. A duplicate shares the mesh.
back = body.copy()
back.data = body.data
back.modifiers.clear()
back.parent = None
bpy.context.collection.objects.link(back)
back.name = 'Preview_Back_Only'
back.location = (1.15,.05,0)
back.rotation_euler.z = math.radians(155)
body.location.x = -.48
rig.location.x = -.48
body.location.x = 0  # child follows rig once, never double-translate


def aim(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


floor_mat = material('StudioOnly', (.14,.135,.13))
bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.012))
floor = bpy.context.object
floor.name = 'Preview_Floor_Only'
floor.data.materials.append(floor_mat)
for name, pos, power, size in [('Key',(-3,-4,5),420,4),('Fill',(3,-2,4),260,4),('Rim',(1,3,4),350,3)]:
    light = bpy.data.lights.new(name,'AREA')
    light.energy, light.size = power,size
    obj = bpy.data.objects.new(name,light)
    bpy.context.collection.objects.link(obj)
    obj.location = pos
    aim(obj,(0,0,1))
scene.world = bpy.data.worlds.new('AshboundStudio')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.22,.21,.20,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .5
camera = bpy.data.objects.new('Ashbound_Studio_Camera',bpy.data.cameras.new('AshboundCamera'))
bpy.context.collection.objects.link(camera)
camera.location = (2,-8,3.1)
aim(camera,(.3,0,1.07))
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 3.7
scene.camera = camera
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 32
scene.cycles.use_denoising = False
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.render.resolution_x, scene.render.resolution_y = 1280,1000
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
scene.render.filepath = str(PREVIEW)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Ashbound_Zombie01.blend'))
bpy.ops.render.render(write_still=True)
print('ASHBOUND_MODEL_PASS='+json.dumps(metadata))
