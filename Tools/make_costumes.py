"""Soldier costume textures (F13): recolour the khaki / olive uniform of Soldier_BaseColor.png, keep skin, boots, belts.

Usage (numpy, pillow):  python Tools/make_costumes.py .
Output: Assets/Art/Characters/Soldier/Textures/Costumes/Soldier_<Costume>.png (1024², shading preserved).
Uniform mask: hue 35–80°, saturation ≥ 0.22. Camo = smoothed random blobs in UV space (seeded, deterministic).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

PROJ = sys.argv[1] if len(sys.argv) > 1 else "."
SRC = os.path.join(PROJ, "Assets/Art/Characters/Soldier/Textures/Soldier_BaseColor.png")
OUT = os.path.join(PROJ, "Assets/Art/Characters/Soldier/Textures/Costumes")
os.makedirs(OUT, exist_ok=True)

img = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32) / 255.0
H, W, _ = img.shape
r, g, b = img[..., 0], img[..., 1], img[..., 2]
mx, mn = img.max(-1), img.min(-1)
v = mx
s = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0)
d = np.maximum(mx - mn, 1e-6)
hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
mask = (hue >= 35) & (hue <= 80) & (s >= 0.22)
# Soft edge so seams don't show.
m = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
mask_f = np.asarray(m).astype(np.float32)[..., None] / 255.0
shade = v / max(1e-4, float(np.median(v[mask])))   # 1 = typical uniform brightness
print(f"uniform pixels {mask.mean() * 100:.1f}%  median v {np.median(v[mask]):.3f}")


def blobs(seed, scale, thresholds):
    rng = np.random.default_rng(seed)
    n = Image.fromarray((rng.random((H // scale, W // scale)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
    n = np.asarray(n.filter(ImageFilter.GaussianBlur(scale * 0.6))).astype(np.float32) / 255.0
    n = (n - n.min()) / (n.max() - n.min() + 1e-6)
    return np.digitize(n, thresholds)


def camo(seed, colors, scale=48):
    idx = blobs(seed, scale, np.linspace(0, 1, len(colors) + 1)[1:-1])
    return np.asarray(colors, np.float32)[idx] / 255.0


def solid(c):
    return np.broadcast_to(np.asarray(c, np.float32) / 255.0, (H, W, 3))


def write(name, base, gloss=0.0):
    col = base * np.clip(shade, 0.3, 1.35)[..., None]
    if gloss:
        col = col + gloss * np.clip(shade - 1.0, 0, 1)[..., None]
    out = img * (1 - mask_f) + np.clip(col, 0, 1) * mask_f
    Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, f"Soldier_{name}.png"), optimize=True)
    print("written", name)


write("Woodland", camo(1, [(70, 84, 44), (44, 58, 32), (96, 78, 48), (28, 30, 22)]))
write("Desert", camo(2, [(176, 150, 104), (150, 120, 80), (196, 176, 132), (120, 96, 66)], 56))
write("Night", camo(3, [(34, 40, 58), (24, 28, 40), (46, 54, 76)], 64))
write("Arctic", camo(4, [(214, 218, 222), (170, 176, 184), (236, 238, 240), (120, 126, 134)], 52))
write("Gold", solid((214, 168, 60)), gloss=0.5)
write("Commando", camo(5, [(30, 30, 32), (22, 22, 24), (150, 30, 28)], 40))
