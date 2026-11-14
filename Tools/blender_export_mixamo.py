"""Export a low-poly character GLB as a T-pose FBX for Mixamo auto-rigging.

Usage:
  blender -b --factory-startup -P Tools/blender_export_mixamo.py -- <in.glb> <basecolor.png> <out.fbx> [height=1.8]

Result: single mesh, origin at the feet (bottom centre), height ~1.8 m, transforms applied,
base-color texture embedded (path_mode=COPY + embed_textures).
"""
import bpy
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, tex_path, dst = argv[0], argv[1], argv[2]
height = float(argv[3]) if len(argv) > 3 else 1.8

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

# Drop the glTF hierarchy (empties) and bake transforms into the mesh.
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

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
