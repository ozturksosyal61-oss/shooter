"""Normalize a weapon model: barrel along +Z (muzzle at +Z end), up = +Y (glTF/Unity space),
given length, origin at the bounding-box rear-bottom-centre. Materials are kept.

Usage:
  blender -b --factory-startup -P Tools/blender_normalize_weapon.py -- <in.glb> <out.glb> [length=0.85]

Heuristics (Blender is Z-up here; glTF export converts to Y-up):
  * longest axis = barrel axis
  * muzzle end = the end whose last 6% slice has the smallest cross-section
  * "down" = the side where most of the cross-section lies away from the muzzle line
    (grip/magazine hang below the barrel)
"""
import bpy, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
length = float(argv[2]) if len(argv) > 2 else 0.85

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

vs = [v.co.copy() for v in obj.data.vertices]
mn = Vector([min(v[i] for v in vs) for i in range(3)])
mx = Vector([max(v[i] for v in vs) for i in range(3)])
size = mx - mn
L = max(range(3), key=lambda i: size[i])
others = [i for i in range(3) if i != L]

def slice_extent(lo, hi):
    sel = [v for v in vs if lo <= v[L] <= hi]
    if not sel: return 0, None
    ext = sum(max(v[i] for v in sel) - min(v[i] for v in sel) for i in others)
    centre = [sum(v[i] for v in sel) / len(sel) for i in range(3)]
    return ext, centre

s = size[L] * 0.06
ext_min, c_min = slice_extent(mn[L], mn[L] + s)
ext_max, c_max = slice_extent(mx[L] - s, mx[L])
muzzle_sign = 1 if ext_max < ext_min else -1
muzzle_c = c_max if muzzle_sign > 0 else c_min

# Vertical axis: of the two remaining axes, the one with larger extent (grip + magazine).
V = max(others, key=lambda i: size[i])
W = [i for i in others if i != V][0]
# Muzzle sits near the top: up direction points from bbox centre toward the muzzle line.
mid = (mn[V] + mx[V]) / 2
up_sign = 1 if muzzle_c[V] > mid else -1
print(f"WEAPON: size={tuple(round(x,3) for x in size)} L={L} V={V} muzzle_sign={muzzle_sign} up_sign={up_sign}")

# Build rotation: barrel -> Blender -Y?  We want glTF +Z forward / +Y up.
# glTF export (+Y up) maps Blender (x, y, z) -> glTF (x, z, -y). So glTF +Z = Blender -Y, glTF +Y = Blender +Z.
def axis(i, sign):
    v = Vector((0, 0, 0)); v[i] = sign; return v
fwd_src = axis(L, muzzle_sign)
up_src = axis(V, up_sign)
right_src = fwd_src.cross(up_src)
fwd_dst, up_dst = Vector((0, -1, 0)), Vector((0, 0, 1))
right_dst = fwd_dst.cross(up_dst)
src_m = Matrix((right_src, fwd_src, up_src)).transposed()
dst_m = Matrix((right_dst, fwd_dst, up_dst)).transposed()
R = (dst_m @ src_m.inverted()).to_4x4()
obj.data.transform(R)
obj.data.update()

vs = [v.co.copy() for v in obj.data.vertices]
mn = Vector([min(v[i] for v in vs) for i in range(3)])
mx = Vector([max(v[i] for v in vs) for i in range(3)])
k = length / (mx.y - mn.y)
obj.data.transform(Matrix.Scale(k, 4))
vs = [v.co.copy() for v in obj.data.vertices]
mn = Vector([min(v[i] for v in vs) for i in range(3)])
mx = Vector([max(v[i] for v in vs) for i in range(3)])
# Origin: rear (max Blender y = glTF -Z end), bottom, centred in x.
obj.data.transform(Matrix.Translation(Vector((-(mn.x + mx.x) / 2, -mx.y, -mn.z))))
obj.data.update()
d = obj.dimensions
print(f"WEAPON: final dims (x,y,z blender)=({d.x:.3f},{d.y:.3f},{d.z:.3f}) tris={sum(len(p.vertices)-2 for p in obj.data.polygons)}")
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_yup=True, export_materials='EXPORT')
print("WEAPON: exported", dst)
