"""Larger human-scale houses and a narrow cliff footprint; preserves prior versions."""
from pathlib import Path
import math
import random
import bpy
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_CrimsonCourtyard03_Background.blend'))
scene=bpy.context.scene; random.seed(83)
buildings=bpy.data.collections['02 Buildings']

def material(name,color):
    mat=bpy.data.materials.new(name); mat.diffuse_color=(*color,1); mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.95
    return mat

occupied=set(); road=set()
for obj in scene.objects:
    if obj.name.startswith('Reusable 2m paving slab'):
        cell=(round((obj.location.x-1)/2),round((obj.location.y-1)/2))
        occupied.add(cell); road.add(cell)

def cover(x0,x1,y0,y1):
    for x in range(math.floor(x0/2),math.ceil(x1/2)):
        for y in range(math.floor(y0/2),math.ceil(y1/2)): occupied.add((x,y))

def path(a,b,width=2.4):
    # Small side approaches connect doorstep terraces to the main path.
    a,b=Vector(a),Vector(b); steps=max(1,math.ceil((b-a).length))
    for i in range(steps+1):
        p=a.lerp(b,i/steps); cover(p.x-width/2,p.x+width/2,p.y-width/2,p.y+width/2)

houses=[('School',(-11,-37),(-14,-37),10,9),
    ('West courtyard house',(-17,-2),(-19,-2),8,8),
    ('East courtyard house',(17,2),(19,2),8,8),
    ('Background cottage',(-12,24),(-13,24),8,7),
    ('Family home',(20,59),(20,62),10,8)]
claimed=set()
for name,old,new,width,depth in houses:
    parts=[obj for obj in buildings.objects if obj.type=='MESH' and not obj.name.startswith('Village cottage ')
        and abs(obj.location.x-old[0])<width/2+.75 and abs(obj.location.y-old[1])<depth/2+.75
        and obj not in claimed]
    transform=Matrix.Translation((*new,0))@Matrix.Diagonal((1.5,1.5,1.35,1))@Matrix.Translation((-old[0],-old[1],0))
    parent=bpy.data.objects.new(name+' enlarged house',None); buildings.objects.link(parent)
    parent.location=(*new,0)
    for obj in parts:
        obj.matrix_world=transform@obj.matrix_world
        claimed.add(obj)
    cover(new[0]-width*.75-.55,new[0]+width*.75+.55,new[1]-depth*.75-1,new[1]+depth*.75+.55)
    door=(new[0],new[1]-depth*.75-1,0)
    nearest=min(road,key=lambda cell:(cell[0]*2+1-door[0])**2+(cell[1]*2+1-door[1])**2)
    path(door,(nearest[0]*2+1,nearest[1]*2+1,0))

old_centers=[(-15,-22),(14,-30),(15,19),(-13,43),(32,30),(-30,25)]
new_centers=[(-12,-21),(13,-29),(13,20),(-11,43),(33,30)]
for index,old in enumerate(old_centers):
    parts=[obj for obj in list(buildings.objects) if obj.name.startswith('Village cottage '+str(index+1)+' ')]
    if index==5:
        for obj in parts: bpy.data.objects.remove(obj,do_unlink=True)
        continue
    new=new_centers[index]
    transform=Matrix.Translation((*new,0))@Matrix.Diagonal((1.5,1.5,1.35,1))@Matrix.Translation((-old[0],-old[1],0))
    parent=bpy.data.objects.new('Street house '+str(index+1)+' enlarged',None); buildings.objects.link(parent); parent.location=(*new,0)
    for obj in parts:
        obj.matrix_world=transform@obj.matrix_world
    cover(new[0]-6.6,new[0]+6.6,new[1]-7,new[1]+6.6)
    door=(new[0],new[1]-7,0)
    nearest=min(road,key=lambda cell:(cell[0]*2+1-door[0])**2+(cell[1]*2+1-door[1])**2)
    path(door,(nearest[0]*2+1,nearest[1]*2+1,0))

# Delete the wide, mostly empty soil. Only route/building/tree ledges remain.
for obj in list(scene.objects):
    if obj.name.endswith('district soil') or obj.name.startswith('Fallen crimson leaves'):
        bpy.data.objects.remove(obj,do_unlink=True)

old_trees=[(-8,-45),(10,-36),(-8,-22),(10,-17),(-10,7),(11,-8),(-23,10),(24,10),
           (-7,17),(9,24),(-8,34),(9,43),(29,44),(29,57),(11,58),(28,65)]
new_trees=[(-5.5,-44),(5.5,-40),(6,-17),(-10,10),(10,-10),(-6,15),
           (6,28),(13,38),(26,43),(12,57),(28,70)]
tree_parts=[[] for _ in old_trees]
for obj in list(scene.objects):
    if not obj.name.startswith('Crimson tree'): continue
    if obj.name.startswith('Crimson tree leaf canopy'):
        center=sum((obj.matrix_world@Vector(corner) for corner in obj.bound_box),Vector())/8
    else: center=obj.location
    nearest=min(range(len(old_trees)),key=lambda i:(center.x-old_trees[i][0])**2+(center.y-old_trees[i][1])**2)
    tree_parts[nearest].append(obj)
for index,parts in enumerate(tree_parts):
    if index>=len(new_trees):
        for obj in parts: bpy.data.objects.remove(obj,do_unlink=True)
        continue
    old=old_trees[index]; new=new_trees[index]
    transform=Matrix.Translation((*new,0))@Matrix.Diagonal((.85,.85,.85,1))@Matrix.Translation((-old[0],-old[1],0))
    for obj in parts: obj.matrix_world=transform@obj.matrix_world
    cover(new[0]-1.1,new[0]+1.1,new[1]-1.1,new[1]+1.1)
    nearest=min(road,key=lambda cell:(cell[0]*2+1-new[0])**2+(cell[1]*2+1-new[1])**2)
    # Trees far behind a house share that house's footprint, not a large park.
    if (Vector((nearest[0]*2+1,nearest[1]*2+1))-Vector(new)).length<5:
        path((*new,0),(nearest[0]*2+1,nearest[1]*2+1,0),1.4)

# Make a continuous rock top and jagged, dark vertical cliff sides under the grid.
top=material('Cliff terrace dark stone',(.043,.036,.045))
rocks=[material('Cliff rock '+str(i),(.022+i*.006,.016+i*.005,.024+i*.006)) for i in range(5)]
vertices=[]; faces=[]; indices=[]
def face(points,index):
    start=len(vertices); vertices.extend(points); faces.append(tuple(range(start,start+len(points)))); indices.append(index)
def bottom(x,y): return -7.5-1.8*math.sin(x*.43+y*.19)-.6*math.cos(y*.7)
for x,y in sorted(occupied):
    x0=x*2; y0=y*2
    face([(x0,y0,-.32),(x0+2,y0,-.32),(x0+2,y0+2,-.32),(x0,y0+2,-.32)],0)
    edges=[((-1,0),(x0,y0+2),(x0,y0)),((1,0),(x0+2,y0),(x0+2,y0+2)),
           ((0,-1),(x0,y0),(x0+2,y0)),((0,1),(x0+2,y0+2),(x0,y0+2))]
    for delta,a,b in edges:
        if (x+delta[0],y+delta[1]) in occupied: continue
        middle=((a[0]+b[0])*.5+delta[0]*.25,(a[1]+b[1])*.5+delta[1]*.25,-3.5)
        face([(*a,-.32),(*b,-.32),middle],random.randrange(1,6))
        face([(*a,-.32),middle,(a[0]-.18*delta[0],a[1]-.18*delta[1],bottom(*a))],random.randrange(1,6))
        face([middle,(*b,-.32),(b[0]-.18*delta[0],b[1]-.18*delta[1],bottom(*b)),
              (a[0]-.18*delta[0],a[1]-.18*delta[1],bottom(*a))],random.randrange(1,6))
data=bpy.data.meshes.new('Narrow route cliff mesh'); data.from_pydata(vertices,[],faces); data.update()
for mat in [top]+rocks: data.materials.append(mat)
for poly,index in zip(data.polygons,indices): poly.material_index=index
cliff=bpy.data.objects.new('Cliff foundation — path and occupied ledges only',data)
bpy.data.collections['01 Road and ground'].objects.link(cliff)

# Scale the existing approved Veil Walker instance to a measurable human height.
hero=next((obj for obj in scene.objects if obj.name.startswith('VeilWalker_Preview_Front')),None)
if hero:
    points=[obj.matrix_world@Vector(corner) for obj in hero.children_recursive if obj.type=='MESH' for corner in obj.bound_box]
    if points:
        height=max(p.z for p in points)-min(p.z for p in points)
        if height>.1: hero.scale*=2.2/height
    hero.location=(0,-38,0)

camera=scene.camera; target=Vector((7,10,0)); camera.location=(47,-135,155)
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.update()
right=camera.rotation_euler.to_matrix()@Vector((1,0,0)); up=camera.rotation_euler.to_matrix()@Vector((0,1,0))
points=[obj.matrix_world@Vector(corner) for obj in scene.objects if obj.type=='MESH' and not obj.name.startswith('Red sky') for corner in obj.bound_box]
xs=[(point-target).dot(right) for point in points]; ys=[(point-target).dot(up) for point in points]
camera.location+=right*(min(xs)+max(xs))*.5+up*(min(ys)+max(ys))*.5
aspect=840/1200
camera.data.ortho_scale=max(max(ys)-min(ys),(max(xs)-min(xs))/aspect)*1.08
backdrop=scene.objects['Red sky atmosphere backdrop']; direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350; backdrop.rotation_euler=camera.rotation_euler
backdrop.scale=(camera.data.ortho_scale*aspect*1.005,camera.data.ortho_scale*1.005,.1)
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=48; scene.cycles.use_denoising=False
scene.cycles.use_adaptive_sampling=True; scene.cycles.adaptive_threshold=.08
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.resolution_x=840; scene.render.resolution_y=1200
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_CliffRoad04.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_CliffRoad04.blend'))
assert not any(obj.name.endswith('district soil') for obj in scene.objects)
print('CLIFF_ROUTE_SAVED: houses XY 1.5x, height 1.35x, doors about 3.5m, hero about 2.2m, no broad soil plane',flush=True)
bpy.ops.render.render(write_still=True)
print('CLIFF_ROUTE_RENDERED',flush=True)
