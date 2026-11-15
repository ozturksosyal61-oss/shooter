"""Export a low-poly character GLB as a T-pose FBX for Mixamo auto-rigging.

Usage:
  blender -b --factory-startup -P Tools/blender_export_mixamo.py -- <in.glb> <basecolor.png|-> <out.fbx> [height=1.8] [tris=0]

basecolor "-": use the GLB's embedded base-color image (saved next to out.fbx as <Name>_BaseColor.png, max 1024).
tris > 0: weld + collapse-decimate the mesh to about that many triangles.

Result: single mesh, origin at the feet (bottom centre), height ~1.8 m, transforms applied,
base-color texture embedded (path_mode=COPY + embed_textures).
"""
import bpy
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, tex_path, dst = argv[0], argv[1], argv[2]
height = float(argv[3]) if len(argv) > 3 else 1.8
target_tris = int(argv[4]) if len(argv) > 4 else 0

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = bpy.path.display_name_from_filepath(dst)

# Embedded base-color texture (taken before the materials are replaced).
if tex_path == "-":
    import os
    src_img = None
    for m in obj.data.materials:
        if m and m.use_nodes:
            b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if b and b.inputs['Base Color'].is_linked:
                n = b.inputs['Base Color'].links[0].from_node
                while n.type != 'TEX_IMAGE' and n.inputs and any(i.is_linked for i in n.inputs):
                    n = next(i for i in n.inputs if i.is_linked).links[0].from_node
                if n.type == 'TEX_IMAGE':
                    src_img = n.image
                    break
    if src_img is None:
        raise RuntimeError("no embedded base-color image")
    w, h = src_img.size
    if max(w, h) > 1024:
        src_img.scale(min(w, 1024 * w // max(w, h)), min(h, 1024 * h // max(w, h)))
    tex_path = os.path.join(os.path.dirname(os.path.abspath(dst)), obj.name + "_BaseColor.png")
    src_img.filepath_raw = tex_path
    src_img.file_format = 'PNG'
    src_img.save()
    print("MIXAMO: base color ->", tex_path, tuple(src_img.size))

# Drop the glTF hierarchy (empties) and bake transforms into the mesh.
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Optional decimation. Generated meshes come with split vertices and non-manifold fans
# (edges shared by 3+ faces) that block edge collapse: weld, cut the fans apart, then decimate.
if target_tris > 0:
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.split_edges(bm, edges=[e for e in bm.edges if len(e.link_faces) > 2])
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    cur = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if cur > target_tris:
        mod = obj.modifiers.new("Decimate", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = target_tris / cur
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)

# Scale to target height, origin at bottom centre, placed at world origin.
coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
mn = Vector((min(c.x for c in coords), min(c.y for c in coords), min(c.z for c in coords)))
mx = Vector((max(c.x for c in coords), max(c.y for c in coords), max(c.z for c in coords)))
s = height / (mx.z - mn.z)
obj.scale = (s, s, s)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
mn = Vector((min(c.x for c in coords), min(c.y for c in coords), min(c.z for c in coords)))
mx = Vector((max(c.x for c in coords), max(c.y for c in coords), max(c.z for c in coords)))
feet = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
bpy.context.scene.cursor.location = feet
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
obj.location = (0, 0, 0)

# Material with the base-color texture.
img = bpy.data.images.load(tex_path)
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
print(f"MIXAMO: {obj.name} dims=({dims.x:.2f},{dims.y:.2f},{dims.z:.2f}) tris={sum(len(p.vertices) - 2 for p in obj.data.polygons)}")

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
