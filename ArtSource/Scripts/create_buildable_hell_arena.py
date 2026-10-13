import bpy
import math
import os
import random
from mathutils import Vector


random.seed(7319)
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART_DIR = os.path.join(ROOT, "ArtSource")
MODEL_DIR = os.path.join(ROOT, "Assets", "Models", "Arena")
PREVIEW_DIR = os.path.join(ART_DIR, "Previews")
for folder in (ART_DIR, MODEL_DIR, PREVIEW_DIR):
    os.makedirs(folder, exist_ok=True)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                       bpy.data.cameras, bpy.data.lights):
        pass


def material(name, color, metallic=0.0, roughness=0.7, noise=0.0,
             emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if noise:
        tex = nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = 2.8
        tex.inputs["Detail"].default_value = 7.0
        tex.inputs["Roughness"].default_value = 0.82
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.22
        ramp.color_ramp.elements[0].color = (color[0] * 0.28, color[1] * 0.28, color[2] * 0.28, 1)
        ramp.color_ramp.elements[1].position = 0.8
        ramp.color_ramp.elements[1].color = (
            min(1, color[0] + noise), min(1, color[1] + noise), min(1, color[2] + noise), 1)
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.3
        bump.inputs["Distance"].default_value = 0.18
        links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
        links.new(tex.outputs["Fac"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    if emission:
        emission_input = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = (*emission, 1.0)
        if bsdf.inputs.get("Emission Strength"):
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def lava_material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = 0.05
    bsdf.inputs["Roughness"].default_value = 0.5

    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 0.42
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Roughness"].default_value = 0.78
    noise.inputs["Lacunarity"].default_value = 2.15
    noise.inputs["Distortion"].default_value = 0.55

    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    colors = [
        (0.0, (0.012, 0.0005, 0.0001, 1)),
        (0.35, (0.05, 0.002, 0.0001, 1)),
        (0.52, (0.24, 0.012, 0.0002, 1)),
        (0.68, (0.72, 0.055, 0.001, 1)),
        (0.84, (1.0, 0.2, 0.004, 1)),
        (1.0, (1.0, 0.52, 0.025, 1)),
    ]
    first = ramp.color_ramp.elements[0]
    first.position, first.color = colors[0]
    for position, color in colors[1:]:
        element = ramp.color_ramp.elements.new(position)
        element.color = color

    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.22
    bump.inputs["Distance"].default_value = 0.16
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    emission_input = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
    if emission_input:
        links.new(ramp.outputs["Color"], emission_input)
    if bsdf.inputs.get("Emission Strength"):
        bsdf.inputs["Emission Strength"].default_value = 1.35
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def finish(obj, mat=None, bevel=0.0, smooth=False, export=True):
    if mat:
        obj.data.materials.append(mat)
    if bevel:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        mod = obj.modifiers.new("ForgedBevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        obj.select_set(False)
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    obj["unity_export"] = export
    return obj


def box(name, location, size, mat, rotation=(0, 0, 0), bevel=0.12, export=True):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0] * 0.5, size[1] * 0.5, size[2] * 0.5)
    return finish(obj, mat, bevel, export=export)


def cylinder(name, location, radius, depth, mat, vertices=8, rotation=(0, 0, 0), bevel=0.08, export=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, bevel, export=export)


def torus(name, location, major, minor, mat, rotation=(0, 0, 0), export=True):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                    major_segments=10, minor_segments=5,
                                    location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, smooth=True, export=export)


def collection(name):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


def move_to(obj, col):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    col.objects.link(obj)
    return obj


reset_scene()

stone = material("Arena_CharcoalStone", (0.26, 0.28, 0.32), roughness=0.88, noise=0.16)
stone_dark = material("Arena_BrokenStone", (0.085, 0.095, 0.12), roughness=0.94, noise=0.1)
edge = material("Arena_BlackIron", (0.022, 0.028, 0.038), metallic=0.88, roughness=0.3, noise=0.06)
trim = material("Arena_WornTrim", (0.16, 0.085, 0.04), metallic=0.72, roughness=0.4, noise=0.08)
lava = material("Arena_Lava", (0.72, 0.07, 0.002), roughness=0.3, noise=0.12,
                emission=(1.0, 0.08, 0.002), emission_strength=1.8)
ember = material("Arena_Ember", (0.72, 0.035, 0.001), roughness=0.25,
                 emission=(1.0, 0.14, 0.006), emission_strength=6.0)
abyss = material("Arena_Abyss", (0.001, 0.0015, 0.003), roughness=1.0)
deep_lava = lava_material("Arena_DeepLava")
blood = material("Arena_DriedBlood", (0.075, 0.002, 0.004), roughness=0.68, noise=0.03)

COL_FLOOR = collection("01_FloorModules")
COL_CENTER = collection("02_CentralDais")
COL_WALLS = collection("03_WallsAndPillars")
COL_HAZARDS = collection("04_LavaAndAbyss")
COL_PROPS = collection("05_Props")
COL_COLLIDERS = collection("06_UnityColliders")

# A deep abyss plane is deliberately separate so Unity can replace it with a shader.
move_to(box("AbyssPlane", (0, 0, -7.35), (86, 86, 0.1), abyss, bevel=0), COL_HAZARDS)
move_to(box("DeepLavaSea", (0, 0, -7.18), (84, 84, 0.12), deep_lava, bevel=0), COL_HAZARDS)
for x, y in ((-35, -35), (35, -35), (-35, 35), (35, 35)):
    glow = move_to(cylinder(f"AbyssGlow_{x}_{y}", (x, y, -6.75), 13, 0.08, lava, vertices=16, bevel=0), COL_HAZARDS)
    glow.hide_render = True

# Heavy foundations hang beneath the arena. These make the platform feel ancient
# and enormous without affecting the playable floor or requiring sculpted arches.
foundation_points = [
    (-24, -29.2), (-12, -29.2), (0, -29.2), (12, -29.2), (24, -29.2),
    (29.2, -20), (29.2, -7), (29.2, 7), (29.2, 20),
    (-29.2, -20), (-29.2, 0), (-29.2, 20),
]
for index, (x, y) in enumerate(foundation_points):
    height = random.uniform(6.4, 9.2)
    support = cylinder(f"HangingFoundation_{index:02d}",
                       (x, y, -0.5 - height * 0.5), random.uniform(1.15, 1.8), height,
                       stone_dark, vertices=8, bevel=0.13)
    move_to(support, COL_HAZARDS)
    foot = cylinder(f"HangingFoundation_{index:02d}_Foot",
                    (x, y, -0.75), 2.15, 0.55, trim, vertices=8, bevel=0.1)
    move_to(foot, COL_HAZARDS)

# A ring of simple tapered rock spires gives depth to the hellscape. They stay
# outside the 60x60 gameplay square and therefore never need colliders.
for index in range(46):
    side = index % 4
    along = random.uniform(-43, 43)
    outside = random.uniform(34, 45)
    if side == 0:
        x, y = along, -outside
    elif side == 1:
        x, y = outside, along
    elif side == 2:
        x, y = along, outside
    else:
        x, y = -outside, along
    height = random.uniform(3.5, 10.5)
    bpy.ops.mesh.primitive_cone_add(
        vertices=random.choice((6, 7, 8)),
        radius1=random.uniform(1.3, 3.7),
        radius2=random.uniform(0.06, 0.42),
        depth=height,
        location=(x, y, -7.0 + height * 0.5),
        rotation=(random.uniform(-0.1, 0.1), random.uniform(-0.1, 0.1), random.uniform(0, math.tau)),
    )
    spire = bpy.context.object
    spire.name = f"AbyssRockSpire_{index:02d}"
    finish(spire, stone_dark, bevel=0.1)
    move_to(spire, COL_HAZARDS)

# Flat vertical strips become convincing distant lava waterfalls once the Unity
# material scrolls. They are intentionally separate for easy animation.
waterfalls = [
    ("SouthA", (-18, -30.05, -3.8), (1.2, 0.12, 6.2)),
    ("SouthB", (17, -30.05, -4.1), (1.7, 0.12, 6.7)),
    ("EastA", (30.05, -17, -3.5), (0.12, 1.3, 5.8)),
    ("EastB", (30.05, 14, -4.0), (0.12, 1.8, 6.8)),
    ("NorthA", (-13, 30.05, -3.9), (1.4, 0.12, 6.4)),
    ("WestA", (-30.05, 11, -4.1), (0.12, 1.4, 6.7)),
]
for name, location, size in waterfalls:
    move_to(box(f"LavaFall_{name}", location, size, deep_lava, bevel=0.04), COL_HAZARDS)


def slab(name, x, y, sx=19.4, sy=19.4, z=-0.5):
    obj = box(name, (x, y, z), (sx, sy, 1.0), stone, bevel=0.18)
    move_to(obj, COL_FLOOR)
    # Inset iron border strips communicate each reusable module.
    for axis, offset in (("x", -sx * 0.5 + 0.38), ("x", sx * 0.5 - 0.38)):
        strip = box(f"{name}_Border_{axis}_{offset:.1f}",
                    (x + offset, y, 0.035), (0.22, sy - 0.55, 0.07), trim, bevel=0.03)
        move_to(strip, COL_FLOOR)
    for axis, offset in (("y", -sy * 0.5 + 0.38), ("y", sy * 0.5 - 0.38)):
        strip = box(f"{name}_Border_{axis}_{offset:.1f}",
                    (x, y + offset, 0.04), (sx - 0.55, 0.22, 0.075), trim, bevel=0.03)
        move_to(strip, COL_FLOOR)


# Seven intact modules plus two deliberately broken corner modules.
for row_y in (-20, 0, 20):
    for col_x in (-20, 0, 20):
        if (col_x, row_y) in ((-20, 20), (20, -20)):
            continue
        slab(f"FloorSlab_{col_x}_{row_y}", col_x, row_y)

# Broken north-west: two overlapping arms leave a true 8x8 metre void at the outer corner.
slab("FloorSlab_BrokenNW_A", -16, 20, 11.4, 19.4)
slab("FloorSlab_BrokenNW_B", -24, 16, 4.6, 11.4)
# Broken south-east, mirrored.
slab("FloorSlab_BrokenSE_A", 16, -20, 11.4, 19.4)
slab("FloorSlab_BrokenSE_B", 24, -16, 4.6, 11.4)

# Stone chunks around the two missing corners make the simple rectangular cuts read as damage.
for prefix, cx, cy, sx, sy in (("NW", -26.0, 26.0, 1, 1), ("SE", 26.0, -26.0, -1, -1)):
    for i in range(20):
        x = cx + random.uniform(-4.2, 4.2)
        y = cy + random.uniform(-4.2, 4.2)
        if random.random() < 0.48:
            x = cx + random.uniform(-4.6, -1.8) * sx
        else:
            y = cy + random.uniform(-4.6, -1.8) * sy
        z = random.uniform(-0.2, 0.12)
        rubble = box(f"BrokenCorner_{prefix}_Rubble_{i:02d}", (x, y, z),
                     (random.uniform(0.45, 1.35), random.uniform(0.45, 1.3), random.uniform(0.3, 0.8)),
                     stone_dark,
                     rotation=(random.uniform(-0.2, 0.2), random.uniform(-0.2, 0.2), random.uniform(0, math.tau)),
                     bevel=0.08)
        move_to(rubble, COL_PROPS)

# Two long side trenches match the Ashen Courtyard concept. They keep the central
# combat floor open while creating the bright orange border visible in the image.
channels = [
    ("North", (0, 23.0, 0.055), (42.0, 2.4, 0.11)),
    ("East", (23.0, 0, 0.055), (2.4, 42.0, 0.11)),
]
for name, loc, size in channels:
    move_to(box(f"LavaChannel_{name}", loc, size, lava, bevel=0.05), COL_HAZARDS)
    if size[0] < size[1]:
        for side in (-1, 1):
            move_to(box(f"ChannelLip_{name}_{side}", (loc[0] + side * 1.25, loc[1], 0.13),
                        (0.35, size[1] + 0.6, 0.26), edge, bevel=0.06), COL_HAZARDS)
    else:
        for side in (-1, 1):
            move_to(box(f"ChannelLip_{name}_{side}", (loc[0], loc[1] + side * 1.25, 0.13),
                        (size[0] + 0.6, 0.35, 0.26), edge, bevel=0.06), COL_HAZARDS)

# Dark grates span the glowing trenches at regular intervals.
for name, loc, size in channels:
    long_x = size[0] > size[1]
    for segment in range(-9, 10):
        offset = segment * 2.15
        grate_loc = (loc[0] + offset, loc[1], 0.19) if long_x else (loc[0], loc[1] + offset, 0.19)
        grate_size = (0.18, size[1] + 0.15, 0.13) if long_x else (size[0] + 0.15, 0.18, 0.13)
        move_to(box(f"ChannelGrate_{name}_{segment + 9:02d}", grate_loc, grate_size,
                    edge, bevel=0.025), COL_HAZARDS)
    center_rail_size = (size[0] + 0.1, 0.16, 0.14) if long_x else (0.16, size[1] + 0.1, 0.14)
    move_to(box(f"ChannelGrate_{name}_CenterRail", (loc[0], loc[1], 0.205),
                center_rail_size, edge, bevel=0.025), COL_HAZARDS)

# Thin iron seams break the open floor into large readable courtyard tiles.
for seam in (-20, -10, 10, 20):
    move_to(box(f"FloorSeam_X_{seam}", (seam, 0, 0.055), (0.13, 58, 0.07), edge, bevel=0.015), COL_FLOOR)
    move_to(box(f"FloorSeam_Y_{seam}", (0, seam, 0.058), (58, 0.13, 0.07), edge, bevel=0.015), COL_FLOOR)

# A compact square altar replaces the former large octagonal dais, leaving much
# more room for survivor combat just like the selected concept.
move_to(box("CentralAltar_Base", (0, 0, 0.42), (9.6, 9.6, 0.84), stone_dark, bevel=0.18), COL_CENTER)
move_to(box("CentralAltar_Upper", (0, 0, 0.91), (7.8, 7.8, 0.22), stone, bevel=0.1), COL_CENTER)
for name, loc, size in (
    ("AltarTrim_N", (0, 3.7, 1.05), (7.0, 0.18, 0.08)),
    ("AltarTrim_S", (0, -3.7, 1.05), (7.0, 0.18, 0.08)),
    ("AltarTrim_E", (3.7, 0, 1.05), (0.18, 7.0, 0.08)),
    ("AltarTrim_W", (-3.7, 0, 1.05), (0.18, 7.0, 0.08)),
    ("AltarRune_NS", (0, 0, 1.07), (0.16, 5.4, 0.06)),
    ("AltarRune_EW", (0, 0, 1.07), (5.4, 0.16, 0.06)),
):
    move_to(box(name, loc, size, trim, bevel=0.025), COL_CENTER)


def altar_stairs(name, direction):
    dx, dy = direction
    for index in range(4):
        distance = 5.15 + index * 0.72
        height = 0.78 - index * 0.18
        width = 4.8 + index * 0.32
        size = (0.9, width, height) if dx else (width, 0.9, height)
        step = box(f"{name}_Step_{index}", (dx * distance, dy * distance, height * 0.5),
                   size, stone, bevel=0.08)
        move_to(step, COL_CENTER)


altar_stairs("NorthStairs", (0, 1))
altar_stairs("SouthStairs", (0, -1))
altar_stairs("EastStairs", (1, 0))
altar_stairs("WestStairs", (-1, 0))


def pillar(name, x, y, height=5.8):
    move_to(box(f"{name}_Foot", (x, y, 0.35), (3.0, 3.0, 0.7), stone_dark, bevel=0.15), COL_WALLS)
    move_to(box(f"{name}_Base", (x, y, 0.9), (2.35, 2.35, 0.55), trim, bevel=0.1), COL_WALLS)
    move_to(box(f"{name}_Shaft", (x, y, 1.25 + height * 0.5), (1.75, 1.75, height), stone_dark, bevel=0.16), COL_WALLS)
    move_to(box(f"{name}_Cap", (x, y, 1.35 + height), (2.5, 2.5, 0.55), trim, bevel=0.1), COL_WALLS)
    move_to(box(f"{name}_Crown", (x, y, 1.72 + height), (2.85, 2.85, 0.3), stone, bevel=0.1), COL_WALLS)
    # Narrow crimson inset, a cheap plane-like box rather than carved ornament.
    side_y = y - math.copysign(0.89, y) if abs(y) > abs(x) else y
    side_x = x - math.copysign(0.89, x) if abs(x) >= abs(y) else x
    size = (0.8, 0.035, 3.5) if abs(y) >= abs(x) else (0.035, 0.8, 3.5)
    move_to(box(f"{name}_EmberInset", (side_x, side_y, 3.7), size, ember, bevel=0.025), COL_WALLS)


pillar_positions = [
    (-28, -28), (0, -28), (28, -28),
    (-28, 28), (0, 28), (28, 28),
    (-28, -9), (-28, 9), (28, -9), (28, 9),
    (-9, -28), (9, 28),
]
for idx, (x, y) in enumerate(pillar_positions):
    pillar(f"PerimeterPillar_{idx:02d}", x, y, random.uniform(5.2, 6.3))

# Low modular perimeter walls, skipped beside the two damaged corners.
wall_specs = [
    ("SouthA", (-18, -29.3, 1.2), (16, 1.2, 2.4)),
    ("SouthB", (9, -29.3, 1.2), (14, 1.2, 2.4)),
    ("NorthA", (-9, 29.3, 1.2), (14, 1.2, 2.4)),
    ("NorthB", (18, 29.3, 1.2), (16, 1.2, 2.4)),
    ("WestA", (-29.3, -18, 1.2), (1.2, 16, 2.4)),
    ("WestB", (-29.3, 4, 1.2), (1.2, 8, 2.4)),
    ("EastA", (29.3, 18, 1.2), (1.2, 16, 2.4)),
    ("EastB", (29.3, -4, 1.2), (1.2, 8, 2.4)),
]
for name, loc, size in wall_specs:
    move_to(box(f"PerimeterWall_{name}", loc, size, stone_dark, bevel=0.13), COL_WALLS)
    cap_loc = (loc[0], loc[1], loc[2] + size[2] * 0.5 + 0.15)
    cap_size = (size[0] + (0.35 if size[0] > 2 else 0.25),
                size[1] + (0.35 if size[1] > 2 else 0.25), 0.3)
    move_to(box(f"PerimeterWall_{name}_Cap", cap_loc, cap_size, trim, bevel=0.07), COL_WALLS)
    # Blocky crenellations create the fortress silhouette from the concept.
    horizontal = size[0] > size[1]
    length = size[0] if horizontal else size[1]
    count = max(2, int(length / 2.8))
    for index in range(count):
        offset = -length * 0.5 + 1.0 + index * ((length - 2.0) / max(1, count - 1))
        crenel_loc = (loc[0] + offset, loc[1], 2.95) if horizontal else (loc[0], loc[1] + offset, 2.95)
        move_to(box(f"PerimeterWall_{name}_Crenel_{index:02d}", crenel_loc,
                    (1.05, 1.35, 1.15) if horizontal else (1.35, 1.05, 1.15),
                    stone_dark, bevel=0.08), COL_WALLS)

# Four simple braziers around the dais.
for index, (x, y) in enumerate(((-5.6, -5.6), (5.6, -5.6), (-5.6, 5.6), (5.6, 5.6))):
    move_to(box(f"Brazier_{index}_Base", (x, y, 0.75), (1.35, 1.35, 1.5), stone_dark, bevel=0.12), COL_PROPS)
    move_to(cylinder(f"Brazier_{index}_Bowl", (x, y, 1.62), 0.68, 0.25, edge, vertices=12, bevel=0.05), COL_PROPS)
    move_to(cylinder(f"Brazier_{index}_Flame", (x, y, 2.02), 0.26, 0.7, ember, vertices=8, bevel=0.03), COL_PROPS)

# Sparse blood pools are flat, cheap decals represented here as dark red discs.
for index, (x, y, sx, sy) in enumerate(((-4, 2.3, 2.2, 1.0), (11, -11, 2.7, 1.1),
                                         (-18, -12, 1.8, 0.9), (15, 17, 2.0, 0.8))):
    stain = cylinder(f"BloodDecal_{index}", (x, y, 0.13 if abs(x) > 9 else 1.13), 1, 0.018, blood,
                     vertices=16, bevel=0)
    stain.scale = (sx, sy, 1)
    move_to(stain, COL_PROPS)

# Lightweight chains: repeated low-poly torus links between selected pillars.
def chain_run(name, start, end, links=14, sag=2.0, height=7.2):
    a = Vector(start)
    b = Vector(end)
    direction = b - a
    heading = math.atan2(direction.y, direction.x)
    for index in range(links):
        t = (index + 0.5) / links
        p = a.lerp(b, t)
        p.z = height - math.sin(math.pi * t) * sag
        rotation = (math.pi / 2, 0, heading + (math.pi / 2 if index % 2 else 0))
        link = torus(f"{name}_Link_{index:02d}", p, 0.34, 0.085, edge, rotation)
        move_to(link, COL_PROPS)


chain_run("NorthChain", (-27, 27, 0), (27, 27, 0), links=22, sag=2.4)
chain_run("SouthChain", (-27, -27, 0), (27, -27, 0), links=22, sag=2.4)
chain_run("WestChain", (-27, -27, 0), (-27, 27, 0), links=22, sag=2.4)
chain_run("EastChain", (27, -27, 0), (27, 27, 0), links=22, sag=2.4)

# Hidden, clearly named Unity collision helpers for the four lava channels and abyss.
for name, loc, size in channels:
    collider = box(f"HazardCollider_Lava_{name}", (loc[0], loc[1], -0.1),
                   (size[0], size[1], 0.6), None, bevel=0, export=True)
    collider.hide_render = True
    collider.display_type = "WIRE"
    move_to(collider, COL_COLLIDERS)

# Dramatic presentation scene; lights/camera are excluded from FBX character geometry filtering.
world = bpy.context.scene.world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.065, 0.012, 0.004, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.48


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def area_light(name, location, color, energy, size, target=(0, 0, 0)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    aim(obj, target)
    obj["unity_export"] = False


area_light("ColdMoon", (-34, -38, 50), (0.62, 0.72, 1.0), 11800, 34)
area_light("OrangeLavaBounce", (28, 24, 11), (1.0, 0.11, 0.012), 3100, 25)
area_light("SoftArenaFill", (0, -18, 34), (0.52, 0.57, 0.7), 8200, 38)

camera_data = bpy.data.cameras.new("ArenaPreviewCamera")
camera = bpy.data.objects.new("ArenaPreviewCamera", camera_data)
bpy.context.scene.collection.objects.link(camera)
camera.location = (48, -62, 62)
camera.data.type = "ORTHO"
camera.data.ortho_scale = 78
aim(camera, (0, 0, 0))
bpy.context.scene.camera = camera
camera["unity_export"] = False

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1536
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
scene.view_settings.exposure = 0.65

blend_path = os.path.join(ART_DIR, "AshenCourtyard_Model02.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

# Export all modeled arena pieces while excluding presentation-only light and camera objects.
bpy.ops.object.select_all(action="DESELECT")
exported = []
for obj in bpy.context.scene.objects:
    if obj.type == "MESH" and obj.get("unity_export", False):
        obj.select_set(True)
        exported.append(obj)
if exported:
    bpy.context.view_layer.objects.active = exported[0]
fbx_path = os.path.join(MODEL_DIR, "AshenCourtyard_Model02.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=False,
)

bpy.ops.object.select_all(action="DESELECT")
preview_path = os.path.join(PREVIEW_DIR, "AshenCourtyard_Model02.png")
scene.render.filepath = preview_path
bpy.ops.render.render(write_still=True)

print(f"BLEND={blend_path}")
print(f"FBX={fbx_path}")
print(f"PREVIEW={preview_path}")
print(f"EXPORTED_MESHES={len(exported)}")
