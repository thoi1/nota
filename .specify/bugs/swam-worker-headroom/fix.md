# Bug Fix: Reserve useful callback headroom for heavy tracks

- **Slug**: swam-worker-headroom (from the confirmed assessment)
- **Fixed**: 2026-11-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

The existing adaptive pool sizes active helpers toward 50% rather than 85% of the buffer
period. It retains overload activation/exit hysteresis and wakes only useful bounded
helpers. In the supplied saved project at 512/48000, four SWAM tracks drop from 59.06% to
30.92% mean callback-budget consumption; the complete mix drops from 60.25% to 32.22%.

## Changes

| File | Change | Notes |
| --- | --- | --- |
| `src/native/nota.engine/src/engine/Engine.h` | modified | One private sizing constant and its headroom rationale; no DSP, pool, priority or driver change. |
| `tests/Nota.SmokeTest/HostedRenderPoolTests.cs` | extended | One/four substantial real VST3 jobs at 512 frames must retain headroom and bounded caller/worker work proxy. |
| `CHANGELOG.md`, `ARCHITECTURE.md` | modified | Describe active-helper headroom and preserve the light-load parking contract. |

## Tests Added or Updated

The new guard fails before: four 512-frame jobs take 6.086 ms while one takes 2.916 ms,
exceeding the 1.6x wall-time budget. Afterward, the focused test takes 3.161 ms for four
versus 2.918 ms for one. The full run takes 3.167 vs 2.923 ms and passes the work-proxy bound.

The commercial project diagnostic added by the preceding buffer investigation is opt-in.
It loads saved instrument states and MIDI in memory, and reports RMS, callback timing,
late blocks and dedicated-worker CPU. No proprietary project/state content is committed.

## Local Verification

- Focused fixture checks and all twenty hosted checks in the full suite: pass.
- Same saved-project profile before/after at confirmed 512/48000: nonzero RMS in each case;
  one SWAM remains about 28%, four SWAM become about 31%, full saved mix about 32%.
  No late blocks in either build's measured 400-block windows.
- Full `scripts/build-win.ps1`: exit 0, native/managed Release, scanner, smoke/performance,
  scan scale; 5,365 PASS lines, zero FAIL. Native CTest: 2/2 pass.
- Existing idle/post-playback helper CPU: 0.0 ms per second; light 16-track playback keeps
  helpers parked, with render CPU 12.8% of one core versus 13.0% single-threaded.
- Existing overload guard: 0/4,125 late blocks, no late bursts. Existing bounce/export
  timing/work and exact serial/parallel output guards pass.
- App/native DLL hashes match. Whitespace checks pass. Source project manifest hash stays
  `F95E30CF1E5849EA3EDAB0407BA4BF6E687E68767B14ED91E0368BDED02D95A7`.

## CPU Tradeoff

For the saved four-SWAM profile, caller elapsed plus worker CPU rises about 4.8% while mean
callback wall time nearly halves. This is a conservative work proxy including caller wait
time, not a precise process-CPU counter. Waking the whole 16-thread pool was tested only
as a diagnostic; the production bounded-wake policy uses about 23% less of that proxy than
the initial whole-pool comparison. See [benchmark.md](benchmark.md) for actual records.

## Deviations from Assessment

None. The 50% target is applied through the existing scheduler; it is not an unconditional
parallel mode. A single large chain is not split, and small workloads still leave workers
asleep. The independent buffer-prefix fix is retained and documented separately.

## Follow-ups

Audition the rebuilt app on the user's physical ASIO driver and the same live/MIDI playback.
The original audible popping was not reproduced by the in-memory profile. Studio One's
meter, internal buffering and audio callback were not measured. Other platforms/Valgrind
were not run. Nota Docs' pure-performance exception applies; no UI or shortcut changed.
No commit, push or PR was made.
