"""Read-only audit of locomotion FBXs; does not save or alter any source."""
import bpy
import json
import sys
from mathutils import Matrix
from pathlib import Path

root=Path(__file__).resolve().parents[2]
folder=root/'Assets/Great Sword Pack'
files=[folder/'VeilWalker_MixamoUpload.fbx']
files+=sorted(p for p in folder.glob('*.fbx') if any(s in p.stem for s in [' idle',' run',' strafe']))
for path in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    rig=next((o for o in bpy.data.objects if o.type=='ARMATURE'),None)
    if rig is None:
        print('PACK_AUDIT '+json.dumps({'file':path.name,'rig':None})); continue
    action=rig.animation_data.action if rig.animation_data else None
    start,end=action.frame_range if action else (0,0)
    hips=next(b for b in rig.pose.bones if b.name.split(':')[-1]=='Hips')
    samples=[]
    for frame in [start,(start+end)/2,end]:
        bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame))
        samples.append(list(rig.matrix_world @ hips.head))
    delta=[samples[-1][i]-samples[0][i] for i in range(3)]
    foot=next(b for b in rig.pose.bones if b.name.split(':')[-1]=='LeftFoot')
    foot_heights=[]
    for i in range(60):
        frame=start+(end-start)*i/60
        bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame))
        foot_heights.append((rig.matrix_world @ foot.head).z)
    phase=min(range(60),key=lambda i:foot_heights[i])/60
    print('PACK_AUDIT '+json.dumps({'file':path.name,'bones':[b.name for b in rig.data.bones] if path==files[0] else len(rig.data.bones),
        'fps':bpy.context.scene.render.fps,'frames':[start,end],'hips_delta':delta,'left_stance_phase':phase}))
    if path==files[0] and '--export-reference' in sys.argv:
        print('PACK_REFERENCE_ROOT',rig.matrix_world)
        rig.animation_data_clear()
        for bone in rig.pose.bones: bone.matrix_basis=Matrix.Identity(4)
        bpy.context.view_layer.update()
        bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Models/Player/Mixamo/GreatSword_Reference.fbx'),
            use_selection=True,object_types={'ARMATURE'},axis_forward='-Z',axis_up='Y',
            add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,apply_unit_scale=True)
