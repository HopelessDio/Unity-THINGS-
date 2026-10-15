"""Complete-map PNG from the latest model, without overwriting its intro camera."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_IntroRight09.blend'))
scene=bpy.context.scene; camera=scene.camera
target=Vector((5,10,0)); camera.location=(-68,-138,138)
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.update()
right=camera.rotation_euler.to_matrix()@Vector((1,0,0)); up=camera.rotation_euler.to_matrix()@Vector((0,1,0))
points=[]
for obj in scene.objects:
    if obj.type!='MESH' or obj.hide_render or obj.name.startswith('Red sky'): continue
    local_points=(vertex.co for vertex in obj.data.vertices) if obj.name.startswith('Cliff foundation') else (Vector(corner) for corner in obj.bound_box)
    points.extend(obj.matrix_world@point for point in local_points)
xs=[point.dot(right) for point in points]; ys=[point.dot(up) for point in points]
scene.render.resolution_x=1100; scene.render.resolution_y=1600
aspect=1100/1600
camera.data.ortho_scale=max(max(ys)-min(ys),(max(xs)-min(xs))/aspect)*1.12
camera.location+=right*((min(xs)+max(xs))*.5-camera.location.dot(right))+up*((min(ys)+max(ys))*.5-camera.location.dot(up))
backdrop=scene.objects['Red sky atmosphere backdrop']; direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350; backdrop.rotation_euler=camera.rotation_euler
backdrop.scale=(camera.data.ortho_scale*aspect*1.005,camera.data.ortho_scale*1.005,.1)
scene.cycles.samples=48; scene.cycles.use_adaptive_sampling=True; scene.cycles.adaptive_threshold=.08
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_WholeMap10.png')
bpy.ops.render.render(write_still=True)
print('COMPLETE_MAP_PNG_RENDERED: latest geometry, no saved camera or scene changes',flush=True)
