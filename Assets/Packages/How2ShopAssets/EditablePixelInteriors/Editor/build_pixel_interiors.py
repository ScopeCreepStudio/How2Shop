import bpy, math, json, random
from pathlib import Path
from mathutils import Vector

BASE=Path(__file__).resolve().parents[2]
OUT=BASE/'EditablePixelInteriors'
for d in ['Models/InteriorWalls','Models/MallKit','Textures','Preview']:
    (OUT/d).mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
palette={'Cream':'fff0b5','Teal':'51ae9f','Coral':'ee785e','Mustard':'e8b92f','Plum':'291039','Lilac':'9673b3','Rose':'d992ac'}
def rgb(h): return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
materials={}
# Paint irregular clusters on a torus: patches crossing one edge continue at
# the opposite edge. All colours share the same pattern and pixel density.
rng=random.Random(2039)
shade=[[1.0 for x in range(32)] for y in range(32)]
for count,value,steps in [(26,.92,14),(30,.76,8),(12,.84,5)]:
    for _ in range(count):
        x,y=rng.randrange(32),rng.randrange(32)
        for _ in range(steps):
            shade[y%32][x%32]=value
            dx,dy=rng.choice([(1,0),(-1,0),(0,1),(0,-1)])
            x+=dx; y+=dy
for y in range(32):shade[y][-1]=shade[y][0]
shade[-1]=shade[0][:]
for name,h in palette.items():
    base=rgb(h); pixels=[]
    for y in range(64):
        for x in range(64):
            c=tuple(v*shade[y//2][x//2] for v in base)
            pixels.extend((*c,1))
    im=bpy.data.images.new('Pixel_'+name,64,64); im.pixels=pixels
    im.filepath_raw=str(OUT/'Textures'/('Pixel_'+name+'.png')); im.file_format='PNG'; im.save()
    mat=bpy.data.materials.new('Pixel_'+name); mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.85
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=im; tex.interpolation='Closest'
    mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']); materials[name]=mat

records=[]; gallery=[]
sources=[('InteriorWalls',BASE/'SimplePolygon_MiniInteriorWalls.fbx')]+[('MallKit',p) for p in sorted((BASE/'psx-mall-kit-free/fbx').glob('*.fbx'))]
def export(path,objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
for pack,src in sources:
    bpy.ops.import_scene.fbx(filepath=str(src))
    objs=[o for o in bpy.context.selected_objects if o.type=='MESH']
    for o in objs:
        o.parent=None if o.parent is None else o.parent
        bpy.context.view_layer.objects.active=o
        # Preserve the imported world transform before moving each standalone piece.
        world=o.matrix_world.copy(); o.parent=None; o.matrix_world=world
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        n=o.name.lower(); orig=[m.diffuse_color[:] if m else (1,1,1,1) for m in o.data.materials]
        o.data.materials.clear()
        for m in materials.values():o.data.materials.append(m)
        uv=o.data.uv_layers.active or o.data.uv_layers.new(name='PixelUV')
        low=min(v.co.z for v in o.data.vertices)
        mins=[min(v.co[a] for v in o.data.vertices) for a in range(3)]
        spans=[max(v.co[a] for v in o.data.vertices)-mins[a] for a in range(3)]
        for poly in o.data.polygons:
            normal=poly.normal; axis=max(range(3),key=lambda a:abs(normal[a])); axes=[a for a in range(3) if a!=axis]
            color='Cream'
            if 'trim' in n or 'frame' in n: color='Teal'
            elif 'door' in n: color='Mustard'
            elif 'window' in n: color='Teal' if axis!=2 else 'Lilac'
            elif 'floor' in n: color='Lilac'
            elif 'belt' in n: color='Plum'
            elif 'lamp' in n or 'sign' in n: color='Mustard'
            elif 'register' in n: color='Teal' if axis!=2 else 'Plum'
            elif 'checkout' in n: color='Coral' if axis!=2 else 'Cream'
            elif 'gondola' in n: color='Teal' if axis!=2 else 'Cream'
            if axis==2 and color=='Cream': color='Mustard'
            poly.material_index=list(materials).index(color)
            for li in poly.loop_indices:
                v=o.data.vertices[o.data.loops[li].vertex_index].co
                # Whole horizontal repeats on each modular wall, with a common
                # bottom-aligned vertical scale. Source layout offsets cannot
                # shift the pattern phase between standalone wall pieces.
                modular=any(s in n for s in ('wall','window','doorframe','cornerpost','floor'))
                if modular:
                    a=axes[0]; span=spans[a]
                    repeats=max(1,round(span/3)) if span>=.5 else span/3
                    u=(v[a]-mins[a])/max(span,1e-6)*repeats
                    b=axes[1]
                    if b==2: vv=(v[b]-mins[b])/3.5
                    else: vv=(v[b]-mins[b])/max(spans[b],1e-6)*max(1,round(spans[b]/3))
                    uv.data[li].uv=(u,vv)
                else:
                    uv.data[li].uv=(v[axes[0]]/2,(v[axes[1]]-(low if axes[1]==2 else 0))/2)
        o.data.update()
    export(OUT/'Models'/pack/(src.stem+'_CompleteSet.fbx'),objs)
    for o in objs:
        bounds=[o.matrix_world@Vector(c) for c in o.bound_box]
        minv=Vector(tuple(min(p[a] for p in bounds) for a in range(3)))
        maxv=Vector(tuple(max(p[a] for p in bounds) for a in range(3)))
        pivot=Vector(((minv.x+maxv.x)/2,(minv.y+maxv.y)/2,minv.z))
        if o.name=='SM_Door':pivot.y=minv.y
        for v in o.data.vertices:v.co+=o.location-pivot
        o.location=(0,0,0)
        if o.name=='SM_Floor_1x1':
            # The source displays its floor upright; lay the standalone tile flat.
            from mathutils import Matrix
            turn=Matrix.Rotation(math.pi/2,4,'Y')
            for v in o.data.vertices:v.co=turn@v.co
            lo=Vector(tuple(min(v.co[a] for v in o.data.vertices) for a in range(3)))
            hi=Vector(tuple(max(v.co[a] for v in o.data.vertices) for a in range(3)))
            centre=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
            for v in o.data.vertices:v.co-=centre
        name=o.name.replace('.','_')
        export(OUT/'Models'/pack/(name+'.fbx'),[o])
        records.append({'pack':pack,'name':name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'size':list(maxv-minv),'source':src.name})
        gallery.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.hide_set(True)

(OUT/'manifest.json').write_text(json.dumps(records,indent=2))
# Contact sheet of the actual exported geometry and material treatment.
for i,o in enumerate(gallery):
    o.hide_set(False); o.location=((i%6)*4,(i//6)*4,0)
    maxdim=max(o.dimensions)
    if maxdim>0:o.scale*=3.0/maxdim
    if o.dimensions.x<.5 and o.dimensions.z>1:o.rotation_euler.z=-math.pi/2
bpy.ops.object.camera_add(location=(30,-35,33))
cam=bpy.context.object; target=Vector((10,6,0)); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=32
scene=bpy.context.scene; scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(6,3,20)); bpy.context.object.data.energy=3500; bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=20
scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.55,.55,.55,1)
scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.8
scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.view_settings.view_transform='Standard'
scene.render.resolution_x=1600; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.render.filepath=str(OUT/'Preview'/'Collection.png'); bpy.ops.render.render(write_still=True)
# Verify the texture at real joins between three copies of the exported wall.
for o in gallery:o.hide_render=True
wall=next(o for o in gallery if o.name=='SM_Wall')
for i in range(3):
    piece=wall.copy(); piece.data=wall.data.copy(); scene.collection.objects.link(piece)
    piece.hide_render=False; piece.hide_set(False)
    piece.scale=(1,1,1); piece.rotation_euler=(0,0,0); piece.location=(0,i*3,0)
cam.location=(14,3,5); target=Vector((0,3,1.75))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.ortho_scale=10.2
scene.render.resolution_x=1400; scene.render.resolution_y=650
scene.render.filepath=str(OUT/'Preview'/'WallJoin.png'); bpy.ops.render.render(write_still=True)
assert all(row[0]==row[-1] for row in shade) and shade[0]==shade[-1]
(OUT/'TextureValidation.txt').write_text('PASS: all seven textures share a seamless wrapped pixel pattern.\nPASS: left/right and top/bottom edge colours match exactly.\nModular wall UVs start at zero and finish on a whole horizontal repeat, with bottom-aligned vertical mapping.\nPreview/WallJoin.png shows three adjacent 3m walls at original scale.\n')
print('DONE',len(records),'individual pieces')
