"""Full-frame intro camera rather than a distant floating-map overview."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_RightHalf07.blend'))
scene=bpy.context.scene
data=bpy.data.cameras.new('Level 1 intro camera')
camera=bpy.data.objects.new('Level 1 intro camera',data)
bpy.data.collections['08 Camera and sky'].objects.link(camera)
camera.location=(0,-65,46)
target=Vector((0,-23,1))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO'; data.ortho_scale=62
scene.camera=camera
hero=next((obj for obj in scene.objects if obj.name.startswith('VeilWalker_Preview_Front')),None)
if hero: hero.location=(0,-35,0)
light=bpy.data.lights.new('Intro cool readability fill','AREA'); light.energy=2400; light.shape='DISK'; light.size=12; light.color=(.59,.68,1)
obj=bpy.data.objects.new('Intro cool readability fill',light)
bpy.data.collections['05 Lanterns and lighting'].objects.link(obj); obj.location=(2,-49,18)
obj.rotation_euler=(Vector((0,-30,1))-obj.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1280; scene.render.resolution_y=720
backdrop=scene.objects['Red sky atmosphere backdrop']
direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350; backdrop.rotation_euler=camera.rotation_euler
backdrop.scale=(62.3,62.3*720/1280,.1)
scene.cycles.samples=32; scene.cycles.use_adaptive_sampling=True; scene.cycles.adaptive_threshold=.08
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_Intro08.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_Intro08.blend'))
print('INTRO_SCENE_SAVED: full-frame gameplay composition; previous overview retained as alternate camera',flush=True)
bpy.ops.render.render(write_still=True)
print('INTRO_SCENE_RENDERED',flush=True)
