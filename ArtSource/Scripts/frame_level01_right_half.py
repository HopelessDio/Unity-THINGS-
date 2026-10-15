"""Fill the right half with the map, preserving model scale and left-side view."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_LowerRight06.blend'))
scene=bpy.context.scene; camera=scene.camera
bpy.context.view_layer.update()
right=camera.rotation_euler.to_matrix()@Vector((1,0,0))
up=camera.rotation_euler.to_matrix()@Vector((0,1,0))
points=[]
for obj in scene.objects:
    if obj.type!='MESH' or obj.hide_render or obj.name.startswith('Red sky'): continue
    # The cliff's global bounding box includes empty space around the bends.
    # Use its real vertices so framing measures the visible map, not that box.
    local_points=(vertex.co for vertex in obj.data.vertices) if obj.name.startswith('Cliff foundation') else (Vector(corner) for corner in obj.bound_box)
    points.extend(obj.matrix_world@point for point in local_points)
xs=[point.dot(right) for point in points]; ys=[point.dot(up) for point in points]
# Match the canvas to the route's projected shape: full-height map in the
# right half, a full atmospheric background in the left half.
height=(max(ys)-min(ys))/.9
aspect=((max(xs)-min(xs))/.47)/height
render_height=900; render_width=round(render_height*aspect/16)*16
aspect=render_width/render_height
camera.data.ortho_scale=height*max(1,aspect)
width=height*aspect
center_x=(min(xs)+max(xs))*.5-(.745-.5)*width
center_y=(min(ys)+max(ys))*.5
camera.location+=right*(center_x-camera.location.dot(right))+up*(center_y-camera.location.dot(up))
scene.render.resolution_x=render_width; scene.render.resolution_y=render_height
backdrop=scene.objects['Red sky atmosphere backdrop']
direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350
backdrop.rotation_euler=camera.rotation_euler
backdrop.scale=(width*1.005,height*1.005,.1)
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_RightHalf07.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_RightHalf07.blend'))
print('RIGHT_HALF_SAVED',render_width,render_height,'model geometry unchanged',flush=True)
bpy.ops.render.render(write_still=True)
print('RIGHT_HALF_PREVIEW_RENDERED',flush=True)
