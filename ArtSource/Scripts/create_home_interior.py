"""Build an editable cutaway family home; export static batches and gameplay boxes.

Authoring coordinates are X/right, Y/up, Z/back. B() converts to Blender space.
Characters in the Blender preview are excluded from the environment FBX.
"""
import bpy
import json
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Models/Levels/HomeInterior'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
rng=random.Random(49)
geometry=[]; boxes=[]; lamps=[]; surfaces=[]


def B(p): return Vector((p[0],-p[2],p[1]))


def material(name,color,emission=0):
    mat=bpy.data.materials.new(name); mat.diffuse_color=(*color,1); mat.use_nodes=True
    node=mat.node_tree.nodes['Principled BSDF']; node.inputs['Base Color'].default_value=(*color,1)
    node.inputs['Roughness'].default_value=.85
    if emission:
        node.inputs['Emission Color'].default_value=(*color,1)
        node.inputs['Emission Strength'].default_value=emission
    surfaces.append({'id':'H1_Render_'+str(len(surfaces)).zfill(2),'source':name,
        'color':dict(zip(['r','g','b','a'],[*color,1])),'emission':emission})
    return mat


stone=material('Charcoal stone',(.055,.051,.055))
plaster=material('Warm aged plaster',(.19,.135,.105))
frame=material('Dark timber',(.045,.022,.014))
wood=material('Warm oak',(.13,.065,.032))
floor_mats=[material('Floor oak '+str(i),(.092+i*.012,.047+i*.007,.026+i*.004)) for i in range(3)]
burgundy=material('Burgundy fabric',(.19,.018,.032))
cream=material('Warm linen',(.55,.43,.30))
metal=material('Blackened iron',(.025,.024,.027))
brass=material('Aged brass',(.26,.13,.037))
glow=material('Amber flame',(.95,.19,.024),3)
red_glass=material('Crimson window',(.22,.008,.019),1.3)


def finish(obj,name,mat,bevel=0):
    obj.name=name; obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Small bevel','BEVEL'); mod.width=bevel; mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    geometry.append(obj)
    return obj


def cube(name,pos,size,mat,bevel=0,collision=False):
    bpy.ops.mesh.primitive_cube_add(size=1,location=B(pos))
    obj=bpy.context.object; obj.scale=(size[0],size[2],size[1])
    finish(obj,name,mat,bevel)
    if collision: box(name,pos,size)
    return obj


def box(name,pos,size,layer='ArenaObstacle'):
    boxes.append({'name':name,'center':dict(zip('xyz',pos)),'size':dict(zip('xyz',size)),'layer':layer})


def beam(name,a,b,width,mat):
    start,end=B(a),B(b); delta=end-start
    bpy.ops.mesh.primitive_cube_add(size=1,location=(start+end)*.5)
    obj=bpy.context.object; obj.scale=(width,width,delta.length)
    obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    return finish(obj,name,mat,.012)


def cylinder(name,pos,radius,depth,mat,vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=B(pos))
    return finish(bpy.context.object,name,mat)


def point(name,pos,power=120,color=(1,.45,.18),range_m=5):
    data=bpy.data.lights.new(name,'POINT'); data.energy=power; data.color=color; data.shadow_soft_size=.65
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj); obj.location=B(pos)
    lamps.append({'name':name,'position':dict(zip('xyz',pos)),'color':dict(zip(['r','g','b','a'],[*color,1])),
                  'intensity':2.2,'range':range_m})


# A single continuous collider supports the visually separate planks.
cube('Stone foundation',(0,-.33,0),(13.4,.5,11.4),stone,.04)
box('Continuous floor',(0,-.12,0),(13,.24,11),'Default')
for row in range(37):
    z=-5.35+row*.29
    for column in range(7):
        x=-6.43+column*1.86
        width=min(1.84,6.5-x)
        if width>0:
            cube('Floor plank',(x+width*.5,-.03,z),(width,.06,.28),rng.choice(floor_mats))

# Full far walls; low near-wall sections reveal the room from the camera.
cube('Back plaster wall',(0,1.70,5.5),(13,.0+3.4,.22),plaster)
cube('Left plaster wall',(-6.5,1.7,0),(.22,3.4,11),plaster)
for pos,size in [((0,1.8,5.65),(13.4,3.6,.3)),((-6.65,1.8,0),(.3,3.6,11.4)),
                 ((6.65,1.8,0),(.3,3.6,11.4)),((0,1.8,-5.65),(13.4,3.6,.3))]:
    box('Room boundary',pos,size)
cube('Front cutaway left',(-3.9,.16,-5.5),(5.2,.32,.25),stone,.025)
cube('Front cutaway right',(3.9,.16,-5.5),(5.2,.32,.25),stone,.025)
cube('Right cutaway',(6.5,.16,0),(.25,.32,11),stone,.025)
for y in [.15,3.35]:
    cube('Back wall beam',(0,y,5.34),(13,.16,.18),frame,.015)
    cube('Left wall beam',(-6.34,y,0),(.18,.16,11),frame,.015)
for x in [-6.3,-3.25,0,3.25,6.3]:
    cube('Back upright',(x,1.72,5.33),(.17,3.25,.2),frame,.015)
for z in [-5.3,-2.6,0,2.6,5.3]:
    cube('Left upright',(-6.33,1.72,z),(.2,3.25,.17),frame,.015)
for x in [-4.8,1.6,4.8]:
    beam('Back diagonal brace',(x-1.35,2.45,5.30),(x-.55,3.27,5.30),.12,frame)


def window(x):
    cube('Red window glass',(x,2.00,5.19),(1.36,1.45,.03),red_glass)
    for dx in [-.76,.76]: cube('Window side',(x+dx,2,5.12),(.12,1.68,.14),frame,.01)
    for y in [1.18,2.82]: cube('Window lintel',(x,y,5.10),(1.65,.13,.19),frame,.01)
    cube('Window centre mullion',(x,2,5.08),(.075,1.52,.16),frame)
    cube('Window crossbar',(x,2,5.08),(1.45,.065,.16),frame)
    cube('Window sill',(x,1.11,5.02),(1.8,.13,.40),stone,.025)


window(-1.7); window(3.8)

# Left-side hearth opens into the room (+X), with a generous approach aisle.
cube('Hearth back',(-6.04,1.02,2.55),(.68,2.04,2.25),stone,.035)
cube('Fire void',(-5.66,.64,2.55),(.04,1.10,1.43),metal)
for z in [1.65,3.45]: cube('Hearth jamb',(-5.52,.72,z),(.66,1.44,.34),stone,.035)
cube('Hearth lintel',(-5.52,1.48,2.55),(.72,.35,2.25),stone,.035)
cube('Oak mantel',(-5.48,1.71,2.55),(.86,.16,2.44),wood,.03)
cube('Hearth slab',(-5.5,.10,2.55),(1.4,.20,2.5),stone,.035)
box('Hearth collision',(-5.76,1.10,2.55),(1.15,2.2,2.5))
for z in [2.15,2.55,2.95]:
    beam('Fire log',(-5.57,.25,z-.28),(-5.38,.25,z+.28),.15,frame)
    bpy.ops.mesh.primitive_cone_add(vertices=7,radius1=.13,radius2=.025,depth=.52,location=B((-5.47,.52,z)))
    finish(bpy.context.object,'Simple flame',glow)
point('Hearth warmth',(-4.98,1.16,2.55),260,range_m=6.5)

# Table and four stools/chairs leave both sides of the room navigable.
cube('Table top',(-.45,.94,.55),(2.8,.16,1.6),wood,.035)
for x in [-1.55,.65]:
    for z in [-.02,1.12]: cube('Table leg',(x,.43,z),(.15,.86,.15),frame,.015)
box('Dining table',(-.45,.49,.55),(2.8,.98,1.6))
cube('Table runner',(-.45,1.027,.55),(.55,.012,1.45),burgundy)
for z in [-.75,1.86]:
    cube('Dining chair seat',(-.45,.47,z),(.68,.12,.64),wood,.025)
    for x in [-.69,-.21]:
        for dz in [-.22,.22]: cube('Chair leg',(x,.23,z+dz),(.08,.46,.08),frame)
    back=z+(-.27 if z<0 else .27)
    cube('Chair back',(-.45,.89,back),(.67,.65,.09),frame,.015)
    box('Dining chair',(-.45,.55,z),(.72,1.1,.7))
for x in [-1.22,.35]:
    cylinder('Wooden plate',(x,1.039,.58),.22,.022,cream,16)
    cylinder('Cup',(x,1.13,.20),.075,.18,wood,12)
    cylinder('Cup rim',(x,1.225,.20),.079,.018,cream,12)
cube('Bread loaf',(-.45,1.11,.75),(.34,.16,.23),cream,.07)

# Kitchen on the far wall and a full-sized bed on the right.
cube('Kitchen cabinet',(1.35,.52,4.9),(2.8,1.04,.82),frame,.025,True)
cube('Kitchen counter',(1.35,1.08,4.88),(3.0,.13,.98),wood,.025)
for x in [.48,1.38,2.28]:
    cube('Cabinet panel',(x,.53,4.46),(.76,.78,.035),wood,.015)
    cylinder('Counter bowl',(x,1.19,4.85),.14,.12,cream,12)
cube('Wall shelf',(1.0,2.12,5.06),(2.3,.1,.55),wood,.02)
for x in [.2,.7,1.3,1.85]:
    cylinder('Shelf jar',(x,2.29,5.02),.10,.25,cream,12)
    cylinder('Jar lid',(x,2.43,5.02),.11,.035,frame,12)
cube('Cupboard',(-4.2,1.04,4.95),(1.55,2.08,.73),frame,.035,True)
for x in [-4.58,-3.82]: cube('Cupboard door',(x,1.02,4.56),(.69,1.85,.05),wood,.02)
cube('Bed frame',(4.6,.35,2.8),(2.0,.56,3.1),frame,.035,True)
cube('Mattress',(4.6,.68,2.8),(1.84,.24,2.94),cream,.07)
cube('Burgundy cover',(4.6,.83,2.35),(1.88,.10,2.05),burgundy,.04)
cube('Pillow',(4.6,.88,3.87),(1.20,.18,.55),cream,.10)
cube('Headboard',(4.6,.98,4.38),(2.05,1.40,.15),wood,.03)
cube('Bedside chest',(3.02,.44,3.8),(.75,.88,.75),wood,.03,True)

# Arrival corner, writing table and a rug (visual only, no trip edges).
cube('Storage chest',(-4.65,.40,-3.75),(1.85,.8,.84),wood,.04,True)
for x in [-5.27,-4.03]: cube('Chest iron strap',(x,.43,-3.75),(.08,.86,.88),metal)
cube('Writing desk',(4.55,.86,-2.5),(2.3,.14,.85),wood,.025)
for x in [3.55,5.55]:
    for z in [-2.82,-2.18]: cube('Desk leg',(x,.41,z),(.12,.82,.12),frame)
box('Writing desk',(4.55,.46,-2.5),(2.3,.92,.85))
cube('Closed book',(4.5,.97,-2.45),(.43,.08,.34),burgundy,.01)
cube('Letter',(5.1,.945,-2.45),(.32,.012,.26),cream)
cube('Desk stool',(4.35,.40,-1.55),(.6,.8,.6),frame,.025,True)
cube('Burgundy rug',(.3,.008,-1.0),(4.3,.012,4.6),burgundy)
for x in [-1.72,2.32]: cube('Rug side border',(x,.016,-1.0),(.045,.006,4.38),cream)
for z in [-3.18,1.18]: cube('Rug end border',(.3,.016,z),(4.08,.006,.045),cream)
cube('Door threshold',(0,.008,-5.30),(2.5,.016,.45),wood)
cube('Entry mat',(0,.014,-4.77),(1.75,.012,.72),frame)
for x,z in [(2.7,4.9),(-3.2,-4.9)]:
    cube('Lantern base',(x,1.25,z),(.26,.06,.26),metal,.012)
    cube('Lantern glass',(x,1.48,z),(.17,.38,.17),glow,.02)
    cube('Lantern cap',(x,1.71,z),(.28,.08,.28),metal,.02)
    point('Amber lantern',(x,1.8,z),100,range_m=5)

layout={'materials':surfaces,'boxes':boxes,'lamps':lamps,
    'player':dict(zip('xyz',(0,1.03,-3.6))),'mother':dict(zip('xyz',(-3.8,0,2.55))),
    'motherForward':dict(zip('xyz',(1,0,-.5))),
    'camera':dict(zip('xyz',(12,14,-17))),'cameraTarget':dict(zip('xyz',(0,.8,0))),
    'route':[dict(zip('xyz',p)) for p in [(0,0,-3.6),(1.8,0,-2.8),(2.1,0,-.5),
        (2.1,0,1.6),(1.9,0,2.85),(0,0,2.85),(-2.0,0,2.85),(-2.8,0,2.55)]],
    'verticalSpan':13.7,'horizontalSpan':23.2}

# Append only the existing frozen mother preview, never her studio or lights.
with bpy.data.libraries.load(str(ROOT/'ArtSource/VillageMother01.blend'),link=False) as (available,loaded):
    loaded.objects=['Preview_Front']
mother=loaded.objects[0]
scene.collection.objects.link(mother); mother.name='Preview_Mother_NotExported'
mother.location=B((-3.8,0,2.55)); mother.scale=(.95,.95,.95)
mother.rotation_euler=Vector((0,-1,0)).rotation_difference(B((1,0,-.5)).normalized()).to_euler()


def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


for name,pos,power,size in [('Room key',(0,9,-1),1800,8),('Soft front fill',(5,7,-9),1300,7)]:
    light=bpy.data.lights.new(name,'AREA'); light.energy=power; light.size=size
    obj=bpy.data.objects.new(name,light); scene.collection.objects.link(obj); obj.location=B(pos); aim(obj,B((0,.6,0)))
scene.world=bpy.data.worlds.new('Dark crimson beyond the cutaway'); scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.08,.035,.045,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
camera=bpy.data.objects.new('HomePreview',bpy.data.cameras.new('HomePreview')); scene.collection.objects.link(camera)
camera.location=B((12,14,-17)); aim(camera,B((0,.8,0))); camera.data.type='ORTHO'; camera.data.ortho_scale=22.8
scene.camera=camera; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=32; scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.resolution_x=1440; scene.render.resolution_y=1080; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.filepath=str(ROOT/'ArtSource/Previews/HomeInterior_Blender01.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/HomeInterior01.blend'))

# Static game export: one mesh per material, no studio mother/cameras/lights.
batches=[]; depsgraph=bpy.context.evaluated_depsgraph_get()
for index,mat in enumerate([stone,plaster,frame,wood,*floor_mats,burgundy,cream,metal,brass,glow,red_glass]):
    copies=[]
    for original in geometry:
        if original.data.materials[0]!=mat: continue
        data=bpy.data.meshes.new_from_object(original.evaluated_get(depsgraph))
        obj=bpy.data.objects.new('ExportPart',data); scene.collection.objects.link(obj)
        obj.matrix_world=original.matrix_world.copy(); copies.append(obj)
    if not copies: continue
    bpy.ops.object.select_all(action='DESELECT')
    for obj in copies: obj.select_set(True)
    bpy.context.view_layer.objects.active=copies[0]; bpy.ops.object.join()
    batch=bpy.context.object; batch.name=surfaces[index]['id']
    bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); batches.append(batch)
markers=[]
for name,pos in [('H1_Origin',(0,0,0)),('H1_BasisX',(1,0,0)),('H1_BasisY',(0,1,0)),('H1_BasisZ',(0,0,1))]:
    obj=bpy.data.objects.new(name,None); scene.collection.objects.link(obj); obj.location=B(pos); markers.append(obj)
bpy.ops.object.select_all(action='DESELECT')
for obj in batches+markers: obj.select_set(True)
bpy.context.view_layer.objects.active=batches[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'HomeInterior.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
    axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,apply_unit_scale=True)
layout['renderBatches']=len(batches)
layout['triangles']=sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in batches)
(OUT/'HomeInterior_Layout.json').write_text(json.dumps(layout,indent=2))
assert layout['triangles']<30000
for obj in batches+markers: bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.render.render(write_still=True)
print('HOME MODEL PASS='+json.dumps({'batches':layout['renderBatches'],'triangles':layout['triangles'],
    'gameplayBoxes':len(boxes),'source':'ArtSource/HomeInterior01.blend','charactersExported':False}))
