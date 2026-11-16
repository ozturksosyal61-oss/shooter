"""Story objective sounds (F8): radio crackle, objective tick, completion stingers, helicopter rotor.

Usage (numpy, scipy, soundfile):  python Tools/synth_objectives.py .

Pure synthesis (plus a light layer of the CC0 explosion clip already in Assets/Audio/SFX for the rotor body), so
every output stays CC0. See CREDITS.md.
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt

SR = 44100
PROJ = sys.argv[1] if len(sys.argv) > 1 else "."
SFX = os.path.join(PROJ, "Assets", "Audio", "SFX")
rng = np.random.default_rng(8)


def t(sec):
    return np.arange(int(round(sec * SR))) / SR


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


def tone(f, sec, harm=((1, 1.0),), decay=0.0):
    tt = t(sec)
    x = sum(np.sin(2 * np.pi * f * h * tt) * a for h, a in harm)
    return x * (np.exp(-tt * decay) if decay else 1.0)


def write(name, x, peak_db=-1.0):
    x = x - np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * 10 ** (peak_db / 20)
    n = min(len(x), int(0.02 * SR))
    x[-n:] *= np.linspace(1, 0, n)
    path = os.path.join(SFX, name + ".ogg")
    with sf.SoundFile(path, "w", SR, 1, format="OGG", subtype="VORBIS", compression_level=0.5) as f:
        f.write(x.astype(np.float32))
    print(f"{name:22s} {len(x) / SR:5.2f}s")


# ---------------------------------------------------------------- radio: squelch click, band-limited crackle, roger beep
for i in range(2):
    sec = 0.55 + 0.1 * i
    hiss = bp(noise(sec), 900, 3400) * 0.35
    # Crackle: sparse random spikes through a narrow radio band.
    crack = np.zeros(int(sec * SR))
    for _ in range(40 + 15 * i):
        p = rng.integers(0, len(crack) - 200)
        crack[p:p + rng.integers(20, 120)] += rng.uniform(-1, 1) * rng.uniform(0.5, 1.4)
    crack = bp(crack, 1200, 4200)
    body = (hiss + crack) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * (6 + 3 * i) * t(sec))))
    body *= env(len(body), 0.005, 0.12)
    click = hp(noise(0.008), 1500) * 1.6
    beep = tone(1250 + 150 * i, 0.07, ((1, 1), (3, 0.2))) * env(len(t(0.07)), 0.003, 0.02) * 0.55
    write(f"radio_static_{i + 1:02d}", mix(click, pad(body, 0.004), pad(beep, sec - 0.02)), -3)

# ---------------------------------------------------------------- hold-zone tick: short bright double blip
blip = tone(1760, 0.06, ((1, 1), (2, 0.3))) * env(len(t(0.06)), 0.002, 0.04)
write("objective_tick", mix(blip, pad(blip * 0.6, 0.09)), -6)

# ---------------------------------------------------------------- objective complete: brassy rising triad
def brass(f, sec):
    tt = t(sec)
    saw = sum(np.sin(2 * np.pi * f * k * tt) / k for k in range(1, 9))
    return lp(saw, 2600) * env(len(tt), 0.02, sec * 0.5)

done = mix(brass(392, 0.22) * 0.8, pad(brass(493.9, 0.22) * 0.8, 0.12), pad(brass(587.3, 0.6), 0.24),
           pad(tone(1174.7, 0.5, decay=6) * 0.25, 0.24))
write("objective_done", done, -2)

# ---------------------------------------------------------------- helicopter story beat: longer fanfare + low swell
big = mix(brass(293.7, 0.3) * 0.7, pad(brass(392, 0.3) * 0.7, 0.16), pad(brass(493.9, 0.3) * 0.75, 0.32),
          pad(brass(587.3, 1.1), 0.5), pad(brass(293.7, 1.1) * 0.5, 0.5),
          lp(noise(1.6), 300) * env(len(t(1.6)), 0.6, 0.6) * 0.25)
write("objective_big", big, -1.5)

# ---------------------------------------------------------------- rotor: thumping blade passes on filtered turbine noise
sec = 1.5
tt = t(sec)
bpf = 13.0  # blade passes per second
phase = (tt * bpf) % 1.0
thump = np.exp(-phase * 9.0) * np.sin(2 * np.pi * 55 * tt)
whoosh = bp(noise(sec), 200, 900) * (0.4 + 0.6 * np.exp(-phase * 5.0))
turbine = bp(noise(sec), 2500, 5000) * 0.08 + np.sin(2 * np.pi * 1900 * tt) * 0.02
x = lp(thump * 1.2 + whoosh * 0.7, 1500) + turbine
write("heli_rotor", x * env(len(x), 0.08, 0.15), -3)
