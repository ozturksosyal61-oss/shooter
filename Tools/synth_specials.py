"""Special-zombie, card and new-weapon sounds (F6/F7).

Usage (numpy, scipy, soundfile):  python Tools/synth_specials.py .

Everything here is either pure synthesis or a pitch/time transform + layering of clips that are already
in Assets/Audio/SFX (all CC0, see CREDITS.md), so every output stays CC0.
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfilt

SR = 44100
PROJ = sys.argv[1] if len(sys.argv) > 1 else "."
SFX = os.path.join(PROJ, "Assets", "Audio", "SFX")
rng = np.random.default_rng(42)


def t(sec):
    return np.arange(int(round(sec * SR))) / SR


def load(name):
    x, sr = sf.read(os.path.join(SFX, name + ".ogg"), always_2d=True)
    x = x.mean(axis=1)
    if sr != SR:
        g = np.gcd(SR, sr)
        x = resample_poly(x, SR // g, sr // g)
    return x


def repitch(x, factor):
    """factor < 1 → lower and longer (tape style)."""
    up, down = 100, int(round(100 * factor))
    return resample_poly(x, up, down)


def lp(x, hz, order=4):
    return sosfilt(butter(order, hz, "low", fs=SR, output="sos"), x)


def hp(x, hz, order=2):
    return sosfilt(butter(order, hz, "high", fs=SR, output="sos"), x)


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def env(n, a=0.005, r=0.1):
    e = np.ones(n)
    na, nr = max(1, int(a * SR)), max(1, int(r * SR))
    e[:na] = np.linspace(0, 1, na)
    e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def sweep(f0, f1, sec, curve=1.0):
    tt = t(sec)
    f = f0 + (f1 - f0) * (tt / sec) ** curve
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def noise(sec):
    return rng.standard_normal(int(round(sec * SR)))


def write(name, x, peak_db=-1.0):
    x = x - np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * 10 ** (peak_db / 20)
    n = min(len(x), int(0.03 * SR))
    x[-n:] *= np.linspace(1, 0, n)
    path = os.path.join(SFX, name + ".ogg")
    with sf.SoundFile(path, "w", SR, 1, format="OGG", subtype="VORBIS", compression_level=0.5) as f:
        f.write(x.astype(np.float32))
    print(f"{name:22s} {len(x) / SR:5.2f}s")


# ---------------------------------------------------------------- shield (armoured brute)
for i, src in enumerate(["zombie_scream_01", "zombie_scream_03"], 1):
    r = repitch(load(src), 0.62)
    sub = sweep(62, 48, len(r) / SR) * env(len(r), 0.05, 0.4) * 0.5
    write(f"shield_roar_{i:02d}", lp(mix(r, sub), 3500), -1.5)

# Bullet glancing off the car-door shield: metal ping + whistling ricochet.
for i, (f0, f1) in enumerate([(3400, 1700), (2900, 1500), (3800, 2100)], 1):
    ping = sum(np.sin(2 * np.pi * f * t(0.4)) * np.exp(-t(0.4) * d) * a
               for f, d, a in [(1870, 9, 0.5), (2730, 12, 0.35), (4100, 16, 0.25)])
    whistle = sweep(f0, f1, 0.32, 0.6) * env(len(t(0.32)), 0.01, 0.2) * 0.55
    click = hp(noise(0.012), 2500) * 0.9
    write(f"shield_ricochet_{i:02d}", mix(click, ping, np.concatenate([np.zeros(int(0.01 * SR)), whistle])), -3)

# ---------------------------------------------------------------- bomb (bloated)
def gurgle(sec, rate, lo, hi):
    out = np.zeros(int(sec * SR))
    pos = 0
    while pos < len(out) - 4000:
        d = rng.uniform(0.02, 0.06)
        f = rng.uniform(lo, hi)
        b = sweep(f, f * rng.uniform(1.3, 1.9), d) * env(len(t(d)), 0.003, d * 0.6)
        out[pos:pos + len(b)] += b * rng.uniform(0.4, 1.0)
        pos += int(SR / rate * rng.uniform(0.5, 1.5))
    return lp(out, 1800)

moan = repitch(load("zombie_moan_02"), 0.8)
write("bomb_spawn", mix(moan * 0.7, gurgle(len(moan) / SR, 14, 120, 320) * 0.8), -2)

# Swell before the blast: accelerating beeps on a rising, bubbling whine (~1.1 s).
sec = 1.1
tt = t(sec)
whine = sweep(260, 1100, sec, 1.6) * (0.25 + 0.75 * tt / sec)
beat = 0.5 + 0.5 * np.sign(np.sin(2 * np.pi * np.cumsum(4 + 22 * (tt / sec) ** 2) / SR))
write("bomb_fuse", mix(whine * beat * 0.6, gurgle(sec, 26, 160, 520) * 0.6) * env(len(tt), 0.01, 0.03), -2)

# Wet, heavier blast variant layered on the existing explosion.
boom = load("explosion")
splash = lp(noise(0.7), 900) * np.exp(-t(0.7) * 5) * 0.6
write("bomb_burst", mix(boom, repitch(boom, 0.8) * 0.6, splash), -0.5)

# ---------------------------------------------------------------- tongue (long-reach grabber)
r = repitch(load("zombie_scream_02"), 1.35)
hiss = bp(noise(len(r) / SR), 3000, 7000) * env(len(r), 0.1, 0.3) * 0.25
write("tongue_spawn", mix(r, hiss), -2)

# Telegraph: two short rising warning chirps.
chirp = sweep(700, 1400, 0.09) * env(len(t(0.09)), 0.004, 0.04)
write("tongue_warn", np.concatenate([chirp, np.zeros(int(0.06 * SR)), chirp]) * 0.8, -4)

# Lash: whip crack — falling band-pass noise + snap.
n = noise(0.28)
lash = np.zeros_like(n)
for k, f in enumerate(np.linspace(5000, 700, 8)):
    a, b = int(k * len(n) / 8), int((k + 1) * len(n) / 8)
    lash[a:b] = bp(n, f * 0.7, f * 1.3)[a:b]
lash *= env(len(n), 0.01, 0.12)
write("tongue_lash", mix(lash, hp(noise(0.01), 3000) * 1.5), -2)

# Grab: wet slap + low thump.
slap = lp(noise(0.18), 2200) * np.exp(-t(0.18) * 25)
thump = np.sin(2 * np.pi * 90 * t(0.25)) * np.exp(-t(0.25) * 14)
write("tongue_grab", mix(slap, thump * 0.8), -2)

# Snap: rubber band twang falling + crack.
twang = sweep(420, 90, 0.35, 0.5) * np.exp(-t(0.35) * 9)
crack = hp(noise(0.015), 2000)
write("tongue_snap", mix(crack * 1.2, twang * 0.9, lp(noise(0.3), 1500) * np.exp(-t(0.3) * 12) * 0.4), -2)

# ---------------------------------------------------------------- weapons / roguelite
shot = load("rifle_shot_01")
for i, src in enumerate(["rifle_shot_01", "rifle_shot_02"], 1):
    s = repitch(load(src), 0.7)
    body = lp(noise(0.35), 700) * np.exp(-t(0.35) * 10)
    write(f"shotgun_{i:02d}", mix(s, body * 0.8, np.sin(2 * np.pi * 70 * t(0.2)) * np.exp(-t(0.2) * 18) * 0.6), -1)
write("turret_shot", hp(repitch(shot, 1.3), 300), -6)

whoosh = bp(noise(0.3), 400, 1800) * np.sin(np.pi * t(0.3) / 0.3) ** 2
write("grenade_throw", whoosh, -6)

# Level up: bright arpeggio with a shimmer tail.
notes = [523.25, 659.25, 783.99, 1046.5]
arp = np.zeros(int(0.9 * SR))
for k, f in enumerate(notes):
    tone = sum(np.sin(2 * np.pi * f * h * t(0.6)) * a for h, a in [(1, 1), (2, 0.35), (3, 0.15)]) * np.exp(-t(0.6) * 6)
    s = int(k * 0.07 * SR)
    arp[s:s + len(tone)] += tone
write("level_up", arp, -2)

ding = sum(np.sin(2 * np.pi * f * t(0.5)) * a * np.exp(-t(0.5) * d) for f, a, d in [(1318.5, 1, 7), (1975.5, 0.4, 10)])
write("card_pick", mix(bp(noise(0.12), 800, 4000) * env(len(t(0.12)), 0.03, 0.06) * 0.3, ding), -3)

# Explosive barrel: deeper, metallic-tail explosion.
clang = sum(np.sin(2 * np.pi * f * t(0.8)) * a * np.exp(-t(0.8) * d) for f, a, d in [(310, 0.5, 5), (520, 0.3, 7), (870, 0.2, 9)])
write("barrel_explode", mix(repitch(boom, 0.85), boom * 0.6, clang * 0.35), -0.5)
