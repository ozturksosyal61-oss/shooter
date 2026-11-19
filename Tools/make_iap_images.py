"""App Store Connect / Play Console in-app purchase images for Holdout.

python Tools/make_iap_images.py <project> [capture_dir]

-> StoreAssets/IAP/<product>/promo_1024.png  (1024x1024, opaque RGB, no text except numbers/VIP: language-neutral)
-> StoreAssets/IAP/<product>/review_screenshot.png (shop screen, 2688x1242 landscape) when capture_dir has
   ui07_shop_gems.png / ui07b_shop_gems_end.png (CaptureUI test).
Colours from the UI StyleGuide; font Bungee (OFL, Assets/Art/UI/Fonts).
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 1024
GROUND = (11, 13, 9)
PANEL = (17, 21, 15)
LINE = (52, 64, 44)
HAZARD = (255, 178, 30)
GEM = (92, 203, 245)
GOLD = (255, 200, 60)
TEXT = (242, 237, 225)
INK = (11, 13, 9)
SUCCESS = (166, 226, 46)

PRODUCTS = [
    # id, gems, gold, extra, glow colour
    ("gems_small", 70, 0, None, GEM),
    ("gems_medium", 150, 0, None, GEM),
    ("gems_large", 250, 0, "character", GEM),
    ("gems_huge", 550, 0, None, GEM),
    ("starter_pack", 150, 2000, "costume", HAZARD),
    ("vip", 0, 0, "vip", GOLD),
]


def font(root, size):
    return ImageFont.truetype(os.path.join(root, "Assets/Art/UI/Fonts/Bungee-Regular.ttf"), size)


def background(glow):
    img = Image.new("RGB", (S, S), GROUND)
    # radial glow
    g = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(g)
    for i in range(60, 0, -1):
        r = int(S * 0.62 * i / 60)
        d.ellipse((S / 2 - r, S * 0.46 - r, S / 2 + r, S * 0.46 + r), fill=int(150 * (1 - i / 60) ** 1.6))
    g = g.filter(ImageFilter.GaussianBlur(30))
    img = Image.composite(Image.new("RGB", (S, S), tuple(int(c * 0.55) for c in glow)), img, g)
    d = ImageDraw.Draw(img)
    # faint diagonal stripes
    stripe = Image.new("L", (S, S), 0)
    sd = ImageDraw.Draw(stripe)
    for x in range(-S, S * 2, 64):
        sd.polygon([(x, 0), (x + 26, 0), (x + 26 - S, S), (x - S, S)], fill=14)
    img = Image.composite(Image.new("RGB", (S, S), glow), img, stripe)
    d = ImageDraw.Draw(img)
    # hazard band at the bottom
    band = Image.new("RGB", (S, 34), HAZARD)
    bd = ImageDraw.Draw(band)
    for x in range(-40, S + 40, 44):
        bd.polygon([(x, 0), (x + 20, 0), (x - 14, 34), (x - 34, 34)], fill=INK)
    img.paste(band, (0, S - 34))
    d.rectangle((0, 0, S - 1, S - 35), outline=LINE, width=6)
    return img


def gem(d, cx, cy, w, shade=1.0):
    """Faceted gem (top crown + pavilion), dark outline."""
    h = w * 0.82
    top = cy - h * 0.42
    mid = cy - h * 0.08
    bot = cy + h * 0.58
    l, r = cx - w / 2, cx + w / 2
    tl, tr = cx - w * 0.3, cx + w * 0.3
    pts_outline = [(tl, top), (tr, top), (r, mid), (cx, bot), (l, mid)]
    ow = max(4, int(w * 0.045))
    d.polygon([(x, y) for x, y in pts_outline], fill=INK)
    # slightly inset faces
    def c(rgb, k):
        return tuple(min(255, int(v * k * shade)) for v in rgb)
    ins = ow * 0.9
    tl2, tr2, l2, r2 = tl + ins * 0.6, tr - ins * 0.6, l + ins, r - ins
    top2, mid2, bot2 = top + ins, mid, bot - ins * 1.6
    m1, m2 = cx - w * 0.12, cx + w * 0.12
    faces = [
        ([(tl2, top2), (m1, top2), (cx - w * 0.2, mid2), (l2, mid2)], c(GEM, 1.25)),
        ([(m1, top2), (m2, top2), (cx + w * 0.2, mid2), (cx - w * 0.2, mid2)], c((200, 240, 255), 1.0)),
        ([(m2, top2), (tr2, top2), (r2, mid2), (cx + w * 0.2, mid2)], c(GEM, 0.95)),
        ([(l2, mid2), (cx - w * 0.2, mid2), (cx, bot2)], c(GEM, 1.05)),
        ([(cx - w * 0.2, mid2), (cx + w * 0.2, mid2), (cx, bot2)], c(GEM, 0.85)),
        ([(cx + w * 0.2, mid2), (r2, mid2), (cx, bot2)], c(GEM, 0.62)),
    ]
    for poly, col in faces:
        d.polygon(poly, fill=col)
    # sparkle
    sx, sy, s = tl2 + w * 0.12, top2 + h * 0.1, w * 0.07
    d.polygon([(sx, sy - s), (sx + s * 0.3, sy - s * 0.3), (sx + s, sy), (sx + s * 0.3, sy + s * 0.3),
               (sx, sy + s), (sx - s * 0.3, sy + s * 0.3), (sx - s, sy), (sx - s * 0.3, sy - s * 0.3)], fill=(255, 255, 255))


def coin(d, cx, cy, r):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=INK)
    r1 = r * 0.9
    d.ellipse((cx - r1, cy - r1, cx + r1, cy + r1), fill=(214, 150, 20))
    r2 = r * 0.78
    d.ellipse((cx - r2, cy - r2 - r * 0.04, cx + r2, cy + r2 - r * 0.04), fill=GOLD)
    r3 = r * 0.52
    d.ellipse((cx - r3, cy - r3, cx + r3, cy + r3), outline=(214, 150, 20), width=max(3, int(r * 0.09)))


def shirt(d, cx, cy, w, col):
    h = w * 0.9
    pts = [(cx - w * 0.18, cy - h / 2), (cx - w * 0.5, cy - h * 0.32), (cx - w * 0.38, cy - h * 0.05),
           (cx - w * 0.28, cy - h * 0.12), (cx - w * 0.28, cy + h / 2), (cx + w * 0.28, cy + h / 2),
           (cx + w * 0.28, cy - h * 0.12), (cx + w * 0.38, cy - h * 0.05), (cx + w * 0.5, cy - h * 0.32),
           (cx + w * 0.18, cy - h / 2), (cx, cy - h * 0.36)]
    d.polygon(pts, fill=INK)
    k = 0.86
    inner = [((x - cx) * k + cx, (y - cy) * k + cy) for x, y in pts]
    d.polygon(inner, fill=col)


def person(d, cx, cy, w, col):
    r = w * 0.2
    d.ellipse((cx - r - 8, cy - w * 0.42 - r - 8, cx + r + 8, cy - w * 0.42 + r + 8), fill=INK)
    d.ellipse((cx - r, cy - w * 0.42 - r, cx + r, cy - w * 0.42 + r), fill=col)
    d.chord((cx - w * 0.42 - 8, cy - w * 0.12 - 8, cx + w * 0.42 + 8, cy + w * 0.7 + 8), 180, 360, fill=INK)
    d.chord((cx - w * 0.42, cy - w * 0.12, cx + w * 0.42, cy + w * 0.7), 180, 360, fill=col)


def text(d, xy, s, f, fill, stroke=10, anchor="mm"):
    d.text(xy, s, font=f, fill=fill, stroke_width=stroke, stroke_fill=INK, anchor=anchor)


def pile(d, n, cx, cy, size):
    """1..5 gems arranged as a heap (bigger packs = bigger heap)."""
    layouts = {
        1: [(0, 0, 1.0)],
        2: [(-0.34, 0.06, 0.8), (0.3, -0.04, 0.95)],
        3: [(-0.42, 0.12, 0.72), (0.42, 0.12, 0.72), (0, -0.08, 0.95)],
        5: [(-0.62, 0.2, 0.58), (0.62, 0.2, 0.58), (-0.3, 0.16, 0.7), (0.3, 0.16, 0.7), (0, -0.12, 0.92)],
    }
    for x, y, k in layouts[n]:
        gem(d, cx + x * size, cy + y * size, size * k, 0.92 if k < 0.9 else 1.0)


def promo(root, pid, gems, gold, extra, glow):
    img = background(glow)
    d = ImageDraw.Draw(img)
    big = font(root, 150)
    mid = font(root, 92)
    if pid == "vip":
        # crown
        cx, cy, w = S / 2, 380, 440
        crown = [(cx - w / 2, cy + 120), (cx - w / 2, cy - 90), (cx - w / 4, cy + 10), (cx, cy - 150),
                 (cx + w / 4, cy + 10), (cx + w / 2, cy - 90), (cx + w / 2, cy + 120)]
        d.polygon(crown, fill=INK)
        k = 0.86
        d.polygon([((x - cx) * k + cx, (y - cy) * k + cy + 6) for x, y in crown], fill=GOLD)
        d.rectangle((cx - w / 2 + 34, cy + 70, cx + w / 2 - 34, cy + 100), fill=(214, 150, 20))
        gem(d, cx, cy + 20, 90)
        text(d, (S / 2, 720), "VIP", font(root, 210), GOLD, 14)
        coin(d, S / 2 - 140, 880, 44)
        text(d, (S / 2 + 30, 884), "+25%", mid, TEXT, 10)
        return img
    if pid == "starter_pack":
        gem(d, 330, 330, 300)
        coin(d, 700, 360, 140)
        shirt(d, 520, 560, 260, (96, 112, 60))
        d.rounded_rectangle((640, 470, 880, 560), 26, fill=INK)
        d.rounded_rectangle((648, 478, 872, 552), 20, fill=HAZARD)
        text(d, (760, 516), "x1", font(root, 56), INK, 0)
        text(d, (330, 780), str(gems), mid, GEM, 10)
        text(d, (720, 780), str(gold), mid, GOLD, 10)
        gem(d, 330, 880, 70)
        coin(d, 720, 880, 38)
        return img
    n = {70: 1, 150: 2, 250: 3, 550: 5}[gems]
    pile(d, n, S / 2, 400, 360)
    if extra == "character":
        # "= 1 character" badge
        d.rounded_rectangle((690, 120, 900, 300), 36, fill=INK)
        d.rounded_rectangle((700, 130, 890, 290), 30, fill=HAZARD)
        person(d, 795, 225, 120, INK)
    text(d, (S / 2, 790), str(gems), big, GEM, 14)
    return img


def review(src, dst):
    im = Image.open(src).convert("RGB").resize((2688, 1242), Image.LANCZOS)
    im.save(dst)


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "."
    caps = sys.argv[2] if len(sys.argv) > 2 else None
    out = os.path.join(root, "StoreAssets", "IAP")
    for pid, gems, gold, extra, glow in PRODUCTS:
        d = os.path.join(out, pid)
        os.makedirs(d, exist_ok=True)
        promo(root, pid, gems, gold, extra, glow).save(os.path.join(d, "promo_1024.png"))
        if caps:
            shot = "ui07b_shop_gems_end.png" if pid in ("starter_pack", "vip") else "ui07_shop_gems.png"
            p = os.path.join(caps, shot)
            if os.path.exists(p):
                review(p, os.path.join(d, "review_screenshot.png"))
        print("ok", pid)


if __name__ == "__main__":
    main()
