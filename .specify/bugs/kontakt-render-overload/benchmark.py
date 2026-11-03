#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
"""Extract comparable hosted-fixture runs and reuse Nota's stdlib SVG plotter.

Usage: python .specify/bugs/kontakt-render-overload/benchmark.py BEFORE.log AFTER.log
This is a deterministic VST3 scheduling benchmark, not a Kontakt/library benchmark.
"""

import csv
import importlib.util
import json
from pathlib import Path
import sys

out = Path(__file__).resolve().parent
root = out.parents[2]


def read(path):
    runs = {}
    for line in Path(path).read_text(encoding="utf-8-sig").splitlines():
        for mode in ("DEVICE", "OFFLINE"):
            prefix = "HOSTED_" + mode + " "
            if line.startswith(prefix):
                runs[mode.lower()] = json.loads(line[len(prefix):])["parallel"]
    if set(runs) != {"device", "offline"}:
        raise ValueError("Expected both device and offline fixture measurements in " + str(path))
    return runs


before, after = map(read, sys.argv[1:3])
data = {
    "machine": "Windows x64, AMD Ryzen 9 7900X, 12 cores / 24 logical processors",
    "workload": "Four deterministic real VST3 instances; 4 configured helpers; not Kontakt",
    "sample_rate": 48000,
    "buffer_frames": 128,
    "buffer_deadline_ms": 128 / 48000 * 1000,
    "measurement": "100 blocks after 80 warm-up blocks; device calls paced to the buffer period",
    "before": before,
    "after": after,
}
(out / "benchmark.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
with (out / "benchmark.csv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.writer(stream)
    writer.writerow(["build", "mode", "median_ms", "p95_ms", "worker_cpu_ms", "late_blocks", "blocks"])
    for label, runs in (("before", before), ("after", after)):
        for mode, result in runs.items():
            writer.writerow([label, mode, result["MedianMs"], result["P95Ms"], result["WorkerMs"], result["Late"], result["Blocks"]])

spec = importlib.util.spec_from_file_location("nota_bench_chart", root / "scripts" / "bench-chart.py")
chart = importlib.util.module_from_spec(spec)
spec.loader.exec_module(chart)
names = {"before": "Before", "after": "After"}
groups = [
    ("Device median", {"before": before["device"]["MedianMs"], "after": after["device"]["MedianMs"]}),
    ("Device p95", {"before": before["device"]["P95Ms"], "after": after["device"]["P95Ms"]}),
    ("Offline median", {"before": before["offline"]["MedianMs"], "after": after["offline"]["MedianMs"]}),
]
chart.bar_chart(str(out / "callback-time.svg"), "Four independent VST3 instruments",
    "Synthetic fixture; Ryzen 9 7900X; 128 samples / 48 kHz; device deadline 2.667 ms",
    "ms per block", groups, ["before", "after"], names)
chart.bar_chart(str(out / "missed-deadlines.svg"), "Device deadlines missed after warm-up",
    "Synthetic VST3 fixture; 100 measured blocks; lower is better",
    "% blocks", [("128 samples / 48 kHz", {
        "before": before["device"]["Late"] / before["device"]["Blocks"] * 100,
        "after": after["device"]["Late"] / after["device"]["Blocks"] * 100})],
    ["before", "after"], names)
print(out / "benchmark.json")
