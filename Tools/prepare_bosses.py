"""Copy the Mixamo-rigged F14 bosses into the Unity project and grade their base-colour textures.

Usage:  python Tools/prepare_bosses.py .

Input : _mixamo/anim/<dir>/<Name>.fbx (Mixamo "FBX with skin") and _mixamo/<Name>_BaseColor.png (keepuv export, 2048).
Output: Assets/Art/Characters/<Name>/<Name>_Rigged.fbx + Textures/<Name>_BaseColor.png (graded, 2048; Unity caps the
        import size). Grading makes each boss read on a dark street and look meaner:
  Sicrayan : colder, sicker skin (desaturated toward grey-green), deeper shadows, green veins pushed.
  KovanAna : stronger contrast, amber sacs pushed toward hot orange.
  Ezici    : the source is nearly black — lifted a lot (gamma + gain) with a dark olive / purple cast so the armour
             and hazard stripes read.
"""
import os
import shutil
import sys

from PIL import Image, ImageEnhance

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
BOSSES = [("sicrayan", "Sicrayan"), ("kovanana", "KovanAna"), ("ezici", "Ezici")]


def curve(img, gamma, gain, lift=0.0):
    lut = []
    for i in range(256):
        v = (i / 255.0) ** gamma * gain + lift
        lut.append(max(0, min(255, int(round(v * 255)))))
    return img.point(lut * 3)


def tint(img, rgb, amount):
    over = Image.new("RGB", img.size, rgb)
    return Image.blend(img, over, amount)


def grade(name, img):
    img = img.convert("RGB")
    if name == "Sicrayan":
        img = ImageEnhance.Color(img).enhance(0.85)
        img = tint(img, (120, 150, 110), 0.10)
        img = ImageEnhance.Contrast(img).enhance(1.18)
    elif name == "KovanAna":
        img = ImageEnhance.Contrast(img).enhance(1.15)
        img = ImageEnhance.Color(img).enhance(1.2)
    elif name == "Ezici":
        img = curve(img, 0.72, 1.35, 0.01)
        img = tint(img, (70, 60, 85), 0.10)
        img = ImageEnhance.Contrast(img).enhance(1.12)
        img = ImageEnhance.Color(img).enhance(1.15)
    return img


for folder, name in BOSSES:
    src_fbx = os.path.join(ROOT, "_mixamo", "anim", folder, name + ".fbx")
    src_tex = os.path.join(ROOT, "_mixamo", name + "_BaseColor.png")
    dst_dir = os.path.join(ROOT, "Assets", "Art", "Characters", name)
    os.makedirs(os.path.join(dst_dir, "Textures"), exist_ok=True)
    shutil.copyfile(src_fbx, os.path.join(dst_dir, name + "_Rigged.fbx"))
    img = grade(name, Image.open(src_tex))
    out = os.path.join(dst_dir, "Textures", name + "_BaseColor.png")
    img.save(out, optimize=True)
    px = img.resize((1, 1), Image.BOX).getpixel((0, 0))
    print(f"BOSS {name}: fbx copied, texture graded -> {out} mean={px}")
