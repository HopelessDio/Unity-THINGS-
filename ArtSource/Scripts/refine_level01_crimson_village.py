"""Build a separate, editable crimson village from the approved route blockout."""
from pathlib import Path
import math
import random
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
random.seed(46)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Level01_CourtyardPath_Blockout01.blend'))
scene=bpy.context.scene

def mat(name,color,emission=0):
    result=bpy.data.materials.new(name); result.diffuse_color=(*color,1); result.use_nodes=True
    shader=result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(*color,1); shader.inputs['Roughness'].default_value=.9
    if emission:
        shader.inputs['Emission Color'].default_value=(*color,1); shader.inputs['Emission Strength'].default_value=emission
    return result

brick=[mat('Crimson village stone '+str(i),(.045+i*.009,.055+i*.009,.075+i*.012)) for i in range(5)]
leafmat=[mat('Crimson foliage '+str(i),color) for i,color in enumerate([
    (.15,.008,.021),(.26,.014,.032),(.36,.022,.04),(.21,.012,.033),(.11,.006,.016)])]
bark=mat('Dark bark',(.035,.018,.022)); metal=mat('Lantern iron',(.012,.014,.023))
timber=mat('Door and beam wood',(.075,.034,.025))
roofmats=[mat('Slate roof tiles '+str(i),(.026+i*.005,.034+i*.006,.052+i*.008)) for i in range(4)]

# Efficient linked unit cube mesh, so thousands of modular details stay light.
mesh=bpy.data.meshes.new('Reusable unit masonry block')
mesh.from_pydata([(-.5,-.5,-.5),(.5,-.5,-.5),(.5,.5,-.5),(-.5,.5,-.5),
    (-.5,-.5,.5),(.5,-.5,.5),(.5,.5,.5),(-.5,.5,.5)],[],
    [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
mesh.materials.append(brick[0])
def block(name,loc,size,material,rotation=0):
    obj=bpy.data.objects.new(name,mesh); scene.collection.objects.link(obj)
    obj.location=loc; obj.scale=size; obj.rotation_euler.z=rotation
    obj.material_slots[0].link='OBJECT'; obj.material_slots[0].material=material
    return obj

def branch(name,a,b,radius):
    a,b=Vector(a),Vector(b); delta=b-a
    vertices=[]
    for z,r in [(0,radius),(delta.length,radius*.52)]:
        vertices += [(r*math.cos(i*math.tau/7),r*math.sin(i*math.tau/7),z) for i in range(7)]
    faces=[tuple(range(6,-1,-1)),tuple(range(7,14))]+[(i,(i+1)%7,(i+1)%7+7,i+7) for i in range(7)]
    data=bpy.data.meshes.new(name); data.from_pydata(vertices,[],faces); data.materials.append(bark)
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj); obj.location=a
    obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler(); return obj

# Keep geometry, but replace the flat studio-blockout surface palette.
for material in list(bpy.data.materials):
    if material.name.startswith('Slate_'):
        nodes=material.node_tree.nodes; links=material.node_tree.links
        shader=nodes.get('Principled BSDF'); base=shader.inputs['Base Color'].default_value
        shader.inputs['Base Color'].default_value=(base[0]*.62,base[1]*.62,base[2]*.65,1)
        noise=nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=5
        bump=nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.18; bump.inputs['Distance'].default_value=.04
        links.new(noise.outputs['Fac'],bump.inputs['Height']); links.new(bump.outputs['Normal'],shader.inputs['Normal'])
    if material.name=='Dried red ground':
        material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.018,.007,.015,1)

# Remove the exposed rectangular diorama base and obsolete stick-tree markers.
for obj in list(scene.objects):
    if obj.name.startswith(('Village ground foundation','Tree trunk','Angular branch','Angular twig','Player scale marker','Sphere')):
        bpy.data.objects.remove(obj,do_unlink=True)
soil=mat('Shadowed village soil',(.021,.009,.015))
block('School district soil',(-1,-27,-.65),(35,44,.8),soil)
block('Courtyard district soil',(0,1,-.65),(48,39,.8),soil)
block('Last lane district soil',(10,37,-.65),(46,53,.8),soil)

# Split low walls into readable courses, with coping and repeated posts.
for obj in list(scene.objects):
    if obj.type!='MESH' or not any(obj.name.startswith(prefix) for prefix in
        ['School lane west wall','School lane east wall','Final lane west wall','Final lane east wall','House approach west wall','House approach east wall']): continue
    x,y,_=obj.location; along_x=obj.dimensions.x>obj.dimensions.y
    for row in range(4):
        for part in range(2):
            offset=(part-.5)*.96
            block('Wall individual stone',(x+(offset if along_x else 0),y+(0 if along_x else offset),.2+row*.4),
                (.92,.55,.37) if along_x else (.55,.92,.37),random.choice(brick))
    bpy.data.objects.remove(obj,do_unlink=True)
for obj in list(scene.objects):
    if obj.name.startswith('Wall cap'): obj.location.z=1.68
for x,y in [(-5,-42),(5,-42),(-5,-30),(5,-30),(-5,-16),(5,-16),(-11,-11),(11,-11),
            (-11,11),(11,11),(-5,15),(5,15),(-5,29),(5,29),(15,40),(25,40),(15,54),(25,54)]:
    block('Boundary pillar',(x,y,1.08),(.9,.9,2.16),brick[1])
    block('Pillar cap',(x,y,2.2),(1.08,1.08,.18),brick[3])

houses=[('School',-11,-37,10,9,6),('West courtyard house',-17,-2,8,8,5),
    ('East courtyard house',17,2,8,8,5),('Background cottage',-12,24,8,7,4.8),('Family home',20,59,10,8,5.2)]
for name,x,y,width,depth,height in houses:
    front=y-depth*.5-.18
    # Repeated facade stones and dark timber braces, not a flat cube facade.
    for row in range(int(height/.5)):
        for col in range(int(width)):
            px=x-width*.5+col+.5
            pz=row*.5+.25
            if abs(px-x)<.9 and pz<2.8: continue
            if any(abs(px-(x+side*width*.31))<.75 for side in [-1,1]) and 1.9<pz<3.7: continue
            block('Facade stone course',(px,front+.05,pz),(.94,.12,.45),random.choice(brick))
    for offset in [-width*.5+.15,width*.5-.15]:
        block('Facade corner pier',(x+offset,front-.03,height*.5),(.24,.22,height),brick[3])
    for z in [height-.1,.3]: block('Facade horizontal beam',(x,front-.05,z),(width,.2,.2),timber)
    for offset in [-.82,.82]: block('Door surround',(x+offset,front,1.4),(.24,.28,2.8),brick[3])
    block('Door lintel',(x,front,2.85),(1.9,.3,.25),brick[3])
    for i in range(5): block('Door plank',(x+(i-2)*.24,front-.015,1.3),(.215,.05,2.5),timber)
    for z in [.65,2]: block('Door iron strap',(x,front-.08,z),(1.2,.08,.09),metal)
    # Linked slate shingle blocks laid on the two simple roof planes.
    w=width*.5+.35; d=depth*.5+.35
    columns=6; rows=math.ceil(2*d/.75); angle=math.atan2(2.3,w)
    for side in [-1,1]:
        for col in range(columns):
            px=side*w*(col+.5)/columns; z=height+2.3*(1-abs(px)/w)+.05
            for row in range(rows):
                py=-d+(row+.5)*2*d/rows
                tile=block('Reusable slate roof shingle',(x+px,y+py,z),(math.hypot(w,2.3)/columns*.98,2*d/rows*.96,.09),random.choice(roofmats))
                tile.rotation_euler.y=side*angle
    for i in range(math.ceil(2*d/.7)):
        block('Roof ridge cap',(x,y-d+(i+.5)*.7,height+2.36),(.35,.68,.18),roofmats[2])

# Iron frames around existing luminous lantern cores.
for obj in list(scene.objects):
    if obj.name.startswith('Lamp lantern'):
        x,y,z=obj.location
        for sx in [-1,1]:
            for sy in [-1,1]: block('Lantern frame',(x+sx*.255,y+sy*.255,z),(.065,.065,.8),metal)
        block('Lantern base',(x,y,z-.41),(.65,.65,.12),metal)
    if obj.type=='LIGHT' and obj.data.type=='POINT':
        obj.data.energy=240; obj.data.color=(1,.23,.045); obj.data.shadow_soft_size=.65

def tree(x,y,height=7.5):
    branch('Crimson tree trunk',(x,y,0),(x+.25,y,height*.8),.35)
    canopy=[]
    for i in range(7):
        angle=i*math.tau/7+random.uniform(-.25,.25)
        reach=random.uniform(1.8,3.1)
        start=(x+.1,y,height*.43)
        tip=(x+math.cos(angle)*reach,y+math.sin(angle)*reach,height*random.uniform(.73,1.03))
        mid=Vector(start).lerp(Vector(tip),.6)
        branch('Crimson tree branch',start,mid,.17); branch('Crimson tree twig',mid,tip,.09)
        canopy.append(Vector(tip))
    verts=[]; faces=[]; colors=[]
    for center in canopy:
        for leaf in range(100):
            theta=random.uniform(0,math.tau); radius=math.sqrt(random.random())*1.5
            position=center+Vector((math.cos(theta)*radius,math.sin(theta)*radius,random.uniform(-.65,.65)))
            length=random.uniform(.18,.36); width=length*.65; angle=random.uniform(0,math.tau)
            a=Vector((math.cos(angle)*length,math.sin(angle)*length,random.uniform(-.12,.12)))
            b=Vector((-math.sin(angle)*width,math.cos(angle)*width,random.uniform(-.08,.08)))
            idx=len(verts); verts += [position-a,position+b,position+a,position-b]
            faces.append((idx,idx+1,idx+2,idx+3)); colors.append(random.randrange(len(leafmat)))
    data=bpy.data.meshes.new('Crimson clustered leaves'); data.from_pydata(verts,[],faces)
    for material in leafmat: data.materials.append(material)
    for poly,index in zip(data.polygons,colors): poly.material_index=index
    obj=bpy.data.objects.new('Crimson tree leaf canopy',data); scene.collection.objects.link(obj)
    return x,y

trees=[(-8,-45),(10,-36),(-8,-22),(10,-17),(-10,7),(11,-8),(-23,10),(24,10),
       (-7,17),(9,24),(-8,34),(9,43),(29,44),(29,57),(11,58),(28,65)]
for x,y in trees: tree(x,y,random.uniform(7,9))

# Leaf litter in merged meshes, concentrated beside trees rather than blocking road.
verts=[]; faces=[]; colors=[]
for x,y in trees:
    for i in range(95):
        angle=random.uniform(0,math.tau); radius=random.uniform(.2,3.6)
        px=x+math.cos(angle)*radius; py=y+math.sin(angle)*radius
        size=random.uniform(.06,.22); idx=len(verts)
        verts += [(px-size,py,.015),(px,py+size*.6,.018),(px+size,py,.015),(px,py-size*.6,.018)]
        faces.append((idx,idx+1,idx+2,idx+3)); colors.append(random.randrange(len(leafmat)))
data=bpy.data.meshes.new('Fallen crimson leaves'); data.from_pydata(verts,[],faces)
for material in leafmat: data.materials.append(material)
for poly,index in zip(data.polygons,colors): poly.material_index=index
obj=bpy.data.objects.new('Fallen crimson leaves',data); scene.collection.objects.link(obj)

# School label on the front of a simple gate; route still begins at y=-42.
for x in [-3.5,3.5]: block('School gate pillar',(x,-42,2.6),(.85,.85,5.2),brick[1])
block('School gate lintel',(0,-42,4.8),(7.9,.85,1),brick[2])
text=bpy.data.curves.new('School sign','FONT'); text.body='SCHOOL'; text.align_x='CENTER'; text.size=.65; text.extrude=.005
obj=bpy.data.objects.new('School sign',text); scene.collection.objects.link(obj); obj.location=(0,-42.44,4.58)
obj.rotation_euler=(math.pi*.5,0,0); text.materials.append(mat('Ivory school lettering',(.65,.51,.34)))

# Append the existing approved static character, rather than inventing a new one.
with bpy.data.libraries.load(str(ROOT/'ArtSource/VeilWalker_Preview09_HoodCollar.blend'),link=False) as (available,loaded):
    loaded.objects=list(available.objects)
hero=next((obj for obj in loaded.objects if obj and obj.name.startswith('VeilWalker_Preview_Front')),None)
if hero:
    for obj in [hero]+list(hero.children_recursive):
        if not obj.users_collection: scene.collection.objects.link(obj)
    hero.location=(-.7,-44,0); hero.rotation_euler.z=math.pi

# Camera-visible procedural red sky backdrop. It is NOT a painted final map.
camera=scene.camera; camera.location=(88,-112,132)
camera.rotation_euler=(Vector((4,9,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=119
view=(Vector((4,9,0))-camera.location).normalized()
sky=mat('Procedural blood-red cloud backdrop',(.1,.005,.012))
nodes=sky.node_tree.nodes; links=sky.node_tree.links
nodes.clear(); output=nodes.new('ShaderNodeOutputMaterial'); emission=nodes.new('ShaderNodeEmission')
noise=nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=7; noise.inputs['Detail'].default_value=4
coords=nodes.new('ShaderNodeTexCoord'); ramp=nodes.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].position=.18; ramp.color_ramp.elements[0].color=(.009,.001,.006,1)
ramp.color_ramp.elements[1].position=.8; ramp.color_ramp.elements[1].color=(.22,.008,.017,1)
links.new(coords.outputs['Generated'],noise.inputs['Vector']); links.new(noise.outputs['Fac'],ramp.inputs[0])
links.new(ramp.outputs[0],emission.inputs['Color']); links.new(emission.outputs[0],output.inputs[0]); emission.inputs['Strength'].default_value=.7
backdrop=block('Red sky atmosphere backdrop',Vector((4,9,0))+view*110,(260,260,.1),sky)
backdrop.rotation_euler=camera.rotation_euler

# Distant simple skyline silhouettes are real meshes, not part of the road.
silhouette=mat('Burgundy distant village silhouettes',(.018,.002,.008),.1)
for i in range(16):
    x=-66+i*8; y=65+random.uniform(-6,8); h=random.uniform(7,17)
    block('Distant village silhouette',(x,y,h*.5),(random.uniform(5,8),5,h),silhouette)
    block('Distant chimney',(x+1,y,h+1.2),(1,1,3),silhouette)

# Cooler fill + much stronger local amber lights, matching the reference palette.
for obj in scene.objects:
    if obj.type=='LIGHT' and obj.data.type=='AREA':
        if obj.name.startswith('Cool'): obj.data.energy=42000; obj.data.color=(.48,.57,.8)
        else: obj.data.energy=21000; obj.data.color=(1,.12,.07)
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.04,.004,.012,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.3
scene.render.engine='CYCLES'; scene.cycles.samples=40; scene.cycles.use_denoising=False
scene.render.resolution_x=1120; scene.render.resolution_y=1600; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(ROOT/'ArtSource/Previews/Level01_CrimsonCourtyard02.png')
scene.view_settings.view_transform='AgX'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Level01_CrimsonCourtyard02.blend'))
print('CRIMSON_VILLAGE_SAVED',len(scene.objects),'objects',flush=True)
bpy.ops.render.render(write_still=True)
print('CRIMSON_VILLAGE_RENDERED: actual Blender geometry; original blockout and Unity unchanged',flush=True)
