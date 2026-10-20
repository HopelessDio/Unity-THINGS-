# Village mother NPC

Native Blender source: `ArtSource/VillageMother01.blend`.
Builder: `ArtSource/Scripts/create_village_mother.py` (Blender background Python).
Model: `Assets/Models/Characters/Mother/VillageMother.fbx`.
Separate motion source: `VillageMother_Motions.fbx` in the same folder.
Prefab: `Assets/Prefab/Characters/Mother/VillageMother.prefab`.
Rebuild prefab in Edit mode using **Survivor > Characters > Build Mother Prefab**.

## Design and optimization

- Simple shaped face, smooth hair shell, one broad swept lock and low bun.
- Burgundy dress, charcoal shoulder shawl, cream apron, brown belt/shoes.
- 2,621 source vertices / 4,758 triangles; no subdivision modifier at runtime.
- One skinned mesh, one submesh, one shared URP/Lit material.
- The eight clothing/skin/hair colors use an **8 x 1 pixel palette**; no large maps, normal maps or separate materials for each garment.
- Maximum two normalized bone influences per vertex, with Unity Bone2 skin quality.
- Read/Write disabled on imported meshes and texture; no unnecessary script-side CPU copies.
- Animator offscreen transform culling; skin does not update while offscreen. Conservative idle/walk bounds are saved in the prefab.
- No hidden legs/body geometry under the closed dress, cloth simulation, particles, extra realtime lights or per-frame custom MonoBehaviour.

These are asset-level optimizations, not a measured frame-rate claim. Shadows and pipeline passes can still create additional draws.

## Rig and animation

52 standard `mixamorig:` body/finger/toe bones in a neutral T-pose. The neutral model and motion FBX are separate so the first animated frame is not used as the Avatar reference. The Unity Avatar is validated as Humanoid.

The default controller plays `Mother_Idle`. `Mother_Walk` is available through the `MoveSpeed` blend parameter (0 = idle; .53 = gentle in-place walk). Root motion is off. No navigation or gameplay movement is attached yet.

The long skirt deliberately retains a simple hip-driven silhouette. It is intended for quiet idle and small walking steps, not acrobatic attacks or sprinting. A Humanoid-compatible Mixamo clip can retarget to the rig, but large leg movements require outfit-specific review; do not assume every action will look right under a long dress. An existing Mixamo run is used only as a skeleton/skin compatibility check, not connected to her gameplay controller.

## Use and verification

Her root is at the feet. Place the prefab at floor height; final height is 1.70m. A matching non-trigger capsule is included. She is a noncombatant with no enemy scripts, health, dialogue, interaction, story trigger or cutscene attached. She is now placed beside the hearth in `Assets/Scenes/Level01_HomeInterior.unity`; story integration remains a separate step.

The builder never saves the open scene or places her in the level. Player, enemy spawners and previous assets are untouched.

Validation checks the geometry budget, single submesh/material, exact tiny texture dimensions, Humanoid Avatar, foot placement/scale and prefab round-trip. It samples 75 baked skin poses across her idle, gentle walk and an existing Mixamo clip, checking finite vertices, sensible bounds and actual leg motion. The model has no animation import/retarget warnings.

Actual renders:

- `ArtSource/Previews/VillageMother_Blender01.png`: native Blender front/back inspection.
- `ArtSource/Previews/VillageMother_Unity01.png`: Unity front, back and gentle walk.

These are renders of the real mesh, not the earlier AI concept PNG.
