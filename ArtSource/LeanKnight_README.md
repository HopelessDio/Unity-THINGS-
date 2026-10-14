# Lean knight and separate longsword

Approved reference: `Concepts/KnightSeparateSword_Prototype02_Lean.png`.
Actual model render: `Previews/LeanKnight_Model01.png`.

## Assets

- `LeanKnight_Model01.blend`: editable Blender source with presentation setup.
- `../Assets/Models/Player/LeanKnight_Model01.fbx`: knight, 20-bone generic skeleton, skinned armor/cloth, and `SwordSocket_R`.
- `../Assets/Models/Player/LeanKnight_Longsword01.fbx`: independent sword, no skeleton, pivot at the grip center.
- `Scripts/create_lean_knight.py`: reproducible geometry and export generator.

## Unity preparation

The one-time `LeanKnightAssets.pending` request prepares URP materials and two independent prefabs after Unity refreshes. Alternatively use **Survivor → Prepare Lean Knight and Separate Sword**.

The preparation only creates assets; it does not replace the gameplay player.

To equip later, instantiate `LeanKnight_Longsword.prefab` beneath the knight's `SwordSocket_R`, then set local position and rotation to zero and local scale to one. The socket aims the blade forward in the neutral pose. Adjust socket orientation for the desired grip and attack pose, not the weapon mesh origin.

Armor is rigidly weighted to the skeleton. Joint gaps keep armor from stretching. Cape follows the chest; cloth simulation, finger articulation, attack clips, trails, and hitboxes are future work. Use a Generic animation rig; a Unity Humanoid avatar has not been configured or validated.

The reference PNG is concept art. This modeled prototype follows the lean silhouette, palette, armor segmentation and separate sword, with simpler surface wear and cloth detail.
