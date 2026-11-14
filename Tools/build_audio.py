"""Builds Assets/Audio from downloaded CC0 packs + Python-synthesised sounds.

Usage (needs numpy, scipy, soundfile):
  python Tools/build_audio.py <source_dir> <project_dir>

<source_dir> layout (as downloaded, see CREDITS.md for URLs):
  x_impact-sounds/, x_interface-sounds/, x_sci-fi-sounds/, x_rpg-audio/, x_digital-audio/, x_music-jingles/  (Kenney)
  oga/fire/Prepared SFX Library/{AR-15,AK-47}/*.wav    (The Free Firearm Sound Library)
  oga/assaultriflereload1_0.wav                         (Gun reload sounds)
  oga/x_Voice Effects Zombie-Skeleton-Monster Human Male/...
  oga/x_undead-moans/, oga/x_zombienoises/, oga/x_monster_-_starninjas/, oga/x_zombies/zombies/
  music/fight_looped.ogg, music/wastelands.ogg

Every output: mono (music: stereo), 44.1 kHz, trimmed, peak-normalised, short fade-out, Ogg Vorbis.
"""
import glob
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import resample_poly, butter, sosfilt

SR = 44100
SRC, PROJ = sys.argv[1], sys.argv[2]
OUT_SFX = os.path.join(PROJ, "Assets", "Audio", "SFX")
OUT_MUS = os.path.join(PROJ, "Assets", "Audio", "Music")
os.makedirs(OUT_SFX, exist_ok=True)
os.makedirs(OUT_MUS, exist_ok=True)
rng = np.random.default_rng(7)
written = []


def src(*parts):
    p = os.path.join(SRC, *parts)
    hits = glob.glob(p, recursive=True)
    if not hits:
        raise FileNotFoundError(p)
    return sorted(hits)[0]


def load(path, mono=True):
    x, sr = sf.read(path, always_2d=True)
    x = x.mean(axis=1) if mono else x
    if sr != SR:
        g = np.gcd(SR, sr)
        x = resample_poly(x, SR // g, sr // g, axis=0)
    return x.astype(np.float64)


def trim(x, db=-45.0, pad=0.004):
    env = np.abs(x) if x.ndim == 1 else np.abs(x).max(axis=1)
    thr = 10 ** (db / 20) * env.max()
    idx = np.where(env > thr)[0]
    if len(idx) == 0:
        return x
    a = max(0, idx[0] - int(pad * SR))
    b = min(len(x), idx[-1] + int(0.02 * SR))
    return x[a:b]


def fade(x, out=0.03, inn=0.002):
    x = x.copy()
    n_out, n_in = min(len(x), int(out * SR)), min(len(x), int(inn * SR))
    if n_out:
        x[-n_out:] *= np.linspace(1, 0, n_out)[:, None] if x.ndim > 1 else np.linspace(1, 0, n_out)
    if n_in:
        x[:n_in] *= np.linspace(0, 1, n_in)[:, None] if x.ndim > 1 else np.linspace(0, 1, n_in)
    return x


def norm(x, peak_db):
    m = np.abs(x).max()
    return x if m == 0 else x * (10 ** (peak_db / 20) / m)


def write(name, x, peak_db=-1.0, music=False, quality=0.4):
    x = norm(x, peak_db)
    path = os.path.join(OUT_MUS if music else OUT_SFX, name + ".ogg")
    x = np.clip(x, -1, 1).astype(np.float32)
    ch = 1 if x.ndim == 1 else x.shape[1]
    # Write in blocks: libsndfile's Vorbis encoder overflows the stack on large single writes.
    with sf.SoundFile(path, "w", SR, ch, format="OGG", subtype="VORBIS", compression_level=1.0 - quality) as f:
        for i in range(0, len(x), 8192):
            f.write(x[i:i + 8192])
    written.append(path)


def clip(path, peak=-1.0, maxlen=None, fade_out=0.04, db=-45):
    x = trim(load(path), db)
    if maxlen:
        x = x[: int(maxlen * SR)]
    return fade(x, fade_out)


def onsets(x, thr=0.35, gap=0.12):
    env = np.convolve(np.abs(x), np.ones(int(0.004 * SR)) / int(0.004 * SR), mode="same")
    t = thr * env.max()
    out, last = [], -1e9
    for i in np.where((env[1:] > t) & (env[:-1] <= t))[0]:
        if i - last > gap * SR:
            out.append(i)
            last = i
    return out


def lowpass(x, hz):
    return sosfilt(butter(4, hz, "low", fs=SR, output="sos"), x)


def highpass(x, hz):
    return sosfilt(butter(2, hz, "high", fs=SR, output="sos"), x)


# ------------------------------------------------------------------ weapon
shots = []
for f in sorted(glob.glob(os.path.join(SRC, "oga", "fire", "Prepared SFX Library", "AR-15", "*.wav"))) + \
         sorted(glob.glob(os.path.join(SRC, "oga", "fire", "Prepared SFX Library", "AK-47", "*.wav"))):
    x = load(f)
    for o in onsets(x)[:3]:
        seg = x[max(0, o - int(0.003 * SR)): o + int(0.55 * SR)]
        if len(seg) > 0.3 * SR:
            shots.append(seg)
picked = [shots[i] for i in np.linspace(0, len(shots) - 1, 3).astype(int)]
for i, s in enumerate(picked):
    s = fade(s, 0.3)
    s = s * np.exp(-np.linspace(0, 3.0, len(s)))  # tighten the tail for rapid fire
    write(f"rifle_shot_{i + 1:02d}", s, -1.0)

# Reload parts sliced from the CC0 assault-rifle reload recording.
rl = load(os.path.join(SRC, "oga", "assaultriflereload1_0.wav"))
# Event times read from the recording's envelope: mag out ~0.15 s, mag in ~1.03 s, bolt ~1.15 s.
for n, a, b in [("reload_mag_out", 0.13, 0.45), ("reload_mag_in", 1.02, 1.15), ("reload_bolt", 1.15, 1.56)]:
    write(n, fade(rl[int(a * SR): int(b * SR)], 0.03), -3.0)

# ------------------------------------------------------------------ impacts
K = lambda pack, name: src(f"x_{pack}", "**", name + ".ogg")
slime = [trim(load(K("sci-fi-sounds", f"slime_00{i}"))) for i in range(2)]
for i in range(4):
    punch = trim(load(K("impact-sounds", f"impactPunch_medium_00{i}")))
    s = slime[i % 2][: len(punch) * 2]
    n = max(len(punch), len(s))
    mix = np.zeros(n)
    mix[: len(punch)] += punch
    mix[: len(s)] += 0.6 * s * (1.0 + 0.15 * i)
    write(f"hit_flesh_{i + 1:02d}", fade(mix, 0.05), -3.0)
for i in range(3):
    write(f"hit_crit_{i + 1:02d}", clip(K("impact-sounds", f"impactPunch_heavy_00{i}")), -1.5)
    write(f"hit_concrete_{i + 1:02d}", clip(K("impact-sounds", f"impactGeneric_light_00{i}")), -6.0)
    write(f"hit_metal_{i + 1:02d}", clip(K("impact-sounds", f"impactMetal_light_00{i}")), -6.0)

# ------------------------------------------------------------------ zombies / player voices
V = lambda n: os.path.join(SRC, "oga", "x_Voice Effects Zombie-Skeleton-Monster Human Male",
                           "Voice Effects Zombie-Skeleton-Monster Human Male", n + ".wav")
for i, f in enumerate(sorted(glob.glob(os.path.join(SRC, "oga", "x_undead-moans", "*.ogg")))[:4] +
                      [os.path.join(SRC, "oga", "x_zombienoises", "zombienoise1.ogg"),
                       os.path.join(SRC, "oga", "x_zombienoises", "zombienoise2.ogg")]):
    write(f"zombie_moan_{i + 1:02d}", clip(f, maxlen=2.2, fade_out=0.25), -3.0)
for i, n in enumerate(["monster.10", "monster.11", "monster.8", "monster.7"]):
    write(f"zombie_scream_{i + 1:02d}", clip(os.path.join(SRC, "oga", "x_monster_-_starninjas", n + ".ogg"), fade_out=0.15), -1.5)
for i, n in enumerate(["zombieYell1", "zombieYell3", "zombieYell5", "zombieYell8"]):
    write(f"zombie_attack_{i + 1:02d}", clip(V(n)), -2.0)
for i in range(4):
    write(f"zombie_death_{i + 1:02d}", clip(V(f"zombieDeath{i + 1}"), fade_out=0.1), -2.0)
for i in range(4):
    write(f"player_hurt_{i + 1:02d}", clip(V(f"humanYell{i + 1}")), -2.0)
write("player_death_01", clip(V("humanDeath1"), fade_out=0.1), -1.5)
write("player_death_02", clip(V("humanDeath2"), fade_out=0.1), -1.5)

# ------------------------------------------------------------------ movement
for i in range(5):
    write(f"footstep_{i + 1:02d}", clip(K("impact-sounds", f"footstep_concrete_00{i}")), -9.0)
write("vault_jump", clip(K("rpg-audio", "cloth1"), fade_out=0.08), -8.0)
for i in range(2):
    write(f"vault_land_{i + 1:02d}", clip(K("impact-sounds", f"impactSoft_heavy_00{i}")), -4.0)

# ------------------------------------------------------------------ UI / feedback (Kenney)
write("ui_click", clip(K("interface-sounds", "click_001")), -6.0)
write("kill_streak", clip(K("digital-audio", "powerUp7"), fade_out=0.1), -3.0)
write("gameover_jingle", clip(K("music-jingles", "jingles_STEEL14"), fade_out=0.2), -2.0)
write("explosion", clip(K("sci-fi-sounds", "lowFrequency_explosion_000"), fade_out=0.3), -1.0)

# ------------------------------------------------------------------ synthesised
t = lambda d: np.arange(int(d * SR)) / SR


def env_ad(n, a=0.003, d=0.2):
    tt = np.arange(n) / SR
    return np.minimum(tt / a, 1.0) * np.exp(-tt / d)


# Dry fire: tiny spring click + metallic ping.
d = 0.12
x = highpass(rng.standard_normal(len(t(d))), 2500) * env_ad(len(t(d)), 0.0005, 0.006)
x += 0.5 * np.sin(2 * np.pi * 3300 * t(d)) * env_ad(len(t(d)), 0.001, 0.025)
write("dry_fire", fade(x, 0.01), -6.0)

# Low-ammo warning: two soft square-ish beeps.
beep = lambda f, d: np.tanh(3 * np.sin(2 * np.pi * f * t(d))) * env_ad(len(t(d)), 0.004, 0.05)
x = np.concatenate([beep(1320, 0.08), np.zeros(int(0.05 * SR)), beep(1320, 0.08)])
write("low_ammo", lowpass(x, 6000), -8.0)

# Combo ding: bell partials with a small upward glide (code raises pitch per multiplier).
d = 0.6
tt = t(d)
glide = 1 + 0.03 * np.minimum(tt / 0.05, 1)
x = sum(a * np.sin(2 * np.pi * f * glide * tt) * np.exp(-tt / dec)
        for f, a, dec in [(988, 1.0, 0.35), (1976, 0.45, 0.18), (2964, 0.25, 0.1), (3952, 0.12, 0.06)])
write("combo_up", fade(x * np.minimum(tt / 0.002, 1), 0.05), -6.0)

# Heartbeat (lub-dub), loopable 0.85 s.
def thump(f0, d):
    tt = t(d)
    ph = 2 * np.pi * np.cumsum(f0 * (1 + 1.5 * np.exp(-tt / 0.02))) / SR
    return np.sin(ph) * env_ad(len(tt), 0.004, 0.07)
x = np.zeros(int(0.85 * SR))
lub, dub = thump(52, 0.25), thump(46, 0.22)
x[: len(lub)] += lub
o = int(0.26 * SR)
x[o: o + len(dub)] += 0.75 * dub
write("heartbeat", lowpass(x, 400), -3.0)

# ------------------------------------------------------------------ music
fight, _ = sf.read(os.path.join(SRC, "music", "fight_looped.ogg"), always_2d=True)
write("music_intense", fight.mean(axis=1), -2.0, music=True, quality=0.35)

wl = load(os.path.join(SRC, "music", "wastelands.ogg"), mono=False)
L, XF = int(60 * SR), int(4 * SR)
start = int(20 * SR)
seg = wl[start: start + L + XF].copy()
ramp = np.linspace(0, 1, XF)[:, None]
loop = seg[:L].copy()
loop[:XF] = seg[:XF] * ramp + seg[L: L + XF] * (1 - ramp)  # seamless crossfaded loop
write("music_calm", loop, -3.0, music=True, quality=0.3)

total = sum(os.path.getsize(p) for p in written)
for p in written:
    print(f"AUDIO: {os.path.getsize(p) // 1024:5d} KB {os.path.relpath(p, PROJ)}")
print(f"AUDIO: total {total / 1024 / 1024:.2f} MB in {len(written)} files")
