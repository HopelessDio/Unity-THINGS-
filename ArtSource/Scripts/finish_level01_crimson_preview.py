"""Frame the complete route and create actual overview/detail renders."""
from pathlib import Path
import math
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'ArtSource/Level01_CrimsonCourtyard02.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
scene=bpy.context.scene
for obj in list(scene.objects):
    if obj.name.startswith(('Distant village silhouette','Distant chimney')):
        bpy.data.objects.remove(obj,do_unlink=True)
soil=[obj for obj in scene.objects if obj.name.endswith('district soil')]
if soil:
    ground=soil[0]
    ground.location=(-1,8,-.65); ground.scale=(78,134,.8)
    for obj in soil[1:]: bpy.data.objects.remove(obj,do_unlink=True)
for obj in scene.objects:
    if obj.type=='LIGHT' and obj.data.type=='POINT': obj.data.energy=850
camera=scene.camera; target=Vector((5,8,0))
camera.location=(34,-142,157)
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1120; scene.render.resolution_y=1600
bpy.context.view_layer.update()
right=camera.rotation_euler.to_matrix()@Vector((1,0,0))
up=camera.rotation_euler.to_matrix()@Vector((0,1,0))
points=[]
for obj in scene.objects:
    if obj.type!='MESH' or obj.hide_render or obj.name.startswith(('Red sky atmosphere','School district soil','Courtyard district soil','Last lane district soil')): continue
    points.extend(obj.matrix_world@Vector(corner) for corner in obj.bound_box)
xs=[(point-target).dot(right) for point in points]; ys=[(point-target).dot(up) for point in points]
center=target+right*(min(xs)+max(xs))*.5+up*(min(ys)+max(ys))*.5
camera.location+=center-target
aspect=scene.render.resolution_x/scene.render.resolution_y
camera.data.ortho_scale=max(max(ys)-min(ys),(max(xs)-min(xs))/aspect)*1.1
backdrop=scene.objects.get('Red sky atmosphere backdrop')
if backdrop:
    direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
    backdrop.location=center+direction*130; backdrop.rotation_euler=camera.rotation_euler
    backdrop.scale=(320,320,.1)
scene.cycles.samples=64; scene.cycles.use_denoising=False
scene.view_settings.look='AgX - Medium High Contrast'
scene.view_settings.exposure=.35
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_CrimsonCourtyard02.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(path))
print('FINAL_OVERVIEW_SAVED',camera.data.ortho_scale,flush=True)
bpy.ops.render.render(write_still=True)
print('OVERVIEW_RENDER_COMPLETE',flush=True)
camera.location=(29,-35,33); target=Vector((0,0,1))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=48
scene.render.resolution_x=1200; scene.render.resolution_y=1000
if backdrop:
    direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
    backdrop.location=target+direction*130; backdrop.rotation_euler=camera.rotation_euler
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_CrimsonCourtyard02_Detail.png')
bpy.ops.render.render(write_still=True)
print('DETAIL_RENDER_COMPLETE',flush=True)
