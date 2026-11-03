# Implementation Plan: Grid Warp

**Branch**: `079-grid-warp` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: [Feature specification](spec.md)

## Summary

Extend the existing audio editor and shared tempo infrastructure to support transient-guided Grid Warp without changing source audio. The codebase already has source-relative SuperFlux analysis, a variable project tempo map with absolute timing anchors, a tempo lane, persisted map/clip references, and a distinct no-stretch Grid follows audio playback path. The key design change is to separate the editor's selected warp tool from committed clip/tempo state: mode switches are UI-only; explicit drag/anchor gestures commit one undoable tempo/anchor edit. Keep Audio follows grid edits on the existing warp-marker and cache path.

## Nota: Read Before Planning

- Engine work follows [`ARCHITECTURE.md`](../../ARCHITECTURE.md): lock-free command queue (AR-4), immutable graph snapshots (AR-5), no audio-thread allocation/locks/I/O (AR-6), pure C ABI (AR-7), and `Transport` as the only musical clock (AR-9).
- UI work follows [`DESIGN.md`](../../DESIGN.md) and [`CONTRIBUTING.md`](../../CONTRIBUTING.md).
- No dependency or new timing model is planned.

## Technical Context

**Language/Version**: C++20 engine, C# / .NET 10 and Avalonia UI.

**Primary Dependencies**: Existing Nota engine, Avalonia controls, transient-analysis service, and project services; no new dependencies.

**Storage**: Reuse serialized `TempoMapState` points/anchors, audio-clip tempo references, warp-marker data, and existing project format/migrations. Add a compatible field or migration only if investigation proves existing anchor metadata insufficient.

**Audio-Thread Impact**: No transient analysis, map construction, allocation, or locking is added to the audio callback. Final tempo edits publish through the existing engine graph/command path. Grid follows audio playback continues reading the original source at its absolute-time mapping; Audio follows grid alone uses the existing prepared stretch cache.

**Testing**: Extend `Nota.SmokeTest` tempo-map, tempo-change, transient-analysis, and mapping suites; add focused headless UI tests for mode toggles, slider behavior, waveform grid gestures, zoom/scroll mapping, and preview/commit separation. Run the repository Windows gate `scripts/build-win.ps1`.

**Target Platform**: Existing Nota desktop platforms; validate on Windows as requested.

**Project Type**: Desktop real-time audio application.

**Performance Goals**: Pointer-move previews do not publish tempo maps or request audio warp-cache builds. Slider input is debounced; only the newest detection settings may update visible results. Detection remains off the audio callback.

**Constraints**: Preserve current dirty/in-progress work in the repository; several feature files, including audio editor, tempo, transient, persistence, native engine and tests, already contain uncommitted changes. Read and integrate those exact files rather than replacing them. Reuse the project's beat/time conversion, BPM bounds, undo, localization and save/load conventions. Use the existing time signature rather than the audio editor's current fixed 4/4 assumptions.

**Scale/Scope**: Sample Editor mode controls and waveform overlay/gesture; transient controls and scheduling; project tempo/anchor transaction; Tempo Track display/edit synchronization; existing persistence and regression tests. No standalone application, separate tempo engine, or general audio-warp redesign.

## Constitution Check

- **I. Real-Time Audio Safety**: PASS — all analysis and tempo-map preparation stay on authoring/background paths; callback work remains immutable lookup/rendering.
- **II. Boundary Discipline**: PASS — any engine capability crosses only the existing pure C ABI; no C++ types or exceptions cross it.
- **III. Licensing Hygiene**: PASS — no dependency is added; new source files, if unavoidable, receive the standard SPDX header.
- **IV. Verification Gate**: PASS — add engine/smoke regressions and run `scripts/build-win.ps1`.
- **V. One Logical Change**: PASS — one integrated Grid Warp feature while preserving existing Audio follows grid semantics.
- **VI. Documentation**: PASS — update `[Unreleased]`, `FEATURES.md`, and directly applicable user documentation/shortcuts.
- **VII. Spec-First**: PASS — feature spec and this plan precede implementation.
- **VIII. Safety Rules**: PASS — no real-time safety or ABI constraint is weakened.

## Research Decisions

1. **One authoritative clock**: retain the native `TempoMap` and `Transport`; derive mapping from beat ticks and absolute seconds. Do not introduce a UI clock or a second tempo model.
2. **Existing anchor representation first**: use tempo-map anchors containing beat tick, absolute seconds, source frame, and clip/reference identity where those fields meet Grid Warp needs. Preserve relationships through current project capture/apply and migration logic.
3. **Mode selection is not a commit**: store None / Audio follows grid / Grid follows audio as editor interaction state. A mode click must not call clip-warp or tempo-map mutation APIs. Explicit marker/anchor gestures are the only commit path.
4. **Preview is local**: keep candidate anchor/map data in the editor while dragging; calculate displayed BPM locally. Publish exactly once on release through the existing engine edit and undo path.
5. **Keep the two warp paths distinct**: Audio follows grid uses existing warp markers and background stretch-cache preparation. Grid follows audio edits timing anchors and the tempo map; its audio rendering continues through the existing no-stretch/source-frame path.
6. **Reuse transient analysis**: update the existing settings limits/UI and debounce/cancel through the shared analysis service. Do not create another detector, cache, or worker pool.
7. **Use project meter**: draw bar/beat lines using the active project time-signature map; do not extend the editor's current hard-coded 4/4 ruler assumption into Grid Warp.
8. **Tempo Track remains authoritative and editable**: keep existing editable tempo events and anchor validation; add point BPM labels and synchronize associated anchors in the same validated tempo edit.

## Project Structure

### Documentation (this feature)

```text
specs/079-grid-warp/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── ui.md
├── quickstart.md
└── tasks.md
```

### Source Code

```text
src/native/nota.engine/
├── include/nota/nota_engine.h
├── src/abi/nota_engine_core.cpp
├── src/engine/Engine_Tempo.cpp
├── src/engine/TempoMap.{h,cpp}
└── src/engine/Engine_Render.cpp

src/managed/
├── Nota.Application/Analysis/ITransientAnalysisService.cs
├── Nota.Application/Engine/IAudioEngine.cs
├── Nota.Infrastructure/Analysis/TransientAnalysisService.cs
├── Nota.Infrastructure/Interop/NotaEngine.Tempo.cs
├── Nota.Infrastructure/Persistence/{AudioClipState,ProjectModel,ProjectService}.cs
└── Nota.App/
    ├── AudioClipEditorView.cs
    ├── AudioClipEditorView.Transients.cs
    ├── TransientWorkspace.cs
    ├── ArrangementView.Tempo.cs
    └── TempoTrackControl.cs

tests/Nota.SmokeTest/
├── TempoMapTests.cs
├── TempoChangeTests.cs
├── TransientAnalysisTests.cs
└── TransientMappingTests.cs
```

**Structure Decision**: Keep the feature in the existing engine, app, analysis, persistence, and smoke-test projects. Add no new subsystem or dependency. The current worktree already contains uncommitted changes in these areas; implementation must preserve and build on the current contents.

## Design and Validation

### Mode state and commands

Represent the Sample Editor's selected tool independently from the clip's persisted warp settings. The three controls behave as mutually-exclusive toggles and may all be off. A click changes only this editor state. Route pointer gestures according to the selected tool; when no mode is active, preserve existing Sample Editor interactions. Audio follows grid keeps its established explicit marker edits, and Grid follows audio commits tempo-anchor edits only on gesture completion.

### Grid Warp coordinates and preview

Build a viewport transform using the existing waveform view zoom/scroll and the project's beat/time functions. Grid lines are project beats rendered at `SecondsAtBeat(beat)` and mapped through the selected clip's absolute source-time placement. During Grid Warp drags, use the inverse mapping to resolve a target source frame/transient. Keep the candidate beat/time anchors local; derive the interval BPM as `60 * beatDelta / secondsDelta`, check neighboring fixed anchors and map limits, and draw the BPM/target preview without publishing an engine map or queuing cache work. Shift bypasses transient snapping for that drag. On release, validate the complete candidate and commit one undoable map/anchor operation; rejection leaves the prior state unchanged.

### Anchor and Tempo Track integration

Treat committed Grid Warp anchors as source/time-to-beat references, distinct from transient candidates. Keep their source and absolute-time identities through re-analysis. Existing tempo-map validation is responsible for monotonic time and locked-anchor conflicts; Grid Warp adds explicit checks for crossing, zero-duration, and BPM-bound intervals before commit. Moving or deleting a point explicitly linked to a Grid Warp anchor updates or removes that anchor in the same transaction. An unrelated tempo edit preserves fixed anchors; a conflicting BPM edit is constrained, rejected, or requires explicit unlinking. Display point BPM labels only in the expanded tempo lane and keep existing point gestures, selection and undo.

### Transient controls

Replace the two `DragNumber` controls with horizontal sliders whose values are 0–100 percent (default 50) and 5–250 ms (default 30). Keep the analysis service's validated sensitivity mapping; update its spacing limit to match the UI. Debounce edits, cancel active obsolete work, and present only the snapshot matching the current settings. The service's existing immutable source snapshots remain shared with the Arrangement and Sample Editor.

### Validation gates

1. Add deterministic unit/smoke tests for sensitivity mapping and spacing, fixed-anchor ordering, BPM derivation, exact/monotonic beat-time mapping, and unsupported BPM rejection.
2. Add engine integration tests that compare source identity/data and rendered absolute sample positions before/after Grid Warp, and verify Audio follows grid still stretches without changing the map.
3. Add UI tests for exclusive mode toggles/no-side-effects, transient slider values, transient target selection, anchor add/remove/drag, Shift bypass, zoom/scroll transforms, local preview without commit, Tempo Track synchronization, and editor closure/cancellation.
4. Test single-step undo/redo and project save/reload for map events, references, clip workflow state and manual anchors.
5. Run the focused tests and `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\build-win.ps1`; stop the specific running Nota process first if it locks Release outputs.

## Complexity Tracking

No constitution violations or new dependencies. Any new C ABI entry point must be narrow, validated, documented in `nota_engine.h`, and exercised by the smoke test; prefer existing tempo-map and clip-reference APIs when they can provide an atomic transaction.

## Completion implementation

Preserve existing manual implementation. Add Application SourceBeatInference and source-tempo
analysis/result port; Infrastructure service and additive AudioClip persistence; App extraction
partial/controls/proposal overlay and captured Arrangement drop routing. Native changes separate
reference geometry from original warp data, assign stable anchor IDs/explicit tempo linkage using
existing ABI slots, validate safe Step region seams and expose reset/reference-state transactions.

No production dependencies. New source files carry SPDX headers. New analysis does not touch
callback state; pointer previews do not build native maps. Graph snapshots own all committed
musical state, metadata and reset baselines; no second project clock. Existing scalar/audio-warp
paths remain intact. All constitution gates pass with these constraints.

Required evidence: deterministic constant/rubato/subdivision/missing/no-evidence inference,
original-before/after-auto-warp identity and project reload, source warp-marker byte identity,
reference offset/nonzero origin, Step seams/protected conflicts/stable IDs, actual editor proposal
and drop gestures, Reset single undo, native/full Windows gates and EN/RU manual screenshots.
