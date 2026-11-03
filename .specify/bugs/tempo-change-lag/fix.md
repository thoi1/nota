# Bug Fix: Responsive project tempo edits

- **Slug**: tempo-change-lag (resolved from the preceding assessment)
- **Fixed**: 2026-11-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

The top transport sends an explicit deferred BPM operation, preserving the existing
synchronous API for offline clients. The existing warp worker prepares the latest
requested tempo without blocking drag events, and exports finish any pending preparation.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/native/nota.engine/src/engine/Engine.h`, `Engine.cpp` | modified | Explicit deferred setter; latest authoring BPM describes desired cache inputs; retains old audio and signals obsolete work. |
| `src/native/nota.engine/src/engine/Engine_Tracks.cpp` | modified | Cache worker queries use desired tempo before clock command drain; cancel/reap unfinished jobs without joining them in a poll; require publication before polling returns done. |
| `src/native/nota.engine/src/engine/Engine_Render.cpp` | modified | Offline rendering joins pending tempo work and prepares correct caches/sample rate, respecting explicit load deferral. No callback work added. |
| `src/native/nota.engine/src/abi/nota_engine_core.cpp`, `include/nota/nota_engine.h` | modified | Add validated C ABI deferred BPM operation; old synchronous entry point preserved. |
| `src/managed/Nota.Application/Engine/IAudioEngine.cs`, `src/managed/Nota.Infrastructure/Interop/NativeMethods.cs`, `NotaEngine.cs` | modified | Bind explicit interactive BPM operation. |
| `src/managed/Nota.Presentation/ViewModels/TransportViewModel.cs` | modified | Route transport BPM edits to deferred operation; preserve clock acknowledgement behavior. |
| `src/managed/Nota.App/MainWindow.TempoWarp.cs` | added | 120 ms settling delay, latest-request/project generation checks, existing poller, completion refresh; stop on closed window or export ownership. |
| `src/managed/Nota.App/MainWindow.axaml.cs`, `MainWindow.AudioImport.cs` | modified | Trigger preparation; check cancellation before engine access; guard a queued tempo redraw after close. |
| `tests/Nota.SmokeTest/TempoChangeTests.cs`, `Program.cs` | added/modified | Focused `--tempo-change` and normal smoke integration. |
| `.specify/bugs/tempo-change-lag/repro`, `ui` | added | Exact view-model benchmark and actual headless transport drag/close regression. |
| `CHANGELOG.md`, `ARCHITECTURE.md` | modified | Record fix and preparation lifecycle. |

## Tests Added or Updated

- Deferred view-model setter leaves the exact Arrangement + Session frame count pending
  before audio commands drain and returns within a generous 100 ms budget.
- Changes during an active worker cancel promptly; the final request converges without
  needing audio command drain. Returning to an already-cached tempo reaps obsolete jobs.
- Rendered Arrangement + Session samples match a forced fresh synchronous rebuild exactly.
- Source identity/anchors remain unchanged; preparation adds no undo entries.
- Explicit load deferral retains audible old audio. Offline rendering completes pending
  tempo work, including an active worker and changed export sample rate.
- Invalid BPM is rejected.
- Headless real MainWindow: 25 pointer-driven transport BPM updates; debounce and polling
  complete without manual worker calls; close with a pending continuation remains safe.

## Local Verification

- Native build: passed (`native-build-final.log`).
- Managed solution build: passed, zero errors; existing warnings (`managed-build.log`).
- `dotnet run --project tests/Nota.SmokeTest -c Release -- --tempo-change`: 12 checks passed;
  rendered sample difference 0 (`targeted-final.log`).
- `dotnet run --project .specify/bugs/tempo-change-lag/ui/TempoUi.csproj -c Release`:
  all three checks passed; slowest input 23.958 ms; preparation 360 ms (`ui-final.log`).
- Unchanged benchmark: before 189.623–239.136 ms; after 0.004–0.460 ms per setter
  (`repro-before.log`, `repro-after.log`). Timing covers the setter, not total preparation.
- `git diff --check`: passed (only an existing line-ending warning).
- Reviewed with `nota-docs`: performance fix leaves documented controls/workflow unchanged;
  no manual or screenshot update needed. No capability added/removed, so FEATURES unchanged.

## Deviations from Assessment

- Actual close testing found the existing queued `RequestTempoRefresh` could access the
  disposed engine. Added the close guard in the already-scoped MainWindow file.
- Background polling now returns at least one pending frame until a new job's result is
  published, covering a tiny worker completing before the initiating poll returns.
- UI tests live inside the bug workspace instead of changing the sibling docs harness.
  No new production dependency or worker pool was introduced.

## Follow-ups

- Run `speckit-bug-test` for independent reproduction and the full Windows regression build.
- Physical audio-device playback and the reporter's exact project remain user validation.
