"""Export a character GLB as a T-pose FBX for Mixamo auto-rigging.

Usage:
  blender -b --factory-startup -P Tools/blender_export_mixamo.py -- <in.glb> <basecolor.png|-> <out.fbx> [height=1.8] [tris=0] [nobake] [seams]

basecolor "-": use the GLB's embedded base-color image (written next to out.fbx as <Name>_BaseColor.png, max 1024).
tris > 0     : weld + collapse-decimate to about that many triangles, then (DEFAULT) weld again, re-unwrap the
               low-poly mesh and bake the original high-poly base colour onto it (Cycles, selected-to-active,
               1024 diffuse colour). This is the method that works in Mixamo: one connected mesh, no UV drift.
nobake       : keep the original UVs instead of baking (only if the decimated texture still lines up).
keepuv       : no bake: weld (UVs are per corner, so they survive), collapse-decimate and keep the ORIGINAL UVs +
               the 2048 texture. Sharp texture, one connected mesh for Mixamo. Best quality for detailed generated
               models (bosses 2026-11-19: 95–133k → 30k tris, visually identical). Locking UV-seam vertices with a
               vertex group was tried: any weight > 0 freezes them and the collapse stalls at ~70%.
seams        : with nobake, cut UV seams into mesh boundaries before collapsing. NOT for Mixamo: the mesh falls
               apart into islands along the seams and the auto-rigger shows torn shapes / shifted texture.
Always: single mesh, transforms applied, normals recalculated outside, loose geometry removed.

Result: single mesh, origin at the feet (bottom centre), given height, transforms applied,
base-color texture embedded (path_mode=COPY + embed_textures).
"""
import os
import sys

import bmesh
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, tex_path, dst = argv[0], argv[1], os.path.abspath(argv[2])
height = float(argv[3]) if len(argv) > 3 else 1.8
target_tris = int(argv[4]) if len(argv) > 4 else 0
flags = set(argv[5:])
keep_uv = "keepuv" in flags
do_bake = target_tris > 0 and "nobake" not in flags and "seams" not in flags and not keep_uv
cut_seams = "seams" in flags and not do_bake
TEX_SIZE = 2048 if keep_uv else 1024


def tris_of(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def bounds(o):
    cs = [o.matrix_world @ v.co for v in o.data.vertices]
    return (Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs))),
            Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs))))


def base_color_image(o):
    for m in o.data.materials:
        if not (m and m.use_nodes):
            continue
        b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if not (b and b.inputs['Base Color'].is_linked):
            continue
        n = b.inputs['Base Color'].links[0].from_node
        while n.type != 'TEX_IMAGE' and any(i.is_linked for i in n.inputs):
            n = next(i for i in n.inputs if i.is_linked).links[0].from_node
        if n.type == 'TEX_IMAGE':
            return n.image
    return None


def select_only(*objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[-1]


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
select_only(*meshes)
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = bpy.path.display_name_from_filepath(dst)

# Drop the glTF hierarchy (empties) and bake transforms into the mesh.
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Scale to target height, origin at bottom centre, placed at world origin.
mn, mx = bounds(obj)
s = height / (mx.z - mn.z)
obj.scale = (s, s, s)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
mn, mx = bounds(obj)
bpy.context.scene.cursor.location = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
obj.location = (0, 0, 0)

src_img = base_color_image(obj)
if tex_path == "-" and src_img is None:
    raise RuntimeError("no embedded base-color image")
src_tris = tris_of(obj)

high = None
if do_bake:
    high = obj.copy()
    high.data = obj.data.copy()
    high.name = "High"
    bpy.context.scene.collection.objects.link(high)

# Decimation. Generated meshes come with split vertices and non-manifold fans (edges shared by
# 3+ faces) that block edge collapse: weld, cut the fans apart, then collapse.
if target_tris > 0 and src_tris > target_tris:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6 * height)
    bmesh.ops.split_edges(bm, edges=[e for e in bm.edges if len(e.link_faces) > 2])
    if cut_seams:
        # Hard-cut UV seams so the collapse never interpolates UVs across them (boundaries are kept).
        uv = bm.loops.layers.uv.active
        eps = 1e-5
        seam = []
        for e in bm.edges:
            if len(e.link_loops) == 2:
                l1, l2 = e.link_loops
                if ((l1[uv].uv - l2.link_loop_next[uv].uv).length > eps or
                        (l1.link_loop_next[uv].uv - l2[uv].uv).length > eps):
                    seam.append(e)
        bmesh.ops.split_edges(bm, edges=seam)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    mod = obj.modifiers.new("Decimate", 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = target_tris / tris_of(obj)
    mod.use_collapse_triangulate = True
    select_only(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def clean_mesh(o, weld):
    """Weld (when the UVs are going to be replaced anyway), drop loose bits, normals outside."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    if weld:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4 * height)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()


clean_mesh(obj, weld=do_bake or keep_uv)
if high is not None:
    clean_mesh(high, weld=False)

base = obj.name + "_BaseColor.png"
if do_bake:
    # Fresh UVs on the low-poly mesh, then bake the high-poly base colour onto them.
    for uv in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(uv)
    obj.data.uv_layers.new(name="UVMap")
    select_only(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004, area_weight=0.0)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')

    baked = bpy.data.images.new(obj.name + "_Baked", TEX_SIZE, TEX_SIZE, alpha=False)
    bake_mat = bpy.data.materials.new("BakeTarget")
    bake_mat.use_nodes = True
    node = bake_mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = baked
    bake_mat.node_tree.nodes.active = node
    obj.data.materials.clear()
    obj.data.materials.append(bake_mat)

    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 4
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.use_cage = False
    sc.render.bake.cage_extrusion = 0.02 * height
    sc.render.bake.max_ray_distance = 0.06 * height
    sc.render.bake.margin = 8
    select_only(high, obj)
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
    tex_path = os.path.join(os.path.dirname(dst), base)
    baked.filepath_raw = tex_path
    baked.file_format = 'PNG'
    baked.save()
    bpy.data.objects.remove(high, do_unlink=True)
    print("MIXAMO: baked base color ->", tex_path)
elif tex_path == "-":
    img = src_img.copy()
    w, h = img.size
    if max(w, h) > TEX_SIZE:
        img.scale(w * TEX_SIZE // max(w, h), h * TEX_SIZE // max(w, h))
    tex_path = os.path.join(os.path.dirname(dst), base)
    img.filepath_raw = tex_path
    img.file_format = 'PNG'
    img.save()
    print("MIXAMO: base color ->", tex_path, tuple(img.size))

# Material with the base-color texture.
img = bpy.data.images.load(tex_path, check_existing=False)
mat = bpy.data.materials.new(obj.name + "_Mat")
mat.use_nodes = True
nt = mat.node_tree
bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
tex = nt.nodes.new('ShaderNodeTexImage')
tex.image = img
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = 1.0
obj.data.materials.clear()
obj.data.materials.append(mat)

dims = obj.dimensions
print(f"MIXAMO: {obj.name} dims=({dims.x:.2f},{dims.y:.2f},{dims.z:.2f}) tris {src_tris} -> {tris_of(obj)}")

select_only(obj)
bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=False,
    object_types={'MESH'},
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    path_mode='COPY',
    embed_textures=True,
    mesh_smooth_type='FACE',
    add_leaf_bones=False,
)
print("MIXAMO: exported", dst)
