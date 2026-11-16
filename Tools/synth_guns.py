"""Weapon + economy sounds (F13).

Usage (numpy, scipy, soundfile):  python Tools/synth_guns.py .
  SFX/smg_shot_01..02     rifle_shot at 1.3x, high-passed, short tail          (derived from CC0)
  SFX/hmg_shot_01..02     rifle_shot at 0.82x + low body thump                (derived from CC0)
  SFX/sniper_shot_01      rifle_shot at 0.72x + sharp crack + long echo tail   (derived from CC0)
  SFX/coin                bright double ping (gold counter tick)               (synth)
  SFX/purchase            cash chime: rising bell triad + shimmer              (synth)
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfilt

SR = 44100
PROJ = sys.argv[1] if len(sys.argv) > 1 else "."
SFX = os.path.join(PROJ, "Assets", "Audio", "SFX")
rng = np.random.default_rng(13)


def t(sec):
    return np.arange(int(round(sec * SR))) / SR


def load(name):
    x, sr = sf.read(os.path.join(SFX, name + ".ogg"), always_2d=True)
    x = x.mean(axis=1)
    if sr != SR:
        g = np.gcd(SR, sr)
        x = resample_poly(x, SR // g, sr // g)
    return x


def repitch(x, f):
    return resample_poly(x, 100, int(round(100 * f)))


def lp(x, hz):
    return sosfilt(butter(4, hz, "low", fs=SR, output="sos"), x)


def hp(x, hz):
    return sosfilt(butter(2, hz, "high", fs=SR, output="sos"), x)


def env(n, a=0.002, r=0.1):
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


def trim(x, sec):
    x = x[: int(sec * SR)].copy()
    n = min(len(x), int(0.04 * SR))
    x[-n:] *= np.linspace(1, 0, n)
    return x


def write(name, x, peak_db=-1.0):
    x = x - np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * 10 ** (peak_db / 20)
    path = os.path.join(SFX, name + ".ogg")
    with sf.SoundFile(path, "w", SR, 1, format="OGG", subtype="VORBIS", compression_level=0.5) as f:
        x = x.astype(np.float32)
        for i in range(0, len(x), 8192):
            f.write(x[i:i + 8192])
    print(f"{name:16s} {len(x) / SR:5.2f}s  {os.path.getsize(path) // 1024} KB")


shots = [load(f"rifle_shot_0{i}") for i in (1, 2, 3)]

for i in range(2):
    write(f"smg_shot_{i + 1:02d}", trim(hp(repitch(shots[i], 1.3), 350), 0.32), -2)

for i in range(2):
    s = repitch(shots[i + 1], 0.82)
    thump = np.sin(2 * np.pi * np.cumsum(np.linspace(110, 45, int(0.25 * SR))) / SR) * np.exp(-t(0.25) * 14) * 0.6
    write(f"hmg_shot_{i + 1:02d}", trim(mix(s, thump), 0.55), -1.5)

s = repitch(shots[0], 0.72)
crack = hp(rng.standard_normal(int(0.03 * SR)), 2500) * env(int(0.03 * SR), 0.0005, 0.025) * 0.8
echo = np.zeros(int(1.2 * SR))
for k, (dly, g) in enumerate([(0.18, 0.35), (0.37, 0.22), (0.6, 0.12)]):
    e = lp(s, 1800 - 400 * k) * g
    st = int(dly * SR)
    echo[st:st + len(e)] += e[: max(0, len(echo) - st)]
write("sniper_shot_01", trim(mix(crack, s, echo), 1.2), -1)


def bell(f, sec, decay):
    tt = t(sec)
    return sum(np.sin(2 * np.pi * f * h * tt) * a for h, a in [(1, 1), (2.76, 0.35), (5.4, 0.12)]) * np.exp(-tt * decay)


coin = mix(bell(1975, 0.12, 30), np.concatenate([np.zeros(int(0.045 * SR)), bell(2637, 0.14, 26)]))
write("coin", coin * env(len(coin), 0.001, 0.04), -8)

notes = [1047, 1319, 1568, 2093]
chime = np.zeros(int(0.9 * SR))
for i, f in enumerate(notes):
    b = bell(f, 0.7, 6) * 0.8
    st = int(i * 0.07 * SR)
    chime[st:st + len(b)] += b[: len(chime) - st]
shimmer = hp(rng.standard_normal(len(chime)), 6000) * np.exp(-t(0.9) * 5) * 0.05
write("purchase", chime + shimmer, -3)
