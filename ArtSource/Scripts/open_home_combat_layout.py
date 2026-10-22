"""Derive an uncluttered combat room without overwriting the furnished source."""
import json
import math
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Models/Levels/HomeInterior'
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'ArtSource/HomeInterior01.blend'))
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
layout = json.loads((OUT / 'HomeInterior_Layout.json').read_text())

def B(p): return Vector((p[0], -p[2], p[1]))
def base(obj): return obj.name.split('.')[0]

removed = {'Table top', 'Table leg', 'Table runner', 'Dining chair seat', 'Chair leg', 'Chair back',
           'Wooden plate', 'Cup', 'Cup rim', 'Bread loaf', 'Desk stool'}
bed = {'Bed frame', 'Mattress', 'Burgundy cover', 'Pillow', 'Headboard'}
desk = {'Writing desk', 'Desk leg', 'Closed book', 'Letter'}
for obj in list(scene.objects):
    name = base(obj)
    if name in removed:
        bpy.data.objects.remove(obj, do_unlink=True)
    elif name in bed:
        obj.location += B((.75, 0, .65))
    elif name == 'Bedside chest':
        obj.location += B((2.83, 0, -2.65))
    elif name in {'Storage chest', 'Chest iron strap'}:
        obj.location += B((-.6, 0, -1))
    elif name in desk:
        transform = Matrix.Translation(B((5.95, 0, -2.4))) @ Matrix.Rotation(math.pi / 2, 4, 'Z') @ Matrix.Translation(-B((4.55, 0, -2.5)))
        obj.matrix_world = transform @ obj.matrix_world

boxes = []
for box in layout['boxes']:
    name, p, size = box['name'], box['center'], box['size']
    if name in {'Dining table', 'Dining chair', 'Desk stool'}: continue
    if name == 'Bed frame': p['x'] += .75; p['z'] += .65
    elif name == 'Bedside chest': p['x'] = 5.85; p['z'] = 1.15
    elif name == 'Storage chest': p['x'] -= .6; p['z'] -= 1
    elif name == 'Writing desk': p['x'] = 5.95; p['z'] = -2.4; size['x'], size['z'] = size['z'], size['x']
    boxes.append(box)
layout['boxes'] = boxes
layout['route'] = [dict(zip('xyz', p)) for p in [(0,0,-3.6),(0,0,-1.5),(0,0,.8),(-2.8,0,1.3),
    (-3.4,0,-2.8),(3.4,0,-2.8),(3.4,0,1.4),(0,0,2.5)]]
layout['variant'] = 'Open central combat space; perimeter furniture; original source preserved'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'ArtSource/HomeInterior02_CombatOpen.blend'))

# Keep the original material IDs so Unity reuses existing materials unchanged.
geometry = [obj for obj in scene.objects if obj.type == 'MESH' and not obj.name.startswith('Preview_')]
depsgraph = bpy.context.evaluated_depsgraph_get()
batches = []
for surface in layout['materials']:
    copies = []
    for original in geometry:
        if not original.data.materials or original.data.materials[0].name != surface['source']: continue
        copy = bpy.data.objects.new('ExportPart', bpy.data.meshes.new_from_object(original.evaluated_get(depsgraph)))
        scene.collection.objects.link(copy); copy.matrix_world = original.matrix_world.copy(); copies.append(copy)
    if not copies: continue
    bpy.ops.object.select_all(action='DESELECT')
    for obj in copies: obj.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]; bpy.ops.object.join()
    obj = bpy.context.object; obj.name = surface['id']; scene.cursor.location = (0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); batches.append(obj)
markers = []
for name, pos in [('H1_Origin',(0,0,0)),('H1_BasisX',(1,0,0)),('H1_BasisY',(0,1,0)),('H1_BasisZ',(0,0,1))]:
    obj = bpy.data.objects.new(name, None); scene.collection.objects.link(obj); obj.location = B(pos); markers.append(obj)
bpy.ops.object.select_all(action='DESELECT')
for obj in batches + markers: obj.select_set(True)
bpy.context.view_layer.objects.active = batches[0]
bpy.ops.export_scene.fbx(filepath=str(OUT / 'HomeInterior_CombatOpen.fbx'), use_selection=True,
    object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False, apply_unit_scale=True)
layout['renderBatches'] = len(batches)
layout['triangles'] = sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in batches)
(OUT / 'HomeInterior_CombatOpen_Layout.json').write_text(json.dumps(layout, indent=2))
assert layout['renderBatches'] <= 12 and len(boxes) == 12
print('OPEN HOME EXPORT PASS: ' + json.dumps({'batches': len(batches), 'triangles': layout['triangles'], 'colliders': len(boxes)}))
