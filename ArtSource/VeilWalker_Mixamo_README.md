# Veil Walker Mixamo/Humanoid rig

Created 2026-10-15 as a separate rig. The original gameplay model, scene, procedural animations and sword asset are unchanged.

## Files

- `ArtSource/VeilWalker_Mixamo.blend`: editable rig and weighted character in T-pose.
- `Assets/Models/Player/Mixamo/VeilWalker_Mixamo.fbx`: skinned character with 25 bones, Mixamo-style names, cape chain and hand attachment points. No sword/gun mesh.
- `Assets/Models/Player/Mixamo/VeilWalker_MixamoUpload.fbx`: unrigged T-pose mesh for uploading to Adobe Mixamo if you want its online auto-rigging. It has not been submitted to Adobe.
- `Assets/Models/Player/Mixamo/VeilWalker_AnimationReference.fbx`: true neutral skeleton reference extracted from the original animation FBX. Animation-only files expose their first animated frame as the imported object pose; using that running/swinging pose as the Avatar reference produces incorrect muscle conversion. This reference prevents that mistake.
- `Assets/Prefab/Player/VeilWalker/Mixamo/VeilWalker_Mixamo.prefab`: mapped Unity Humanoid Avatar and existing URP character materials, empty weapon sockets.
- `Assets/Prefab/Player/VeilWalker/Mixamo/VeilWalker_Mixamo_RunPreview.prefab`: standalone preview with the recovered Mixamo run clip. This is not the gameplay player.
- `ArtSource/Previews/VeilWalker_Mixamo_TPose.png`: actual Blender render of the rigged model.
- `ArtSource/Previews/VeilWalker_Humanoid_Run.png`: actual Unity Humanoid-retargeted run poses.

## Rig

The old two-handed grip is not used as a bind pose. Arms are horizontal, legs are straight, and anatomical left/right are mapped by physical side rather than the old mesh-part labels. Weights are normalized. The head controls the mask and face; the hood/collar blends across head, neck and chest. Three additional cape bones are retained, outside the Humanoid body map.

Mixamo body names include Hips, Spine/Spine1/Spine2, Neck, Head, shoulders, Arm/ForeArm/Hand, UpLeg/Leg/Foot/ToeBase. The stylized mitten hands have no separately rigged fingers. Toe bones are available to the Avatar; the current boot skin follows Foot. Cape secondary animation and a two-handed sword grip still need a runtime driver/constraint when this rig is installed in gameplay.

## Unity setup and verification

Run **Survivor → Prepare Mixamo Humanoid Rig** to rebuild the importer mappings and preview prefabs. The setup requires the original eight FBXs in `Assets/MaximoAnimations`, recovered from the backup stash without restoring the discarded animation integration. Their binary FBX files are unchanged; importer metadata now defines valid Humanoid Avatars. Each source generates its own Avatar, rather than copying the target Avatar onto a different skeleton.

Validation requires both `Avatar.isValid` and `Avatar.isHuman` for the new character and all eight sources. The run must be a Humanoid clip and produce actual foot motion on the new character through Unity's animation system. A render contact sheet checks the deformed mesh, not just skeleton movement. No procedural locomotion component is attached to the new prefabs.

Regeneration: run Blender with `ArtSource/Scripts/create_veil_walker_mixamo.py`, then `ArtSource/Scripts/build_mixamo_reference.py`, then run the Unity preparation menu. Both scripts create separate new assets; neither overwrites the original gameplay FBX or production Blender file.

## Great Sword Pack movement integration

**Survivor → Install Great Sword Movement Only** installs the T-rigged model on `Player3d` and applies five clips from `Assets/Great Sword Pack`: plain idle, `run (2)` forward, plain `run` backward, `strafe (3)` left, and `strafe (4)` right. Direction was verified from source root travel, not guessed from numbered filenames. Other pack actions are not connected or reconfigured.

`GreatSwordLocomotion` blends the five Humanoid clips using actual Rigidbody velocity relative to mouse-controlled facing. Diagonals blend neighboring directions. Left-foot stance phases are aligned and animation cadence follows distance travelled. Root motion is extracted but not applied; the animation cannot move the collision capsule. An armature-only `GreatSword_Reference.fbx` supplies the pack's real neutral skeleton when generating source Avatars.

The existing `VeilWalkerLocomotion` now recognizes the new Mixamo bone names. When the imported driver is present it does not generate another run gait: it supplies only the supporting-hand constraint, dash overlay and the existing two-sweep attack pose helpers. `PlayerSwordAttack` and the cloudy sword VFX are retained. The separate sword attaches to the right hand and the left hand follows its supporting grip. The old generic model/prefabs remain available, unmodified.

Regenerate the pack reference using Blender `--background --python ArtSource/Scripts/audit_great_sword_locomotion.py -- --export-reference`, then use the movement installer. The general Humanoid preparation menu above prepares assets only; it does not install the five-clip movement set.

To use Adobe's auto-rigger instead, upload the `MixamoUpload.fbx` mesh, place its required markers, then download a T-pose **With Skin**. Subsequent animations should be downloaded for that same character, normally **Without Skin**. Retain the local weighted rig/source as a backup; online auto-rigging may require additional cape weight cleanup.

References: [Adobe Mixamo rigging workflow](https://helpx.adobe.com/creative-cloud/help/mixamo-rigging-animation.html), [Unity Humanoid Avatar setup](https://docs.unity.com/en-us/engine/6000.3/manual/animation-section/animation-mecanim/avatar-creationand-setup).
