"""Lit inspection image only; does not alter the saved environment lighting."""
from pathlib import Path
import bpy
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Level01_LeftCliff05.blend'))
scene=bpy.context.scene; camera=scene.objects['Knight and house scale camera']; scene.camera=camera
camera.location=(-31,-65,24); camera.rotation_euler=(Vector((-14,-38,3))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=34
light=bpy.data.lights.new('Inspection-only soft fill','AREA'); light.energy=5000; light.shape='DISK'; light.size=10
obj=bpy.data.objects.new('Inspection-only soft fill',light); scene.collection.objects.link(obj); obj.location=(-13,-53,10)
obj.rotation_euler=(Vector((-13,-43,2))-obj.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1000; scene.render.resolution_y=800
backdrop=scene.objects['Red sky atmosphere backdrop']; direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350; backdrop.rotation_euler=camera.rotation_euler; backdrop.scale=(43,34.2,.1)
scene.cycles.samples=24; scene.cycles.adaptive_threshold=.1
scene.render.filepath=str(root/'ArtSource/Previews/Level01_LeftCliff05_KnightScale.png')
bpy.ops.render.render(write_still=True)
print('LIT_KNIGHT_SCALE_PREVIEW_COMPLETE: saved scene lighting unchanged',flush=True)
