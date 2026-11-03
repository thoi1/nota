# Bug Assessment: Project tempo changes block on audio stretching

- **Slug**: tempo-change-lag (generated from the report for this repair run)
- **Created**: 2026-11-03
- **Source**: pasted text
- **Verdict**: valid
- **Severity**: high

## Report

> the engine lags when changing the tempo

Clarification: **Project BPM in the top transport**.

## Symptom and reproduction

Changing the project tempo should update the control/transport without blocking the UI
on offline audio stretching. Every drag update currently stretches every warped source
synchronously on the UI/authoring thread.

Automated reproduction (`repro/TempoLag.csproj`) uses the real TransportViewModel and
C ABI, two generated 20-second mono sources with Complex warp, and changes BPM to
121, 132, 145 and 110. The setter alone took **214.548, 201.196, 189.623 and 239.136 ms**
respectively on Windows x64. Source generation and initial warp preparation are outside
those measurements. These are local measurements, not general benchmark guarantees.

## Suspected code paths

- `MainWindow.axaml.cs`: DragNumber.ValueChanged writes TransportViewModel.Bpm on each move.
- `TransportViewModel.OnBpmChanged`: calls synchronous IAudioEngine.SetBpm.
- `Engine.cpp::setBpm`: queues the clock command, then reconfigureAllWarpStreams eagerly
  rebuilds all stretched caches before returning.
- `Engine_Tracks.cpp::configureClipWarp/buildWarpCache`: full-window offline DSP.
- Existing warpBuildPoll already builds captured immutable inputs off-thread, but uses
  the audio thread's possibly not-yet-updated BPM to choose/validate jobs and joins an
  obsolete job before it has finished, which can itself block a tempo drag.
- `Engine_Render.cpp::renderOffline`: exports need completed caches, unlike interactive
  tempo edits. Preserve its correctness when a deferred edit is still pending.

## Root cause

Confidence: **high**. Directly reproduced through the exact transport view-model path.
The existing worker is used for clip edits/imports but not top-level tempo edits. A second
cache-target issue appears before the SetBpm command drains: the remaining-work query
compares newly prepared caches with the old transport tempo. The existing authoringBpm_
mirror can describe desired cache inputs without introducing another playback clock.

## Proposed remediation

**Preferred**:

1. Add an explicit deferred BPM C ABI/engine-port operation for interactive clients.
   Keep the existing synchronous SetBpm API behavior for offline clients. Both queue the
   same lock-free transport command; deferred edits skip full cache rebuilding and retain
   existing audio until background replacements are ready. Do not toggle/clobber a caller's
   existing warp-build deferral scope.
2. Drive cache work against the existing latest-requested authoring tempo mirror, including
   requests made before the audio command drains. In warpBuildPoll, mark obsolete jobs
   cancelled and return until they finish; join only finished jobs on this polling path.
3. Use the deferred operation in TransportViewModel. Coalesce rapid project-tempo edits
   on the UI thread, wait briefly for the last value, then drive the existing warp poller.
   Stop obsolete/project-closed polling before accessing the engine and refresh affected
   waveform views after completion. No new worker pool or callback DSP.
4. Complete pending tempo caches synchronously when offline rendering/export begins,
   without adding checks or work to the real-time callback. Preserve explicit project-load
   deferral semantics.
5. Add C ABI/view-model responsiveness, cache convergence/cancellation, Session, offline
   render and actual UI gesture tests. Keep a reproducible before/after timing record.

**Files likely to change**:
- Native: Engine.h, Engine.cpp, Engine_Tracks.cpp, Engine_Render.cpp,
  abi/nota_engine_core.cpp, include/nota/nota_engine.h.
- Managed: IAudioEngine.cs, NativeMethods.cs, NotaEngine.cs, TransportViewModel.cs,
  MainWindow.axaml.cs, MainWindow.AudioImport.cs, new MainWindow.TempoWarp.cs if useful.
- Tests: TempoChangeTests.cs, smoke Program.cs, existing docs headless scene/harness as
  needed to exercise actual transport editing. Bug-local reproduction and reports.
- Changelog and narrowly affected manual/architecture descriptions if needed.

**Tests**: two long warped clips; rapid BPM changes while a worker is active; last request
before command drain; no stale job publication; old audio retained; final tempo audible;
Session caches; offline render immediately after an edit; nested load deferral; latest
request survives polling; actual transport drag remains responsive.

## Risks and considerations

- Do not move authoring graph access to Task.Run or put stretching on the audio callback.
- A deferred setter must not change existing synchronous API semantics or break exports.
- Cancellation may not publish partial/stale caches; worker teardown still joins safely.
- No need to reproduce the reporter's exact project to establish this blocking path. Its
  exact clip count/length is unknown; the deterministic reproduction isolates the cause.
- Existing source, warp and editor changes in the working tree must be preserved.

## Open questions

No blocking questions. The reporter confirmed the top project BPM control. Physical-device
playback and arbitrary project sizes remain broader validation limits.
