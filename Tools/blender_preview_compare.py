"""Render character models side by side (front, flat textured) for a visual before/after check.

Usage:
  blender -b --factory-startup -P Tools/blender_preview_compare.py -- <out.png> <model1.glb|fbx> [model2 ...]

Each model is normalised to the same height (feet on the ground, centred), so the shape and texture
mapping can be compared directly. Armatures are ignored; meshes are shown in their bind/rest pose.
"""
import bpy
import sys
import math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
import os
out, models = os.path.abspath(argv[0]), argv[1:]
H = 2.0
GAP = 0.6

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

x = 0.0
for path in models:
    before = set(bpy.data.objects)
    if path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type == 'ARMATURE':
            o.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    meshes = [o for o in new if o.type == 'MESH']
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in meshes:
        ev = o.evaluated_get(dg)
        m = ev.to_mesh()
        pts += [ev.matrix_world @ v.co for v in m.vertices]
        ev.to_mesh_clear()
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    s = H / (mx.z - mn.z)
    w = (mx.x - mn.x) * s
    root = bpy.data.objects.new("root_" + bpy.path.basename(path), None)
    scene.collection.objects.link(root)
    for o in new:
        if o.parent is None:
            o.parent = root
    root.scale = (s, s, s)
    root.location = (x + w / 2 - (mn.x + mx.x) / 2 * s, -(mn.y + mx.y) / 2 * s, -mn.z * s)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    print(f"PREVIEW: {path} tris={tris} w/h={(mx.x - mn.x) / (mx.z - mn.z):.3f} d/h={(mx.y - mn.y) / (mx.z - mn.z):.3f}")
    x += w + GAP

total_w = x - GAP
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'FLAT'
scene.display.shading.color_type = 'TEXTURE'
scene.world = bpy.data.worlds.new("w")
scene.display.shading.background_type = 'VIEWPORT'
scene.display.shading.background_color = (0.75, 0.75, 0.78)
scale = max(total_w + 0.4, (H + 0.4) * 1.0)
px_per_m = 360
scene.render.resolution_x = int((total_w + 0.4) * px_per_m)
scene.render.resolution_y = int((H + 0.4) * px_per_m)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.type = 'ORTHO'
cam.data.ortho_scale = max(total_w, H) + 0.4
cam.location = (total_w / 2, -20, H / 2)
cam.rotation_euler = (math.radians(90), 0, 0)
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("PREVIEW: saved", out)
