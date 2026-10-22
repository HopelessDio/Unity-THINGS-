# Crimson Arena

Crimson Arena is an original isometric 3D arena shooter built with Unity 6 and the Universal Render Pipeline. Its direction is inspired by the speed, pressure, and dark retro-futuristic presentation of games such as KILL KNIGHT while using original gameplay code, assets, names, and arena designs.

Websites for asset and models
unity story for map assets
sketchfab for 3d models use 3d ripper pro to get the 3d models
unityassetpack.com for unity asset store stuff

## Current prototype

A Mixamo-compatible T-pose/Humanoid Veil Walker now drives the gameplay player. Great Sword Pack clips provide forward/backward running and left/right strafing with smooth directional blending; standing uses a static two-handed sword guard. Mouse aiming, the existing two-hit sword combo, slash effects and dash remain in place. Other pack actions are not connected. See [rig files, previews and setup notes](ArtSource/VeilWalker_Mixamo_README.md).

Level 1's crimson courtyard cliff road is installed in `Assets/Scenes/Game.unity`, using the approved shifted-right intro composition and a smooth follow camera. WASD and dash follow the camera's ground-plane directions. The actual 3D map has floor/building collision, a void kill zone, crimson foliage, amber lanterns and a camera-attached village backdrop. The previous arena is disabled, not deleted; its pre-install scene copy is `Assets/Scenes/Backups/Game_Before_Level01_20261015.unity`. Random arena spawning is disabled for this opening path; the home interior and door entrance are connected, while the story reveal remains to be built. Reinstall with **Survivor > Install Level 1 Crimson Village**, then **Survivor > Levels > Connect Home Door** to reconnect its regenerated entrance marker. Export source: `ArtSource/Scripts/export_level01_unity.py`; approved Blender file: `ArtSource/Level01_IntroRight09.blend`.

- WASD movement with smooth mouse-controlled facing
- Rigged Veil Walker with separate two-handed sword, running and dash animations
- Click-buffered two-hit sword combo: right sweep, left return sweep (15 damage each); no heavy third hit
- Broad white-ivory slash crescents with cool cloudy edges and fading mist wisps
- Soft enemy targeting, connected combo transitions and dash cancellation
- Dash afterimages, short ivory streak and pale ash puffs
- Directional dash with temporary invulnerability
- Enemy pursuit, spawning, health, contact damage, knockback, and hit feedback
- Player health, score, survival timer, game-over, and restart systems
- Difficulty scaling over survival time

The earlier projectile shooter is retained but disabled in the current sword-only scene. Retry now rebuilds the character after scene loading. Blender sources and implementation notes are in [ArtSource/VeilWalker_README.md](ArtSource/VeilWalker_README.md).

The Ashbound zombie is a separate reusable enemy prefab with a low-density mesh and Mixamo-compatible Humanoid rig. It does not enable enemy spawning in the intro. See [model, prefab, animation and testing notes](ArtSource/Ashbound_Zombie_README.md).

The mother character is an optimized noncombatant prefab: about 2.6k vertices, one skinned mesh/material, an eight-pixel palette and a Humanoid rig with idle/gentle walk. The living prefab is preserved; the current interior uses a separate static lying-down pose beside a visual-only feeding zombie. Story reaction/combat triggers are not connected yet. See [mother model and setup notes](ArtSource/VillageMother_README.md).

The separate [house interior](ArtSource/HomeInterior_README.md) is `Assets/Scenes/Level01_HomeInterior.unity`, with the mother near the hearth, a cutaway room, furniture collision and the existing playable Veil Walker. In the outdoor `Game.unity`, approach the family home's front door and press **E** (controller **Y / Triangle**) to fade into the interior at its entry marker. The outdoor visuals/geometry are preserved. This is currently one-way; the mother/zombie story reveal and an exit interaction are not implemented. Death/Retry reloads the interior once inside.

## Unity version

Unity `6000.6.0f1`

## Controls

- `WASD` or arrow keys: Move
- Face the mouse cursor independently of WASD movement; attacks use the cursor direction
- Left mouse click: Attack; press again during a strike to queue the next combo hit
- `Space`: Dash
- `E`: Enter home when the doorway prompt appears (controller north button: Y / Triangle)
- Gamepad: Left stick to move, west button to attack, south button to dash

## Working agreement

- Unity Editor work covers scenes, GameObjects, components, Inspector settings, models, materials, and lighting.
- Code work covers C# scripting, debugging, architecture, and step-by-step Unity guidance.
- Gameplay is built with placeholders first and improved visually afterward.
- The game takes inspiration from KILL KNIGHT's presentation and combat intensity without copying its assets, names, layouts, or implementation.

## Development roadmap

### Milestone 1 — Basic 3D prototype

- [x] Flat graybox arena
- [x] Capsule player
- [x] Orthographic isometric camera
- [x] Smooth WASD movement on the XZ plane
- [x] Independent mouse aiming
- [x] Hold-to-fire shooting
- [x] Projectile collision and damage
- [x] Dark arena material

### Milestone 2 — Enemy combat

- [x] Placeholder capsule enemies
- [x] Enemy spawning
- [x] Player pursuit
- [x] Contact damage
- [x] Enemy health and death
- [x] Kill score
- [x] Projectile knockback
- [x] Enemy hit flash

### Milestone 3 — Core game loop

- [x] Player health
- [x] Health bar
- [x] Game-over screen
- [x] Arena restart
- [x] Survival timer
- [x] Increasing spawn pressure and enemy difficulty
- [x] Directional dash
- [x] Dash invulnerability
- [x] Dash trail support
- [ ] Dash cooldown indicator
- [ ] Final score and survival-time summary
- [ ] Combat balance pass

### Milestone 4 — Modular arena

- [ ] Reusable floor slabs
- [ ] Broken platform edges
- [ ] Pits and void areas
- [ ] Raised structures
- [ ] Pillars and cover
- [ ] Surface cracks and damage
- [ ] Invisible gameplay boundaries
- [ ] Valid enemy spawn areas

The arena will use modular 3D prefabs rather than a Tilemap.

### Milestone 5 — Presentation

- [x] Orthographic isometric perspective
- [x] Nearly black prototype floor
- [ ] Original low-poly retro-futuristic environment
- [ ] Crimson corruption treatment
- [ ] Green pickups
- [ ] Bright projectile and hazard indicators
- [ ] Accumulating blood decals
- [ ] Strong shadows and restrained lighting
- [ ] Impact particles, camera feedback, and audio

### Milestone 6 — Advanced arena systems

- [ ] Laser hazards
- [ ] Shifting arena sections
- [ ] Environmental corruption over time
- [ ] Elite enemies
- [ ] Pickups
- [ ] Wave events
- [ ] Boss encounters
- [ ] Score multipliers and combo systems

## Current next step

Polish the home discovery with camera framing and the protagonist's reaction, then refine sound and the first-fight feel. The door, mother pose, blood dressing, feeding loop, smooth head turn, stare, stand-up and first combat handoff are connected. The stand-up uses a temporary synthesized growl; pursuit, contact damage, enemy death and Retry have passed live tests.
