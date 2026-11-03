# Bug Verification: Independent hosted VST3 scheduling

- **Slug**: kontakt-render-overload (from assessment/fix context)
- **Tested**: 2026-11-03
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial — host defect verified; the exact Kontakt/Strings session awaits audition

## Summary

The original engine reproduces the host-scheduling defect with four real VST3 fixture
instances: render workers perform no work, six intended checks fail, and all 100 measured
device buffers miss their deadline. The repaired engine passes all fourteen hosted checks
and the full Windows build/smoke gate. The actual user-provided Kontakt 8 / Sample Modeling
Strings report cannot be declared fully verified without its patches and audio settings.

## Checks Performed

| Check | Command / Action | Result | Notes |
| --- | --- | --- | --- |
| Scheduling reproduction, before | Run final hosted fixture tests with the original DLL preserved before edits | pass (reproduces defect) | Six expected failures: effect/instrument helper participation, offline/device speedup and device deadlines. No helper CPU, 100/100 late device blocks. Isolated test catalog and process. |
| Reproduction, after | `dotnet run --project tests/Nota.SmokeTest -c Release -- --hosted-render-pool` plus its inclusion in the full gate | pass | Four real independent VST3s use helpers. Device median 1.6317 ms versus 5.7741 ms before; device p95 1.8457 ms; 0/100 late callbacks after warm-up. |
| Audio/regression coverage | Full hosted fixture checks | pass | Exact serial/parallel output through instrument/insert adapters, MIDI, automation, previous-block sidechains, group/return routing, bypass, insert removal, instrument replacement and track undo. |
| Conservative fallback | Hosted instrument rack and light device workloads | pass | Rack remains audible and serial; light workload helpers stay parked (0.0 ms measured worker CPU). Unknown format fallback is also confirmed by capability/constructor inspection; no Audio Unit runtime was available on Windows. |
| Concurrent graph edits | Add/remove eight VST3 tracks while a background thread renders worker jobs | pass | 1,333 blocks completed with finite output and no crash in the full run. |
| Full regression suite | `scripts/build-win.ps1` in VS developer environment | pass | Exit 0. Native + managed Release, scanner self-test, SMOKE TEST PASSED and SCAN SCALE PASSED; 5,359 PASS lines, zero FAIL lines. |
| Existing performance guards | Included in full smoke | pass | Idle pool: 0.0 ms CPU/second. Existing overload guard: 19/4,125 late blocks, below 1%, no burst regression. Existing single/parallel built-in renders remain sample-identical; bounce/export guards pass. |
| Native unit tests | `ctest --test-dir src/native/nota.engine/build --output-on-failure` | pass | 2/2 tempo/SuperFlux tests. |
| Documentation | `npm run build`, `npm run check` in sibling `../nota-docs` | pass | EN/RU audio-performance guidance updated. 213 pages built; 106 source pages checked, zero errors, 64 existing warnings. No visual UI or shortcut change, so no screenshots or shortcut regeneration required for this fix. |
| Hygiene and delivery | App/native DLL SHA-256 comparison, `git diff --check` in both repositories, source/architecture review | pass | Native DLLs match. New sources carry AGPL headers. No new dependency, public C ABI, schema, UI setting, pool or render-path allocation/lock introduced. Unrelated existing edits retained. |
| Actual desktop startup | Open rebuilt Release `Nota.App.exe`, inspect process responsiveness | pass | PID 29476, `Nota — Untitled`, Responding=True. App/native build DLL hashes match. |
| Original Kontakt / Sample Modeling Strings audition | Matching four patches, voice pattern and device setup | not-run | Library family supplied; exact patches, buffer/sample rate and matching project unavailable. The app is rebuilt for user audition. |
| Other platforms / instruction-count gate | macOS/Linux builds and Linux Valgrind gate | not-run | This execution host is Windows x64. |

## Output Excerpts

```text
[PASS] real VST3 insert adapters use helpers
[PASS] VST3 instruments: MIDI, automation, group/return output is sample-identical with helpers
[PASS] four indivisible VST3 jobs: 0/100 device deadlines missed after warm-up (<= 1)
[PASS] VST3 graph additions/removals during worker renders stay finite and complete (1333 blocks)
SMOKE TEST PASSED
SCAN SCALE PASSED
100% tests passed out of 2
106 pages · 0 error(s) · 64 warning(s)
```

Persistent numerical evidence and reproducible plots are in [benchmark.md](benchmark.md),
[benchmark.json](benchmark.json), [benchmark.csv](benchmark.csv),
[callback-time.svg](callback-time.svg) and [missed-deadlines.svg](missed-deadlines.svg).
Ignored local logs are identified in the benchmark report.

## Residual Risks

- Fixture DSP is deterministic and expensive, but it does not emulate the proprietary
  Kontakt engine, internal workers, sample streaming or Sample Modeling Strings scripts.
- One short Windows measurement window is not a general dropout/latency guarantee.
  The device hook covers the production scheduling branch without opening a physical device.
- Audio Units and hosted-containing racks remain serial; one overloaded track cannot be
  split by adding helpers. Real plug-ins' own multiprocessing may affect optimal behavior.
- JUCE's existing per-instance VST3 process synchronization is unchanged. The host adds
  no callback locks and never intentionally processes the same instance concurrently.
- Existing compiler warnings and unrelated manual source-path warnings remain.

## Recommendation

The host repair is ready to try: load the same four Kontakt VST3 instances at the previous
sample rate/buffer, play the same material and compare Nota's DSP/dropouts. Mark the
original user report fully verified after that audition confirms it. If it still overloads,
capture the exact patches/voice pattern, audio configuration and whether any instances are
inside a rack; use those to separate a remaining serial chain, internal Kontakt scheduling
or a different bottleneck from the repaired independent-track scheduling.
