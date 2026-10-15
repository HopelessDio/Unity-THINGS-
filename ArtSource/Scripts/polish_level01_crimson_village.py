"""Reuse cottage modules for village density and render a clean realtime preview."""
from pathlib import Path
import bpy
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
path=root/'ArtSource/Level01_CrimsonCourtyard02.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
scene=bpy.context.scene
prefixes=('East courtyard house','Facade','Door','Reusable slate roof shingle','Roof ridge cap')
templates=[obj for obj in scene.objects if obj.type=='MESH' and obj.name.startswith(prefixes)
    and abs(obj.location.x-17)<5 and abs(obj.location.y-2)<5]
group=bpy.data.collections.get('02 Buildings')
assert templates and group
for index,(x,y) in enumerate([(-15,-22),(14,-30),(15,19),(-13,43),(32,30),(-30,25)]):
    for source in templates:
        obj=source.copy(); obj.data=source.data; obj.name='Village cottage '+str(index+1)+' '+source.name
        obj.location+=Vector((x-17,y-2,0)); group.objects.link(obj)
scene.render.engine='BLENDER_EEVEE'
if hasattr(scene.eevee,'taa_render_samples'): scene.eevee.taa_render_samples=64
for obj in scene.objects:
    if obj.type=='LIGHT' and obj.data.type=='AREA': obj.data.energy*=1.2
data=bpy.data.lights.new('Soft ambient sky fill','SUN'); data.energy=.3; data.color=(.54,.63,.9); data.angle=.3
lamp=bpy.data.objects.new('Soft ambient sky fill',data); bpy.data.collections['05 Lanterns and lighting'].objects.link(lamp)
lamp.rotation_euler=(.4,-.5,-.3)
scene.view_settings.exposure=.55
scene.render.filepath=str(root/'ArtSource/Previews/Level01_CrimsonCourtyard02.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(path))
print('FINAL_SCENE_SAVED',len(scene.objects),'objects',flush=True)
bpy.ops.render.render(write_still=True)
print('CLEAN_OVERVIEW_RENDERED',flush=True)
camera=scene.camera; camera.location=(29,-35,33); target=Vector((0,0,1))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=48
scene.render.resolution_x=1200; scene.render.resolution_y=1000
backdrop=scene.objects.get('Red sky atmosphere backdrop')
if backdrop:
    direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
    backdrop.location=target+direction*130; backdrop.rotation_euler=camera.rotation_euler
scene.render.filepath=str(root/'ArtSource/Previews/Level01_CrimsonCourtyard02_Detail.png')
bpy.ops.render.render(write_still=True)
print('CLEAN_COURTYARD_DETAIL_RENDERED',flush=True)
