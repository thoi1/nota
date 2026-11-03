# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
"""Rebuild native ODF golden vectors with NumPy/SciPy, never used by the app.

Adapted from CPJKU/SuperFlux SuperFlux.py (BSD-2-Clause), Sebastian Böck,
Copyright (c) 2012 - 2014. Full upstream notice in LICENSES/SuperFlux.txt.
Follows original Filter/Spectrogram/SpectralODF calculations at 44.1 kHz.
Run with numpy/scipy installed; output is committed, tests need neither package.
"""
from pathlib import Path
import numpy as np
from scipy.ndimage import maximum_filter

sr, size, fps = 44100, 2048, 200
audio = np.zeros(8192, dtype=np.float32)
for i in range(1000, len(audio)):
    period = 16 if i < 4096 else 17
    phase = (i - 1000) % period
    value = (phase if phase < period // 2 else period - phase) / 16.0 - 0.25
    envelope = min(1, (i - 1000) / 32.0) * min(1, (8192 - i) / 2048.0)
    audio[i] = value * envelope

factor = 2 ** (1 / 24.0)
frequencies = [440.0]
freq = 440.0
while freq <= 16000:
    freq *= factor
    frequencies.append(freq)
freq = 440.0
while freq >= 27.5:
    freq /= factor
    frequencies.append(freq)
corners = np.unique(np.round(np.array(sorted(frequencies)) / (sr / size)).astype(int))
corners = corners[corners < size // 2]
bank = np.zeros((size // 2, len(corners) - 2))
for b, (start, mid, stop) in enumerate(zip(corners, corners[1:], corners[2:])):
    bank[start:mid, b] = np.linspace(0, 1, mid - start, endpoint=False)
    bank[mid:stop, b] = np.linspace(1, 0, stop - mid, endpoint=False)

window = np.hanning(size)
spectra = []
for frame in range(int(np.ceil(len(audio) / (sr / fps)))):
    start = int(frame * sr / fps - size / 2)
    signal = np.zeros(size)
    lo, hi = max(0, start), min(len(audio), start + size)
    signal[lo - start:hi - start] = audio[lo:hi]
    spectra.append(np.log10(1 + np.abs(np.fft.fft(signal * window)[:size // 2]) @ bank))
spectra = np.asarray(spectra, dtype=np.float32)
lag = max(1, round((size / 2 - np.argmax(window > .5)) / (sr / fps)))
diff = np.zeros_like(spectra)
diff[lag:] = np.maximum(0, spectra[lag:] - maximum_filter(spectra, size=(1, 3))[:-lag])
golden = np.sum(diff, axis=1)
out = Path(__file__).with_name("odf-golden.h")
out.write_text("// SPDX-License-Identifier: AGPL-3.0-only\n"
    "// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.\n"
    "// Generated with generate.py, original SuperFlux spectral equations; 44.1 kHz.\n"
    "constexpr double kSuperFluxGolden[] = {\n    "
    + ",\n    ".join(f"{v:.10g}" for v in golden) + "\n};\n")
print(f"Wrote {len(golden)} reference frames, lag={lag}, bands={bank.shape[1]}")
