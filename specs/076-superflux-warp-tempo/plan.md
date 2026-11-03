# Implementation Plan: SuperFlux, shared analysis, warp and tempo mapping

**Branch**: `dev/windows-fixes` (existing edits preserved) | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

## Summary

Deliver US1 first: native streaming SuperFlux with optional conservative attack refinement, exposed by opaque C ABI jobs and an Application service port implemented in Infrastructure. Continue with existing waveform/warp editor integration, then a coordinated variable-tempo clock migration. Do not offer incomplete timing modes as working controls.

## Technical Context

**Language/Version**: C++20, C#/.NET 10; Avalonia in later UI increment.
**Primary Dependencies**: Existing Signalsmith Linear FFT and Signalsmith Stretch; SuperFlux BSD-2-Clause reference adaptation with retained notice, no Essentia or new runtime package.
**Storage**: Bounded in-memory source/settings cache; no persisted analysis yet. Existing source IDs change on audio edits (revision represented by replacement identity).
**Audio-Thread Impact**: None for US1. Capture on authoring thread, retain shared immutable audio, drive step jobs on .NET Task pool under bounded concurrency. No analysis/locks/allocations added to render. Later map publication must use immutable graph/lock-free commands with off-thread reclamation.
**Testing**: C ABI smoke tests, synthetic piano/chord/arpeggio/vibrato/noise/stereo/rate fixtures, cancellation/invalidation/lifetime and reference golden vectors. Windows build script is final gate.
**Target Platform**: Windows x64 locally; other platforms unverified.
**Performance Goals**: O(log N) nearest/range boundaries, constant DSP working storage excluding output, bounded cache, two active workers maximum. Measure actual elapsed time; no invented real-recording accuracy.
**Constraints**: AR-4/5/6/7/9; no full-source managed copies, no new pool, preserve existing edits.
**Scale/Scope**: All stories tracked. First increment has no enabled UI command or clock changes.

## Constitution Check

All eight principles pass: workers isolated from audio; pure C ABI handles/POD; BSD notice; smoke coverage; coherent feature; docs/features reflect delivered capability only; spec before implementation; no safety exceptions. Rechecked after design: no violations.

## Project Structure

- Native: `src/native/nota.engine/src/media/SuperFlux.{h,cpp}`, `src/abi/nota_engine_transients.cpp`, `include/nota/nota_engine.h`, CMake registration; `Engine_Tracks.cpp` source capture only.
- Application: `Analysis/ITransientAnalysisService.cs` immutable results/settings/shared operations.
- Infrastructure: `Interop/NativeMethods.Transients.cs`, `Interop/NotaEngine.Transients.cs`, `Analysis/TransientAnalysisService.cs`.
- Tests: `tests/Nota.SmokeTest/TransientAnalysisTests.cs` and native reference fixtures via ABI.
- Existing UI/mapping/serialization reused in US2/3; see research/tasks.

## Algorithm and lifecycle

At 44.1 kHz use original 2048-sample symmetric Hann, 200 fps, quarter-tone triangular filterbank 27.5-16000 Hz with duplicate FFT boundaries removed, unnormalised triangles, log10(1 + magnitude). Other rates scale FFT to nearby power of two; frame centres use absolute 200 fps timing without accumulated hop rounding. Derive lag from Hann height ratio .5. Subtract three-band maximum of delayed spectrum, half-wave rectify, sum. Paper offline peak windows: 30 ms before/after max, 100/70 ms mean; additive threshold default 1.1, spacing 30 ms.

Stereo averages channel magnitudes (phase cancellation safe), explicitly differing from reference waveform downmix. Refinement is a separate optional local energy-envelope rise search; only strong isolated rises refined, otherwise retain frame timing and spectral-window uncertainty. Band contributions optional/deferred.

Capture does no FFT. Worker prepares processing state. Bounded ODF/spectral rings avoid duration-sized spectrograms. Atomic cancellation checked per frame. Partial results inaccessible; completed output sorted/immutable. Exceptions caught at allocating ABI boundaries. Managed cancellation registration disposed before handle destruction. Invalidation removes entries, cancels jobs and rejects stale publication by entry identity. Any caller cancellation cancels its shared job, subsequent requests retry. Cache bounded by entries/record count.

## Later integration

US2 reuses ClipTimeMap/ClipTempo, AudioClipEditorView and Arrangement geometry, marker validation, undo/persistence and offline stretch. US3 adds tested monotonic mapping then migrates Transport and every scalar samplesPerBeat consumer (MIDI, Session, automation, plugin TransportInfo, recording, warp caches). Only then expose preview/apply/reset and Grid follows audio.
