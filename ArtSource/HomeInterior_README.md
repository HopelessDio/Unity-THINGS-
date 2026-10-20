# Level 1 house interior

Scene: `Assets/Scenes/Level01_HomeInterior.unity`.
Native editable source: `ArtSource/HomeInterior01.blend`.
Builder: `ArtSource/Scripts/create_home_interior.py` (Blender background Python).
Static export/layout: `Assets/Models/Levels/HomeInterior/`.

The cutaway room has a hearth, mother beside the fire, dining table/chairs, kitchen counter and shelf, cupboard, bed, writing desk, chest, burgundy rug and crimson windows. The far walls are visible; the roof and near walls are cut away for the isometric camera. Full invisible wall collision still contains the player.

The open room is approximately 13 x 11 metres, scaled for the existing 2m player and 1.7m mother. It is a separate playable level, not an image pasted behind the camera. The current discovery staging places the mother on the floor beside a feeding zombie; dialogue, cinematic reaction and the first combat encounter are not connected yet.

## Gameplay and scope

- Same Veil Walker, two-handed sword, mouse facing, camera-relative WASD and dash.
- Fixed room-wide orthographic camera: no player-follow bob or direction changes.
- Continuous floor collision, simple furniture obstacles, and an open walking route to the mother.
- Existing health/dash UI and Retry wiring copied as a scene, avoiding broken cross-scene player references.
- Mother uses a separate static resting-pose prefab; the original living Humanoid prefab is preserved.
- One visual-only feeding zombie, with no combat AI or damage; no enemy spawner.
- The exterior `Game.unity` is preserved. The builder verifies its file hash remains unchanged.
- The home is appended to Build Settings so Retry can reload it by name. The outdoor level remains first/default.

Start in `Assets/Scenes/Game.unity`, follow the path to the family home, approach its front door and press **E** (controller north button: **Y / Triangle**). A 0.3-second fade to black covers the asynchronous interior load, followed by a 0.4-second fade in. The destination scene's existing player starts at `Home_EntryMarker`; no persistent duplicate player is carried over. You can also open the interior directly to explore. The entrance is currently one-way; returning outside and the mother/zombie story reveal are not implemented.

`HomeDoor` is attached only to the existing `Level01_HomeEntrance` marker, with a 1.9-metre horizontal interaction radius and a nearby prompt. Entry is blocked when distant, dead, paused or already transitioning. A temporary persistent overlay covers the load; outgoing movement/aim/attacks are disabled without removing the knight visual. Missing/disabled destination scenes are rejected before fading. **Survivor > Levels > Connect Home Door** reinstalls the component without rebuilding geometry, changing lighting or saving over the interior. Save scene edits and stop Play mode first. Rebuilding the outdoor level with its original installer replaces that marker, so reconnect the door afterward.

`HomeDoorPlayValidation` passed on 2026-10-20: entry rejected from school and while paused; the actual paved doorstep has capsule clearance; virtual keyboard E triggers one fade/load; the interior has one equipped, live player and the mother; the overlay cleans up; actual death/Retry remains in the home. Tests do not save runtime changes. The interior file hash remained unchanged during door installation.

## Discovery staging (2026-10-20)

The current interior now contains `Home_Discovery`: the mother lying beside the hearth, a crouched zombie with a subtle feeding loop, and a small dark blood stain. This is visual staging only: no player reaction, reveal camera, sound, zombie awakening or combat trigger has been connected yet. The original `VillageMother.prefab`, its living animations, and `AshboundZombie.prefab` remain unchanged.

`MotherResting.prefab` uses `MotherRestingPose.asset`, a derived static mesh baked from the original mother in a relaxed asymmetrical pose. It retains the original one-material palette and vertex count but needs no skeleton/Animator at runtime. The staged zombie is an unpacked copy of the existing model with its combat components, colliders, Rigidbody and Animator removed; `FeedingZombiePose` gently animates the authored crouch's head/neck/arms. It cannot chase, damage the player or award a kill yet. The normal enemy prefab remains fully rigged and combat-ready for a later reaction-to-combat handoff.

Use **Survivor > Levels > Stage Home Discovery** only with saved scenes and Play mode stopped. It backs up the existing interior under `Assets/Scenes/Backups/HomeInterior_BeforeDiscovery_*`, rebuilds only its generated tableau and replaces the scene's standing mother. The outdoor scene is hash-checked and untouched. Running the original full interior builder would restore the original living-mother layout; do not use it to update this tableau.

Actual current preview: `ArtSource/Previews/HomeDiscovery_Unity01.png`. `HomeDiscoveryPlayValidation` checks above-floor mother placement, crouch mesh bounds, moving feeding bones, no zombie combat/physics, clear player entry and the actual Retry reload. The earlier `HomeInteriorPlayValidation` targets the original living-mother layout; use this discovery validator for the staged scene instead.

Final discovery Play validation and outdoor-door-to-discovery/Retry regression both passed on 2026-10-20. Pose baking forces offscreen Animator evaluation, corrects mirrored FBX triangle winding, and uses yaw-only placement so the resting mesh cannot be flipped underneath the floor. The simulated-keyboard door test lets device/focus synchronization settle before issuing E.

### Base room optimization

Environment: 12 static material batches, 7,644 triangles and 16 simple collision boxes. The many planks/furniture details are combined for rendering rather than separate runtime renderers; individual pieces remain editable in Blender. No dense mesh colliders, realtime cloth, moving furniture, or imported Blender preview character is included in the environment FBX. Mother/player are separate Unity actors. Three warm point lights do not cast shadows; the room has one soft directional shadow light and restrained post-processing.

This is a prototype room with flat-color materials and native geometry, not a promise of a particular FPS. Actual cost depends on resolution, shadows, pipeline and hardware.

## Editing and tests

**Survivor > Levels > Build and Open House Interior** creates the scene from the saved Game scene the first time. Save current scene edits and stop Play mode first. Subsequent rebuilds retain a timestamped copy of the previous interior in `Assets/Scenes/Backups/`; the generated environment and mother are replaced while unrelated root objects are retained. Do not invoke a rebuild over unsaved scene changes.

The installer checks eight full-capsule route samples, floor coverage, furniture clearance, framing, mother/player placement and no enemy systems. `HomeInteriorPlayValidation` is a one-shot pending-file test for actual keyboard movement, dash, stable camera, floor containment, the mother and a real death/Retry scene reload. Its temporary background/input-focus settings are restored before leaving Play mode.

Actual renders: `ArtSource/Previews/HomeInterior_Blender01.png` and `HomeInterior_Unity01.png`.

Validation passed on 2026-10-20, including a real keyboard walk/dash and actual game-over/Retry reload. The final Unity preview was captured from the running interior after Retry. The exterior scene hash matched before and after installation and testing.
