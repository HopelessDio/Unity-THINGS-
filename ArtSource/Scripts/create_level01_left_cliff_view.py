"""Left-side cliff presentation and larger architecture; new version only."""
from pathlib import Path
import math
import random
import bpy
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_CliffRoad04.blend'))
scene=bpy.context.scene; random.seed(83)
collection=bpy.data.collections['02 Buildings']
markers=[obj for obj in collection.objects if obj.type=='EMPTY' and 'enlarged' in obj.name]
prefixes=('School ','West courtyard house','East courtyard house','Background cottage','Family home',
          'Facade','Door','Reusable slate','Roof ridge','Village cottage ')
for obj in collection.objects:
    if obj.type!='MESH' or not obj.name.startswith(prefixes): continue
    center=min(markers,key=lambda marker:(Vector((obj.location.x,obj.location.y))-Vector((marker.location.x,marker.location.y))).length).location
    matrix=Matrix.Translation(center)@Matrix.Diagonal((1.1,1.1,1.15,1))@Matrix.Translation(-center)
    obj.matrix_world=matrix@obj.matrix_world
bpy.context.view_layer.update()
cliff=next(obj for obj in scene.objects if obj.name.startswith('Cliff foundation'))
occupied=set()
for poly in cliff.data.polygons:
    if poly.material_index==0 and poly.normal.z>.9:
        occupied.add((math.floor(poly.center.x/2),math.floor(poly.center.y/2)))
for obj in collection.objects:
    if obj.type!='MESH' or ' walls' not in obj.name: continue
    corners=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
    for x in range(math.floor((min(p.x for p in corners)-.35)/2),math.ceil((max(p.x for p in corners)+.35)/2)):
        for y in range(math.floor((min(p.y for p in corners)-.35)/2),math.ceil((max(p.y for p in corners)+.35)/2)): occupied.add((x,y))
verts=[]; faces=[]; materials=[]
def face(points,index):
    start=len(verts); verts.extend(points); faces.append(tuple(range(start,start+len(points)))); materials.append(index)
def bottom(x,y): return -9-1.8*math.sin(x*.43+y*.19)-.6*math.cos(y*.7)
for x,y in sorted(occupied):
    x0=x*2; y0=y*2
    face([(x0,y0,-.32),(x0+2,y0,-.32),(x0+2,y0+2,-.32),(x0,y0+2,-.32)],0)
    for delta,a,b in [((-1,0),(x0,y0+2),(x0,y0)),((1,0),(x0+2,y0),(x0+2,y0+2)),
                      ((0,-1),(x0,y0),(x0+2,y0)),((0,1),(x0+2,y0+2),(x0,y0+2))]:
        if (x+delta[0],y+delta[1]) in occupied: continue
        mid=((a[0]+b[0])*.5+delta[0]*.25,(a[1]+b[1])*.5+delta[1]*.25,-4)
        lowa=(a[0]-.18*delta[0],a[1]-.18*delta[1],bottom(*a))
        lowb=(b[0]-.18*delta[0],b[1]-.18*delta[1],bottom(*b))
        face([(*a,-.32),(*b,-.32),mid],random.randrange(1,6))
        face([(*a,-.32),mid,lowa],random.randrange(1,6)); face([mid,(*b,-.32),lowb,lowa],random.randrange(1,6))
mesh=bpy.data.meshes.new('Left-view route cliff with occupied house ledges'); mesh.from_pydata(verts,[],faces); mesh.update()
for mat in cliff.data.materials: mesh.materials.append(mat)
for poly,index in zip(mesh.polygons,materials): poly.material_index=index
cliff.data=mesh

hero=next((obj for obj in scene.objects if obj.name.startswith('VeilWalker_Preview_Front')),None)
if hero: hero.location=(-11,-45,0)

camera=scene.camera; target=Vector((6,10,0)); camera.location=(-68,-138,138)
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.update()
right=camera.rotation_euler.to_matrix()@Vector((1,0,0)); up=camera.rotation_euler.to_matrix()@Vector((0,1,0))
points=[obj.matrix_world@Vector(corner) for obj in scene.objects if obj.type=='MESH' and not obj.name.startswith('Red sky') for corner in obj.bound_box]
xs=[(p-target).dot(right) for p in points]; ys=[(p-target).dot(up) for p in points]
aspect=1000/1300
camera.data.ortho_scale=max(max(ys)-min(ys),(max(xs)-min(xs))/aspect)*1.22
camera.location+=right*((min(xs)+max(xs))*.5+camera.data.ortho_scale*aspect*.065)+up*(min(ys)+max(ys))*.5
backdrop=scene.objects['Red sky atmosphere backdrop']
def fit_background():
    direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
    backdrop.location=camera.location+direction*350; backdrop.rotation_euler=camera.rotation_euler
    backdrop.scale=(camera.data.ortho_scale*scene.render.resolution_x/scene.render.resolution_y*1.005,camera.data.ortho_scale*1.005,.1)
scene.render.resolution_x=1000; scene.render.resolution_y=1300; fit_background()
scene.cycles.samples=48; scene.cycles.use_adaptive_sampling=True; scene.cycles.adaptive_threshold=.08
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_LeftCliff05.png')
detail=bpy.data.objects.new('Knight and house scale camera',bpy.data.cameras.new('Knight and house scale camera'))
bpy.data.collections['08 Camera and sky'].objects.link(detail)
detail.location=(-29,-63,18); detail.rotation_euler=(Vector((-12,-39,3))-detail.location).to_track_quat('-Z','Y').to_euler()
detail.data.type='ORTHO'; detail.data.ortho_scale=26
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_LeftCliff05.blend'))
print('LEFT_CLIFF_SAVED: left angle, left-biased composition, larger houses, approx 2.2m knight',flush=True)
bpy.ops.render.render(write_still=True)
print('LEFT_CLIFF_OVERVIEW_RENDERED',flush=True)
scene.camera=detail; camera=detail
scene.render.resolution_x=1000; scene.render.resolution_y=800; fit_background()
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_LeftCliff05_KnightScale.png')
bpy.ops.render.render(write_still=True)
print('KNIGHT_SCALE_RENDERED',flush=True)
