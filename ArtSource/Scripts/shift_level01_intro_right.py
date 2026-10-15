"""Camera pan only: same intro zoom, geometry, and lighting."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_Intro08.blend'))
scene=bpy.context.scene; camera=scene.camera
right=camera.rotation_euler.to_matrix()@Vector((1,0,0))
# Pan the camera left by 15% of the frame width, shifting the map right.
camera.location-=right*(camera.data.ortho_scale*.15)
backdrop=scene.objects['Red sky atmosphere backdrop']
direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_IntroRight09.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_IntroRight09.blend'))
print('INTRO_SHIFTED_RIGHT: same orthographic scale, resolution, geometry and lights',flush=True)
bpy.ops.render.render(write_still=True)
print('INTRO_RIGHT_RENDERED',flush=True)
