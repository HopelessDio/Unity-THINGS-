import bpy
import math
import os
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART_DIR = os.path.join(ROOT, "ArtSource")
MODEL_DIR = os.path.join(ROOT, "Assets", "Models", "Player")
PREVIEW_DIR = os.path.join(ART_DIR, "Previews")
for directory in (ART_DIR, MODEL_DIR, PREVIEW_DIR):
    os.makedirs(directory, exist_ok=True)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def make_material(name, base, metallic, roughness, noise_strength=0.12, emission=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*base, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness

    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 18.0
    noise.inputs["Detail"].default_value = 5.0
    noise.inputs["Roughness"].default_value = 0.75
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.22
    ramp.color_ramp.elements[0].color = (base[0] * 0.3, base[1] * 0.3, base[2] * 0.3, 1)
    ramp.color_ramp.elements[1].position = 0.8
    ramp.color_ramp.elements[1].color = (
        min(1.0, base[0] + noise_strength),
        min(1.0, base[1] + noise_strength),
        min(1.0, base[2] + noise_strength),
        1,
    )
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.16
    bump.inputs["Distance"].default_value = 0.06
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])

    if emission:
        emission_input = shader.inputs.get("Emission Color") or shader.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = (*emission, 1.0)
        strength = shader.inputs.get("Emission Strength")
        if strength:
            strength.default_value = 8.0
    return mat


def finish(obj, mat, bevel=0.0):
    if mat:
        obj.data.materials.append(mat)
    if bevel:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        modifier = obj.modifiers.new("ForgedEdge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        obj.select_set(False)
    return obj


def box(name, location, scale, mat, rotation=(0, 0, 0), bevel=0.018):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish(obj, mat, bevel)


def cyl(name, location, radius, depth, mat, rotation=(0, 0, 0), vertices=12, bevel=0.012):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, bevel)


def ico(name, location, scale, mat, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish(obj, mat, 0.012)


def ellipsoid(name, location, scale, mat, rotation=(0, 0, 0), segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    finish(obj, mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)
    return obj


def limb(name, start, end, radius_start, radius_end, mat, vertices=16):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    midpoint = (start_v + end_v) * 0.5
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=radius_start, radius2=radius_end,
        depth=direction.length, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    finish(obj, mat, 0.012)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)
    return obj


def cone(name, location, radius1, radius2, depth, mat, rotation=(0, 0, 0), vertices=10):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=radius1, radius2=radius2, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, mat, 0.012)


def panel(name, location, width, height, mat, rotation=(0, 0, 0), depth=0.018, seed=0):
    half_w = width * 0.5
    half_h = height * 0.5
    notch = 0.1 + (seed % 3) * 0.035
    outline = [
        (-half_w, half_h),
        (half_w, half_h),
        (half_w * 0.88, -half_h + height * 0.10),
        (half_w * 0.42, -half_h + height * notch),
        (half_w * 0.08, -half_h),
        (-half_w * 0.28, -half_h + height * (notch + 0.05)),
        (-half_w * 0.82, -half_h + height * 0.06),
    ]
    front = [(x, -depth, z) for x, z in outline]
    back = [(x, depth, z) for x, z in outline]
    verts = front + back
    count = len(outline)
    faces = [tuple(range(count)), tuple(range(count * 2 - 1, count - 1, -1))]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    return finish(obj, mat, 0.008)


def blade(name, location, length, width, thickness, mat, rotation=(0, 0, 0)):
    hw = width * 0.5
    ht = thickness * 0.5
    low = -length * 0.5
    shoulder = length * 0.37
    tip = length * 0.5
    verts = [
        (-hw, -ht, low), (hw, -ht, low), (-hw, ht, low), (hw, ht, low),
        (-hw, -ht, shoulder), (hw, -ht, shoulder), (-hw, ht, shoulder), (hw, ht, shoulder),
        (0, -ht, tip), (0, ht, tip),
    ]
    faces = [
        (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5),
        (4, 5, 8), (6, 9, 7), (4, 8, 9, 6), (5, 7, 9, 8), (0, 2, 3, 1),
    ]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    return finish(obj, mat, 0.008)


def sallet(name, location, mat):
    # Elliptical rings form a narrow, pointed medieval sallet rather than a cone or hat.
    rings = [
        (-0.25, 0.23, 0.18),
        (-0.08, 0.245, 0.19),
        (0.10, 0.205, 0.175),
        (0.27, 0.135, 0.12),
        (0.39, 0.035, 0.035),
    ]
    sides = 12
    verts = []
    for z, rx, ry in rings:
        for index in range(sides):
            angle = index * math.tau / sides
            verts.append((math.cos(angle) * rx, math.sin(angle) * ry, z))
    faces = []
    for level in range(len(rings) - 1):
        for index in range(sides):
            nxt = (index + 1) % sides
            a = level * sides + index
            b = level * sides + nxt
            c = (level + 1) * sides + nxt
            d = (level + 1) * sides + index
            faces.append((a, b, c, d))
    faces.append(tuple(range((len(rings) - 1) * sides, len(rings) * sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return finish(obj, mat, 0.012)


def parent_to_bone(obj, rig, bone_name):
    matrix = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = matrix


def make_rig():
    arm_data = bpy.data.armatures.new("AshenWardenRig")
    rig = bpy.data.objects.new("AshenWardenRig", arm_data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(name, head, tail, parent=None):
        edit_bone = arm_data.edit_bones.new(name)
        edit_bone.head = head
        edit_bone.tail = tail
        if parent:
            edit_bone.parent = arm_data.edit_bones[parent]
        return edit_bone

    bone("Root", (0, 0, 0), (0, 0, 0.2))
    bone("Hips", (0, 0, 0.98), (0, 0, 1.17), "Root")
    bone("Spine", (0, 0, 1.17), (0, 0, 1.38), "Hips")
    bone("Chest", (0, 0, 1.38), (0, 0, 1.62), "Spine")
    bone("Neck", (0, 0, 1.62), (0, 0, 1.74), "Chest")
    bone("Head", (0, 0, 1.74), (0, 0, 2.08), "Neck")

    bone("LeftUpperLeg", (-0.16, 0, 1.02), (-0.16, 0, 0.66), "Hips")
    bone("LeftLowerLeg", (-0.16, 0, 0.66), (-0.16, 0, 0.27), "LeftUpperLeg")
    bone("LeftFoot", (-0.16, 0, 0.27), (-0.16, -0.22, 0.10), "LeftLowerLeg")
    bone("RightUpperLeg", (0.16, 0, 1.02), (0.16, 0, 0.66), "Hips")
    bone("RightLowerLeg", (0.16, 0, 0.66), (0.16, 0, 0.27), "RightUpperLeg")
    bone("RightFoot", (0.16, 0, 0.27), (0.16, -0.22, 0.10), "RightLowerLeg")

    bone("LeftShoulder", (0, 0, 1.56), (-0.32, 0, 1.56), "Chest")
    bone("LeftUpperArm", (-0.32, 0, 1.56), (-0.58, 0, 1.42), "LeftShoulder")
    bone("LeftLowerArm", (-0.58, 0, 1.42), (-0.69, -0.02, 1.14), "LeftUpperArm")
    bone("LeftHand", (-0.69, -0.02, 1.14), (-0.71, -0.05, 1.00), "LeftLowerArm")
    bone("RightShoulder", (0, 0, 1.56), (0.32, 0, 1.56), "Chest")
    bone("RightUpperArm", (0.32, 0, 1.56), (0.58, 0, 1.42), "RightShoulder")
    bone("RightLowerArm", (0.58, 0, 1.42), (0.69, -0.02, 1.14), "RightUpperArm")
    bone("RightHand", (0.69, -0.02, 1.14), (0.71, -0.05, 1.00), "RightLowerArm")

    bpy.ops.object.mode_set(mode="OBJECT")
    rig.show_in_front = True
    rig.hide_render = True
    rig.select_set(False)
    return rig


reset_scene()

armor = make_material("BlackenedSteel", (0.018, 0.022, 0.028), 0.9, 0.27, 0.11)
steel = make_material("WornSteel", (0.105, 0.12, 0.145), 0.95, 0.25, 0.18)
edge = make_material("PolishedEdges", (0.24, 0.25, 0.27), 0.95, 0.2, 0.2)
leather = make_material("CrackedLeather", (0.035, 0.018, 0.012), 0.05, 0.78, 0.035)
cloth = make_material("AshCloth", (0.025, 0.025, 0.03), 0.0, 0.92, 0.025)
red_cloth = make_material("BloodMantle", (0.085, 0.004, 0.009), 0.0, 0.9, 0.055)
chain = make_material("DarkChainmail", (0.045, 0.052, 0.06), 0.85, 0.45, 0.15)
ember = make_material("CrimsonEyes", (0.16, 0.0, 0.004), 0.2, 0.22, 0.02, emission=(1.0, 0.002, 0.006))

rig = make_rig()
parts = []

def part(obj, bone_name):
    parent_to_bone(obj, rig, bone_name)
    parts.append(obj)
    return obj


# Boots, legs, kneecaps and thigh guards. Rounded anatomical volumes keep the
# silhouette human; the hard plates sit over them instead of replacing them.
part(ellipsoid("LeftBoot", (-0.16, -0.11, 0.11), (0.125, 0.255, 0.105), leather), "LeftFoot")
part(ellipsoid("RightBoot", (0.16, -0.11, 0.11), (0.125, 0.255, 0.105), leather), "RightFoot")
part(limb("LeftShin", (-0.16, 0, 0.24), (-0.16, 0, 0.67), 0.105, 0.125, armor), "LeftLowerLeg")
part(limb("RightShin", (0.16, 0, 0.24), (0.16, 0, 0.67), 0.105, 0.125, armor), "RightLowerLeg")
part(cone("LeftKnee", (-0.16, -0.14, 0.68), 0.16, 0.035, 0.26, steel, rotation=(math.radians(90), 0, 0), vertices=6), "LeftLowerLeg")
part(cone("RightKnee", (0.16, -0.14, 0.68), 0.16, 0.035, 0.26, steel, rotation=(math.radians(90), 0, 0), vertices=6), "RightLowerLeg")
part(limb("LeftThigh", (-0.16, 0, 0.68), (-0.16, 0, 1.07), 0.13, 0.16, cloth), "LeftUpperLeg")
part(limb("RightThigh", (0.16, 0, 0.68), (0.16, 0, 1.07), 0.13, 0.16, cloth), "RightUpperLeg")
for side, x in (("Left", -0.16), ("Right", 0.16)):
    for index in range(3):
        part(box(f"{side}ThighPlate{index}", (x, -0.155, 0.91 - index * 0.1),
                 (0.135 - index * 0.008, 0.025, 0.07), steel,
                 rotation=(math.radians(8), 0, 0), bevel=0.012), f"{side}UpperLeg")

# Pelvis, belt and layered torso.
part(ellipsoid("PelvisArmor", (0, 0, 1.08), (0.31, 0.19, 0.18), armor), "Hips")
part(box("MainBelt", (0, -0.01, 1.18), (0.37, 0.205, 0.055), leather, bevel=0.015), "Hips")
part(cyl("BeltBuckle", (0, -0.22, 1.18), 0.07, 0.035, edge, rotation=(math.radians(90), 0, 0), vertices=10), "Hips")
part(ellipsoid("Cuirass", (0, 0, 1.43), (0.35, 0.19, 0.31), armor), "Chest")
for index in range(4):
    part(box(f"ChestLamella{index}", (0, -0.193, 1.30 + index * 0.095),
             (0.31 - index * 0.018, 0.026, 0.065), steel,
             rotation=(math.radians(-5 + index * 2), 0, 0), bevel=0.01), "Chest")
part(box("ChestLeatherCross", (-0.02, -0.225, 1.43), (0.035, 0.022, 0.34), leather,
         rotation=(0, 0, math.radians(-25)), bevel=0.008), "Chest")

# Split coat and ragged mantle panels.
coat_specs = [
    ("CoatFrontLeft", (-0.19, -0.13, 0.78), 0.32, 0.95, cloth, (0.05, 0, -0.035)),
    ("CoatFrontRight", (0.19, -0.13, 0.78), 0.32, 0.9, cloth, (0.04, 0, 0.035)),
    ("CoatRearLeft", (-0.22, 0.14, 0.79), 0.38, 1.02, red_cloth, (-0.08, 0, -0.05)),
    ("CoatRearCenter", (0, 0.17, 0.76), 0.34, 1.12, cloth, (-0.1, 0, 0)),
    ("CoatRearRight", (0.22, 0.14, 0.81), 0.38, 0.98, red_cloth, (-0.07, 0, 0.06)),
]
for index, (name, loc, width, height, mat, rot) in enumerate(coat_specs):
    part(panel(name, loc, width, height, mat, rot, seed=index), "Hips")

mantle_specs = [
    ("MantleBack", (0.03, 0.2, 1.45), 0.78, 0.72, cloth, (-0.12, 0, 0.02)),
    ("MantleLeft", (-0.3, 0.04, 1.48), 0.48, 0.72, red_cloth, (-0.02, 0.1, -0.3)),
    ("MantleRight", (0.3, 0.06, 1.46), 0.45, 0.62, cloth, (-0.03, -0.08, 0.28)),
]
for index, (name, loc, width, height, mat, rot) in enumerate(mantle_specs):
    part(panel(name, loc, width, height, mat, rot, seed=index + 6), "Chest")

# Layered pauldrons, mail sleeves, arms, bracers and hands.
for side, sign in (("Left", -1), ("Right", 1)):
    shoulder_bone = f"{side}Shoulder"
    upper_bone = f"{side}UpperArm"
    lower_bone = f"{side}LowerArm"
    hand_bone = f"{side}Hand"
    part(ellipsoid(f"{side}ChainSleeve", (0.43 * sign, 0, 1.48), (0.145, 0.14, 0.22), chain), upper_bone)
    for index in range(4):
        part(box(f"{side}PauldronPlate{index}",
                 ((0.38 + index * 0.055) * sign, -0.005, 1.62 - index * 0.055),
                 (0.155 - index * 0.017, 0.145, 0.045), steel,
                 rotation=(0, math.radians(index * 3 * sign), math.radians(10 * sign)), bevel=0.018), shoulder_bone)
    part(limb(f"{side}UpperArm", (0.36 * sign, 0, 1.53), (0.57 * sign, 0, 1.34), 0.115, 0.095, cloth), upper_bone)
    part(limb(f"{side}Forearm", (0.57 * sign, 0, 1.34), (0.69 * sign, -0.02, 1.10), 0.105, 0.082, leather), lower_bone)
    for index in range(3):
        part(box(f"{side}BracerPlate{index}", ((0.64 + index * 0.018) * sign, -0.105, 1.25 - index * 0.09),
                 (0.11, 0.03, 0.065), steel, rotation=(math.radians(8), 0, math.radians(8 * sign)), bevel=0.008), lower_bone)
    part(ellipsoid(f"{side}Gauntlet", (0.71 * sign, -0.03, 1.04), (0.085, 0.072, 0.12), armor), hand_bone)

# Helmet and face assembly.
part(ellipsoid("Coif", (0, 0, 1.76), (0.205, 0.185, 0.225), chain), "Head")
part(sallet("SalletHelmet", (0, 0, 1.91), armor), "Head")
part(ellipsoid("AngularFaceplate", (0, -0.175, 1.86), (0.19, 0.065, 0.205), steel,
         rotation=(math.radians(-8), 0, 0)), "Head")
part(cone("FaceplateBeak", (0, -0.25, 1.83), 0.095, 0.018, 0.2, steel,
          rotation=(math.radians(90), 0, 0), vertices=6), "Head")
part(box("CrimsonEyeSlit", (0, -0.237, 1.94), (0.15, 0.010, 0.014), ember,
         rotation=(math.radians(-3), 0, 0), bevel=0.005), "Head")
for sign in (-1, 1):
    part(box(f"HelmetCheek{sign}", (0.145 * sign, -0.185, 1.79), (0.045, 0.025, 0.15), edge,
             rotation=(math.radians(-5), math.radians(8 * sign), math.radians(3 * sign)), bevel=0.01), "Head")

# Greatsword and scabbard across the back.
sword_rot = (math.radians(7), math.radians(-9), math.radians(-27))
part(blade("GreatswordBlade", (-0.31, 0.27, 1.48), 2.0, 0.16, 0.045, edge, sword_rot), "Chest")
part(box("GreatswordFuller", (-0.31, 0.245, 1.48), (0.018, 0.012, 0.7), armor, sword_rot, bevel=0.004), "Chest")
part(box("GreatswordGuard", (0.18, 0.275, 0.68), (0.31, 0.045, 0.045), steel, sword_rot, bevel=0.012), "Chest")
part(cyl("GreatswordGrip", (0.31, 0.28, 0.48), 0.045, 0.34, leather, sword_rot, vertices=10), "Chest")
part(ico("GreatswordPommel", (0.39, 0.285, 0.36), (0.07, 0.055, 0.08), edge), "Chest")
part(box("ScabbardStrap", (0.02, 0.225, 1.46), (0.035, 0.025, 0.43), leather,
         rotation=(0, 0, math.radians(-28)), bevel=0.006), "Chest")

# Gothic hand cannon, parented to the right hand.
part(box("HandCannonReceiver", (0.76, -0.12, 1.02), (0.10, 0.19, 0.09), steel,
         rotation=(math.radians(78), 0, math.radians(3)), bevel=0.025), "RightHand")
part(cyl("HandCannonBarrel", (0.76, -0.34, 1.06), 0.065, 0.42, armor,
         rotation=(math.radians(90), 0, 0), vertices=12), "RightHand")
part(cyl("HandCannonMuzzle", (0.76, -0.56, 1.06), 0.105, 0.075, edge,
         rotation=(math.radians(90), 0, 0), vertices=12), "RightHand")
part(box("HandCannonGrip", (0.76, -0.11, 0.91), (0.07, 0.07, 0.15), leather,
         rotation=(math.radians(-12), 0, 0), bevel=0.012), "RightHand")
part(cyl("HandCannonEmber", (0.76, -0.605, 1.06), 0.045, 0.018, ember,
         rotation=(math.radians(90), 0, 0), vertices=12, bevel=0.004), "RightHand")

# Dagger and utility pouches.
part(blade("HipDagger", (-0.42, -0.08, 0.98), 0.66, 0.065, 0.025, edge,
           rotation=(0, math.radians(8), math.radians(-12))), "Hips")
part(box("LeftPouch", (-0.34, -0.18, 1.1), (0.105, 0.055, 0.13), leather, bevel=0.015), "Hips")
part(box("RightPouch", (0.34, -0.17, 1.08), (0.1, 0.055, 0.11), leather, bevel=0.015), "Hips")

# Presentation floor and dramatic neutral lighting.
floor_mat = make_material("PreviewFloor", (0.018, 0.02, 0.025), 0.1, 0.8, 0.02)
bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=1.25, depth=0.1, location=(0, 0, -0.08))
floor = bpy.context.object
floor.name = "PreviewPedestal"
floor.data.materials.append(floor_mat)

world = bpy.context.scene.world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.003, 0.004, 0.007, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.13

def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

def light(name, location, color, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    aim(obj, (0, 0, 1.1))

light("ColdKey", (3.2, -4.2, 4.1), (0.55, 0.68, 1.0), 900, 3.2)
light("CrimsonRim", (-3.0, 2.5, 3.0), (1.0, 0.005, 0.012), 1050, 2.8)
light("SoftFill", (1.5, 3.0, 2.5), (0.35, 0.4, 0.55), 420, 3.0)

camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (3.6, -5.4, 3.1)
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.65
aim(camera, (0, 0, 1.12))
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

blend_path = os.path.join(ART_DIR, "AshenWarden_Model01.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

# Export character mesh and rig only.
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
fbx_path = os.path.join(MODEL_DIR, "AshenWarden_Model01.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=False,
)

bpy.ops.object.select_all(action="DESELECT")
preview_path = os.path.join(PREVIEW_DIR, "AshenWarden_Model01.png")
scene.render.filepath = preview_path
bpy.ops.render.render(write_still=True)

print(f"BLEND={blend_path}")
print(f"FBX={fbx_path}")
print(f"PREVIEW={preview_path}")
print(f"PARTS={len(parts)}")
