# Bug Assessment: Worker sizing leaves avoidable SWAM callback load

- **Slug**: swam-worker-headroom (user confirmed)
- **Created**: 2026-11-03
- **Source**: user report and isolated profile of the supplied saved project
- **Verdict**: valid
- **Severity**: high — insufficient headroom for four heavy hosted tracks and reported popping

## Report

The user reports four SWAM String Sections instances turning DSP red and popping, despite
one instance costing about the same as in other DAWs. Nota is also approximately 40% higher
than Studio One with matching settings. The confirmed output configuration is 512 samples
at 48 kHz: 10.667 ms per callback. The supplied `D:/Projects/Untitled.nota` contains four
SWAM Violin Section tracks, four Kontakt tracks, two built-ins and two return effects.

## Reproduction

The opt-in project diagnostic reads the saved states and MIDI, seeds a temporary installed
catalog, initializes plug-in message processing and renders into memory without changing
the original project or opening the output driver. Start at the first SWAM note, beat
88.932, with 120 warm-up and 400 paced measured callbacks at 512/48000.

Measured current adaptive scheduling (buffer-prefix correction already present):
- One saved SWAM track: median 2.9266 ms, mean deadline load 27.83%, 0/400 late.
- Four saved SWAM tracks: median 6.2957 ms, mean 59.06%, 0/400 late.
- Complete saved mix: median 6.4137 ms, mean 60.25%, 0/400 late.
- Same four saved SWAM tracks with all available offline helpers: median 3.5349 ms,
  mean 33.41%, 0/400 late, but more worker CPU and unnecessary whole-pool wake-ups.

This reproduces the avoidable callback-load gap. It has NOT reproduced the user's audible
popping on the physical Yamaha Steinberg ASIO driver, which remains an explicit limit.

## Suspected Code Paths

- `src/native/nota.engine/src/engine/Engine.h::kRtSizeLoad`: targets 85% of buffer time.
- `Engine_Render.cpp::mixGraph`: uses that target plus total/largest job estimates to wake
  only enough helpers to meet it; four approximately 3 ms jobs can therefore run in two
  approximately 6 ms waves, despite useful cores being available.
- `AudioThreadPool.h::dispatch`: already supports waking a bounded helper count; offline
  `helpers=-1` wakes the whole pool and is a diagnostic comparison, not the proposed fix.

## Root Cause

High confidence from same-project measurements. The existing target prioritizes fewer
active threads until near the deadline. For large indivisible plug-in tasks it leaves
substantially more wall time on the callback than necessary, reducing jitter/voice-attack
headroom. This is distinct from the now-corrected maximum-buffer clearing overhead.

## Proposed Remediation

**Preferred**: change the existing adaptive helper-sizing target from 85% to 50% of the
buffer. Retain overload activation/exit hysteresis, largest-job accounting, worker limits,
ordered summation and exact DSP paths. Keep the callback caller participating and wake only
the bounded number of useful helpers; do not use the offline whole-pool wake-up policy.

This deliberately reserves more callback headroom for an already overloaded workload. It
must be measured against total render-work and idle/bounce guards: a smaller meter alone
does not establish improved CPU efficiency. Light workloads that fit the callback remain
serial/parked, and a single expensive chain remains indivisible.

**Files likely to change**:
- `src/native/nota.engine/src/engine/Engine.h` (one private target constant/comment)
- `tests/Nota.SmokeTest/HostedRenderPoolTests.cs` (512-frame large-job regression)
- `CHANGELOG.md`, `ARCHITECTURE.md`, directly affected performance reference prose

**Tests**:
- Real VST3 fixture: compare one vs four 512-frame expensive tracks, require four-track
  median within 1.6x one-track time after warm-up and bound caller+worker work proxy.
- Before/after the same saved four-SWAM state/MIDI profile with nonzero rendered RMS.
- Existing parked/light/overload/leave-pool/bounce/export, exact output and lifecycle guards.
- Full Windows build/smoke suite, native tests and unchanged project hash.

## Risks & Considerations

More simultaneous workers can raise CPU/energy cost. Measure the actual policy's bounded
wakes rather than substituting the all-workers comparison. Do not change driver/buffer
settings, graph/DSP code, process priority, third-party parameters, schema, ABI or UI.
Keep the buffer-prefix repair and the prior VST3 eligibility repair intact. No dependency
is required. Other platforms and physical-device dropout reproduction need separate checks.

## Open Questions

The direct Studio One timing/meter and physical ASIO popping are not instrumented here.
The original saved bundle remains read-only; diagnostic muting exists only in memory.
