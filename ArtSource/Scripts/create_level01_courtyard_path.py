"""Actual modular Level 1 route blockout and render; does not edit Unity assets."""
from pathlib import Path
import math
import random
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
random.seed(17)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene

def material(name,color,emission=0):
    mat=bpy.data.materials.new(name); mat.diffuse_color=(*color,1); mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(*color,1)
    shader.inputs['Roughness'].default_value=.85
    if emission:
        shader.inputs['Emission Color'].default_value=(*color,1)
        shader.inputs['Emission Strength'].default_value=emission
    return mat

stone=[material('Slate_'+str(i),(.10+i*.008,.12+i*.008,.15+i*.009)) for i in range(5)]
wall=material('Charcoal masonry',(.07,.082,.105))
roof=material('Simple dark roofs',(.045,.055,.08))
wood=material('Muted wood',(.12,.078,.065))
red=material('Burgundy cloth',(.22,.035,.052))
earth=material('Dried red ground',(.07,.019,.029))
iron=material('Black iron',(.026,.028,.038))
glow=material('Amber windows',(1,.19,.035),3)

def cube(name,loc,size,mat,bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    obj=bpy.context.object; obj.name=name; obj.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.append(mat)
    if bevel:
        mod=obj.modifiers.new('Small readable edge bevel','BEVEL'); mod.width=bevel; mod.segments=1
        obj.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return obj

def cylinder(name,loc,radius,depth,mat,vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
    obj=bpy.context.object; obj.name=name; obj.data.materials.append(mat); return obj

def beam(name,a,b,radius,mat):
    a,b=Vector(a),Vector(b); obj=cylinder(name,(a+b)*.5,radius,(b-a).length,mat,8)
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); return obj

tiles=set()
def pave(x0,x1,y0,y1):
    for x in range(x0,x1,2):
        for y in range(y0,y1,2): tiles.add((x,y))

# Roughly 110 m centreline from school gate to the house entrance.
pave(-4,4,-44,-12); pave(-12,12,-12,12)
pave(-4,4,12,32); pave(-4,24,30,38); pave(16,24,36,56)
for x,y in sorted(tiles):
    cube('Reusable 2m paving slab',(x+1,y+1,-.16),(1.96,1.96,.3),random.choice(stone),.025)
    for dx,dy in [(-2,0),(2,0),(0,-2),(0,2)]:
        if (x+dx,y+dy) not in tiles:
            cube('Road edge coping',(x+1+dx*.5,y+1+dy*.5,.09),
                 (.22,2,.22) if dx else (2,.22,.22),wall)
cube('Village ground foundation',(4,6,-.64),(57,110,.65),earth,.1)

def low_wall(name,x,y,length,along_x=True):
    for start in range(0,int(length),2):
        px=x+start+1 if along_x else x
        py=y if along_x else y+start+1
        cube(name,(px,py,.65),(1.94,.5,1.3) if along_x else (.5,1.94,1.3),wall)
        cube('Wall cap',(px,py,1.35),(2,.62,.15) if along_x else (.62,2,.15),random.choice(stone))

low_wall('School lane west wall',-5,-43,28,False)
low_wall('School lane east wall',5,-43,28,False)
low_wall('Final lane west wall',-5,14,16,False)
low_wall('Final lane east wall',5,14,14,False)
low_wall('House approach west wall',15,39,16,False)
low_wall('House approach east wall',25,39,16,False)

def building(name,x,y,width=8,depth=7,height=5):
    cube(name+' walls',(x,y,height*.5),(width,depth,height),wall,.08)
    # One simple wedge mesh rather than individually modelled roof tiles.
    z=height; w=width*.5+.35; d=depth*.5+.35
    verts=[(-w,-d,z),(w,-d,z),(0,-d,z+2.3),(-w,d,z),(w,d,z),(0,d,z+2.3)]
    mesh=bpy.data.meshes.new(name+' roof mesh'); mesh.from_pydata(verts,[],[(0,1,2),(3,5,4),(0,3,4,1),(0,2,5,3),(1,4,5,2)])
    obj=bpy.data.objects.new(name+' gable roof',mesh); scene.collection.objects.link(obj); obj.location=(x,y,0); mesh.materials.append(roof)
    front=y-depth*.5-.06
    cube(name+' door',(x,front,1.3),(1.35,.16,2.6),wood)
    cube(name+' doorway step',(x,front-.5,.12),(2.1,1.1,.24),stone[3])
    for side in [-1,1]:
        wx=x+side*width*.31
        cube(name+' window frame',(wx,front,2.8),(1.25,.18,1.65),wood)
        cube(name+' amber glass',(wx,front-.1,2.8),(.94,.035,1.35),glow,0)
        cube(name+' window mullion',(wx,front-.14,2.8),(.09,.05,1.4),iron,0)
    cube(name+' chimney',(x+width*.29,y+.7,height+1.65),(.9,1,2.5),wall)

building('School',-11,-37,10,9,6)
building('West courtyard house',-17,-2,8,8,5)
building('East courtyard house',17,2,8,8,5)
building('Background cottage',-12,24,8,7,4.8)
building('Family home',20,59,10,8,5.2)

def lamp(x,y):
    cube('Lamp pedestal',(x,y,.3),(.8,.8,.6),wall)
    cylinder('Lamp post',(x,y,2),.09,3.4,iron,8)
    cube('Lamp lantern',(x,y,3.6),(.48,.48,.7),glow)
    cube('Lantern cap',(x,y,4),(.68,.68,.16),iron)
    data=bpy.data.lights.new('Warm lantern pool','POINT'); data.energy=100; data.color=(1,.25,.06); data.shadow_soft_size=1
    obj=bpy.data.objects.new('Warm lantern pool',data); scene.collection.objects.link(obj); obj.location=(x,y,3.5)

for point in [(-4,-40),(4,-28),(-4,-16),(-10,-8),(10,8),(-4,20),(4,30),(14,36),(24,46),(16,54)]: lamp(*point)

def bench(x,y,rotation=0):
    parts=[]
    parts.append(cube('Bench seat',(x,y,.65),(2.8,.75,.18),wood))
    parts.append(cube('Bench back',(x,y+.34,1.1),(2.8,.14,.85),wood))
    for offset in [-1,1]: parts.append(cube('Bench leg',(x+offset,y,.3),(.15,.6,.6),iron))
    if rotation:
        for obj in parts:
            delta=obj.location-Vector((x,y,0)); delta.rotate(__import__('mathutils').Matrix.Rotation(rotation,3,'Z'))
            obj.location=Vector((x,y,0))+delta; obj.rotation_euler.z=rotation

bench(-8,-2,math.pi*.5); bench(8,3,-math.pi*.5)
# Hollow dry well assembled from reusable wedge-like blocks.
for layer in range(3):
    for i in range(12):
        angle=2*math.pi*(i+.5*(layer%2))/12
        obj=cube('Dry well block',(2*math.cos(angle),2*math.sin(angle),.25+layer*.48),(.92,.5,.46),stone[layer])
        obj.rotation_euler.z=angle+math.pi*.5
cylinder('Dark dry well interior',(0,0,.03),1.7,.08,iron,24)

def dead_tree(x,y):
    beam('Tree trunk',(x,y,0),(x+.15,y,4.3),.24,wood)
    for side in [-1,1]:
        a=(x+.1,y,2.8); b=(x+side*1.4,y+.1,4.3); c=(x+side*2.2,y+.1,5)
        beam('Angular branch',a,b,.12,wood); beam('Angular twig',b,c,.06,wood)
        beam('Angular twig',b,(b[0]-.2,y+.4,5.3),.05,wood)

dead_tree(-7,6); dead_tree(10,-19); dead_tree(29,51)
for x,y in [(-9,-33),(-13,-7),(13,-6),(13,7),(27,56),(-8,28)]:
    cube('Simple supply crate',(x,y,.65),(1.2,1.2,1.3),wood)
    for z in [.25,1.05]: cube('Crate binding',(x,y-.61,z),(1.22,.05,.1),iron,0)

# Original character, included purely as a scale reference near school exit.
source=ROOT/'ArtSource/VeilWalker_Model01.blend'
with bpy.data.libraries.load(str(source),link=False) as (available,loaded):
    loaded.objects=[name for name in available.objects if name in ['VeilWalkerRig','VeilWalker_Armature','VeilWalkerBody']]
hero=[obj for obj in loaded.objects if obj is not None]
if not hero:
    cylinder('Player scale marker',(-.8,-39,1),.32,1.4,red)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,radius=.32,location=(-.8,-39,1.9))
    bpy.context.object.data.materials.append(stone[4])
else:
    for obj in hero: scene.collection.objects.link(obj)
    for obj in hero:
        if obj.parent is None: obj.location+=Vector((-.8,-39,0))

def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('Dark burgundy atmosphere'); world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.055,.022,.035,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45; scene.world=world
for name,location,power,size,color in [
    ('Cool readable sky',(-25,-10,65),85000,65,(.57,.69,1)),
    ('Soft warm fill',(35,35,40),55000,55,(1,.38,.22))]:
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size; data.color=color
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj); obj.location=location; aim(obj,(4,6,0))
camera=bpy.data.objects.new('Level01 overview',bpy.data.cameras.new('Level01 overview'))
scene.collection.objects.link(camera); camera.location=(94,-110,142); aim(camera,(5,8,0))
camera.data.type='ORTHO'; camera.data.ortho_scale=122; camera.data.lens=45; scene.camera=camera
scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.cycles.use_denoising=False
scene.render.resolution_x=1120; scene.render.resolution_y=1330; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_CourtyardPath_Blockout01.blend'))
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_CourtyardPath_Blockout01.png')
bpy.ops.render.render(write_still=True)
print('LEVEL01 ACTUAL BLOCKOUT: saved Blender source and native render; Unity scene unchanged.')
