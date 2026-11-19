"""Instagram launch set for Holdout (Turkish): logo post, carousel, reel.

python Tools/make_instagram.py <project> <store_capture_dir> <reel_capture_dir> <out_dir>

store_capture_dir: CaptureStore output (raw_*/clean_* 5112x2358, STORE_LANG=tr)
reel_capture_dir:  CaptureReel output (reel/NNNNN.jpg 1440x2560, audio.wav, segments.txt)
-> <out>/1_logo.png, <out>/2_carousel/NN_*.png (1080x1350), <out>/3_reels.mp4 (1080x1920, 30 fps)
Fonts: Bungee + Barlow Condensed (OFL). Colours from the UI StyleGuide. Needs Pillow, numpy, soundfile, imageio-ffmpeg.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

GROUND = (11, 13, 9)
LINE = (52, 64, 44)
HAZARD = (255, 178, 30)
TEXT = (242, 237, 225)
SUB = (200, 205, 188)
INK = (11, 13, 9)
POST = (1080, 1350)
REEL = (1080, 1920)

ROOT = "."


def font(name, size):
    return ImageFont.truetype(os.path.join(ROOT, "Assets/Art/UI/Fonts", name), size)


def big(s): return font("Bungee-Regular.ttf", s)
def semi(s): return font("BarlowCondensed-SemiBold.ttf", s)
def bold(s): return font("BarlowCondensed-ExtraBold.ttf", s)


def wrap(d, text, f, width):
    words, lines, cur = text.split(), [], ""
    for w in words:
        t = (cur + " " + w).strip()
        if d.textlength(t, font=f) <= width: cur = t
        else:
            if cur: lines.append(cur)
            cur = w
    if cur: lines.append(cur)
    return lines


def fit(d, lines, maker, start, width):
    s = start
    while s > 30:
        f = maker(s)
        if all(d.textlength(l, font=f) <= width for l in lines): return f, s
        s -= 3
    return maker(s), s


def cover(im, size, cx=0.5, cy=0.5):
    W, H = size
    k = max(W / im.width, H / im.height)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    x = int(min(max(cx * im.width - W / 2, 0), im.width - W))
    y = int(min(max(cy * im.height - H / 2, 0), im.height - H))
    return im.crop((x, y, x + W, y + H))


def gradient(size, top, bottom, a0, a1):
    """Black overlay alpha: a0 above `top`, ramps to a1 at `bottom` (fractions of height)."""
    W, H = size
    col = np.zeros((H,), np.float32)
    ys = np.arange(H) / H
    col = np.clip((ys - top) / max(bottom - top, 1e-3), 0, 1) * (a1 - a0) + a0
    a = (np.tile(col[:, None], (1, W)) * 255).astype(np.uint8)
    return Image.fromarray(a, "L")


def darken(img, mask):
    return Image.composite(Image.new("RGB", img.size, (0, 0, 0)), img, mask)


def shadow_text(d, xy, text, f, fill=TEXT, off=5, anchor="la"):
    x, y = xy
    d.text((x + off, y + off + 1), text, font=f, fill=(0, 0, 0), anchor=anchor)
    d.text((x, y), text, font=f, fill=fill, anchor=anchor)


def hazard_band(d, W, y0, y1, step=46):
    h = y1 - y0
    for x in range(-h, W + step, step):
        d.polygon([(x, y0), (x + step // 2, y0), (x + step // 2 - h, y1), (x - h, y1)], fill=HAZARD)


def chip(d, x, y, text, f, pad=18, fill=HAZARD, color=INK, anchor_center=False):
    tw = d.textlength(text, font=f)
    h = int(f.size * 1.5)
    if anchor_center: x = int(x - (tw + 2 * pad) / 2)
    d.rounded_rectangle((x, y, x + tw + 2 * pad, y + h), h // 4, fill=fill)
    d.text((x + pad + tw / 2, y + h / 2), text, font=f, fill=color, anchor="mm")
    return x + tw + 2 * pad, y + h


def app_icon(size):
    im = Image.open(os.path.join(ROOT, "Assets/Art/Icon/AppIcon.png")).convert("RGB").resize((size, size), Image.LANCZOS)
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size - 1, size - 1), int(size * 0.225), fill=255)
    return im, m


def logo_block(img, cx, y, width, tagline=True):
    """HOLDOUT (Bungee, cream, hard shadow) + hazard chip and spaced LAST CITY, centred at cx."""
    d = ImageDraw.Draw(img)
    f, s = fit(d, ["HOLDOUT"], big, 260, width)
    tw = d.textlength("HOLDOUT", font=f)
    x = cx - tw / 2
    # soft green glow + hard ink shadow, like the menu logo
    glow = Image.new("L", img.size, 0)
    ImageDraw.Draw(glow).text((x, y), "HOLDOUT", font=f, fill=150)
    glow = glow.filter(ImageFilter.GaussianBlur(s * 0.12))
    img.paste(Image.new("RGB", img.size, (120, 200, 40)), (0, 0), glow)
    d = ImageDraw.Draw(img)
    off = max(4, int(s * 0.045))
    d.text((x + off, y + off * 1.3), "HOLDOUT", font=f, fill=INK)
    d.text((x, y), "HOLDOUT", font=f, fill=TEXT)
    bottom = y + int(s * 1.02)
    if tagline:
        ft = big(int(s * 0.25))
        spaced = "  ".join("LAST CITY".split(" "))
        spaced = " ".join(spaced)
        lw = d.textlength(spaced, font=ft)
        bw = int(s * 0.5)
        total = bw + 24 + lw
        lx = cx - total / 2
        by = bottom + int(s * 0.06)
        bh = int(s * 0.16)
        for i in range(0, bw, max(10, bw // 6)):
            d.polygon([(lx + i, by + bh), (lx + i + bw // 12, by + bh), (lx + i + bw // 12 + bh * 0.6, by), (lx + i + bh * 0.6, by)], fill=HAZARD)
        d.text((lx + bw + 24, by + bh / 2), spaced, font=ft, fill=HAZARD, anchor="lm")
        bottom = by + bh
    return bottom


def store_line(d, cx, y, scale=1.0):
    """'ŞİMDİ APP STORE'DA' pill + 'Ücretsiz indir' under it."""
    f = big(int(46 * scale))
    _, y1 = chip(d, cx, y, "ŞİMDİ APP STORE'DA", f, pad=int(30 * scale), anchor_center=True)
    d.text((cx, y1 + int(22 * scale)), "Ücretsiz indir · Holdout: Last City", font=semi(int(40 * scale)), fill=SUB, anchor="mt")


# ---------------------------------------------------------------- 1) logo post

def logo_post(caps, out, size=POST, cy=0.5):
    W, H = size
    bg = Image.open(os.path.join(caps, "clean_action.png")).convert("RGB")
    img = cover(bg, size, 0.5, cy).filter(ImageFilter.GaussianBlur(5))
    img = darken(img, Image.new("L", size, 120))
    img = darken(img, gradient(size, 0.45, 1.0, 0.0, 0.85))
    img = darken(img, gradient(size, 0.0, 0.3, 0.55, 0.0))
    d = ImageDraw.Draw(img)
    ic = int(W * 0.26)
    icon, m = app_icon(ic)
    iy = int(H * 0.17)
    sh = Image.new("L", size, 0)
    ImageDraw.Draw(sh).rounded_rectangle((W // 2 - ic // 2 + 8, iy + 14, W // 2 + ic // 2 + 8, iy + ic + 14), int(ic * 0.225), fill=190)
    img = darken(img, sh.filter(ImageFilter.GaussianBlur(18)))
    img.paste(icon, (W // 2 - ic // 2, iy), m)
    bottom = logo_block(img, W // 2, iy + ic + int(H * 0.05), int(W * 0.84))
    d = ImageDraw.Draw(img)
    d.text((W // 2, bottom + int(H * 0.045)), "Şehir düştü. Son devriye sensin.", font=semi(int(W * 0.05)), fill=TEXT, anchor="mt")
    store_line(d, W // 2, int(H * 0.80) if H < 1600 else int(H * 0.74), W / 1080)
    hazard_band(d, W, H - 34, H)
    return img


# ---------------------------------------------------------------- 2) carousel

FULL = [
    # file, crop centre x, title, sub
    ("clean_action", 0.5, "ŞEHİR DÜŞTÜ.\nSEN DÜŞME.", "Sonsuz zombi dalgalarına karşı son devriye sensin."),
    ("clean_boss", 0.55, "DEV\nMUTANTLAR", "Kasap, Leş Kuşu, Veba Ana ve Kıyamet seni bekliyor."),
    ("clean_specials", 0.5, "HER ZOMBİ\nAYNI DEĞİL", "Kalkanlı, Bombalı, Dilci. Taktiğini değiştir."),
    ("clean_barrel", 0.5, "PATLAT VE\nZİNCİRLE", "Varili vur, kalabalığı tek hamlede temizle."),
    ("clean_crusher_close", 0.5, "KIYAMET\nGELİYOR", "Zırhlı önü mermiyi sektirir. Arkasından dolaş, öyle vur!"),
]
FRAMED = [
    # file, crop width (fraction, centred), title, sub
    ("raw_cards", 0.74, "HER TUR\nFARKLI", "Seviye atla, 15 karttan seç: delici mermi, taret, bomba…"),
    ("raw_modes", 0.86, "4 OYUN\nMODU", "Hikâye, Sonsuz, Zor ve arkadaşlarınla Co-op."),
    ("raw_characters", 0.86, "KARAKTERİNİ\nSEÇ", "Yeni karakterler, kostümler ve 5 farklı silah."),
]


def caption(img, title, sub, y, width, x=None, size0=118):
    W, H = img.size
    d = ImageDraw.Draw(img)
    x = x if x is not None else int(W * 0.07)
    chip(d, x, y, "HOLDOUT", big(30), pad=16)
    lines = title.split("\n")
    f, s = fit(d, lines, big, size0, width)
    yy = y + 78
    for l in lines:
        shadow_text(d, (x, yy), l, f)
        yy += int(s * 1.1)
    yy += 14
    d.rectangle((x, yy, x + 110, yy + 10), fill=HAZARD)
    yy += 34
    fs = semi(46)
    for l in wrap(d, sub, fs, width):
        shadow_text(d, (x, yy), l, fs, fill=SUB, off=3)
        yy += 54
    return yy


def swipe_hint(d, W, H):
    x, y = W - 70, H - 120
    for i in range(2):
        xx = x - 26 + i * 24
        d.polygon([(xx, y - 22), (xx + 14, y - 22), (xx + 36, y), (xx + 14, y + 22), (xx, y + 22), (xx + 22, y)], fill=HAZARD)
    d.text((x - 46, y), "KAYDIR", font=bold(34), fill=TEXT, anchor="rm")


def full_slide(caps, name, cx, title, sub, first=False):
    W, H = POST
    img = cover(Image.open(os.path.join(caps, name + ".png")).convert("RGB"), POST, cx, 0.5)
    img = darken(img, gradient(POST, 0.42, 0.98, 0.0, 0.88))
    img = darken(img, gradient(POST, 0.0, 0.14, 0.35, 0.0))
    probe = ImageDraw.Draw(img)
    lines = title.split("\n")
    f, s = fit(probe, lines, big, 118, int(W * 0.86))
    nsub = len(wrap(probe, sub, semi(46), int(W * 0.86)))
    h = 78 + len(lines) * int(s * 1.1) + 48 + nsub * 54
    caption(img, title, sub, H - 90 - h, int(W * 0.86))
    d = ImageDraw.Draw(img)
    hazard_band(d, W, H - 30, H)
    if first: swipe_hint(d, W, H - 60)
    return img


def framed_slide(caps, name, cw, title, sub):
    W, H = POST
    src = Image.open(os.path.join(caps, name + ".png")).convert("RGB")
    x0 = int(src.width * (1 - cw) / 2)
    src = src.crop((x0, 0, src.width - x0, src.height))
    img = cover(src, POST).filter(ImageFilter.GaussianBlur(14))
    img = darken(img, Image.new("L", POST, 165))
    yy = caption(img, title, sub, 80, int(W * 0.86))
    fw = W - 2 * 50
    fh = int(fw * src.height / src.width)
    fx, fy = 50, max(yy + 40, int((H + yy) / 2 - fh / 2) - 10)
    sh = Image.new("L", POST, 0)
    ImageDraw.Draw(sh).rounded_rectangle((fx + 8, fy + 14, fx + fw + 8, fy + fh + 14), 26, fill=200)
    img = darken(img, sh.filter(ImageFilter.GaussianBlur(20)))
    m = Image.new("L", (fw, fh), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, fw - 1, fh - 1), 24, fill=255)
    img.paste(src.resize((fw, fh), Image.LANCZOS), (fx, fy), m)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((fx - 3, fy - 3, fx + fw + 3, fy + fh + 3), 26, outline=LINE, width=4)
    hazard_band(d, W, H - 30, H)
    return img


# ---------------------------------------------------------------- 3) reel

REEL_TEXT = {
    "action": ("ŞEHİR DÜŞTÜ.", None),
    "action2": ("SEN DÜŞME.", None),
    "specials": ("HER ZOMBİ", "AYNI DEĞİL"),
    "barrel": ("PATLAT!", None),
    "boss": ("DEV", "MUTANTLAR"),
    "leaper": ("KAÇACAK", "YER YOK"),
}


def reel_text_layer(lines):
    """Pre-rendered RGBA text block (Bungee, cream, hard shadow) for the reel."""
    W = REEL[0]
    probe = ImageDraw.Draw(Image.new("RGB", (8, 8)))
    ls = [l for l in lines if l]
    f, s = fit(probe, ls, big, 150, int(W * 0.86))
    h = int(len(ls) * s * 1.12 + 40)
    lay = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    y = 10
    for l in ls:
        d.text((W / 2 + 7, y + 9), l, font=f, fill=(0, 0, 0, 230), anchor="mt")
        d.text((W / 2, y), l, font=f, fill=TEXT + (255,), anchor="mt")
        y += int(s * 1.12)
    return lay


def ease_out(t): return 1 - (1 - t) ** 3


def reel(caps, rdir, out, fps=30):
    import imageio_ffmpeg
    import soundfile as sf
    W, H = REEL
    segs = []
    for line in open(os.path.join(rdir, "segments.txt"), encoding="utf-8"):
        p = line.split()
        if len(p) == 3: segs.append((p[0], int(p[1]), int(p[2])))
    layers = {n: reel_text_layer(REEL_TEXT.get(n, ("", None))) for n, _, _ in segs}
    chip_l = Image.new("RGBA", (W, 90), (0, 0, 0, 0))
    chip(ImageDraw.Draw(chip_l), W // 2, 10, "HOLDOUT", big(36), pad=20, anchor_center=True)
    end = logo_post(caps, None, REEL, 0.5)
    end_frames = int(3.2 * fps)
    total = segs[-1][2] + end_frames

    # audio: game mix with short fades at the cuts + the intense music loop under the end card
    a, sr = sf.read(os.path.join(rdir, "audio.wav"), dtype="float32", always_2d=True)
    spf = sr / fps
    fade = int(sr * 0.04)
    for _, s0, s1 in segs:
        i0, i1 = int(s0 * spf), min(int(s1 * spf), len(a))
        n = min(fade, (i1 - i0) // 2)
        if n > 0:
            ramp = np.linspace(0, 1, n, dtype=np.float32)[:, None]
            a[i0:i0 + n] *= ramp
            a[i1 - n:i1] *= ramp[::-1]
    need = int(total * spf)
    if len(a) < int(segs[-1][2] * spf): a = np.vstack([a, np.zeros((int(segs[-1][2] * spf) - len(a), a.shape[1]), np.float32)])
    a = a[:int(segs[-1][2] * spf)]
    m, msr = sf.read(os.path.join(ROOT, "Assets/Audio/Music/music_intense.ogg"), dtype="float32", always_2d=True)
    if msr != sr:
        idx = (np.arange(int(len(m) * sr / msr)) * msr / sr).astype(int)
        m = m[np.clip(idx, 0, len(m) - 1)]
    if m.shape[1] != a.shape[1]: m = np.repeat(m[:, :1], a.shape[1], axis=1)
    tail = need - len(a)
    mt = m[:tail] * 0.8
    if len(mt) < tail: mt = np.vstack([mt, np.zeros((tail - len(mt), a.shape[1]), np.float32)])
    env = np.ones((tail, 1), np.float32)
    k = int(sr * 0.15); env[:k, 0] = np.linspace(0, 1, k)
    k = int(sr * 1.2); env[-k:, 0] = np.linspace(1, 0, k)
    a = np.vstack([a, mt * env])
    peak = float(np.max(np.abs(a))) or 1.0
    a = a * min(1.0, 0.95 / peak)
    wav = os.path.join(os.path.dirname(out), "_reel_audio.wav")
    sf.write(wav, a, sr)

    w = imageio_ffmpeg.write_frames(out, (W, H), fps=fps, codec="libx264", quality=None, bitrate=None,
                                    output_params=["-crf", "18", "-preset", "slow", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-shortest"],
                                    audio_path=wav, audio_codec="aac", macro_block_size=8)
    w.send(None)
    seg_of = {}
    for n, s0, s1 in segs:
        for f in range(s0, s1): seg_of[f] = (n, f - s0, s1 - s0)
    for f in range(segs[-1][2]):
        fr = Image.open(os.path.join(rdir, f"{f:05d}.jpg")).convert("RGB").resize(REEL, Image.LANCZOS)
        n, i, ln = seg_of[f]
        # vignette-ish top darkening for text legibility
        lay = layers[n]
        t_in = min(1.0, i / 7.0)
        t_out = min(1.0, (ln - 1 - i) / 5.0)
        alpha = min(t_in, t_out)
        if alpha > 0:
            sc = 1.25 - 0.25 * ease_out(t_in)
            l2 = lay.resize((int(lay.width * sc), int(lay.height * sc)), Image.LANCZOS)
            if alpha < 1:
                al = l2.getchannel("A").point(lambda v: int(v * alpha))
                l2.putalpha(al)
            fr = darken(fr, gradient(REEL, 0.10, 0.42, 0.45 * alpha, 0.0))
            fr.paste(l2, (W // 2 - l2.width // 2, int(H * 0.2) - l2.height // 2), l2)
        fr.paste(chip_l, (0, 150), chip_l)
        if i < 3 and f > 0:  # quick white flash on cuts
            fr = Image.blend(fr, Image.new("RGB", REEL, (255, 255, 255)), 0.35 * (1 - i / 3))
        w.send(np.asarray(fr, dtype=np.uint8).tobytes())
    for i in range(end_frames):
        t = min(1.0, i / 9.0)
        fr = end if t >= 1 else Image.blend(Image.new("RGB", REEL, (0, 0, 0)), end, t)
        w.send(np.asarray(fr, dtype=np.uint8).tobytes())
    w.close()
    os.remove(wav)


def main():
    global ROOT
    ROOT = sys.argv[1]
    caps, rcap, out = sys.argv[2], sys.argv[3], sys.argv[4]
    os.makedirs(os.path.join(out, "2_carousel"), exist_ok=True)
    logo_post(caps, out).save(os.path.join(out, "1_logo.png"))
    n = 1
    for i, (name, cx, t, s) in enumerate(FULL):
        full_slide(caps, name, cx, t, s, first=(i == 0)).save(os.path.join(out, "2_carousel", f"{n:02d}_{name.replace('clean_', '')}.png")); n += 1
    for name, cw, t, s in FRAMED:
        framed_slide(caps, name, cw, t, s).save(os.path.join(out, "2_carousel", f"{n:02d}_{name.replace('raw_', '')}.png")); n += 1
    logo_post(caps, out).save(os.path.join(out, "2_carousel", f"{n:02d}_appstore.png"))
    print("ok images")
    rdir = os.path.join(rcap, "reel") if os.path.isdir(os.path.join(rcap, "reel")) else rcap
    if os.path.exists(os.path.join(rdir, "segments.txt")):
        reel(caps, rdir, os.path.join(out, "3_reels.mp4"))
        print("ok reel")


if __name__ == "__main__":
    main()
