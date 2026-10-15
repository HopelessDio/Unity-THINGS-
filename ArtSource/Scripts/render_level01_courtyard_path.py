"""Low-memory preview render of the saved Level 1 model, no geometry edits."""
from pathlib import Path
import bpy
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Level01_CourtyardPath_Blockout01.blend'))
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.cycles.use_denoising=False
scene.render.resolution_x=1120; scene.render.resolution_y=1330
scene.render.resolution_percentage=100
scene.render.filepath=str(root/'ArtSource/Previews/Level01_CourtyardPath_Blockout01.png')
bpy.ops.render.render(write_still=True)
print('LEVEL01_PREVIEW_RENDERED_FROM_SAVED_MODEL')
