"""Refined low-density zombie, true T bind, standard Mixamo bones and motions.

Native source 02 preserves the original attempt. FBX is exported before studio
objects exist. Rest-model and motion-only FBXs share the exact same bind pose.
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Assets/Models/Enemies/Ashbound'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.fps = 30
scene.unit_settings.system = 'METRIC'
parts = []
PREFIX = 'mixamorig:'


def mat(name, color, emission=0):
    result = bpy.data.materials.new('Ashbound_'+name)
    result.diffuse_color = (*color,1)
    result.use_nodes = True
    shader = result.node_tree.nodes['Principled BSDF']
    shader.inputs['Base Color'].default_value = (*color,1)
    shader.inputs['Roughness'].default_value = .8
    shader.inputs['Emission Color'].default_value = (*color,1)
    shader.inputs['Emission Strength'].default_value = emission
    return result


SKIN=mat('Skin',(.30,.32,.29)); CLOTH=mat('Tunic',(.042,.045,.05))
PANTS=mat('Trousers',(.024,.027,.033)); RED=mat('Burgundy',(.17,.02,.029))
BOOTS=mat('Boots',(.048,.035,.03)); BLACK=mat('Sockets',(.004,.003,.004))
EYES=mat('Eyes',(.7,.008,.004),2)


def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)))
    return t*t*(3-2*t)


def blend(a,b,t):
    return {a:1-t,b:t}


def mesh(name, vertices, faces, material, weights):
    data=bpy.data.meshes.new(name)
    data.from_pydata(vertices,[],faces); data.update()
    obj=bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(material)
    for face in data.polygons: face.use_smooth=True
    groups={}
    for v in data.vertices:
        influences=weights(v.co) if callable(weights) else {weights:1}
        for bone,weight in influences.items():
            if weight<.00001: continue
            key=PREFIX+bone
            if key not in groups: groups[key]=obj.vertex_groups.new(name=key)
            groups[key].add([v.index],weight,'REPLACE')
    parts.append(obj)
    return obj


def tube(name, points, radii, material, weights, sides=14, caps=True):
    verts=[]; faces=[]
    for row,point in enumerate(points):
        p=Vector(point)
        tangent=(Vector(points[min(row+1,len(points)-1)])-Vector(points[max(0,row-1)])).normalized()
        axis=Vector((0,1,0))
        if abs(tangent.dot(axis))>.95: axis=Vector((1,0,0))
        u=axis.cross(tangent).normalized(); v=tangent.cross(u)
        for i in range(sides):
            angle=i*math.tau/sides
            verts.append(p+u*radii[row][0]*math.cos(angle)+v*radii[row][1]*math.sin(angle))
    for row in range(len(points)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    if caps:
        faces.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+i for i in range(sides))])
    return mesh(name,verts,faces,material,weights)


def rings(name, rows, material, weights, sides=24, hem=False):
    verts=[]; faces=[]
    for row,(z,x,y,rx,ry) in enumerate(rows):
        for i in range(sides):
            angle=i*math.tau/sides
            drop=(-.055 if i%4==0 else 0) if hem and row<2 else 0
            verts.append((x+rx*math.sin(angle),y-ry*math.cos(angle),z+drop))
    for row in range(len(rows)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    return mesh(name,verts,faces,material,weights)


def ellipsoid(name,center,scale,material,weights,segments=16,rings_count=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings_count,location=center)
    obj=bpy.context.object
    coords=[Vector(center)+Vector((v.co.x*scale[0],v.co.y*scale[1],v.co.z*scale[2])) for v in obj.data.vertices]
    faces=[tuple(p.vertices) for p in obj.data.polygons]
    bpy.data.objects.remove(obj,do_unlink=True)
    return mesh(name,coords,faces,material,weights)


def torso_weight(p):
    if p.z<1.14: return {'Hips':1}
    if p.z<1.36: return blend('Hips','Spine',smooth(1.14,1.36,p.z))
    if p.z<1.51: return blend('Spine','Spine1',smooth(1.36,1.51,p.z))
    return blend('Spine1','Spine2',smooth(1.51,1.63,p.z))


# Conventional neutral bind: hips centred, straight knees, horizontal arms.
specs=[('Hips',(0,0,1.03),(0,0,1.15),None),
       ('Spine',(0,0,1.15),(0,0,1.36),'Hips'),
       ('Spine1',(0,0,1.36),(0,0,1.48),'Spine'),
       ('Spine2',(0,0,1.48),(0,0,1.61),'Spine1'),
       ('Neck',(0,0,1.61),(0,0,1.74),'Spine2'),
       ('Head',(0,0,1.74),(0,0,2.055),'Neck')]
for side,sign in [('Left',1),('Right',-1)]:
    specs += [(side+'Shoulder',(sign*.055,0,1.60),(sign*.255,0,1.60),'Spine2'),
        (side+'Arm',(sign*.255,0,1.60),(sign*.57,0,1.60),side+'Shoulder'),
        (side+'ForeArm',(sign*.57,0,1.60),(sign*.855,0,1.60),side+'Arm'),
        (side+'Hand',(sign*.855,0,1.60),(sign*.955,0,1.60),side+'ForeArm'),
        (side+'UpLeg',(sign*.128,0,1.03),(sign*.128,0,.58),'Hips'),
        (side+'Leg',(sign*.128,0,.58),(sign*.128,0,.145),side+'UpLeg'),
        (side+'Foot',(sign*.128,0,.145),(sign*.128,-.12,.065),side+'Leg'),
        (side+'ToeBase',(sign*.128,-.12,.065),(sign*.128,-.24,.065),side+'Foot')]
    for finger,index in [('Index',0),('Middle',1),('Ring',2),('Pinky',3)]:
        y=-.032+index*.022
        length=[.082,.094,.086,.067][index]
        for segment in range(3):
            a=.94+length*segment/3; b=.94+length*(segment+1)/3
            parent=side+'Hand' if segment==0 else side+'Hand'+finger+str(segment)
            specs.append((side+'Hand'+finger+str(segment+1),(sign*a,y,1.60),(sign*b,y,1.60),parent))
    for segment in range(3):
        a=Vector((sign*(.889+.022*segment),-.055-.015*segment,1.598))
        b=Vector((sign*(.889+.022*(segment+1)),-.055-.015*(segment+1),1.598))
        parent=side+'Hand' if segment==0 else side+'HandThumb'+str(segment)
        specs.append((side+'HandThumb'+str(segment+1),a,b,parent))
data=bpy.data.armatures.new('AshboundHumanoidSkeleton')
rig=bpy.data.objects.new('AshboundRig',data)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in specs:
    bone=data.edit_bones.new(PREFIX+name); bone.head=head; bone.tail=tail
    if parent: bone.parent=data.edit_bones[PREFIX+parent]
bpy.ops.object.mode_set(mode='OBJECT'); rig.select_set(False); rig.show_in_front=True

# A collar, tailored taper and uneven hem provide shape without cloth microdetail.
rings('Tunic',[(.765,0,0,.265,.163),(.78,0,0,.267,.165),(.88,0,0,.25,.158),
    (1.015,0,0,.219,.14),(1.10,0,0,.202,.132),(1.24,0,0,.216,.145),
    (1.40,0,0,.256,.16),(1.51,0,0,.281,.162),(1.575,0,0,.273,.148),
    (1.615,0,0,.221,.121),(1.645,0,0,.104,.079),(1.65,0,0,.097,.075)],CLOTH,torso_weight,hem=True)
rings('Belt',[(1.055,0,0,.215,.143),(1.06,0,0,.218,.145),
    (1.125,0,0,.212,.143),(1.13,0,0,.209,.14)],RED,'Hips')
mesh('BeltTail',[(-.12,-.146,1.08),(-.048,-.147,1.08),(-.055,-.18,.825),(-.095,-.185,.795),(-.13,-.18,.825)],
     [(0,4,3,2,1)],RED,'Hips')
rings('Neck',[(1.59,0,.014,.088,.073),(1.65,0,.008,.077,.069),
    (1.71,0,0,.073,.066),(1.775,0,-.008,.076,.071)],SKIN,
    lambda p:blend('Spine2','Neck',smooth(1.61,1.71,p.z)),sides=16)

# Single sculpted head surface. Sockets, brow, nose and jaw are NOT stacked blobs.
profile=[(1.725,.041,.048,-.025),(1.745,.067,.067,-.021),(1.77,.084,.075,-.015),
    (1.795,.096,.084,-.008),(1.825,.105,.093,-.002),(1.855,.118,.100,0),
    (1.885,.13,.108,0),(1.915,.137,.113,0),(1.94,.14,.117,0),
    (1.965,.141,.121,0),(1.99,.139,.122,.003),(2.015,.132,.119,.006),
    (2.04,.117,.108,.009),(2.062,.092,.086,.01),(2.079,.06,.057,.01),
    (2.089,.027,.026,.01),(2.092,.002,.002,.01)]


def at_z(z):
    for a,b in zip(profile,profile[1:]):
        if a[0]<=z<=b[0]:
            t=(z-a[0])/(b[0]-a[0]); return tuple(a[i]*(1-t)+b[i]*t for i in [1,2,3])
    return profile[0][1:] if z<profile[0][0] else profile[-1][1:]


def face_offset(x,z):
    def g(cx,cz,wx,wz): return math.exp(-((x-cx)/wx)**2-((z-cz)/wz)**2)
    value=.024*(g(.061,1.932,.037,.026)+g(-.061,1.932,.037,.026))
    value-=.014*(g(.061,1.965,.052,.019)+g(-.061,1.965,.052,.019))
    value-=.039*g(0,1.889,.023,.043) # nose formed in the same surface
    value-=.011*(g(.094,1.884,.034,.028)+g(-.094,1.884,.034,.028))
    value+=.008*g(0,1.818,.058,.014)
    return value


HEAD_BVH=None


def front_y(x,z):
    if HEAD_BVH is not None:
        hit,_,_,_=HEAD_BVH.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)
        if hit is not None: return hit.y
    rx,ry,cy=at_z(z)
    front=math.sqrt(max(0,1-(x/rx)**2))
    return cy-ry*front+face_offset(x,z)*front**5


verts=[]; faces=[]; N=40
for z,rx,ry,cy in profile:
    for i in range(N):
        angle=i*math.tau/N; x=rx*math.sin(angle); facing=max(0,math.cos(angle))**5
        verts.append((x,cy-ry*math.cos(angle)+face_offset(x,z)*facing,z))
for row in range(len(profile)-1):
    for i in range(N):
        j=(i+1)%N
        faces.append((row*N+i,row*N+j,(row+1)*N+j,(row+1)*N+i))
mesh('SculptedHead',verts,faces,SKIN,'Head')
HEAD_BVH=BVHTree.FromPolygons(verts,faces)
for sign in [-1,1]:
    ellipsoid('Ear',(sign*.141,.004,1.913),(.018,.022,.035),SKIN,'Head',12,6)
    # Dark recessed patches conform to the face surface instead of bulging eyeballs.
    cx=sign*.06; cz=1.933
    patch=[(cx,front_y(cx,cz)-.004,cz)]
    for i in range(16):
        a=i*math.tau/16; x=cx+.036*math.cos(a); z=cz+.019*math.sin(a)-sign*(x-cx)*.18
        patch.append((x,front_y(x,z)-.004,z))
    mesh('InsetEyeSocket',patch,[(0,i+1,(i+1)%16+1) for i in range(16)],BLACK,'Head')
    ellipsoid('EmberEye',(cx,front_y(cx,cz)-.009,cz),(.007,.004,.0065),EYES,'Head',12,6)
mouth=[]
for x,z in [(-.04,1.810),(-.021,1.813),(0,1.812),(.022,1.813),(.04,1.810),
            (.037,1.803),(.02,1.805),(0,1.805),(-.021,1.805),(-.038,1.803)]:
    mouth.append((x,front_y(x,z)-.004,z))
mesh('MouthCrease',mouth,[tuple(range(10))],BLACK,'Head')

for side,sign in [('Left',1),('Right',-1)]:
    def arm_weight(p,side=side):
        x=abs(p.x)
        if x<.32: return blend('Spine2',side+'Arm',smooth(.20,.285,x))
        if x<.69: return blend(side+'Arm',side+'ForeArm',smooth(.52,.62,x))
        return blend(side+'ForeArm',side+'Hand',smooth(.815,.88,x))
    points=[(sign*x,0,1.60) for x in [.20,.245,.29,.35,.43,.51,.565,.61,.67,.75,.815,.855,.89,.925,.95]]
    radii=[(.075,.075),(.10,.10),(.102,.099),(.094,.087),(.08,.075),(.062,.062),
           (.056,.057),(.063,.062),(.066,.061),(.052,.048),(.036,.035),(.031,.033),
           (.025,.052),(.024,.049),(.022,.04)]
    arm=tube(side+'ContinuousArmAndPalm',points,radii,SKIN,arm_weight,sides=16)
    arm.data.materials.append(CLOTH)
    for face in arm.data.polygons:
        center=sum((arm.data.vertices[v].co for v in face.vertices),Vector())/len(face.vertices)
        if abs(center.x)<.34: face.material_index=1
    for finger,index in [('Index',0),('Middle',1),('Ring',2),('Pinky',3)]:
        y=-.032+index*.022; length=[.082,.094,.086,.067][index]
        points=[(sign*(.935+length*t),y,1.60-.012*t*t) for t in [0,.2,.5,.78,1]]
        def finger_weight(p,side=side,finger=finger,length=length):
            t=(abs(p.x)-.94)/length
            if t<.13: return blend(side+'Hand',side+'Hand'+finger+'1',smooth(-.03,.13,t))
            if t<.5: return blend(side+'Hand'+finger+'1',side+'Hand'+finger+'2',smooth(.25,.45,t))
            return blend(side+'Hand'+finger+'2',side+'Hand'+finger+'3',smooth(.6,.82,t))
        tube(side+finger,points,[(.009,.010),(.010,.010),(.009,.009),(.008,.008),(.004,.005)],SKIN,finger_weight,8)
    points=[(sign*(.88+.078*t),-.045-.055*t,1.598-.006*t) for t in [0,.25,.5,.75,1]]
    tube(side+'Thumb',points,[(.014,.014),(.014,.013),(.013,.012),(.011,.011),(.006,.006)],SKIN,
         lambda p,s=side:blend(s+'HandThumb1',s+'HandThumb2',smooth(.895,.94,abs(p.x))),8)

    def leg_weight(p,side=side):
        if p.z>.92: return blend(side+'UpLeg','Hips',smooth(.97,1.065,p.z))
        return blend(side+'Leg',side+'UpLeg',smooth(.53,.64,p.z))
    rings(side+'Leg',[(.19,sign*.128,0,.05,.054),(.28,sign*.128,0,.061,.065),
        (.4,sign*.128,-.003,.08,.077),(.51,sign*.128,-.006,.074,.072),
        (.58,sign*.128,-.006,.069,.07),(.65,sign*.128,-.003,.083,.079),
        (.78,sign*.128,0,.103,.092),(.92,sign*.128,0,.109,.103),
        (1.07,sign*.128,0,.101,.095)],PANTS,leg_weight,sides=20)
    boot=rings(side+'Boot',[(.015,sign*.128,-.06,.081,.156),(.025,sign*.128,-.06,.084,.16),
        (.065,sign*.128,-.062,.087,.162),(.105,sign*.128,-.049,.084,.149),
        (.145,sign*.128,-.019,.068,.102),(.18,sign*.128,0,.06,.063),
        (.255,sign*.128,0,.06,.061),(.265,sign*.128,0,.063,.064)],BOOTS,
        lambda p,s=side:blend(s+'Foot',s+'ToeBase',smooth(.10,.19,-p.y)),sides=20)

bpy.ops.object.select_all(action='DESELECT')
for part in parts: part.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join(); body=bpy.context.object; body.name='AshboundBody'
bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
body.parent=rig
modifier=body.modifiers.new('HumanoidSkin','ARMATURE'); modifier.object=rig
assert len(data.bones)==52
assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in body.data.vertices)


def export(path,animation):
    bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=animation,
        bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
        use_armature_deform_only=True,apply_unit_scale=True)


export(OUT/'AshboundZombie.fbx',False)


def orient(name,head,tail):
    bone=rig.pose.bones[PREFIX+name]; rest=bone.bone
    rotation=(rest.tail_local-rest.head_local).normalized().rotation_difference((Vector(tail)-Vector(head)).normalized())
    bone.matrix=Matrix.Translation(Vector(head)) @ (rotation@rest.matrix_local.to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()


def tilt(name,angle):
    bone=rig.pose.bones[PREFIX+name]
    bone.matrix=Matrix.Translation(bone.head) @ Matrix.Rotation(math.radians(angle),4,'X') @ bone.matrix.to_quaternion().to_matrix().to_4x4()
    bpy.context.view_layer.update()


def leg_pose(side,target):
    a=rig.pose.bones[PREFIX+side+'UpLeg']; b=rig.pose.bones[PREFIX+side+'Leg']
    hip=a.head.copy(); target=Vector(target); delta=target-hip
    distance=min(delta.length,a.length+b.length-.001); direction=delta.normalized()
    target=hip+direction*distance
    bend=Vector((0,-1,0)); bend=(bend-direction*bend.dot(direction)).normalized()
    along=(a.length*a.length-b.length*b.length+distance*distance)/(2*distance)
    knee=hip+direction*along+bend*math.sqrt(max(0,a.length*a.length-along*along))
    orient(side+'UpLeg',hip,knee); orient(side+'Leg',knee,target)
    foot=rig.pose.bones[PREFIX+side+'Foot']
    foot.matrix=Matrix.Translation(target) @ foot.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
    bpy.context.view_layer.update()


def pose(phase,moving):
    for bone in rig.pose.bones:
        bone.rotation_mode='QUATERNION'; bone.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    hips=rig.pose.bones[PREFIX+'Hips']
    bob=(.008*math.sin(phase*math.tau*2)) if moving else (.003*math.sin(phase*math.tau))
    hips.matrix=Matrix.Translation((0,0,-.035+bob)) @ hips.bone.matrix_local
    bpy.context.view_layer.update()
    tilt('Spine',9); tilt('Spine1',5); tilt('Neck',-6)
    for side,sign,offset in [('Left',1,0),('Right',-1,.5)]:
        t=(phase+offset)%1; y=0; lift=0
        if moving:
            if t<.6: y=-.22+.44*t/.6
            else:
                u=(t-.6)/.4; y=.22-.44*(u*u*(3-2*u)); lift=.075*math.sin(math.pi*u)
        leg_pose(side,(sign*.128,y,.145+lift))
        upper=rig.pose.bones[PREFIX+side+'Arm']
        shoulder=upper.head.copy(); sway=(.022*math.sin((phase+offset)*math.tau)) if moving else 0
        elbow=shoulder+Vector((sign*.105,-.045+sway,-.294))
        orient(side+'Arm',shoulder,elbow)
        wrist=elbow+Vector((sign*.045,-.105-sway,-.258))
        orient(side+'ForeArm',elbow,wrist)
        orient(side+'Hand',wrist,wrist+Vector((sign*.013,-.025,-.096)))


for name,frames in [('Ashbound_Idle',61),('Ashbound_Shuffle',33)]:
    action=bpy.data.actions.new(name); action.use_fake_user=True
    rig.animation_data_create(); rig.animation_data.action=action
    for frame in range(1,frames+1):
        pose((frame-1)/(frames-1),name.endswith('Shuffle'))
        for bone in rig.pose.bones:
            bone.keyframe_insert('location',frame=frame,group=bone.name)
            bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
            bone.keyframe_insert('scale',frame=frame,group=bone.name)
scene.frame_start=1; scene.frame_end=61
rig.animation_data.action=bpy.data.actions['Ashbound_Idle']; scene.frame_set(1)
export(OUT/'AshboundZombie_Motions.fbx',True)
metadata={'height':2.092,'vertices':len(body.data.vertices),
    'triangles':sum(len(p.vertices)-2 for p in body.data.polygons),'bones':len(data.bones),
    'normalizedWeights':True,'tPose':True,'fingers':'five three-joint chains per hand',
    'clips':['Ashbound_Idle','Ashbound_Shuffle'],'forward':'Blender -Y','nominalWalkSpeed':.9}
(OUT/'AshboundZombie.json').write_text(json.dumps(metadata,indent=2))
assert metadata['vertices']<6000

# Freeze copies for the studio only. The actual rig is retained, not merged into props.
depsgraph=bpy.context.evaluated_depsgraph_get()
preview_collection=bpy.data.collections.new('Studio preview only (not exported)')
scene.collection.children.link(preview_collection)
for name,pos,angle in [('Front',(-.56,0,0),0),('Back',(1.02,.08,0),math.radians(170))]:
    preview=bpy.data.objects.new('Preview_'+name,bpy.data.meshes.new_from_object(body.evaluated_get(depsgraph)))
    preview_collection.objects.link(preview); preview.location=pos; preview.rotation_euler.z=angle
body.hide_render=True
floor_material=mat('StudioOnly',(.115,.12,.13))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025))
floor=bpy.context.object; floor.data.materials.append(floor_material)


def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


for name,pos,power,size in [('Key',(-3,-4,5),450,4),('Fill',(3,-3,3),220,4),('Rim',(1,3,4),400,3)]:
    light=bpy.data.lights.new(name,'AREA'); light.energy=power; light.size=size
    obj=bpy.data.objects.new(name,light); scene.collection.objects.link(obj); obj.location=pos; aim(obj,(0,0,1))
scene.world=bpy.data.worlds.new('NeutralStudio'); scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.18,.19,.21,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
camera=bpy.data.objects.new('AshboundPreview',bpy.data.cameras.new('AshboundPreview'))
scene.collection.objects.link(camera); camera.location=(1.6,-8,2.9); aim(camera,(.24,0,1.03))
camera.data.type='ORTHO'; camera.data.ortho_scale=3.55; scene.camera=camera
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=40; scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.resolution_x=1280; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.filepath=str(ROOT/'ArtSource/Previews/Ashbound_Zombie_Blender02.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Ashbound_Zombie02_Humanoid.blend'))
bpy.ops.render.render(write_still=True)
print('ASHBOUND_REFINED_PASS='+json.dumps(metadata))
