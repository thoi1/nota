# Bug Fix: Independent hosted VST3 tracks use the render pool

- **Slug**: kontakt-render-overload (from the user-confirmed assessment)
- **Fixed**: 2026-11-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Independent VST3 instrument/effect adapters opt into the existing adaptive render pool.
Helper sizing also accounts for the largest indivisible track job so four expensive
tracks are not assigned too few helpers. The controlled device-path fixture improves
from 5.7741 ms to 1.6317 ms median per block; the user's Kontakt/Strings session still
requires audition.

## Changes

| Files | Change | Notes |
| --- | --- | --- |
| `src/native/nota.engine/src/devices/{Device.h,Instrument.h}` | modified | Separate hosted identity from an explicit parallel-render capability; unknown hosted types and hosted-containing racks default to serial. |
| `pluginhost/hosting/{PluginInstrument,PluginEffect}.{h,cpp}` | modified | Cache VST3 capability at construction. Per-instance buffers, MIDI, playhead and sidechain state remain exclusive to a single block job. Other formats remain serial. |
| `src/engine/Engine_Render.cpp` | modified | Use capability instead of excluding all hosted leaves. Preserve shared-writer exclusions, ordered mixing and the existing pool. Size active helpers using total and largest-task cost. |
| `src/engine/Engine.h` | modified | Store the smoothed largest eligible task duration alongside existing adaptive timing state. |
| `pluginhost/tests/RenderProbe.cpp`, native `CMakeLists.txt` | added/modified | Deterministic real VST3 fixture with bounded expensive DSP, MIDI, parameter/state and sidechain support. Validation-only target stays in the native build tree. No new dependency. |
| `tests/Nota.SmokeTest/{HostedRenderPoolTests.cs,RenderPoolTests.cs,Program.cs}` | added/modified | Isolated catalog/process; direct selector and inclusion in existing performance/full smoke runs. |
| `ARCHITECTURE.md`, `BUILD.md`, `FEATURES.md`, `CHANGELOG.md` | modified | Document the render contract, limits, test command and repaired behavior using Nota Features/Docs workflows. |
| Sibling manual `preferences/audio.mdx` and RU twin | modified | Explain deadline-based DSP load, parallel VST3 tracks, serial fallbacks and heavy single-track limits. |

All abbreviated native paths above are relative to `src/native/nota.engine/`.

## Tests Added or Updated

Fourteen hosted fixture assertions cover scan/isolation; serial/parallel exact output for
instrument and insert adapters; MIDI, automation, previous-block sidechains, groups and
returns; bypass/removal/replacement/undo; helper participation; callback/bounce timing;
bounded render-work proxy; light-load parking; hosted-rack fallback; and adding/removing
eight plug-in tracks during worker rendering (1,333 completed blocks in the full run).

The original DLL was retained outside the repository before changes. The same final
instrument/insert workload fails six scheduling/performance assertions against it:
zero helper participation, no speedup and 100/100 device deadlines missed. Actual audio
comparisons and rack fallback pass before and after.

## Local Verification

- Focused `--hosted-render-pool`: pass after the fix; negative control with the preserved
  original DLL fails the intended six checks.
- `scripts/build-win.ps1`: exit 0, native and managed Release build, scanner self-test,
  full smoke/performance suite and scan-scale check; 5,359 PASS lines, zero FAIL lines.
- Existing pool guards: idle helper CPU 0.0 ms over one second; overloaded small-buffer
  guard 19/4,125 late blocks (under 1%); serial/parallel built-in output remains exact.
- `ctest --test-dir src/native/nota.engine/build --output-on-failure`: 2/2 pass.
- Sibling manual build: 213 pages. Check: 106 source pages, zero errors, 64 existing warnings.
- App/native build DLL hashes match. Whitespace checks pass in both repositories.

See [benchmark.md](benchmark.md) for raw results, charts and commands.

## Deviations from Assessment

The adapter-only correction exposed a second scheduling gap within the same render path:
using total work alone assigned four approximately 1.4 ms indivisible jobs to three
threads at a 2.67 ms deadline. An initial measurement still missed 42/100 deadlines.
The fix therefore also tracks the largest job and uses the list-scheduling upper bound
to size helpers. This adds one smoothed counter and constant-time arithmetic; it adds
no callback allocation, sorting, lock, permanent thread or forced wake-up under light load.

No C ABI, schema, dependencies, user-facing controls or shortcuts were added. Existing
unrelated work was preserved; no commit, push or PR was made.

## Follow-ups

Use the rebuilt desktop app to audition the same four Kontakt 8 / Sample Modeling Strings
tracks. Exact patches, voice pattern, buffer size and sample rate were unavailable. Do not
interpret fixture speedup as a measured Kontakt performance guarantee. Audio Units,
hosted-containing racks and a single overloaded device chain retain their documented limits.
