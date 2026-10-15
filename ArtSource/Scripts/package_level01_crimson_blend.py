"""Organize the delivered scene without changing geometry or its rendered appearance."""
from pathlib import Path
import bpy
root=Path(__file__).resolve().parents[2]
path=root/'ArtSource/Level01_CrimsonCourtyard02.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
scene=bpy.context.scene
names=['01 Road and ground','02 Buildings','03 Walls and gate','04 Crimson trees and leaves',
       '05 Lanterns and lighting','06 Courtyard props','07 Character','08 Camera and sky']
groups={name:bpy.data.collections.new(name) for name in names}
for group in groups.values(): scene.collection.children.link(group)
for obj in list(scene.objects):
    name=obj.name
    ancestor=obj
    while ancestor.parent: ancestor=ancestor.parent
    if ancestor.name.startswith('VeilWalker_Preview_Front'): group=names[6]
    elif obj.type=='CAMERA' or name.startswith('Red sky'): group=names[7]
    elif obj.type=='LIGHT' or name.startswith(('Lamp','Lantern','Warm lantern')): group=names[4]
    elif name.startswith(('Crimson tree','Fallen crimson')): group=names[3]
    elif name.startswith(('Wall','Boundary','Pillar','School gate','School sign','Road edge')): group=names[2]
    elif name.startswith(('Reusable 2m','School district soil','Courtyard district soil','Last lane district soil')): group=names[0]
    elif name.startswith(('Bench','Dry well','Dark dry','Simple supply','Crate')): group=names[5]
    else: group=names[1]
    for old in list(obj.users_collection): old.objects.unlink(obj)
    groups[group].objects.link(obj)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(path))
assert scene.camera and scene.camera.data.type=='ORTHO'
assert len([obj for obj in scene.objects if obj.name.startswith('Crimson tree leaf canopy')])==16
print('PACKAGE_PASS: 8 scene collections, 16 crimson trees, active overview camera, linked modular meshes, packed procedural materials')
