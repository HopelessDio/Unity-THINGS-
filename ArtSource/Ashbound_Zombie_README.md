# Ashbound zombie

Refined native source: `Ashbound_Zombie02_Humanoid.blend`.
Build: run Blender with `--background --python ArtSource/Scripts/create_ashbound_zombie_refined.py` from the project root.
The original `Ashbound_Zombie01.blend` is retained, not used by the prefab.

## Model and rig

- 2,985 source vertices / 5,483 triangles, versus 14,450 vertices in version 01 (79% fewer).
- Smooth shading, one shaped head surface, inset red eyes, connected arm/palm surfaces, simple short sleeves, burgundy belt and torn tunic.
- Seven shared flat-color materials; no large texture maps.
- Neutral T-pose with 52 `mixamorig:` bones: body, toes, and five three-joint finger chains per hand.
- Normalized skin weights; a separate neutral model and motion source avoid treating an animation's first frame as its bind pose.
- `Assets/Models/Enemies/Ashbound/AshboundZombie.fbx`: neutral skinned model.
- `AshboundZombie_Motions.fbx`: the same bind skeleton with original idle/shuffle motions.
- `AshboundZombie.json`: source geometry/rig statistics.

## Unity

Run **Survivor > Enemies > Build Ashbound Zombie Prefab** with Play mode stopped.
Prefab: `Assets/Prefab/Enemies/AshboundZombie.prefab`.
Controller: adjacent `AshboundZombie.controller`. The blend tree uses Rigidbody speed to blend idle/shuffle; animation root motion stays off so it cannot compete with physics.
The model is fitted to a 2.14m height with the capsule origin at the character's centre, consistent with the existing enemies. Place the root at floor height + 1.08m.

The prefab reuses EnemyChaser, EnemyHealth, EnemyContactDamage and EnemyHitFlash: 1.2m/s pursuit, 30 health, 10 contact damage each second, sword knockback, hit flash, kill score and cleanup. AshboundZombie finds the player on Start when placed manually. The flash uses per-material property blocks, not duplicated/mutated shared materials.

This setup does not place enemies, enable the intro spawner, overwrite the placeholder Enemy prefab, or save the Game scene. Drag this prefab into a combat/test location when ready. Existing authored scene edits are left alone.

## Mixamo clips

The zombie model has its own valid Unity Humanoid Avatar. A Mixamo animation should be imported as **Humanoid**, with a valid Avatar for its own source rig. It can then be assigned to a state in the zombie controller (or replace the shuffle child motion). Keep the zombie Animator's Avatar set to the **zombie** Avatar. Do not copy the zombie's bind skeleton onto a differently proportioned source character, rename source bones blindly, or use an animated first frame as a T-pose.

This follows Unity's [Humanoid retargeting workflow](https://docs.unity.com/en-us/engine/6000.0/manual/animation-section/animation-mecanim/avatar-creationand-setup/retargeting). Fingers are mapped; the imported clip must actually contain finger animation to animate them.

The existing Great Sword Pack run is evaluated on the zombie only as a compatibility regression; it is not connected to enemy gameplay. The enemy does not carry a sword.

## Verification

Setup checks the Avatar, skin weights/bones, character dimensions, materials, and 75 baked skin poses across idle, shuffle, and a real existing Mixamo run. `ArtSource/Previews/Ashbound_Unity_Humanoid.png` shows idle, two shuffle poses and the retargeted run, left to right. `Ashbound_Zombie_Blender02.png` is an actual Blender render, not an AI concept.

**Survivor > Enemies > Validate Ashbound Zombie in Play Mode** runs a disposable distant fixture for pursuit, animated movement, repeated all-material flash/restoration, contact damage, knockback, health, one kill event and cleanup, then exits Play mode. No test objects or scene changes are saved.

The setup/retarget checks and Play-mode fixture passed on 2026-10-20. Logs are in the ignored `Logs/AshboundSetup*.log` and `Logs/AshboundPlay*.log` files.
