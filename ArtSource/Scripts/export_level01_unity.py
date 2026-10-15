"""Export the approved native geometry in material batches, plus gameplay metadata."""
from pathlib import Path
import json
import math
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Models/Levels/Level01'; OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_IntroRight09.blend'))
scene=bpy.context.scene; bpy.context.view_layer.update()
def vec(v): return dict(x=float(v[0]),y=float(v[1]),z=float(v[2]))
def color(v): return dict(r=float(v[0]),g=float(v[1]),b=float(v[2]),a=1)
hero=bpy.data.collections.get('07 Character')
excluded=set(hero.all_objects) if hero else set()
deps=bpy.context.evaluated_depsgraph_get()
buckets={}; records=[]; colliders=[]; floors=[]; lights=[]; material_ids={}
for obj in list(scene.objects):
    if obj.type=='LIGHT' and obj.data.type=='POINT':
        lights.append(dict(position=vec(obj.matrix_world.translation),color=color(obj.data.color),power=float(obj.data.energy)))
    if obj.type not in {'MESH','FONT'} or obj in excluded or obj.hide_render or obj.name.startswith('Red sky'): continue
    name=obj.name
    corners=[obj.matrix_world@Vector(p) for p in obj.bound_box]
    low=Vector(tuple(min(p[i] for p in corners) for i in range(3)))
    high=Vector(tuple(max(p[i] for p in corners) for i in range(3)))
    center=(low+high)*.5; size=high-low
    if name.startswith('Reusable 2m paving slab'):
        floors.append(dict(name=name,center=vec(center),size=vec(size)))
    obstacle=(' walls' in name or name.startswith(('Boundary pillar','School gate pillar','School gate lintel','Crimson tree trunk','Bench seat')))
    if name.startswith('Wall individual stone') and center.z<.45:
        obstacle=True; center.z=.82; size.z=1.7
    if name.startswith('Dry well block') and center.z<.5:
        obstacle=True; center.z=.75; size.z=1.5
    if obstacle: colliders.append(dict(name=name,center=vec(center),size=vec(size)))
    evaluated=obj.evaluated_get(deps); mesh=evaluated.to_mesh()
    if not mesh: continue
    mesh.calc_loop_triangles()
    normal_matrix=obj.matrix_world.to_3x3().inverted().transposed()
    for tri in mesh.loop_triangles:
        material=obj.material_slots[tri.material_index].material if tri.material_index<len(obj.material_slots) else None
        source_name=material.name if material else 'Default stone'
        positions=[obj.matrix_world@mesh.vertices[vertex_index].co for vertex_index in tri.vertices]
        middle=sum(positions,Vector())/3
        # Local batches avoid the forward renderer's per-object light limit
        # assigning four distant lanterns to a single 110m-long mesh.
        local_lighting=source_name.startswith(('Slate_','Slate roof tiles','Crimson village stone','Charcoal masonry','Door and beam wood'))
        key=(source_name,math.floor(middle.x/24) if local_lighting else 0,math.floor(middle.y/24) if local_lighting else 0)
        if key not in buckets:
            index=len(buckets); ident='L1_Render_'+str(index).zfill(3)
            base=(.06,.07,.09,1); emission=(0,0,0,1); strength=0
            if material and material.use_nodes:
                shader=material.node_tree.nodes.get('Principled BSDF')
                if shader:
                    base=tuple(shader.inputs['Base Color'].default_value)
                    emission=tuple(shader.inputs['Emission Color'].default_value)
                    strength=float(shader.inputs['Emission Strength'].default_value)
            buckets[key]=dict(id=ident,vertices=[],faces=[],normals=[])
            if source_name not in material_ids: material_ids[source_name]='Level01_Material_'+str(len(material_ids)).zfill(3)
            records.append(dict(id=ident,materialId=material_ids[source_name],source=source_name,color=color(base),emission=color(emission),strength=strength,
                                walkable=source_name=='Cliff terrace dark stone'))
        bucket=buckets[key]; start=len(bucket['vertices'])
        for position,loop_index in zip(positions,tri.loops):
            bucket['vertices'].append(tuple(position))
            bucket['normals'].append(tuple((normal_matrix@mesh.corner_normals[loop_index].vector).normalized()))
        bucket['faces'].append((start,start+1,start+2))
    evaluated.to_mesh_clear()

camera=scene.camera
camera_position=camera.matrix_world.translation.copy()
camera_forward=camera.matrix_world.to_3x3()@Vector((0,0,-1))
camera_up=camera.matrix_world.to_3x3()@Vector((0,1,0))
metadata=dict(materials=records,floors=floors,obstacles=colliders,lights=lights,
    cameraPosition=vec(camera_position),cameraTarget=vec(camera_position+camera_forward*50),cameraUp=vec(camera_up),
    referenceWidth=float(camera.data.ortho_scale),spawn=vec((0,-35,1.03)),
    route=[vec(p) for p in [(0,-35,0),(0,-20,0),(0,-8,0),(3,5,0),(0,18,0),(0,28,0),(0,34,0),(10,34,0),(20,34,0),(20,44,0),(20,53,0)]])

# Export only new material-batched meshes, never the old source hero/cameras/lights.
bpy.ops.object.select_all(action='DESELECT'); exports=[]
for bucket in buckets.values():
    mesh=bpy.data.meshes.new(bucket['id']); mesh.from_pydata(bucket['vertices'],[],bucket['faces']); mesh.update()
    for polygon in mesh.polygons: polygon.use_smooth=True
    mesh.normals_split_custom_set_from_vertices(bucket['normals'])
    obj=bpy.data.objects.new(bucket['id'],mesh); scene.collection.objects.link(obj); obj.select_set(True); exports.append(obj)
for name,position in [('L1_Origin',(0,0,0)),('L1_BasisX',(1,0,0)),('L1_BasisY',(0,1,0)),('L1_BasisZ',(0,0,1))]:
    obj=bpy.data.objects.new(name,None); scene.collection.objects.link(obj); obj.location=position; obj.select_set(True); exports.append(obj)
bpy.context.view_layer.objects.active=exports[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Level01_CrimsonVillage.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
    axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,path_mode='STRIP')
(OUT/'Level01_Layout.json').write_text(json.dumps(metadata,indent=2))
assert len(floors)>300 and len(records)>10 and colliders
print('LEVEL01_EXPORT_PASS '+json.dumps(dict(render_batches=len(records),floor_boxes=len(floors),obstacles=len(colliders),
    triangles=sum(len(bucket['faces']) for bucket in buckets.values()),source_blend_unchanged=True)),flush=True)
