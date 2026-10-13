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
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        pass


def material(name, color, metallic=0.0, roughness=0.5, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    if emission is not None:
        emission_input = shader.inputs.get("Emission Color") or shader.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = (*emission, 1.0)
        strength_input = shader.inputs.get("Emission Strength")
        if strength_input:
            strength_input.default_value = emission_strength
    return mat


def finish_object(obj, mat, bevel=0.0):
    if mat:
        obj.data.materials.append(mat)
    if bevel > 0.0:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        modifier = obj.modifiers.new("EdgeWear", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    obj.select_set(False)
    return obj


def cube(name, location, scale, mat, rotation=(0, 0, 0), bevel=0.035):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_object(obj, mat, bevel)


def cylinder(name, location, radius, depth, mat, rotation=(0, 0, 0), vertices=10, bevel=0.02):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_object(obj, mat, bevel)


def sphere(name, location, scale, mat, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_object(obj, mat, 0.025)


def cone(name, location, radius1, radius2, depth, mat, rotation=(0, 0, 0), vertices=10):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius1,
        radius2=radius2,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    return finish_object(obj, mat, 0.025)


def tattered_panel(name, location, width, height, mat, rotation=(0, 0, 0), depth=0.035, points=5):
    top = height * 0.5
    bottom = -height * 0.5
    half = width * 0.5
    front = [
        (-half, 0, top),
        (half, 0, top),
        (half * 0.76, 0, bottom + height * 0.08),
        (half * 0.18, 0, bottom + height * 0.2),
        (-half * 0.25, 0, bottom),
        (-half * 0.78, 0, bottom + height * 0.12),
    ]
    verts = [(x, -depth, z) for x, _, z in front] + [(x, depth, z) for x, _, z in front]
    faces = [
        (0, 1, 2, 3, 4, 5),
        (11, 10, 9, 8, 7, 6),
        (0, 6, 7, 1), (1, 7, 8, 2), (2, 8, 9, 3),
        (3, 9, 10, 4), (4, 10, 11, 5), (5, 11, 6, 0),
    ]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    return finish_object(obj, mat, 0.012)


def sword_blade(name, location, length, width, thickness, mat, rotation=(0, 0, 0)):
    half_w = width * 0.5
    half_t = thickness * 0.5
    z0 = -length * 0.5
    z1 = length * 0.36
    z2 = length * 0.5
    verts = [
        (-half_w, -half_t, z0), (half_w, -half_t, z0),
        (-half_w, half_t, z0), (half_w, half_t, z0),
        (-half_w, -half_t, z1), (half_w, -half_t, z1),
        (-half_w, half_t, z1), (half_w, half_t, z1),
        (0, -half_t, z2), (0, half_t, z2),
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
    return finish_object(obj, mat, 0.012)


def point_camera(camera, target):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


reset_scene()

armor = material("ObsidianArmor", (0.012, 0.018, 0.028), metallic=0.88, roughness=0.25)
steel = material("AshenSteel", (0.12, 0.145, 0.18), metallic=0.95, roughness=0.22)
edge = material("WornEdges", (0.27, 0.29, 0.32), metallic=0.85, roughness=0.31)
leather = material("BlackLeather", (0.035, 0.018, 0.015), metallic=0.05, roughness=0.72)
cloth = material("DriedBloodCloth", (0.105, 0.004, 0.012), metallic=0.0, roughness=0.88)
ember = material("CrimsonEmber", (0.38, 0.002, 0.008), metallic=0.15, roughness=0.24,
                 emission=(1.0, 0.005, 0.015), emission_strength=7.0)

# Lean legs and armored boots.
cube("LeftBoot", (-0.19, -0.03, 0.13), (0.15, 0.28, 0.12), armor, bevel=0.035)
cube("RightBoot", (0.19, -0.03, 0.13), (0.15, 0.28, 0.12), armor, bevel=0.035)
cube("LeftGreave", (-0.19, 0.0, 0.48), (0.13, 0.16, 0.32), steel, rotation=(0.02, 0, -0.03))
cube("RightGreave", (0.19, 0.0, 0.48), (0.13, 0.16, 0.32), steel, rotation=(-0.02, 0, 0.03))
cube("LeftThigh", (-0.18, 0.0, 0.88), (0.14, 0.17, 0.3), armor)
cube("RightThigh", (0.18, 0.0, 0.88), (0.14, 0.17, 0.3), armor)

# Tattered coat silhouette.
tattered_panel("LeftCoatTail", (-0.25, 0.11, 0.76), 0.42, 1.15, cloth, rotation=(0.10, 0.0, 0.07))
tattered_panel("CenterCoatTail", (0.0, 0.15, 0.73), 0.38, 1.25, leather, rotation=(0.13, 0.0, 0.0))
tattered_panel("RightCoatTail", (0.25, 0.11, 0.78), 0.42, 1.08, cloth, rotation=(0.09, 0.0, -0.08))

# Torso, layered chest plates and belt.
cube("Waist", (0, 0, 1.12), (0.34, 0.22, 0.19), leather)
cube("Belt", (0, -0.01, 1.22), (0.4, 0.235, 0.075), edge, bevel=0.025)
cube("LeanCuirass", (0, 0, 1.53), (0.39, 0.22, 0.36), armor, rotation=(0.0, 0.0, 0.0), bevel=0.055)
cube("LowerChestPlate", (0, -0.225, 1.43), (0.31, 0.045, 0.16), steel, rotation=(0.08, 0, 0))
cube("UpperChestPlate", (0, -0.23, 1.68), (0.34, 0.05, 0.15), edge, rotation=(-0.08, 0, 0))
cube("CrimsonSash", (0.0, -0.25, 1.24), (0.42, 0.035, 0.055), cloth, rotation=(0, 0, -0.08), bevel=0.012)

# Asymmetric mantle and cape.
tattered_panel("BackMantle", (-0.03, 0.24, 1.47), 0.92, 1.05, cloth, rotation=(-0.1, 0, -0.05))
sphere("LeftMantle", (-0.49, 0.02, 1.7), (0.36, 0.3, 0.22), cloth, rotation=(0, 0, -0.18))
sphere("RightPauldron", (0.48, 0.0, 1.68), (0.34, 0.27, 0.22), steel, rotation=(0, 0, 0.17))
cone("RightPauldronSpike", (0.69, 0.0, 1.73), 0.11, 0.015, 0.38, edge, rotation=(0, math.radians(90), 0), vertices=8)

# Arms in a restrained weapon-ready pose.
cylinder("LeftUpperArm", (-0.49, -0.01, 1.42), 0.115, 0.48, armor, rotation=(0.0, math.radians(5), math.radians(-7)), vertices=8)
cylinder("LeftBracer", (-0.5, -0.11, 1.12), 0.12, 0.38, steel, rotation=(math.radians(18), 0, math.radians(-4)), vertices=8)
cylinder("RightUpperArm", (0.49, -0.08, 1.43), 0.115, 0.46, armor, rotation=(math.radians(12), 0, math.radians(8)), vertices=8)
cylinder("RightBracer", (0.5, -0.27, 1.17), 0.12, 0.38, steel, rotation=(math.radians(42), 0, math.radians(4)), vertices=8)

# Masked face, wide brim and tall crooked helm.
sphere("ShadowedHead", (0, 0, 2.02), (0.29, 0.27, 0.32), armor)
cube("BeakedMask", (0, -0.27, 1.99), (0.24, 0.17, 0.19), steel, rotation=(math.radians(-13), 0, 0), bevel=0.035)
cube("CrimsonEyeSlit", (0, -0.447, 2.075), (0.205, 0.018, 0.027), ember, rotation=(math.radians(-5), 0, 0), bevel=0.01)
cylinder("HunterHelmBrim", (0, 0, 2.23), 0.49, 0.075, edge, rotation=(0, 0, math.radians(6)), vertices=12, bevel=0.018)
cone("PointedHunterHelm", (-0.055, 0.035, 2.58), 0.35, 0.035, 0.76, armor,
     rotation=(math.radians(7), math.radians(-5), math.radians(9)), vertices=10)
cone("CrookedHelmTip", (-0.14, 0.03, 2.96), 0.085, 0.01, 0.36, armor,
     rotation=(math.radians(8), math.radians(-18), math.radians(16)), vertices=8)

# Massive back sword, clearly readable from the isometric camera.
sword_rotation = (math.radians(8), math.radians(-12), math.radians(-34))
sword_blade("GreatswordBlade", (-0.34, 0.29, 1.54), 2.15, 0.19, 0.055, edge, sword_rotation)
cube("GreatswordFuller", (-0.34, 0.245, 1.54), (0.026, 0.015, 0.78), steel, rotation=sword_rotation, bevel=0.006)
cube("GreatswordGuard", (0.24, 0.27, 0.74), (0.37, 0.065, 0.055), steel,
     rotation=(math.radians(8), math.radians(-12), math.radians(-34)))
cylinder("GreatswordGrip", (0.42, 0.28, 0.48), 0.055, 0.4, leather,
         rotation=(math.radians(8), math.radians(-12), math.radians(-34)), vertices=8)

# Compact dark-fantasy firearm in the right hand.
cube("HunterPistolBody", (0.49, -0.5, 1.13), (0.13, 0.35, 0.12), steel, rotation=(math.radians(78), 0, 0), bevel=0.04)
cylinder("HunterPistolBarrel", (0.49, -0.78, 1.2), 0.085, 0.55, armor,
         rotation=(math.radians(90), 0, 0), vertices=10, bevel=0.018)
cylinder("HunterPistolMuzzle", (0.49, -1.06, 1.2), 0.13, 0.09, edge,
         rotation=(math.radians(90), 0, 0), vertices=10, bevel=0.015)
sphere("MuzzleEmber", (0.49, -1.12, 1.2), (0.075, 0.035, 0.075), ember)
cube("PistolGrip", (0.49, -0.48, 0.94), (0.09, 0.095, 0.22), leather, rotation=(math.radians(-10), 0, 0), bevel=0.025)

# Offhand dagger and small armor details.
sword_blade("HipDagger", (-0.5, -0.03, 0.96), 0.82, 0.08, 0.035, steel,
            rotation=(0, math.radians(10), math.radians(-18)))
cube("DaggerGuard", (-0.38, -0.02, 1.28), (0.18, 0.04, 0.035), edge, rotation=(0, 0, math.radians(-18)), bevel=0.01)

# Presentation pedestal and background.
pedestal_mat = material("Pedestal", (0.018, 0.022, 0.03), metallic=0.25, roughness=0.72)
bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=1.45, depth=0.18, location=(0, 0, -0.12))
pedestal = bpy.context.object
pedestal.name = "PresentationPedestal"
pedestal.data.materials.append(pedestal_mat)

# Lighting.
world = bpy.context.scene.world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.002, 0.003, 0.007, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.16

def area_light(name, location, color, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    point_camera(obj, (0, 0, 1.3))
    return obj


area_light("ColdKey", (3.5, -4.5, 5.3), (0.53, 0.67, 1.0), 1050, 4.0)
area_light("CrimsonRim", (-3.5, 2.2, 3.8), (1.0, 0.008, 0.015), 1250, 3.0)
area_light("SoftFill", (1.5, 3.5, 2.0), (0.3, 0.36, 0.5), 550, 3.5)

# Camera and render.
camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (4.6, -6.8, 4.3)
camera.data.type = "ORTHO"
camera.data.ortho_scale = 3.9
camera.data.lens = 55
point_camera(camera, (0, 0, 1.35))
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.filepath = os.path.join(PREVIEW_DIR, "DarkHunter_FirstPass.png")
scene.render.image_settings.color_depth = "8"

scene.view_settings.look = "AgX - Medium High Contrast"

# Save source before exporting, then export only the character objects (not presentation lights/camera).
blend_path = os.path.join(ART_DIR, "DarkHunter_FirstPass.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

for obj in bpy.context.scene.objects:
    obj.select_set(obj.type == "MESH" and obj.name != "PresentationPedestal")
bpy.context.view_layer.objects.active = bpy.context.selected_objects[0] if bpy.context.selected_objects else None

fbx_path = os.path.join(MODEL_DIR, "DarkHunter_FirstPass.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    apply_unit_scale=True,
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=False,
)

# Restore everything for the render and save the final preview.
bpy.ops.object.select_all(action="DESELECT")
scene.render.filepath = os.path.join(PREVIEW_DIR, "DarkHunter_FirstPass.png")
bpy.ops.render.render(write_still=True)

print(f"BLEND={blend_path}")
print(f"FBX={fbx_path}")
print(f"PREVIEW={scene.render.filepath}")
