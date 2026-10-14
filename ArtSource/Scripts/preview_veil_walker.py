"""Preview-only Veil Walker geometry. No FBX export or Unity scene changes."""
import bpy
import ast
import math
import os
import sys
import bmesh
from mathutils.bvhtree import BVHTree
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ART = os.path.join(ROOT, 'ArtSource')
HOOD_COLLAR = '--hood-collar' in sys.argv
MASK_SHAPED_FACE = '--mask-shaped-face' in sys.argv or HOOD_COLLAR
KNEE_CAPE = '--knee-cape' in sys.argv or MASK_SHAPED_FACE
CAPE_CLEARANCE = '--cape-clearance' in sys.argv or KNEE_CAPE
LONG_RED_CAPE = '--long-red-cape' in sys.argv or CAPE_CLEARANCE
FACE_REFINED = '--face-refined' in sys.argv or LONG_RED_CAPE
REFINED = '--refined' in sys.argv or FACE_REFINED
SMOOTH = '--smooth' in sys.argv or REFINED
parts = []
sword_parts = []
# Reuse modeling primitives, without executing the knight generator.
with open(os.path.join(ART, 'Scripts', 'create_lean_knight.py'), encoding='utf-8') as source:
    definitions = ast.parse(source.read())
helpers = {'material', 'finish', 'mesh', 'box', 'volume', 'panel', 'rail', 'aim'}
for node in definitions.body:
    if isinstance(node, ast.FunctionDef) and node.name in helpers:
        exec(compile(ast.Module(body=[node], type_ignores=[]), '<modeling primitives>', 'exec'))

if SMOOTH:
    def volume(name, rings, mat, bone, sides=24):
        # Interpolate the silhouette as well as smoothing normals. This turns
        # straight prism limbs into rounded anatomical forms.
        samples=[]
        for index in range(len(rings)-1):
            p0=Vector(rings[max(0,index-1)])
            p1=Vector(rings[index]); p2=Vector(rings[index+1])
            p3=Vector(rings[min(len(rings)-1,index+2)])
            for step in range(6):
                t=step/6
                value=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
                samples.append(value)
        samples.append(Vector(rings[-1]))
        vertices=[]
        for x,y,z,rx,ry in samples:
            for i in range(24):
                a=math.tau*i/24
                vertices.append((x+rx*math.cos(a),y+ry*math.sin(a),z))
        faces=[tuple(reversed(range(24)))]
        for row in range(len(samples)-1):
            for i in range(24):
                a=row*24+i; b=row*24+(i+1)%24
                faces.append((a,b,b+24,a+24))
        faces.append(tuple(range((len(samples)-1)*24,len(samples)*24)))
        obj=mesh(name,vertices,faces,mat,bone,bevel=.001)
        for polygon in obj.data.polygons:
            polygon.use_smooth=len(polygon.vertices)==4
        return obj
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

slate = material('Veil_Slate', (.045,.052,.063), .12, .78)
dark = material('Veil_Joints', (.014,.017,.022), 0, .95)
burgundy = material('Veil_Burgundy', (.17,.037,.032), 0, .86)
if REFINED:
    burgundy.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.115,.025,.023,1)
if LONG_RED_CAPE and not CAPE_CLEARANCE:
    burgundy.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.28,.014,.021,1)
ivory = material('Veil_IvoryMask', (.66,.59,.46), .03, .64)
gold = material('Veil_AgedBronze', (.32,.22,.105), .5, .55)
eye = material('Veil_EyeVoid', (.001,.002,.003), 0, 1)

# Slender clothed torso, fitted hips and diamond clasp.
volume('Torso', [(0,0,1.05,.13,.085),(0,0,1.2,.125,.081),(0,0,1.43,.172,.092),(0,0,1.55,.18,.083)],slate,None,8)
volume('Hips', [(0,0,.97,.16,.084),(0,0,1.035,.184,.097),(0,0,1.09,.148,.09),(0,0,1.12,.139,.088)]
    if REFINED else [(0,0,.98,.132,.085),(0,0,1.09,.143,.09)],slate,None,8)
volume('Belt', [(0,0,1.07,.147,.095),(0,0,1.12,.147,.095)],gold,None,8)
panel('BeltCenter',[(-.044,-.106,1.118),(0,-.125,1.142),(.044,-.106,1.118),(0,-.127,1.07)],gold,None,depth=.014)
panel('ChestDiamond',[(0,-.12,1.49),(-.044,-.122,1.42),(0,-.144,1.35),(.044,-.122,1.42)],gold,None,depth=.019)
volume('Neck',[(0,0,1.51,.073,.057),(0,0,1.71,.072,.055)],dark,None,8)

for side,sign in [('Left',-1),('Right',1)]:
    s=sign
    volume(side+'Thigh',[(s*.13,0,.62,.058,.062),(s*.113,0,.97,.081,.072)],slate,None,8)
    volume(side+'KneeJoint',[(s*.134,0,.55,.044,.048),(s*.13,0,.63,.052,.053)],dark,None,8)
    volume(side+'Shin',[(s*.15,0,.155,.031,.039),(s*.141,0,.4,.042,.048),(s*.133,0,.56,.052,.054)],slate,None,8)
    panel(side+'KneeCap',[(s*.134-.032,-.055,.62),(s*.134+.032,-.055,.62),(s*.134+.04,-.063,.578),(s*.134,-.076,.546),(s*.134-.04,-.063,.578)],gold,None)
    volume(side+'AnkleCuff',[(s*.15,0,.14,.043,.049),(s*.15,0,.19,.043,.049)],gold,None,8)
    volume(side+'Boot',[(s*.15,-.06,.013,.049,.11),(s*.15,-.064,.05,.049,.108),(s*.15,-.01,.145,.034,.05)],slate,None,8)
    volume(side+'UpperArm',[(s*.285,0,1.21,.044,.046),(s*.215,0,1.45,.057,.062),(s*.196,0,1.51,.056,.062)],slate,None,8)
    volume(side+'ElbowJoint',[(s*.287,0,1.18,.036,.04),(s*.284,0,1.23,.04,.042)],dark,None,8)
    volume(side+'Forearm',[(s*.346,-.007,.99,.031,.035),(s*.326,0,1.1,.036,.041),(s*.288,0,1.18,.044,.047)],slate,None,8)
    volume(side+'WristCuff',[(s*.346,-.007,.965,.038,.04),(s*.346,-.007,1.013,.038,.04)],gold,None,8)
    volume(side+'Mitten',[(s*.352,-.011,.855,.021,.026),(s*.355,-.021,.90,.04,.032),(s*.346,-.007,.974,.032,.029)],slate,None,8)
    volume(side+'Thumb',[(s*.314,-.046,.912,.015,.018),(s*.315,-.025,.951,.018,.021)],slate,None,6)
    panel(side+'HipFlap',[(s*.065,-.084,1.059),(s*.165,-.054,1.05),(s*.184,-.099,.812),(s*.128,-.104,.796),(s*.09,-.097,.824)],slate,None,depth=.012)

# Cape shoulders wrap around the arms with faceted triangular silhouette.
for s in [-1,1]:
    verts=[(s*.05,-.117,1.57),(s*.25,-.11,1.56),(s*.30,-.105,1.40),
        (s*.317,-.105,1.315),(s*.25,-.12,1.34),(s*.14,-.125,1.43),(s*.06,-.124,1.52),
        (s*.05,.13,1.56),(s*.25,.125,1.54),(s*.30,.115,1.33),(s*.22,.15,1.34)]
    faces=[(0,1,5,6),(1,2,4,5),(2,3,4),(0,7,8,1),(1,8,9,3,2),(7,10,9,8),(3,9,10,4),(4,10,7,0,6,5)]
    mesh('CapeShoulder'+str(s),verts,faces,burgundy,None,bevel=.002)
for i in range(3):
    verts=[]; cols=5; rows=8
    for r in range(rows):
        t=r/(rows-1)
        for c in range(cols):
            u=c/(cols-1)
            x=-.195+i*.13+.137*u+(u-.5)*.035*t
            y=.10+.065*t+.011*math.sin(u*math.tau+i)*t
            z=1.565-.37*t
            if r==rows-1: z-=.025*(c%2)+.015*math.cos(c*2.7+i)
            verts.append((x,y,z))
    faces=[]
    for r in range(rows-1):
        for c in range(cols-1):
            a=r*cols+c; faces.append((a,a+1,a+cols+1,a+cols))
    obj=mesh('CapeBack'+str(i),verts,faces,burgundy,None,bevel=0)
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Cape cloth thickness','SOLIDIFY'); mod.thickness=.007
    bpy.ops.object.modifier_apply(modifier=mod.name)

# Hood is an actual hollow shell, with an inset dark opening and peaked crown.
outline=[(-.16,1.595),(-.225,1.76),(-.205,1.955),(-.105,2.075),(0,2.103),
    (.105,2.075),(.205,1.955),(.225,1.76),(.16,1.595)]
verts=[]
for ring in range(3):
    for x,z in outline:
        if ring==0: verts.append((x,-.115,z))
        elif ring==1: verts.append((x*.91,.13,z-.022))
        else: verts.append((x*.80,-.13,1.835+(z-1.835)*.86))
n=len(outline); faces=[]
for i in range(n-1):
    faces.append((i,i+1,n+i+1,n+i))
    faces.append((i,2*n+i,2*n+i+1,i+1))
verts.append((0,.163,1.85))
for i in range(n-1): faces.append((n+i,n+i+1,3*n))
faces += [(0,n,3*n,2*n),(n-1,2*n-1,3*n,3*n-1),(0,2*n,n*3-1,n-1)]
hood=mesh('HoodShell',verts,faces,burgundy,None,bevel=.003)
volume('HeadShadow',[(0,.01,1.63,.067,.06),(0,.005,1.83,.118,.09),(0,.007,1.99,.072,.068)],eye,None,10)

# A pointed ivory mask with broad angular cheek planes and recessed-looking eyes.
verts=[(0,-.175,2.018),(-.10,-.172,1.967),(-.119,-.174,1.878),(-.088,-.166,1.779),
    (0,-.168,1.648),(.088,-.166,1.779),(.119,-.174,1.878),(.10,-.172,1.967),
    (0,-.211,1.91),(0,-.219,1.827)]
mask=mesh('IvoryMask',verts,[(0,1,8),(1,2,9,8),(2,3,4,9),(4,5,6,9),(6,7,8,9),(7,0,8),(8,9,4)],ivory,None,bevel=.0015)
for s in [-1,1]:
    mesh('MaskEye'+str(s),[(s*.018,-.224,1.888),(s*.075,-.202,1.915),(s*.065,-.205,1.873),(s*.025,-.221,1.858)],[(0,1,2,3)],eye,None,bevel=0)

if SMOOTH:
    # Replace the faceted hood/cape shells with actual curved surfaces.
    replace=[obj for obj in parts if obj.name.startswith(('HoodShell','CapeShoulder','CapeBack'))]
    for obj in replace:
        parts.remove(obj)
        bpy.data.objects.remove(obj,do_unlink=True)

    count=48; rings=12; vertices=[]; faces=[]
    for row in range(rings):
        t=row/(rings-1)*math.pi*.485
        radius=math.cos(t)
        for col in range(count):
            a=math.tau*col/count
            if REFINED:
                # Smaller hood follows the head, with a soft peaked crown rather
                # than the previous large oval shell.
                upper=max(0,math.sin(a))
                x=.19*radius*math.cos(a)*(1-.2*upper)
                z=1.844+radius*(.231*math.sin(a)+.023*upper**10)
                y=-.11+.235*math.sin(t)+.005*math.sin(a*7)*math.sin(t)*radius*radius
            else:
                x=.222*radius*math.cos(a)
                z=1.837+.278*radius*math.sin(a)
                y=-.115+.295*math.sin(t)
            vertices.append((x,y,z))
    for row in range(rings-1):
        for col in range(count):
            a=row*count+col; b=row*count+(col+1)%count
            faces.append((a,b,b+count,a+count))
    faces.append(tuple(range((rings-1)*count,rings*count)))
    hood=mesh('HoodShell',vertices,faces,burgundy,None,bevel=0)
    bpy.context.view_layer.objects.active=hood
    mod=hood.modifiers.new('Soft cloth thickness','SOLIDIFY'); mod.thickness=.016
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in hood.data.polygons: p.use_smooth=True

    # One continuous mantle drapes across shoulders and falls down the back.
    # Its front opening leaves the diamond clasp and slim torso readable.
    vertices=[]; faces=[]; columns=49; rows=17
    for row in range(rows):
        t=row/(rows-1)
        for col in range(columns):
            a=-math.pi/2 + .36 + (math.tau-.72)*col/(columns-1)
            shoulder=math.sin(min(1,t/.32)*math.pi/2)
            rx=.085+.212*shoulder
            ry=.068+.095*shoulder
            frontness=max(0,-math.sin(a))
            length=.39-.16*frontness
            fold=math.sin(a*7+.5)*.009*t*t
            x=(rx+fold)*math.cos(a)
            y=(ry+fold)*math.sin(a)
            z=1.588-length*t+.007*math.sin(a*9)*t**8
            vertices.append((x,y,z))
    for row in range(rows-1):
        for col in range(columns-1):
            a=row*columns+col
            faces.append((a,a+1,a+columns+1,a+columns))
    mantle=mesh('ContinuousSoftCape',vertices,faces,burgundy,None,bevel=0)
    bpy.context.view_layer.objects.active=mantle
    mod=mantle.modifiers.new('Fine cloak thickness','SOLIDIFY'); mod.thickness=.009
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in mantle.data.polygons: p.use_smooth=True
    # Round cloth skirt corners; keep the metal clasps crisp and readable.
    for obj in parts:
        if obj.name.endswith('HipFlap'):
            bpy.context.view_layer.objects.active=obj
            mod=obj.modifiers.new('Rounded cloth corners','SUBSURF'); mod.levels=2
            bpy.ops.object.modifier_apply(modifier=mod.name)
            for p in obj.data.polygons: p.use_smooth=True
    for p in mask.data.polygons: p.use_smooth=True

if REFINED:
    # Replace segmented trousers/shins with one shaped leg on each side. The
    # knee has an anatomical narrowing; the calf expands then tapers to ankle.
    obsolete=[obj for obj in parts if obj.name.endswith(
        ('Thigh','Shin','KneeJoint','KneeCap','AnkleCuff','Boot','HipFlap'))
        or obj.name.startswith(('IvoryMask','MaskEye'))]
    for obj in obsolete:
        parts.remove(obj)
        bpy.data.objects.remove(obj,do_unlink=True)
    for side,s in [('Left',-1),('Right',1)]:
        volume(side+'ContinuousLeg',[
            (s*.15,.003,.13,.029,.035),
            (s*.146,.007,.23,.034,.041),
            (s*.14,.017,.37,.05,.059),
            (s*.13,-.005,.52,.042,.049),
            (s*.127,-.014,.59,.047,.053),
            (s*.12,-.003,.71,.064,.067),
            (s*.111,0,.86,.075,.072),
            (s*.104,0,1.045,.073,.077),
            (s*.09,0,1.105,.058,.075)],slate,None,24)
        volume(side+'Boot',[
            (s*.15,-.05,.012,.05,.113),
            (s*.15,-.063,.045,.051,.12),
            (s*.15,-.026,.09,.043,.083),
            (s*.15,0,.155,.032,.038),
            (s*.15,0,.205,.035,.041)],slate,None,24)
        # A narrow cuff provides a boot edge without dividing the leg into blocks.
        volume(side+'BootTrim',[(s*.15,0,.198,.036,.042),(s*.15,0,.213,.036,.042)],gold,None,24)

    # Weld the visible trouser silhouette so the hips and thighs read as one
    # garment. Skinning will be added to this continuous mesh after approval.
    trousers=[obj for obj in parts if obj.name in ('Hips','LeftContinuousLeg','RightContinuousLeg')]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in trousers:
        obj.select_set(True)
        parts.remove(obj)
    bpy.context.view_layer.objects.active=trousers[0]
    bpy.ops.object.join()
    trousers_mesh=bpy.context.object
    trousers_mesh.name='ContinuousTrousers'
    mod=trousers_mesh.modifiers.new('Weld hip and leg surfaces','REMESH')
    mod.mode='VOXEL'; mod.voxel_size=.004; mod.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=trousers_mesh.modifiers.new('Soften trouser joins','SMOOTH')
    mod.factor=.65; mod.iterations=5
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in trousers_mesh.data.polygons: p.use_smooth=True
    parts.append(trousers_mesh)
    trousers_mesh.select_set(False)

    outline=[(0,1.992),(-.071,1.967),(-.094,1.902),(-.084,1.831),
        (-.052,1.768),(0,1.706),(.052,1.768),(.084,1.831),(.094,1.902),(.071,1.967)]
    vertices=[]; rings=5; faces=[]; count=len(outline)
    for row in range(rings):
        r=1-row/rings
        for x,z in outline:
            vertices.append((x*r,-.17-.037*(1-r*r),1.866+(z-1.866)*r))
    for row in range(rings-1):
        for col in range(count):
            a=row*count+col; b=row*count+(col+1)%count
            faces.append((a,b,b+count,a+count))
    vertices.append((0,-.208,1.866))
    for col in range(count):
        faces.append(((rings-1)*count+col,(rings-1)*count+(col+1)%count,len(vertices)-1))
    mask=mesh('IvoryMask',vertices,faces,ivory,None,bevel=0)
    for p in mask.data.polygons: p.use_smooth=True
    for s in [-1,1]:
        eye_obj=mesh('MaskEye'+str(s),[
            (s*.014,-.213,1.884),(s*.068,-.19,1.912),
            (s*.058,-.198,1.882),(s*.021,-.212,1.865)],[(0,1,2,3)],eye,None,bevel=0)
    # More visible but soft drape folds in the back of the mantle.
    for vertex in mantle.data.vertices:
        if vertex.co.y>.04:
            t=max(0,min(1,(1.57-vertex.co.z)/.38))
            vertex.co.y+=.012*math.sin(vertex.co.x*45)*t*t
    mantle.data.update()

if FACE_REFINED:
    # Build a dense curved surface with a smoothly interpolated silhouette;
    # the eye openings are cut into the mesh, rather than painted-on polygons.
    for obj in list(parts):
        if obj.name.startswith(('IvoryMask','MaskEye','Torso')):
            parts.remove(obj)
            bpy.data.objects.remove(obj,do_unlink=True)
    volume('Torso',[(0,.006,1.065,.135,.084),(0,.006,1.18,.115,.079),
        (0,.005,1.29,.131,.092),(0,0,1.41,.162,.099),
        (0,0,1.51,.177,.09),(0,0,1.56,.146,.072)],slate,None,24)

    controls=[(0,1.988),(-.052,1.972),(-.086,1.933),(-.093,1.877),
        (-.081,1.825),(-.045,1.759),(0,1.711),(.045,1.759),
        (.081,1.825),(.093,1.877),(.086,1.933),(.052,1.972)]
    boundary=[]
    for i in range(len(controls)):
        p0=Vector(controls[(i-1)%len(controls)])
        p1=Vector(controls[i]); p2=Vector(controls[(i+1)%len(controls)])
        p3=Vector(controls[(i+2)%len(controls)])
        for step in range(6):
            t=step/6
            boundary.append(.5*(2*p1+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
    count=len(boundary); rings=16; vertices=[]; faces=[]
    for row in range(rings):
        r=1-row/rings
        for x,z in boundary:
            xx=x*r; zz=1.86+(z-1.86)*r
            # Gentle convex forehead and cheeks with a soft vertical crest.
            ridge=.006*math.exp(-(xx/.024)**2)*(1-r*r)
            yy=-.164-.041*(1-r*r)-ridge
            vertices.append((xx,yy,zz))
    for row in range(rings-1):
        for col in range(count):
            a=row*count+col; b=row*count+(col+1)%count
            faces.append((a,b,b+count,a+count))
    vertices.append((0,-.211,1.86))
    for col in range(count):
        faces.append(((rings-1)*count+col,(rings-1)*count+(col+1)%count,len(vertices)-1))
    mask=mesh('IvoryMask',vertices,faces,ivory,None,bevel=0)
    bm=bmesh.new(); bm.from_mesh(mask.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mask.data); bm.free()
    lining=mask.copy(); lining.data=mask.data.copy()
    lining.name='MaskEyeLining'; lining.location.y=.024
    lining.data.materials.clear(); lining.data.materials.append(eye)
    bpy.context.collection.objects.link(lining); parts.append(lining)
    bpy.context.view_layer.objects.active=mask
    mod=mask.modifiers.new('Carved mask shell','SOLIDIFY'); mod.thickness=.01; mod.offset=-1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for s in [-1,1]:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=1,
            location=(s*.042,-.185,1.886))
        cutter=bpy.context.object; cutter.name='TemporaryEyeCutter'
        cutter.scale=(.028,.085,.0125)
        cutter.rotation_euler.y=math.radians(-s*18)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        bpy.context.view_layer.objects.active=mask
        mod=mask.modifiers.new('Recessed eye opening','BOOLEAN'); mod.operation='DIFFERENCE'; mod.solver='EXACT'; mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter,do_unlink=True)
    bpy.context.view_layer.objects.active=mask
    mod=mask.modifiers.new('Soft carved eye rims','BEVEL'); mod.width=.0015; mod.segments=3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in mask.data.polygons: p.use_smooth=True
    for p in lining.data.polygons: p.use_smooth=True
    for vertex in mantle.data.vertices:
        t=max(0,min(1,(1.57-vertex.co.z)/.38))
        if vertex.co.y>.04:
            vertex.co.y+=.008*math.sin(vertex.co.x*35+.4)*t*t
        vertex.co.z-=.008*math.cos(vertex.co.x*17)*t**4
    mantle.data.update()

# Lengthen the existing smooth cloth while retaining the shoulder shape.
if LONG_RED_CAPE:
    for vertex in mantle.data.vertices:
        t=max(0,min(1,(1.575-vertex.co.z)/.39))
        # The back falls to the upper thighs; front wings remain shorter so
        # the belt and hands stay visible in the isometric gameplay view.
        backness=max(0,min(1,(vertex.co.y+.035)/.17))
        vertex.co.z-=(.12+.24*backness)*t*t
        vertex.co.y+=.024*backness*t*t
    mantle.data.update()

if CAPE_CLEARANCE:
    # Lift the side hems above the upper arms, leaving the longer back panel
    # behind the body. This gives the elbows and wrists clear working space.
    for vertex in mantle.data.vertices:
        x,y,z=vertex.co
        t=max(0,min(1,(1.575-z)/.48))
        side=math.exp(-(y/.08)**2)*min(1,(abs(x)/.285)**4)
        vertex.co.z+=.42*side*t*t
        vertex.co.x+=math.copysign(.025*side*t,x)
    mantle.data.update()
    # Check the actual preview surfaces rather than relying on the render.
    bpy.context.view_layer.objects.active=mantle
    mod=mantle.modifiers.new('Soften raised arm openings','SMOOTH')
    mod.factor=.6; mod.iterations=6
    bpy.ops.object.modifier_apply(modifier=mod.name)
    if KNEE_CAPE:
        # Keep the shoulder openings short; only the back curtain descends to
        # just below knee height, like a long heroic cloak.
        for vertex in mantle.data.vertices:
            t=max(0,min(1,(1.575-vertex.co.z)/.67))
            backness=max(0,min(1,(vertex.co.y-.015)/.12))
            vertex.co.z-=.37*backness*t*t
            vertex.co.y+=.035*backness*t*t
        mantle.data.update()
    bpy.context.view_layer.update()
    depsgraph=bpy.context.evaluated_depsgraph_get()
    cape_bvh=BVHTree.FromObject(mantle,depsgraph)
    for obj in parts:
        if obj.name.endswith(('UpperArm','Forearm','ElbowJoint','WristCuff','Mitten')):
            overlaps=cape_bvh.overlap(BVHTree.FromObject(obj,depsgraph))
            print('CAPE_ARM_CHECK '+obj.name+'='+str(len(overlaps)))

if KNEE_CAPE:
    for obj in list(parts):
        if obj.name in ('HeadShadow','Neck'):
            parts.remove(obj)
            bpy.data.objects.remove(obj,do_unlink=True)
    volume('ShapedNeck',[(0,.018,1.52,.044,.044),(0,.015,1.6,.04,.047),
        (0,.009,1.68,.037,.044)],dark,None,24)
    # Closed, smoothly shaped head: tapered chin and jaw, fuller cheeks and
    # temples, then a rounded cranial dome instead of a flat cylindrical cap.
    volume('HeadShadow',[(0,-.001,1.644,.027,.033),(0,-.007,1.68,.044,.048),
        (0,-.01,1.73,.061,.057),(0,-.014,1.80,.075,.072),
        (0,-.013,1.87,.087,.081),(0,-.006,1.94,.08,.081),
        (0,.001,1.989,.052,.057),(0,.003,2.008,.007,.009)],dark,None,32)
    for side,s in [('Left',-1),('Right',1)]:
        volume(side+'CheekStructure',[(s*.045,-.067,1.74,.013,.014),
            (s*.051,-.071,1.79,.023,.023),(s*.048,-.073,1.84,.024,.025),
            (s*.043,-.068,1.875,.012,.014)],dark,None,24)
    volume('NoseBridge',[(0,-.09,1.80,.014,.019),(0,-.10,1.84,.018,.028),
        (0,-.092,1.90,.014,.021),(0,-.077,1.94,.008,.009)],dark,None,24)

if MASK_SHAPED_FACE:
    for obj in list(parts):
        if obj.name in ('HeadShadow','LeftCheekStructure','RightCheekStructure','NoseBridge'):
            parts.remove(obj)
            bpy.data.objects.remove(obj,do_unlink=True)
    # The face underneath follows the approved mask outline, enlarged by 17%.
    # A continuous convex front and rounded back keep it a head, not a flat rim.
    face_boundary=[Vector((point.x*1.17,1.86+(point.y-1.86)*1.17)) for point in boundary]
    count=len(face_boundary); rows=14; vertices=[]; faces=[]
    for side in range(2):
        for row in range(rows):
            r=1-row/rows
            for x,z in face_boundary:
                y=(-.127-.025*(1-r*r)) if side==0 else (.007+.06*(1-r*r))
                vertices.append((x*r,y,1.86+(z-1.86)*r))
    front_center=len(vertices); vertices.append((0,-.152,1.86))
    back_center=len(vertices); vertices.append((0,.067,1.86))
    surface_size=rows*count
    for side in range(2):
        offset=side*surface_size
        for row in range(rows-1):
            for col in range(count):
                a=offset+row*count+col; b=offset+row*count+(col+1)%count
                faces.append((a,b,b+count,a+count))
        center=front_center if side==0 else back_center
        for col in range(count):
            a=offset+(rows-1)*count+col
            b=offset+(rows-1)*count+(col+1)%count
            faces.append((a,b,center))
    for col in range(count):
        next_col=(col+1)%count
        faces.append((col,surface_size+col,surface_size+next_col,next_col))
    head=mesh('MaskShapedFace',vertices,faces,dark,None,bevel=0)
    bm=bmesh.new(); bm.from_mesh(head.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(head.data); bm.free()
    for p in head.data.polygons: p.use_smooth=True
    print('MASK_BACKING_FACE: curved closed head, 17% larger matching silhouette, behind ivory mask')

if HOOD_COLLAR:
    # A draped cowl closes the rear neck opening, joining the hood visually to
    # the mantle. The front remains lower so the mask is not obscured.
    vertices=[]; faces=[]; columns=64; rows=16
    for row in range(rows):
        t=row/(rows-1)
        for col in range(columns):
            a=math.tau*col/columns
            backness=(math.sin(a)+1)/2
            x_radius=.059+.058*t
            y_radius=.057+.061*t
            fold=.003*math.sin(a*5+.3)*math.sin(math.pi*t)
            x=(x_radius+fold)*math.cos(a)
            y=.015+(y_radius+fold)*math.sin(a)
            top=1.685+.085*backness
            z=top*(1-t)+1.565*t
            vertices.append((x,y,z))
    for row in range(rows-1):
        for col in range(columns):
            a=row*columns+col; b=row*columns+(col+1)%columns
            faces.append((a,b,b+columns,a+columns))
    collar=mesh('HoodDrapedCollar',vertices,faces,burgundy,None,bevel=0)
    bpy.context.view_layer.objects.active=collar
    mod=collar.modifiers.new('Soft cowl thickness','SOLIDIFY'); mod.thickness=.011
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in collar.data.polygons: p.use_smooth=True
    # Let the rear hood fabric drape down toward the collar instead of ending
    # in a floating rounded rim above the visible neck.
    for vertex in hood.data.vertices:
        backness=max(0,min(1,(vertex.co.y+.03)/.14))
        low=max(0,min(1,(1.845-vertex.co.z)/.23))
        vertex.co.z-=.055*backness*low*low
    hood.data.update()

# Save editable preview geometry; the approved production rig will follow later.
character_parts=list(parts)
front=bpy.data.objects.new('VeilWalker_Preview_Front',None)
bpy.context.collection.objects.link(front)
for obj in character_parts: obj.parent=front
front.location=(-.58,0,0)
front.rotation_euler.z=math.radians(-8)
rear=bpy.data.objects.new('VeilWalker_Preview_Back',None)
bpy.context.collection.objects.link(rear)
rear.location=(.40,.06,0)
rear.rotation_euler.z=math.radians(166)
for obj in character_parts:
    copy=obj.copy(); copy.data=obj.data.copy(); copy.parent=rear
    bpy.context.collection.objects.link(copy)

# Separate hood and mask are displayed to make the construction reviewable.
detail=bpy.data.objects.new('SeparateHoodAndMask',None)
bpy.context.collection.objects.link(detail)
detail.location=(1.14,0,-.27)
detail.rotation_euler.z=math.radians(-8)
for obj in [hood,mask]+[o for o in character_parts if o.name.startswith('MaskEye')]:
    copy=obj.copy(); copy.data=obj.data.copy(); copy.parent=detail
    if obj==mask or obj.name.startswith('MaskEye'): copy.location.y-=.28
    bpy.context.collection.objects.link(copy)

floor=material('PreviewFloor',(.22,.20,.18),0,.92)
box('StudioGround',(0,0,-.032),(200,200,.04),floor)
world=bpy.context.scene.world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.17,.16,.15,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
for name,loc,energy,color,size in [
    ('CoolKey',(-3,-4,5),430,(.87,.93,1),4),
    ('WarmFill',(3,-3,3),220,(1,.88,.76),4),
    ('Rim',(0,3,4),420,(1,.83,.72),3)]:
    data=bpy.data.lights.new(name,'AREA'); data.energy=energy; data.color=color; data.size=size
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj); obj.location=loc; aim(obj,(0,0,1.1))
data=bpy.data.cameras.new('ActualPreviewCamera'); data.type='ORTHO'; data.ortho_scale=3.7
camera=bpy.data.objects.new('ActualPreviewCamera',data); bpy.context.collection.objects.link(camera)
camera.location=(2.2,-8,2.85); aim(camera,(.20,0,1.07))
scene=bpy.context.scene; scene.camera=camera
scene.render.engine='BLENDER_EEVEE'; scene.eevee.taa_render_samples=32
if '--cpu-render' in sys.argv:
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=16
    scene.cycles.use_denoising=True
scene.render.resolution_x=1536; scene.render.resolution_y=1024; scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
scene.render.image_settings.file_format='PNG'
version='09_HoodCollar' if HOOD_COLLAR else ('08_MaskShapedFace' if MASK_SHAPED_FACE else ('07_KneeCapeShapedHead' if KNEE_CAPE else ('06_CapeClearance' if CAPE_CLEARANCE else ('05_LongRedCape' if LONG_RED_CAPE else ('04_CurvedFace' if FACE_REFINED else ('03_Refined' if REFINED else ('02_Smooth' if SMOOTH else '01')))))))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART,'VeilWalker_Preview'+version+'.blend'))
scene.render.filepath=os.path.join(ART,'Previews','VeilWalker_ActualPreview'+version+'.png')
bpy.ops.render.render(write_still=True)
print('ACTUAL_BLENDER_PREVIEW='+scene.render.filepath)
print('CHARACTER_PARTS='+str(len(character_parts)))
print('PREVIEW_ONLY: no FBX export, rig, or Unity scene changes')
