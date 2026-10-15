"""Use the generated distant-village picture behind the real 3D map."""
from pathlib import Path
import bpy
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Level01_CrimsonCourtyard02.blend'))
scene=bpy.context.scene; camera=scene.camera
backdrop=scene.objects['Red sky atmosphere backdrop']
material=bpy.data.materials.new('Crimson village picture background'); material.use_nodes=True
nodes=material.node_tree.nodes; links=material.node_tree.links; nodes.clear()
output=nodes.new('ShaderNodeOutputMaterial'); emission=nodes.new('ShaderNodeEmission')
texture=nodes.new('ShaderNodeTexImage'); texture.image=bpy.data.images.load(str(root/'ArtSource/Concepts/Level01_CrimsonVillage_Backdrop03.png'))
texture.image.pack(); coords=nodes.new('ShaderNodeTexCoord')
links.new(coords.outputs['Generated'],texture.inputs['Vector']); links.new(texture.outputs['Color'],emission.inputs['Color'])
links.new(emission.outputs[0],output.inputs[0]); emission.inputs['Strength'].default_value=.8
backdrop.material_slots[0].link='OBJECT'; backdrop.material_slots[0].material=material
direction=camera.rotation_euler.to_matrix()@Vector((0,0,-1))
backdrop.location=camera.location+direction*350; backdrop.rotation_euler=camera.rotation_euler
aspect=scene.render.resolution_x/scene.render.resolution_y
backdrop.scale=(camera.data.ortho_scale*aspect*1.005,camera.data.ortho_scale*1.005,.1)
# Fade only the large soil base's border so it meets the foggy picture naturally.
for obj in scene.objects:
    if not obj.name.endswith('district soil'): continue
    mat=obj.material_slots[0].material.copy(); mat.name='Village ground fading into backdrop'
    obj.material_slots[0].link='OBJECT'; obj.material_slots[0].material=mat
    nodes=mat.node_tree.nodes; links=mat.node_tree.links
    shader=nodes.get('Principled BSDF'); output=nodes.get('Material Output')
    coord=nodes.new('ShaderNodeTexCoord'); split=nodes.new('ShaderNodeSeparateXYZ')
    links.new(coord.outputs['Generated'],split.inputs[0]); edges=[]
    for axis in ['X','Y']:
        inverse=nodes.new('ShaderNodeMath'); inverse.operation='SUBTRACT'; inverse.inputs[0].default_value=1
        links.new(split.outputs[axis],inverse.inputs[1])
        near=nodes.new('ShaderNodeMath'); near.operation='MINIMUM'
        links.new(split.outputs[axis],near.inputs[0]); links.new(inverse.outputs[0],near.inputs[1]); edges.append(near)
    near=nodes.new('ShaderNodeMath'); near.operation='MINIMUM'
    for i in range(2): links.new(edges[i].outputs[0],near.inputs[i])
    fade=nodes.new('ShaderNodeMath'); fade.operation='MULTIPLY'; fade.inputs[1].default_value=7; fade.use_clamp=True
    links.new(near.outputs[0],fade.inputs[0])
    transparent=nodes.new('ShaderNodeBsdfTransparent'); mix=nodes.new('ShaderNodeMixShader')
    links.new(fade.outputs[0],mix.inputs[0]); links.new(transparent.outputs[0],mix.inputs[1]); links.new(shader.outputs[0],mix.inputs[2])
    links.new(mix.outputs[0],output.inputs['Surface'])
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=24; scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED'; scene.render.threads=2
scene.render.resolution_x=840; scene.render.resolution_y=1200
scene.render.filepath=str(root/'ArtSource/Previews/Level01_CrimsonCourtyard03_Background.png')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtSource/Level01_CrimsonCourtyard03_Background.blend'))
print('PICTURE_BACKGROUND_PACKED_AND_SAVED',flush=True)
bpy.ops.render.render(write_still=True)
print('UPDATED_BACKGROUND_RENDER_COMPLETE',flush=True)
