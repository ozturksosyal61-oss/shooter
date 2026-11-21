"""Weather sounds (1.1.0). Usage (numpy, scipy, soundfile):  python Tools/synth_weather.py .
  SFX/rain_loop   6 s seamless rain: filtered noise bed + sparse droplet ticks (synth, CC0)
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt

SR = 44100
PROJ = sys.argv[1] if len(sys.argv) > 1 else "."
OUT = os.path.join(PROJ, "Assets", "Audio", "SFX", "rain_loop.ogg")
rng = np.random.default_rng(7)
n = int(6.0 * SR)
pad = int(0.5 * SR)
x = rng.standard_normal(n + pad)
x = sosfilt(butter(2, [500, 7000], "bandpass", fs=SR, output="sos"), x) * 0.35
# droplets: short bright ticks
for _ in range(900):
    i = rng.integers(0, n + pad - 400)
    k = np.exp(-np.arange(400) / 40.0) * rng.uniform(0.05, 0.25)
    x[i:i + 400] += sosfilt(butter(2, 2500, "highpass", fs=SR, output="sos"), rng.standard_normal(400)) * k
# seamless: crossfade the tail into the head
fade = np.linspace(0, 1, pad)
y = x[:n].copy()
y[:pad] = y[:pad] * fade + x[n:n + pad] * (1 - fade)
y /= np.max(np.abs(y)) / 10 ** (-6 / 20)
sf.write(OUT, y.astype(np.float32), SR, format="OGG", subtype="VORBIS")
print("rain_loop", f"{n / SR:.1f}s", os.path.getsize(OUT) // 1024, "KB")
