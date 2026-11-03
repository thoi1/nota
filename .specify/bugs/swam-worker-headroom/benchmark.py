#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
"""Extract the buffer and SWAM follow-ups from local logs; stdlib plots only."""

import importlib.util
import json
from pathlib import Path

out = Path(__file__).resolve().parent
root = out.parents[2]
logs = root / "src/native/nota.engine/build"


def records(file, prefix, key):
    result = {}
    for line in (logs / file).read_text(encoding="utf-8-sig").splitlines():
        if line.startswith(prefix + " "):
            row = json.loads(line[len(prefix) + 1:])
            result[row[key]] = row
    if not result:
        raise ValueError("No measurements in " + file)
    return result


data = {
    "machine": "Windows x64, Ryzen 9 7900X, 12 cores / 24 logical processors",
    "settings": {"frames": 512, "sampleRate": 48000, "deadlineMs": 512 / 48},
    "method": "Saved plug-in states and MIDI, device-path hook into memory, 120 warm-up / 400 measured callbacks",
    "limits": "Physical ASIO playback, audible popping and Studio One are not measured by this profile",
    "before": records("project-512-after.log", "SAVED_PROJECT_PROFILE", "mode"),
    "after": records("project-512-final.log", "SAVED_PROJECT_PROFILE", "mode"),
}
(out / "benchmark.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
spec = importlib.util.spec_from_file_location("nota_bench_chart", root / "scripts/bench-chart.py")
chart = importlib.util.module_from_spec(spec)
spec.loader.exec_module(chart)
names = {"before": "Before", "after": "After"}
groups = [(label, {build: data[build][mode]["deadlinePercent"] for build in names})
          for mode, label in (("one-swam", "One SWAM"), ("four-swam", "Four SWAM"),
                              ("complete-project", "Complete project"))]
chart.bar_chart(str(out / "callback-load.svg"), "Saved SWAM project: callback headroom",
    "512 samples / 48 kHz; actual saved states and MIDI; render into memory, not the ASIO device",
    "% buffer time", groups, list(names), names)

buffers = {
    "workload": "Four real test VST3 instances, 56 channels; not a Kontakt or SWAM instrument benchmark",
    "frames": 128, "sampleRate": 48000,
    "before": records("buffer-before.log", "HOSTED_BUFFER", "adapter"),
    "after": records("buffer-after.log", "HOSTED_BUFFER", "adapter"),
}
buffer_out = out.parent / "kontakt-buffer-overhead"
(buffer_out / "benchmark.json").write_text(json.dumps(buffers, indent=2) + "\n", encoding="utf-8")
groups = [(label, {build: buffers[build][mode]["small"]["MedianMs"] for build in names})
          for mode, label in (("instrument", "Instrument adapter"), ("insert", "Insert adapter"))]
chart.bar_chart(str(buffer_out / "callback-time.svg"), "Active-range buffer initialization",
    "Four test VST3s, 56 channels; 128 samples / 48 kHz; zero-work DSP, median callback time",
    "ms per block", groups, list(names), names)
print(out / "benchmark.json")
print(buffer_out / "benchmark.json")
