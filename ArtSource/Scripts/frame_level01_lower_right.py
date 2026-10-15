"""Camera-only lower-right composition; architecture and cliff geometry unchanged."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_LeftCliff05.blend'))
scene=bpy.context.scene; camera=scene.camera
bpy.context.view_layer.update()
right=camera.rotation_euler.to_matrix()@Vector((1,0,0))
up=camera.rotation_euler.to_matrix()@Vector((0,1,0))
points=[obj.matrix_world@Vector(corner) for obj in scene.objects
        if obj.type=='MESH' and not obj.hide_render and not obj.name.startswith('Red sky') for corner in obj.bound_box]
xs=[point.dot(right) for point in points]; ys=[point.dot(up) for point in points]
aspect=1000/1300
# Place all actual map geometry inside the lower-right region, leaving a
# substantial sky/background area above and to the left. Keep a small margin.
scale=max((max(xs)-min(xs))/(aspect*.64),(max(ys)-min(ys))/.64)
camera.data.ortho_scale=scale
width=scale*aspect
desired_x=.65; desired_y=.36
center_x=(min(xs)+max(xs))*.5-(desired_x-.5)*width
center_y=(min(ys)+max(ys))*.5-(desired_y-.5)*scale
camera.location+=right*(center_x-camera.location.dot(right))+up*(center_y-camera.location.dot(up))
scene.render.resolution_x=1000; scene.render.resolution_y=1300
backdrop=scene.objects['Red sky atmosphere backdrop']
direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350
backdrop.rotation_euler=camera.rotation_euler
backdrop.scale=(width*1.005,scale*1.005,.1)
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_LowerRight06.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_LowerRight06.blend'))
print('LOWER_RIGHT_CAMERA_SAVED: geometry unchanged, map center 65% across / 36% up frame',flush=True)
bpy.ops.render.render(write_still=True)
print('LOWER_RIGHT_PREVIEW_RENDERED',flush=True)
