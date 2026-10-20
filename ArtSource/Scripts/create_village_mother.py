"""Build the approved simple mother NPC, one palette material and a T-pose rig.

Blender --background --threads 2 --python ArtSource/Scripts/create_village_mother.py
No scene, player, enemy, or previously approved model is overwritten.
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Assets/Models/Characters/Mother'
TEX = ROOT/'Assets/Textures/Characters/Mother'
OUT.mkdir(parents=True, exist_ok=True)
TEX.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.fps = 30
scene.unit_settings.system = 'METRIC'
parts = []
PREFIX = 'mixamorig:'

# A tiny colour lookup replaces eight submaterials and all large textures.
COLORS = [(.64,.43,.29,1), (.18,.036,.046,1), (.047,.044,.045,1), (.68,.60,.46,1),
          (.12,.065,.033,1), (.09,.05,.03,1), (.014,.008,.006,1), (.24,.10,.07,1)]
SKIN,DRESS,SHAWL,APRON,HAIR,SHOES,EYES,LIPS = range(8)
palette = bpy.data.images.new('MotherPalette', width=8, height=1, alpha=False)
palette.colorspace_settings.name = 'sRGB'
palette.pixels = [value for color in COLORS for value in color]
palette.file_format = 'PNG'
palette.filepath_raw = str(TEX/'MotherPalette.png')
palette.save()
palette.pack()
material = bpy.data.materials.new('MotherPalette')
material.use_nodes = True
bsdf = material.node_tree.nodes['Principled BSDF']
bsdf.inputs['Roughness'].default_value = .82
texture = material.node_tree.nodes.new('ShaderNodeTexImage')
texture.image = palette
texture.interpolation = 'Closest'
texture.extension = 'EXTEND'
material.node_tree.links.new(texture.outputs['Color'],bsdf.inputs['Base Color'])


def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)))
    return t*t*(3-2*t)


def blend(a,b,t): return {a:1-t,b:t}


def mesh(name,vertices,faces,color,weights):
    data=bpy.data.meshes.new(name)
    data.from_pydata(vertices,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj)
    data.materials.append(material)
    uv=data.uv_layers.new(name='PaletteUV')
    for loop in uv.data: loop.uv=((color+.5)/8,.5)
    for face in data.polygons: face.use_smooth=True
    groups={}
    for vertex in data.vertices:
        influences=weights(vertex.co) if callable(weights) else {weights:1}
        for bone,weight in influences.items():
            if weight<=0: continue
            key=PREFIX+bone
            if key not in groups: groups[key]=obj.vertex_groups.new(name=key)
            groups[key].add([vertex.index],weight,'REPLACE')
    parts.append(obj)
    return obj


def rings(name,rows,color,weights,sides=24,folds=False):
    vertices=[]; faces=[]
    for z,x,y,rx,ry in rows:
        for i in range(sides):
            a=i*math.tau/sides
            fold=1+.018*math.cos(4*a)*max(0,min(1,(.96-z)/.8)) if folds else 1
            vertices.append((x+rx*math.sin(a)*fold,y-ry*math.cos(a)*fold,z))
    for row in range(len(rows)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    return mesh(name,vertices,faces,color,weights)


def tube(name,points,radii,color,weights,sides=12):
    vertices=[]; faces=[]
    for row,point in enumerate(points):
        p=Vector(point)
        tangent=(Vector(points[min(row+1,len(points)-1)])-Vector(points[max(0,row-1)])).normalized()
        axis=Vector((0,1,0))
        if abs(tangent.dot(axis))>.95: axis=Vector((1,0,0))
        u=axis.cross(tangent).normalized(); v=tangent.cross(u)
        for i in range(sides):
            a=i*math.tau/sides
            vertices.append(p+u*radii[row][0]*math.cos(a)+v*radii[row][1]*math.sin(a))
    for row in range(len(points)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    faces.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+i for i in range(sides))])
    return mesh(name,vertices,faces,color,weights)


def ellipsoid(name,center,scale,color,weights,segments=16,ring_count=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=ring_count)
    temporary=bpy.context.object
    vertices=[Vector(center)+Vector((v.co.x*scale[0],v.co.y*scale[1],v.co.z*scale[2])) for v in temporary.data.vertices]
    faces=[tuple(p.vertices) for p in temporary.data.polygons]
    bpy.data.objects.remove(temporary,do_unlink=True)
    return mesh(name,vertices,faces,color,weights)


specs=[('Hips',(0,0,.88),(0,0,1.02),None),('Spine',(0,0,1.02),(0,0,1.19),'Hips'),
       ('Spine1',(0,0,1.19),(0,0,1.31),'Spine'),('Spine2',(0,0,1.31),(0,0,1.41),'Spine1'),
       ('Neck',(0,0,1.41),(0,0,1.51),'Spine2'),('Head',(0,0,1.51),(0,0,1.77),'Neck')]
for side,sign in [('Left',1),('Right',-1)]:
    specs += [(side+'Shoulder',(sign*.04,0,1.39),(sign*.20,0,1.39),'Spine2'),
        (side+'Arm',(sign*.20,0,1.39),(sign*.44,0,1.39),side+'Shoulder'),
        (side+'ForeArm',(sign*.44,0,1.39),(sign*.66,0,1.39),side+'Arm'),
        (side+'Hand',(sign*.66,0,1.39),(sign*.735,0,1.39),side+'ForeArm'),
        (side+'UpLeg',(sign*.105,0,.88),(sign*.105,0,.48),'Hips'),
        (side+'Leg',(sign*.105,0,.48),(sign*.105,0,.10),side+'UpLeg'),
        (side+'Foot',(sign*.105,0,.10),(sign*.105,-.09,.045),side+'Leg'),
        (side+'ToeBase',(sign*.105,-.09,.045),(sign*.105,-.18,.045),side+'Foot')]
    for finger,index in [('Index',0),('Middle',1),('Ring',2),('Pinky',3)]:
        y=-.025+index*.017; length=[.06,.069,.062,.05][index]
        for joint in range(3):
            parent=side+'Hand' if joint==0 else side+'Hand'+finger+str(joint)
            a=.727+length*joint/3; b=.727+length*(joint+1)/3
            specs.append((side+'Hand'+finger+str(joint+1),(sign*a,y,1.39),(sign*b,y,1.39),parent))
    for joint in range(3):
        parent=side+'Hand' if joint==0 else side+'HandThumb'+str(joint)
        a=(sign*(.686+joint*.017),-.037-joint*.012,1.39)
        b=(sign*(.686+(joint+1)*.017),-.037-(joint+1)*.012,1.39)
        specs.append((side+'HandThumb'+str(joint+1),a,b,parent))
data=bpy.data.armatures.new('MotherHumanoidSkeleton')
rig=bpy.data.objects.new('VillageMotherRig',data); scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in specs:
    bone=data.edit_bones.new(PREFIX+name); bone.head=head; bone.tail=tail
    if parent: bone.parent=data.edit_bones[PREFIX+parent]
bpy.ops.object.mode_set(mode='OBJECT'); rig.select_set(False); rig.show_in_front=True


def torso_weight(p):
    if p.z<1.02: return {'Hips':1}
    if p.z<1.20: return blend('Hips','Spine',smooth(1.02,1.20,p.z))
    if p.z<1.32: return blend('Spine','Spine1',smooth(1.20,1.32,p.z))
    return blend('Spine1','Spine2',smooth(1.32,1.41,p.z))


dress_rows=[(.12,0,0,.335,.222),(.135,0,0,.337,.223),(.30,0,0,.315,.21),
    (.50,0,0,.277,.188),(.70,0,0,.233,.16),(.84,0,0,.202,.141),
    (.96,0,0,.158,.114),(1.02,0,0,.145,.106),(1.12,0,0,.17,.118),
    (1.23,0,-.004,.194,.135),(1.31,0,0,.20,.127),(1.365,0,0,.214,.116),
    (1.405,0,0,.188,.105),(1.43,0,0,.078,.062),(1.438,0,0,.072,.058)]
rings('Dress',dress_rows,DRESS,torso_weight,24,folds=True)
rings('WaistBelt',[(.966,0,0,.159,.117),(.972,0,0,.162,.12),
    (1.011,0,0,.152,.115),(1.018,0,0,.149,.111)],SHOES,'Hips',24)

# The apron is close to the skirt and uses the same rigid hip weights, not cloth simulation.
apron_rows=[(.30,.315,.21),(.5,.277,.188),(.7,.233,.16),(.84,.202,.141),(.964,.158,.114)]
vertices=[]; faces=[]
for row,(z,rx,ry) in enumerate(apron_rows):
    for i in range(7):
        a=(i/6-.5)*1.40
        fold=1+.018*math.cos(4*a)*max(0,min(1,(.96-z)/.8))
        bottom_raise=.009*(abs(i/6-.5)*2)**2 if row==0 else 0
        vertices.append((rx*math.sin(a)*fold,-ry*math.cos(a)*fold-.006,z+bottom_raise))
for row in range(len(apron_rows)-1):
    for i in range(6): faces.append((row*7+i,row*7+i+1,(row+1)*7+i+1,(row+1)*7+i))
mesh('CreamApron',vertices,faces,APRON,'Hips')
rings('ShortShoulderShawl',[(1.345,0,.003,.234,.137),(1.355,0,.003,.235,.137),
    (1.405,0,.002,.215,.126),(1.445,0,0,.125,.087),(1.458,0,0,.075,.060)],SHAWL,
    lambda p:blend('Spine1','Spine2',smooth(1.33,1.43,p.z)),24)
rings('Neck',[(1.40,0,0,.065,.056),(1.46,0,0,.058,.051),(1.515,0,-.002,.060,.053),
    (1.555,0,0,.066,.057)],SKIN,lambda p:blend('Neck','Head',smooth(1.49,1.55,p.z)),16)

# One soft face surface; no separate cheek, brow or nose blobs.
profile=[(1.505,.031,.036,-.016),(1.518,.052,.051,-.011),(1.537,.072,.063,-.007),
    (1.56,.084,.074,-.003),(1.585,.095,.082,0),(1.61,.103,.087,0),
    (1.635,.108,.092,0),(1.66,.111,.095,0),(1.685,.111,.098,.003),
    (1.71,.105,.096,.006),(1.735,.09,.085,.009),(1.755,.067,.065,.011),
    (1.769,.037,.037,.012),(1.775,.002,.002,.012)]


def face_offset(x,z):
    def g(cx,cz,wx,wz): return math.exp(-((x-cx)/wx)**2-((z-cz)/wz)**2)
    return (-.027*g(0,1.608,.017,.030)-.005*(g(.063,1.612,.03,.028)+g(-.063,1.612,.03,.028))
            +.004*(g(.044,1.65,.023,.020)+g(-.044,1.65,.023,.020)))


vertices=[]; faces=[]; sides=32
for z,rx,ry,cy in profile:
    for i in range(sides):
        a=i*math.tau/sides; x=rx*math.sin(a); front=max(0,math.cos(a))**5
        vertices.append((x,cy-ry*math.cos(a)+face_offset(x,z)*front,z))
for row in range(len(profile)-1):
    for i in range(sides):
        j=(i+1)%sides
        faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
mesh('MotherFace',vertices,faces,SKIN,'Head')
head_bvh=BVHTree.FromPolygons(vertices,faces)


def front_y(x,z):
    hit,_,_,_=head_bvh.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)
    if hit is None: raise RuntimeError('Face feature outside head')
    return hit.y-.003


for sign in [-1,1]:
    ellipsoid('Ear',(sign*.111,.007,1.638),(.015,.017,.026),SKIN,'Head',12,6)
    x=sign*.043; z=1.653
    points=[(x,front_y(x,z),z)]
    for i in range(12):
        a=i*math.tau/12; px=x+.008*math.cos(a); pz=z+.013*math.sin(a)
        points.append((px,front_y(px,pz),pz))
    mesh('SimpleEye',points,[(0,i+1,(i+1)%12+1) for i in range(12)],EYES,'Head')
    points=[]
    for dx,dz in [(-.018,0),(0,.004),(.018,0),(.017,-.004),(0,0),(-.017,-.004)]:
        px=x+dx; pz=1.687+dz; points.append((px,front_y(px,pz),pz))
    mesh('Eyebrow',points,[(0,5,4,3,2,1)],HAIR,'Head')
points=[]
for x,z in [(-.023,1.563),(0,1.561),(.023,1.563),(.022,1.559),(0,1.557),(-.022,1.559)]:
    points.append((x,front_y(x,z),z))
mesh('Mouth',points,[(0,5,4,3,2,1)],LIPS,'Head')

# One low-detail hair shell with a side-swept boundary, plus one low bun.
vertices=[]; faces=[]; sides=32; rows=9
for row in range(rows):
    for i in range(sides):
        a=i*math.tau/sides
        back=(1-math.cos(a))*.5
        end=1.07+back*1.23+.15*math.sin(a)
        phi=.035+(end-.035)*row/(rows-1)
        vertices.append((.122*math.sin(phi)*math.sin(a),.006-.121*math.sin(phi)*math.cos(a),1.648+.141*math.cos(phi)))
for row in range(rows-1):
    for i in range(sides):
        j=(i+1)%sides
        faces.append((row*sides+i,(row+1)*sides+i,(row+1)*sides+j,row*sides+j))
faces.append(tuple(reversed(range(sides))))
mesh('HairCap',vertices,faces,HAIR,'Head')
tube('BroadSideSweptLock',[(.034,-.030,1.782),(.012,-.070,1.765),
    (-.025,-.095,1.736),(-.065,-.095,1.701),(-.097,-.066,1.665)],
    [(.012,.008),(.018,.009),(.020,.009),(.018,.009),(.008,.006)],HAIR,'Head',8)
ellipsoid('LowBun',(0,.136,1.592),(.066,.054,.065),HAIR,'Head',20,10)

for side,sign in [('Left',1),('Right',-1)]:
    def arm_weight(p,side=side):
        x=abs(p.x)
        if x<.30: return blend('Spine2',side+'Arm',smooth(.165,.225,x))
        return blend(side+'Arm',side+'ForeArm',smooth(.405,.475,x))
    points=[(sign*x,0,1.39) for x in [.168,.20,.24,.29,.35,.405,.44,.48,.53,.59,.64,.661]]
    radii=[(.043,.047),(.064,.065),(.071,.068),(.069,.064),(.06,.057),(.05,.049),
           (.047,.047),(.051,.05),(.048,.047),(.041,.04),(.035,.035),(.035,.035)]
    tube(side+'Sleeve',points,radii,DRESS,arm_weight,12)
    tube(side+'Hand',[(sign*x,0,1.39) for x in [.653,.675,.702,.726,.74]],
        [(.024,.025),(.023,.029),(.020,.040),(.019,.038),(.016,.029)],SKIN,
        lambda p,s=side:blend(s+'ForeArm',s+'Hand',smooth(.65,.68,abs(p.x))),10)
    for finger,index in [('Index',0),('Middle',1),('Ring',2),('Pinky',3)]:
        y=-.025+index*.017; length=[.06,.069,.062,.05][index]
        points=[(sign*(.725+length*t),y,1.39-.008*t*t) for t in [0,.3,.65,1]]
        def finger_weight(p,s=side,f=finger,length=length):
            t=(abs(p.x)-.727)/length
            if t<.25: return blend(s+'Hand',s+'Hand'+f+'1',smooth(-.05,.20,t))
            if t<.6: return blend(s+'Hand'+f+'1',s+'Hand'+f+'2',smooth(.3,.52,t))
            return blend(s+'Hand'+f+'2',s+'Hand'+f+'3',smooth(.65,.87,t))
        tube(side+finger,points,[(.0075,.008),(.008,.008),(.007,.007),(.004,.004)],SKIN,finger_weight,6)
    points=[(sign*(.68+.057*t),-.032-.044*t,1.39-.005*t) for t in [0,.33,.67,1]]
    tube(side+'Thumb',points,[(.011,.011),(.011,.01),(.009,.009),(.005,.005)],SKIN,
         lambda p,s=side:blend(s+'HandThumb1',s+'HandThumb2',smooth(.69,.725,abs(p.x))),6)
    # No invisible legs/body under the closed dress: keep only shoes that can show.
    rings(side+'Shoe',[(.012,sign*.105,-.075,.060,.145),(.02,sign*.105,-.075,.064,.151),
        (.055,sign*.105,-.075,.066,.153),(.085,sign*.105,-.060,.063,.14),
        (.118,sign*.105,-.009,.048,.067),(.15,sign*.105,0,.043,.049)],SHOES,
        lambda p,s=side:blend(s+'Foot',s+'ToeBase',smooth(.08,.145,-p.y)),16)

bpy.ops.object.select_all(action='DESELECT')
for part in parts: part.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join(); body=bpy.context.object; body.name='MotherBody'
bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
body.parent=rig
modifier=body.modifiers.new('HumanoidSkin','ARMATURE'); modifier.object=rig
assert len(body.data.materials)==1
assert len(body.data.vertices)<4000
assert len(data.bones)==52
assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 and len(v.groups)<=2 for v in body.data.vertices)


def export(path,animation):
    bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=animation,
        bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
        use_armature_deform_only=True,apply_unit_scale=True,path_mode='STRIP')


export(OUT/'VillageMother.fbx',False)


def orient(name,head,tail):
    bone=rig.pose.bones[PREFIX+name]; rest=bone.bone
    rotation=(rest.tail_local-rest.head_local).normalized().rotation_difference((Vector(tail)-Vector(head)).normalized())
    bone.matrix=Matrix.Translation(Vector(head)) @ (rotation@rest.matrix_local.to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()


def leg_pose(side,target):
    a=rig.pose.bones[PREFIX+side+'UpLeg']; b=rig.pose.bones[PREFIX+side+'Leg']
    hip=a.head.copy(); delta=Vector(target)-hip
    distance=min(delta.length,a.length+b.length-.001); direction=delta.normalized(); target=hip+direction*distance
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
    bob=.003*math.sin(phase*math.tau*2) if moving else .0018*math.sin(phase*math.tau)
    hips.matrix=Matrix.Translation((0,0,(-.025 if moving else -.006)+bob)) @ hips.bone.matrix_local
    bpy.context.view_layer.update()
    for side,sign,offset in [('Left',1,0),('Right',-1,.5)]:
        t=(phase+offset)%1; y=0; lift=0
        if moving:
            if t<.6: y=-.17+.34*t/.6
            else:
                u=(t-.6)/.4; y=.17-.34*(u*u*(3-2*u)); lift=.045*math.sin(math.pi*u)
        leg_pose(side,(sign*.105,y,.10+lift))
        upper=rig.pose.bones[PREFIX+side+'Arm']; shoulder=upper.head.copy()
        sway=.025*math.sin((phase+offset)*math.tau) if moving else .0018*math.sin(phase*math.tau)
        elbow=shoulder+Vector((sign*.085,.005+sway,-.224))
        orient(side+'Arm',shoulder,elbow)
        wrist=elbow+Vector((sign*.035,-.023-sway,-.216))
        orient(side+'ForeArm',elbow,wrist)
        orient(side+'Hand',wrist,wrist+Vector((sign*.006,-.006,-.0745)))


for name,frames in [('Mother_Idle',61),('Mother_Walk',33)]:
    action=bpy.data.actions.new(name); action.use_fake_user=True
    rig.animation_data_create(); rig.animation_data.action=action
    for frame in range(1,frames+1):
        pose((frame-1)/(frames-1),name.endswith('Walk'))
        for bone in rig.pose.bones:
            bone.keyframe_insert('location',frame=frame,group=bone.name)
            bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
            bone.keyframe_insert('scale',frame=frame,group=bone.name)
scene.frame_start=1; scene.frame_end=61
rig.animation_data.action=bpy.data.actions['Mother_Idle']; scene.frame_set(1)
export(OUT/'VillageMother_Motions.fbx',True)
stats={'vertices':len(body.data.vertices),'triangles':sum(len(p.vertices)-2 for p in body.data.polygons),
       'bones':len(data.bones),'materialSlots':len(body.data.materials),'palettePixels':8,
       'maxWeightsPerVertex':2,'normalizedWeights':True,'tPose':True,'sourceHeight':1.789,
       'unityHeight':1.70,'clips':['Mother_Idle','Mother_Walk'],'nominalWalkSpeed':.53,
       'clothSimulation':False,'hiddenBodyGeometry':False}
(OUT/'VillageMother.json').write_text(json.dumps(stats,indent=2))

# Freeze the idle skin for an honest two-angle studio render, outside the FBX.
depsgraph=bpy.context.evaluated_depsgraph_get()
studio=bpy.data.collections.new('Studio only (not exported)'); scene.collection.children.link(studio)
for name,pos,angle in [('Front',(-.48,0,0),0),('Back',(.91,.07,0),math.radians(165))]:
    preview=bpy.data.objects.new('Preview_'+name,bpy.data.meshes.new_from_object(body.evaluated_get(depsgraph)))
    studio.objects.link(preview); preview.location=pos; preview.rotation_euler.z=angle
body.hide_render=True
floor_mat=bpy.data.materials.new('StudioFloor'); floor_mat.diffuse_color=(.15,.15,.15,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012))
bpy.context.object.data.materials.append(floor_mat)


def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


for name,pos,power,size in [('Key',(-3,-4,5),400,4),('Fill',(3,-3,3),200,4),('Rim',(1,3,4),320,3)]:
    light=bpy.data.lights.new(name,'AREA'); light.energy=power; light.size=size
    obj=bpy.data.objects.new(name,light); scene.collection.objects.link(obj); obj.location=pos; aim(obj,(0,0,.9))
scene.world=bpy.data.worlds.new('NeutralStudio'); scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.19,.19,.20,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
camera=bpy.data.objects.new('MotherPreview',bpy.data.cameras.new('MotherPreview'))
scene.collection.objects.link(camera); camera.location=(1.4,-8,2.65); aim(camera,(.2,0,.90))
camera.data.type='ORTHO'; camera.data.ortho_scale=3.0; scene.camera=camera
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=32; scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.resolution_x=1280; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.filepath=str(ROOT/'ArtSource/Previews/VillageMother_Blender01.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/VillageMother01.blend'))
bpy.ops.render.render(write_still=True)
print('MOTHER_MODEL_PASS='+json.dumps(stats))
