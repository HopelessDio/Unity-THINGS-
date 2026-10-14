# Crimson Arena

Crimson Arena is an original isometric 3D arena shooter built with Unity 6 and the Universal Render Pipeline. Its direction is inspired by the speed, pressure, and dark retro-futuristic presentation of games such as KILL KNIGHT while using original gameplay code, assets, names, and arena designs.

Websites for asset and models
unity story for map assets
sketchfab for 3d models use 3d ripper pro to get the 3d models
unityassetpack.com for unity asset store stuff

## Current prototype

- WASD movement with smooth keyboard-controlled facing
- Rigged Veil Walker with separate two-handed sword, running and dash animations
- Click-buffered sword combo: diagonal sweep, return sweep, heavy overhead cut (15/15/35 damage)
- Soft enemy targeting, connected combo transitions and dash cancellation
- Dash afterimages, short ivory streak and pale ash puffs
- Directional dash with temporary invulnerability
- Enemy pursuit, spawning, health, contact damage, knockback, and hit feedback
- Player health, score, survival timer, game-over, and restart systems
- Difficulty scaling over survival time

The earlier projectile shooter is retained but disabled in the current sword-only scene. Retry now rebuilds the character after scene loading. Blender sources and implementation notes are in [ArtSource/VeilWalker_README.md](ArtSource/VeilWalker_README.md).

## Unity version

Unity `6000.6.0f1`

## Controls

- `WASD` or arrow keys: Move
- Facing follows movement; mouse position does not rotate the player
- Left mouse click: Attack; press again during a strike to queue the next combo hit
- `Space`: Dash
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

Add a visible dash cooldown indicator, then begin the modular arena and gameplay-boundary pass.
