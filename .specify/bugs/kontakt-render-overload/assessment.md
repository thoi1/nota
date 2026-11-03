# Bug Assessment: Hosted instrument tracks overload the serial render path

- **Slug**: kontakt-render-overload (user confirmed)
- **Created**: 2026-11-03
- **Source**: pasted text and source inspection
- **Verdict**: valid
- **Severity**: high — multiple hosted instruments can exhaust the audio deadline while render workers remain unused

## Report

The user reports that four Kontakt 8 instances running Sample Modeling Strings reached
100% DSP and became unplayable, and asks for an efficient engine fix. The installed CPU
is a Ryzen 9 7900X (12 cores/24 logical processors); the engine defaults to 16 helpers.

## Symptom

Independent hosted-instrument tracks are processed consecutively on the calling audio
thread. Existing parallel rendering only helps eligible built-in tracks. More helpers
therefore do not relieve this workload. DSP measures buffer deadline consumption, not
whole-machine utilization, and its display is capped at 100%.

## Reproduction

1. Create four independent Kontakt 8 tracks and load Sample Modeling Strings (user report).
2. Play the instruments together; observe DSP reaching 100% and unusable playback.
3. Code-grounded automated reproduction: render four independently instantiated VST3
   test instruments with deterministic expensive DSP, comparing helpers=0 and helpers=4.
   The current hosted-plugin exclusion leaves helper CPU approximately zero and produces
   no parallel speedup. Cover both offline and the existing device-block test hook.

Exact patch names, note/voice pattern, audio device, buffer and sample rate are unavailable.
They are needed to match the user's session numerically, not to establish the scheduling
exclusion. Synthetic measurements must not be described as a Kontakt/Strings benchmark.

## Suspected Code Paths

- `src/native/nota.engine/src/engine/Engine_Render.cpp:2379`: rejects every hosted
  instrument/effect, including hosted children of racks, from leaf jobs.
- `src/native/nota.engine/src/engine/Engine_Render.cpp:2480`: excluded leaves render
  consecutively through shared serial scratch.
- `pluginhost/hosting/PluginInstrument.cpp` and `PluginEffect.cpp`: adapters own their
  own processor, buffers, MIDI queues and playhead; no shared per-instance render scratch.
- `src/engine/AudioThreadPool.{h,cpp}`: existing nonallocating fork/join scheduler already
  joins track jobs before ordered mixing and adaptively wakes helpers under device load.
- `src/engine/Engine.cpp::setRenderThreads`: worker configuration changes only when stopped.

## Root Cause Hypothesis

High confidence in the scheduling defect: a conservative hosted-plugin identity check is
being used as a blanket parallel-safety policy. Independent VST3 processors can perform
their process calls on audio threads, while initialization/editor/state lifecycle remains
on the message thread. This does not permit simultaneous processing of the same instance.
The code establishes serial scheduling; the user's exact Kontakt cost still requires a
matching session. Reference: Steinberg's VST3 threading model,
https://steinbergmedia.github.io/vst3_dev_portal/pages/Technical%2BDocumentation/API%2BDocumentation/Index.html#vst-3-threading-model
and local JUCE 8.0.14 hosting source (the shipped dependency).

## Proposed Remediation

**Preferred**: reuse the existing adaptive leaf-render pool. Introduce an explicit cheap
parallel-render capability on Instrument/Device, conservative for unknown hosted types.
Opt the audited, independent VST3 instrument/effect adapters in using a format decision
cached during construction. Keep Audio Units/unknown hosted formats and hosted-containing
racks on their existing serial fallback for this focused fix. Preserve shared-writer
recording exclusions, previous-block sidechain reads, per-instance sequential MIDI/DSP,
ordered mixing, preallocated scratch, graph lifetime and idle parking. Do not create a
second pool, force all workers awake, increase default threads or add callback locks.

Add a deterministic real VST3 test fixture built only for validation (never installed or
packaged), exercising the actual JUCE adapter and engine through the C ABI. Capture a
failing-before/passing-after helper-participation assertion and before/after latency/CPU
measurements. Test instruments and insert adapters, exact output, MIDI/automation, routing,
bypass, rack fallback and lifecycle edits. Keep tests independent of Kontakt licenses and
personal sample content. A test-only serial fallback override may be used when running
the same fixture to measure the old scheduling; do not expose a new product control.

**Alternatives**: simply removing the hosted check broadens the thread contract to formats
and nested rack snapshots that have not been validated. Per-plugin permanent threads add
oversubscription and synchronization costs instead of reusing the bounded existing pool.

**Files likely to change**:
- `src/native/nota.engine/src/devices/{Device.h,Instrument.h}`
- `src/native/nota.engine/src/engine/{Engine_Render.cpp,Engine.h}` (comments/policy only as needed)
- `src/native/nota.engine/pluginhost/hosting/{PluginInstrument.h,PluginInstrument.cpp,PluginEffect.h,PluginEffect.cpp}`
- `src/native/nota.engine/pluginhost/tests/` and `src/native/nota.engine/CMakeLists.txt`
- `tests/Nota.SmokeTest/{HostedRenderPoolTests.cs,RenderPoolTests.cs,Program.cs}`
- `ARCHITECTURE.md`, `CHANGELOG.md`, `FEATURES.md`, build/test documentation if required
- directly affected EN/RU manual content under the existing documentation workflow

**Tests to add or update**:
- Real VST3 leaf instruments and inserts use helpers when configured and expensive.
- A single instance is never reentered; parallel and serial renders are sample-identical,
  including note/controller/automation events and sidechain routing.
- Hosted racks and unsupported formats retain conservative scheduling.
- Light device workloads keep helpers parked; overloaded device blocks activate helpers;
  offline renders use helpers without regressing total CPU work disproportionately.
- Existing render-pool race/idle/performance guards and full native/managed smoke gate.

## Risks & Considerations

The core remains JUCE-free; no dependency, project schema or user-facing control is needed.
The workspace already contains extensive unrelated changes, so preserve them and record
the start-of-task baseline outside the repository. Real plug-ins may use their own workers;
measure rather than claiming linear speedup. Never process the same instance concurrently,
or perform state/prepare/editor lifecycle operations inside worker jobs. Validate third-party
Kontakt separately if a usable fixture is available; otherwise state that limit clearly.

## Open Questions

No blocker for the bounded host-scheduling repair. Exact Sample Modeling Strings patches,
buffer/sample rate and a matching saved session remain useful for subsequent user audition.
