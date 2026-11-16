"""Boss sounds + boss music layer (F9, Kasap).

Usage (numpy, scipy, soundfile):  python Tools/synth_boss.py .

Outputs (all CC0: pure synthesis, or derived from CC0 clips already in Assets/Audio/SFX — see CREDITS.md):
  SFX/boss_roar_01..02   zombie_scream_02/04 slowed to ~0.55x + growl (AM saw) + sub-bass
  SFX/boss_step_01..03   low thump + gravel noise
  SFX/boss_charge        rising roar + rushing noise
  SFX/boss_slam_01..02   explosion at 0.6x + sub drop + debris crackle
  Music/music_boss       19.2 s seamless loop: war drums, low drone, metal hits (100 BPM)
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfilt

SR = 44100
PROJ = sys.argv[1] if len(sys.argv) > 1 else "."
SFX = os.path.join(PROJ, "Assets", "Audio", "SFX")
MUSIC = os.path.join(PROJ, "Assets", "Audio", "Music")
rng = np.random.default_rng(9)


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


def noise(sec):
    return rng.standard_normal(int(round(sec * SR)))


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def pad(x, sec):
    return np.concatenate([np.zeros(int(sec * SR)), x])


def norm(x, peak_db):
    x = x - np.mean(x)
    return x / (np.max(np.abs(x)) + 1e-9) * 10 ** (peak_db / 20)


def write(folder, name, x, peak_db=-1.0, fade=True, q=0.5):
    x = norm(x, peak_db)
    if fade:
        n = min(len(x), int(0.02 * SR))
        x[-n:] *= np.linspace(1, 0, n)
    path = os.path.join(folder, name + ".ogg")
    with sf.SoundFile(path, "w", SR, 1, format="OGG", subtype="VORBIS", compression_level=q) as f:
        # Block-wise: libsndfile's Vorbis encoder crashes on one huge write.
        x = x.astype(np.float32)
        for i in range(0, len(x), 8192):
            f.write(x[i:i + 8192])
    print(f"{name:18s} {len(x) / SR:5.2f}s  {os.path.getsize(path) // 1024} KB")


def growl(sec, f0, rate):
    tt = t(sec)
    saw = sum(np.sin(2 * np.pi * f0 * k * tt + rng.uniform(0, 6)) / k for k in range(1, 12))
    am = 0.55 + 0.45 * np.sin(2 * np.pi * rate * tt) * (0.7 + 0.3 * rng.standard_normal(len(tt)).clip(-1, 1))
    return lp(saw * am, 900)


def sub(sec, f0, f1, decay):
    tt = t(sec)
    f = np.linspace(f0, f1, len(tt))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * decay)


# ---------------------------------------------------------------- roar: huge slowed scream + growl + sub
for i, src in enumerate(["zombie_scream_02", "zombie_scream_04"]):
    base = repitch(load(src), 0.55 - 0.04 * i)
    sec = len(base) / SR
    g = growl(sec, 52 + 8 * i, 23 + 4 * i) * env(len(t(sec)), 0.15, sec * 0.5)
    s = sub(sec, 70, 38, 1.2) * env(len(t(sec)), 0.08, sec * 0.5)
    write(SFX, f"boss_roar_{i + 1:02d}", mix(lp(base, 3200) * 1.0, g[:len(base)] * 0.45, s[:len(base)] * 0.7), -1)

# ---------------------------------------------------------------- footsteps: low thump + gravel
for i in range(3):
    sec = 0.45
    thump = sub(sec, 95 - 8 * i, 42, 11) * 1.2
    grit = bp(noise(0.12), 300 + 120 * i, 2400) * env(len(t(0.12)), 0.002, 0.1) * 0.35
    write(SFX, f"boss_step_{i + 1:02d}", mix(thump, grit), -3)

# ---------------------------------------------------------------- charge: roar burst + rushing wind
sec = 1.4
rush = bp(noise(sec), 180, 1500) * np.linspace(0.2, 1.0, len(t(sec))) * env(len(t(sec)), 0.05, 0.3)
roar = repitch(load("zombie_scream_01"), 0.62)[: int(sec * SR)]
roar = lp(roar, 2500) * env(len(roar), 0.01, 0.4)
g = growl(sec, 60, 30) * env(len(t(sec)), 0.02, 0.5)
write(SFX, "boss_charge", mix(roar, rush * 0.5, g * 0.4), -1.5)

# ---------------------------------------------------------------- slam: slowed explosion + sub drop + debris
boom = repitch(load("explosion"), 0.6)
for i in range(2):
    sec = 1.6
    s = sub(sec, 80 - 10 * i, 28, 2.6) * 1.1
    deb = np.zeros(int(sec * SR))
    for _ in range(40):
        p = rng.integers(int(0.05 * SR), int(1.2 * SR))
        n = rng.integers(80, 500)
        deb[p:p + n] += rng.standard_normal(n) * np.exp(-np.arange(n) / (n / 4)) * rng.uniform(0.2, 0.8)
    deb = bp(deb, 900, 5000) * 0.35
    b = lp(boom, 2200 - 400 * i)[: int(sec * SR)]
    write(SFX, f"boss_slam_{i + 1:02d}", mix(b * 0.9, s, deb), -1)

# ---------------------------------------------------------------- music: 100 BPM war-drum loop over a low drone
BPM = 100
beat = 60 / BPM
bars = 8
L = int(round(bars * 4 * beat * SR))
out = np.zeros(L)


def place(x, at):
    """Adds x at sample `at`, wrapping the tail to the start (seamless loop)."""
    at %= L
    n = len(x)
    end = min(L, at + n)
    out[at:end] += x[: end - at]
    if at + n > L:
        out[: at + n - L] += x[end - at:]


kick = mix(sub(0.6, 110, 40, 7), lp(noise(0.03), 900) * env(len(t(0.03)), 0.001, 0.02) * 0.3)
tom = sub(0.45, 160, 90, 9) * 0.55
taiko = lp(sub(0.9, 75, 45, 4) + bp(noise(0.9), 60, 400) * np.exp(-t(0.9) * 6) * 0.6, 600) * 0.9
metal = sum(np.sin(2 * np.pi * f * t(1.2)) * a for f, a in [(523, 0.5), (1342, 0.3), (2210, 0.2), (3307, 0.12)]) * np.exp(-t(1.2) * 4) * 0.22

for bar in range(bars):
    b0 = bar * 4 * beat
    # Kick on 1 and the "and" of 2, 3; toms fill on 4 (busier every 4th bar).
    for pos in (0, 1.5, 2):
        place(kick, int((b0 + pos * beat) * SR))
    for k in range(4 if bar % 4 == 3 else 2):
        place(tom * (0.8 + 0.1 * k), int((b0 + (3 + k * (0.25 if bar % 4 == 3 else 0.5)) * beat) * SR))
    if bar % 2 == 0:
        place(taiko, int(b0 * SR))
    if bar % 4 == 2:
        place(metal, int((b0 + 2 * beat) * SR))

# Drone: two detuned low saws (D1 / A1) + a slow filter swell, phase-continuous over the loop.
tt = np.arange(L) / SR
cycles = lambda f: round(f * L / SR) * SR / L  # snap frequency so it loops exactly
d1, a1 = cycles(36.7), cycles(55.0)
drone = sum(np.sin(2 * np.pi * d1 * k * tt) / k for k in range(1, 8)) + 0.6 * sum(np.sin(2 * np.pi * a1 * k * tt) / k for k in range(1, 6))
swell = 0.6 + 0.4 * np.sin(2 * np.pi * tt / (L / SR))
drone = lp(np.concatenate([drone, drone]), 380)[L:] * swell * 0.28
out += drone
write(MUSIC, "music_boss", out, -2, fade=False, q=0.4)
