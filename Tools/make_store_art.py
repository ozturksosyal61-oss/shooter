"""App Store screenshots + video stills for Holdout from the CaptureStore test (raw 5112x2358 renders).

python Tools/make_store_art.py <project> <capture_dir_tr> [capture_dir_en]
(the EN screenshots use the English-UI capture when given: STORE_LANG=en)

-> StoreAssets/Screenshots/{tr,en}/NN_<name>_2556x1179.png and _2622x1206.png (caption panel left, game frame right)
   + _2752x2064.png (iPad 13\", caption on top, game frame below)
-> StoreAssets/VideoStills/<name>_{2556x1179,1920x886}.png  (no UI at all; menus/HUD shots under VideoStills/ui/)
Fonts: Bungee + Barlow Condensed (OFL, Assets/Art/UI/Fonts). Colours from the UI StyleGuide.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

GROUND = (11, 13, 9)
LINE = (52, 64, 44)
HAZARD = (255, 178, 30)
TEXT = (242, 237, 225)
SUB = (180, 186, 168)
INK = (11, 13, 9)

SHOTS = [
    # file, TR title, TR sub, EN title, EN sub
    ("raw_action", "ŞEHİR DÜŞTÜ.\nSEN DÜŞME.", "Sonsuz zombi dalgalarına karşı son devriye sensin.", "THE CITY FELL.\nHOLD OUT.", "You are the last patrol against endless waves."),
    ("raw_boss", "DEV\nMUTANTLAR", "Kasap, Leş Kuşu, Veba Ana ve Kıyamet seni bekliyor.", "GIANT\nMUTANTS", "Butcher, Carrion, Plague Mother and Doombringer await."),
    ("raw_specials", "HER ZOMBİ\nAYNI DEĞİL", "Kalkanlı, Bombalı, Dilci — taktiğini değiştir.", "NOT EVERY\nZOMBIE IS SLOW", "Shields, bombers, tongues — change your tactics."),
    ("raw_barrel", "PATLAT VE\nZİNCİRLE", "Varilleri vur, kalabalığı tek hamlede temizle.", "BLOW IT UP", "Shoot the barrels, clear the crowd in one blast."),
    ("raw_cards", "HER TUR\nFARKLI", "Seviye atla, 15 karttan güçlen: delici mermi, taret, bomba…", "EVERY RUN\nIS DIFFERENT", "Level up and pick from 15 cards: piercing shots, turrets, bombs…"),
    ("raw_leaper", "ENGEL\nTANIMAZ", "Bazı mutantlar binaların üstünden atlar — halkadan çık!", "NOWHERE\nTO HIDE", "Some mutants leap over buildings — get out of the ring!"),
    ("raw_modes", "4 OYUN\nMODU", "Hikâye, Sonsuz, Zor ve arkadaşlarınla Co-op.", "4 GAME\nMODES", "Story, Endless, Hard and Co-op with friends."),
    ("raw_characters", "KARAKTERİNİ\nSEÇ", "Yeni karakterler, kostümler ve 5 farklı silah.", "PICK YOUR\nSOLDIER", "New characters, costumes and 5 different guns."),
]

STILLS = ["clean_action", "clean_action2", "clean_boss", "clean_leaper", "clean_specials", "clean_barrel", "clean_crusher_close", "clean_soldier_close"]
UI_STILLS = ["raw_menu", "raw_modes", "raw_characters", "raw_cards"]


def fonts(root):
    d = os.path.join(root, "Assets/Art/UI/Fonts")
    return (lambda s: ImageFont.truetype(os.path.join(d, "Bungee-Regular.ttf"), s),
            lambda s: ImageFont.truetype(os.path.join(d, "BarlowCondensed-SemiBold.ttf"), s))


def wrap(draw, text, font, width):
    words, lines, cur = text.split(), [], ""
    for w in words:
        t = (cur + " " + w).strip()
        if draw.textlength(t, font=font) <= width: cur = t
        else:
            if cur: lines.append(cur)
            cur = w
    if cur: lines.append(cur)
    return lines


def compose(root, raw, title, sub, size):
    W, H = size
    k = W / 2556.0
    big, semi = fonts(root)
    img = Image.new("RGB", (W, H), GROUND)
    d = ImageDraw.Draw(img)
    # faint diagonal stripes on the panel side
    for x in range(-H, int(820 * k), int(70 * k)):
        d.polygon([(x, 0), (x + 24 * k, 0), (x + 24 * k + H * 0.6, H), (x + H * 0.6, H)], fill=(16, 19, 13))
    # game frame (right), with shadow + outline
    fw = int(W - 800 * k - 60 * k)
    fh = int(fw * 2358 / 5112)
    fx = int(800 * k)
    fy = (H - fh) // 2
    shot = Image.open(raw).convert("RGB").resize((fw, fh), Image.LANCZOS)
    shadow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shadow).rounded_rectangle((fx + 10, fy + 18, fx + fw + 10, fy + fh + 18), int(34 * k), fill=170)
    shadow = shadow.filter(ImageFilter.GaussianBlur(26 * k))
    img = Image.composite(Image.new("RGB", (W, H), (0, 0, 0)), img, shadow)
    mask = Image.new("L", (fw, fh), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, fw - 1, fh - 1), int(30 * k), fill=255)
    img.paste(shot, (fx, fy), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((fx - 3, fy - 3, fx + fw + 3, fy + fh + 3), int(32 * k), outline=LINE, width=max(3, int(5 * k)))
    # caption (left)
    x0 = int(70 * k)
    tw = int(690 * k)
    # brand chip
    fb = big(int(30 * k))
    d.rounded_rectangle((x0, int(150 * k), x0 + int(260 * k), int(200 * k)), int(10 * k), fill=HAZARD)
    d.text((x0 + int(130 * k), int(176 * k)), "HOLDOUT", font=fb, fill=INK, anchor="mm")
    # title: fit to width
    size_t = int(104 * k)
    lines = title.split("\n")
    while size_t > 40:
        ft = big(size_t)
        if all(d.textlength(l, font=ft) <= tw for l in lines): break
        size_t -= 4
    y = int(250 * k)
    for l in lines:
        d.text((x0 + 5, y + 6), l, font=ft, fill=(0, 0, 0))
        d.text((x0, y), l, font=ft, fill=TEXT)
        y += int(size_t * 1.12)
    y += int(24 * k)
    d.rectangle((x0, y, x0 + int(120 * k), y + int(10 * k)), fill=HAZARD)
    y += int(46 * k)
    fs = semi(int(48 * k))
    for l in wrap(d, sub, fs, tw):
        d.text((x0, y), l, font=fs, fill=SUB)
        y += int(58 * k)
    # hazard band bottom-left
    by = H - int(54 * k)
    for x in range(0, int(760 * k), int(46 * k)):
        d.polygon([(x, by), (x + 22 * k, by), (x + 22 * k - 26 * k, H), (x - 26 * k, H)], fill=HAZARD)
    return img


def compose_tablet(root, raw, title, sub, size):
    """iPad 13" landscape (4:3): caption on top, wide game frame below."""
    W, H = size
    k = W / 2752.0
    big, semi = fonts(root)
    img = Image.new("RGB", (W, H), GROUND)
    d = ImageDraw.Draw(img)
    for x in range(-H, W, int(70 * k)):
        d.polygon([(x, 0), (x + 24 * k, 0), (x + 24 * k + H * 0.35, int(700 * k)), (x + H * 0.35, int(700 * k))], fill=(16, 19, 13))
    mx = int(100 * k)
    fw = W - 2 * mx
    fh = int(fw * 2358 / 5112)
    fx = mx
    fy = H - int(160 * k) - fh
    shot = Image.open(raw).convert("RGB").resize((fw, fh), Image.LANCZOS)
    shadow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shadow).rounded_rectangle((fx + 10, fy + 18, fx + fw + 10, fy + fh + 18), int(34 * k), fill=170)
    shadow = shadow.filter(ImageFilter.GaussianBlur(26 * k))
    img = Image.composite(Image.new("RGB", (W, H), (0, 0, 0)), img, shadow)
    mask = Image.new("L", (fw, fh), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, fw - 1, fh - 1), int(30 * k), fill=255)
    img.paste(shot, (fx, fy), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((fx - 3, fy - 3, fx + fw + 3, fy + fh + 3), int(32 * k), outline=LINE, width=max(3, int(5 * k)))
    x0 = mx
    tw = fw
    fb = big(int(34 * k))
    d.rounded_rectangle((x0, int(110 * k), x0 + int(290 * k), int(166 * k)), int(10 * k), fill=HAZARD)
    d.text((x0 + int(145 * k), int(139 * k)), "HOLDOUT", font=fb, fill=INK, anchor="mm")
    line = title.replace("\n", " ")
    size_t = int(130 * k)
    while size_t > 40:
        ft = big(size_t)
        if d.textlength(line, font=ft) <= tw: break
        size_t -= 4
    y = int(215 * k)
    d.text((x0 + 6, y + 7), line, font=ft, fill=(0, 0, 0))
    d.text((x0, y), line, font=ft, fill=TEXT)
    y += int(size_t * 1.18)
    d.rectangle((x0, y, x0 + int(130 * k), y + int(11 * k)), fill=HAZARD)
    y += int(40 * k)
    fs = semi(int(60 * k))
    for l in wrap(d, sub, fs, tw)[:2]:
        d.text((x0, y), l, font=fs, fill=SUB)
        y += int(70 * k)
    by = H - int(60 * k)
    for x in range(0, W + int(46 * k), int(46 * k)):
        d.polygon([(x, by), (x + 22 * k, by), (x + 22 * k - 26 * k, H), (x - 26 * k, H)], fill=HAZARD)
    return img


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "."
    caps = sys.argv[2]
    caps_en = sys.argv[3] if len(sys.argv) > 3 else caps
    out = os.path.join(root, "StoreAssets")
    for lang, ti, si in (("tr", 1, 2), ("en", 3, 4)):
        src = caps if lang == "tr" else caps_en
        d = os.path.join(out, "Screenshots", lang)
        os.makedirs(d, exist_ok=True)
        for n, row in enumerate(SHOTS, 1):
            raw = os.path.join(src, row[0] + ".png")
            if not os.path.exists(raw): continue
            name = row[0].replace("raw_", "")
            for size in ((2556, 1179), (2622, 1206)):
                compose(root, raw, row[ti], row[si], size).save(os.path.join(d, f"{n:02d}_{name}_{size[0]}x{size[1]}.png"))
            # iPad 13" (App Store requires it when the app runs on iPad)
            size = (2752, 2064)
            compose_tablet(root, raw, row[ti], row[si], size).save(os.path.join(d, f"{n:02d}_{name}_{size[0]}x{size[1]}.png"))
        print("ok", lang)
    vd = os.path.join(out, "VideoStills")
    os.makedirs(os.path.join(vd, "ui"), exist_ok=True)
    for name in STILLS + UI_STILLS:
        raw = os.path.join(caps, name + ".png")
        if not os.path.exists(raw): continue
        im = Image.open(raw).convert("RGB")
        base = name.replace("clean_", "").replace("raw_", "")
        dst = vd if name.startswith("clean_") else os.path.join(vd, "ui")
        for size in ((2556, 1179), (1920, 886)):
            im.resize(size, Image.LANCZOS).save(os.path.join(dst, f"{base}_{size[0]}x{size[1]}.png"))
    print("ok stills")


if __name__ == "__main__":
    main()
