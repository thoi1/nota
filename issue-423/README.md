# Issue #423 PR evidence

## Revisions and machine

- Before: official develop `4da86c88f6f792a603d5199f6c63fa85deead08c`.
- After: `6ba07c46da54beedcdbdce5d7e0b3316eefc4c3d`.
- Windows 11 build 26300, AMD Ryzen 9 7900X, 24 logical processors.
- Each revision has independent Release engine and managed build outputs. Pinned,
  immutable dependency source downloads are reused; compiled outputs are not shared.

## Engine benchmark

`results.csv` contains all 48 observations: three interleaved before/after runs for eight
representative scenarios from `scripts/bench/engine-bench.cpp`. Each active scenario renders
10 seconds at 48 kHz. Idle samples measure five seconds after one second of settling.
The Windows adaptation `engine-bench-windows.cpp` changes only process CPU accounting
(`GetProcessTimes`) and optional-symbol lookup (`GetProcAddress`); workloads stay the same.
The CSV and charts retain every regression and outlier.

The 8-synth paced median is 1.2% before and 1.4% after (one core); individual runs span
0.6–2.0% before and 1.2–1.6% after. Three Windows timing samples do not establish a
systematic change at that scale. Offline/export CPU also varies across runs. Use the raw
CSV and CI instruction-count gate for further assessment rather than treating timing
medians as hardware-independent guarantees.
The 64-frame scenario has one late block per build across three runs (22,500 blocks
per build), with zero median late blocks. A late paced offline callback is not a directly
observed hardware dropout. The actual-window samples separately report zero dropouts.

Reproduce in a Visual Studio x64 Developer PowerShell after building both revisions:

```powershell
# For each label: copy its Release nota_engine.dll into libs/<label>/ and link the
# same benchmark source against that build's nota_engine.lib.
cl /std:c++20 /O2 /EHsc /I<repo>/src/native/nota.engine/include engine-bench-windows.cpp <build>/nota_engine.lib /Fe:libs/<label>/engine-bench.exe
python -X utf8 run-benchmarks.py
python -X utf8 <repo>/scripts/bench-chart.py evidence before="Official develop" after="Playhead fix"
```

The driver expects libs/before and libs/after beside it and writes evidence/results.csv.
The normal chart script needs rsvg-convert or headless Chromium to rasterize PNGs; the
Windows captures here use an isolated headless Edge profile at 2x scale. SVGs and Markdown
must be written with Python UTF-8 mode on Windows.

## Actual-window measurements and screenshots

See `ui-summary.md`, raw `ui-before.json` / `ui-after.json`, and `UiProbe/` sources.
Build that project twice with `-p:AppDir=<revision>/src/managed/Nota.App/bin/Release/net10.0`
and distinct output folders. Then run each `UiProbe.dll before|after <output-directory>`.
It opens/closes an actual Nota window with scratch data and the same generated MIDI clip.

## Validation

The after revision passed `scripts/build-win.ps1`: native and managed builds, scanner
self-test, full smoke suite including transport snapshot/interpolation assertions and
render-pool performance guards, and the large-folder scan check. No new dependency.
The primary branch retains the prior Yamaha ASIO and window-lifecycle verification
reports; the newly recorded comparison here uses WASAPI. macOS/Linux were not run locally.
