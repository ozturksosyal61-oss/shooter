"""District zombie looks (1.1.0): recolour the walker's clothes, keep the green skin, hair and shoes.
Usage: python Tools/make_zombie_variants.py .
  Zombie_Liman.png  : light shirt → high-vis orange, jeans → navy        (harbour workers)
  Zombie_Sanayi.png : light shirt → grey-blue overall, jeans → dark grey  (factory hands)
  Zombie_Carsi.png  : light shirt → maroon, jeans → brown                 (bazaar sellers)
"""
import colorsys
import os
import sys

import numpy as np
from PIL import Image

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
DIR = os.path.join(ROOT, "Assets", "Art", "Characters", "Zombie", "Textures")
src = np.asarray(Image.open(os.path.join(DIR, "Zombie_BaseColor.png")).convert("RGB")).astype(np.float32) / 255.0

r, g, b = src[..., 0], src[..., 1], src[..., 2]
mx, mn = src.max(-1), src.min(-1)
light = (mx + mn) / 2
sat = np.where(mx - mn < 1e-5, 0, (mx - mn) / (1 - np.abs(2 * light - 1) + 1e-5))
hue = np.zeros_like(mx)
d = mx - mn + 1e-6
hue = np.where(mx == r, ((g - b) / d) % 6, hue)
hue = np.where(mx == g, (b - r) / d + 2, hue)
hue = np.where(mx == b, (r - g) / d + 4, hue)
hue = hue * 60.0

shirt = (sat < 0.18) & (light > 0.45)                         # white / light grey cloth
jeans = (hue > 195) & (hue < 250) & (sat > 0.2) & (light > 0.12)  # blue denim


def paint(mask, rgb):
    out = src.copy()
    tgt = np.array(rgb, np.float32)
    shade = (light[mask] / max(light[mask].mean(), 1e-3))[:, None]
    out[mask] = np.clip(tgt[None, :] * shade, 0, 1)
    return out


for name, shirt_c, jeans_c in [("Liman", (0.95, 0.45, 0.08), (0.12, 0.17, 0.32)),
                               ("Sanayi", (0.42, 0.5, 0.6), (0.22, 0.23, 0.25)),
                               ("Carsi", (0.5, 0.1, 0.12), (0.35, 0.24, 0.14))]:
    img = paint(shirt, shirt_c)
    tmp = src; src_backup = src
    img2 = img.copy()
    m = jeans
    tgt = np.array(jeans_c, np.float32)
    shade = (light[m] / max(light[m].mean(), 1e-3))[:, None]
    img2[m] = np.clip(tgt[None, :] * shade, 0, 1)
    Image.fromarray((img2 * 255).astype(np.uint8)).save(os.path.join(DIR, f"Zombie_{name}.png"))
    print("ZOMBIE", name, "shirt px", int(shirt.sum()), "jeans px", int(jeans.sum()))
