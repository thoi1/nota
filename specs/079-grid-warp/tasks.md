# Tasks: Grid Warp

**Input**: Design documents from `specs/079-grid-warp/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/ui.md`, `quickstart.md`

**Tests**: Required by the feature specification. Add or extend tests before implementing behavior.

## Phase 1: Setup

**Purpose**: Create a focused headless UI test entry point without changing the existing smoke-test project structure.

- [X] T001 Create the Avalonia headless UI test project referencing the app in `specs/079-grid-warp/ui/GridWarpUi.csproj`.
- [X] T002 Add the headless test entry point and reusable Nota test setup in `specs/079-grid-warp/ui/Program.cs`.

## Phase 2: Foundational Tempo/Anchor Transaction

**Purpose**: Provide a single validated project edit for a committed Grid Warp map and its clip/source references. This blocks Grid Warp implementation but not mode-only UI work.

- [X] T003 Add the engine transaction declaration for committing Grid Warp tempo points, anchors, and clip reference metadata in `src/managed/Nota.Application/Engine/IAudioEngine.cs`, `src/managed/Nota.Infrastructure/Interop/NativeMethods.Tempo.cs`, `src/managed/Nota.Infrastructure/Interop/NotaEngine.Tempo.cs`, and `src/native/nota.engine/include/nota/nota_engine.h`.
- [X] T004 Add smoke tests for the Grid Warp transaction's source-time preservation, BPM derivation, out-of-range/crossing rejection, unchanged state on rejection, single-step undo/redo, and tempo-map serialization in `tests/Nota.SmokeTest/GridWarpTests.cs` and register them in `tests/Nota.SmokeTest/Program.cs`.
- [X] T005 Implement the native Grid Warp transaction in `src/native/nota.engine/src/engine/Engine.h`, `src/native/nota.engine/src/engine/Engine_Tempo.cpp`, and `src/native/nota.engine/src/abi/nota_engine_core.cpp`; commit map points, anchors, and the clip's Grid follows audio reference together, preserve events outside the affected range, and reject invalid intervals without publishing a partial graph.

**Checkpoint**: Managed callers can commit one validated map/reference edit; invalid candidates leave project/audio/undo state unchanged.

## Phase 3: User Story 1 - Choose a warp editing mode (Priority: P1)

**Goal**: Provide mutually exclusive None, Audio follows grid, and Grid follows audio tool toggles whose mode-only clicks do not modify audio or tempo.

**Independent Test**: Exercise every mode transition and compare clip warp/tempo state before and after each mode-only click.

### Tests for User Story 1

- [X] T006 [US1] Extend `specs/079-grid-warp/ui/Program.cs` to assert exclusive mode states, active-mode deactivation to None, and unchanged clip/tempo state on mode-only clicks.

### Implementation for User Story 1

- [X] T007 [US1] Replace the Audio follows grid and Grid follows audio action buttons with mutually exclusive, orange-accented toggle controls and editor-local mode state in `src/managed/Nota.App/AudioClipEditorView.cs`.
- [X] T008 [US1] Route Sample Editor pointer gestures by the selected warp mode, preserve existing no-mode interactions, and prevent mode switching from calling clip-warp or tempo-map mutations in `src/managed/Nota.App/AudioClipEditorView.cs` and `src/managed/Nota.App/AudioClipEditorView.Transients.cs`.
- [X] T009 [P] [US1] Add or verify localized accessible names, help text, and French translations for the three mode controls in `src/managed/Nota.App/AudioClipEditorView.cs` and `src/managed/Nota.App/Languages/fr.json`.

**Checkpoint**: Mode selection is latched, exclusive, side-effect-free, and independently testable.

## Phase 4: User Story 2 - Fit the musical grid to a performance (Priority: P1)

**Goal**: Draw and drag project beat lines onto detected attacks, maintain fixed anchors, preview locally, and commit real piecewise-constant tempo events without altering source audio timing.

**Independent Test**: Map known audio attacks to beats; verify tempo points, BPM, anchor constraints, source sample identity and absolute playback time through edit/undo/redo/save/reload.

### Tests for User Story 2

- [X] T010 [US2] Add tests for beat/time-to-waveform coordinate mapping, project-meter bar/beat generation, BPM calculation, and fixed-anchor interval constraints in `tests/Nota.SmokeTest/GridWarpTests.cs`.
- [X] T011 [US2] Extend `specs/079-grid-warp/ui/Program.cs` to test grid rendering/hit locations, anchor add/move/Alt-delete, Shift snapping bypass, zoom/scroll mapping, local preview without commit, and invalid-gesture cancellation.
- [X] T012 [US2] Add Tempo Track tests for linked-point move/delete synchronization, preservation of unrelated fixed anchors, conflicting BPM rejection, and single-action undo/redo in `tests/Nota.SmokeTest/GridWarpTests.cs`.

### Implementation for User Story 2

- [X] T013 [US2] Add a project-meter-aware beat/time-to-waveform transform and interval validation helper that delegates musical time to the existing engine in `src/managed/Nota.Application/Editing/GridWarpMapping.cs`.
- [X] T014 [US2] Draw visible bar/beat grid lines and distinct hover/committed-anchor/transient styles in the waveform using the current zoom and scroll transforms in `src/managed/Nota.App/AudioClipEditorView.cs`.
- [X] T015 [US2] Implement Grid follows audio hover and drag previews with target-transient snapping, Shift bypass, beat/time/BPM readout, and no engine commit during pointer movement in `src/managed/Nota.App/AudioClipEditorView.cs`.
- [X] T016 [US2] Implement fixed-anchor creation, drag, Alt-click removal, neighbor constraints, cancellation, and one validated commit through the Grid Warp engine transaction in `src/managed/Nota.App/AudioClipEditorView.cs` and `src/managed/Nota.Infrastructure/Interop/NotaEngine.Tempo.cs`.
- [X] T017 [US2] Display Tempo Track BPM values and update linked anchor state atomically when its linked points move or delete; preserve fixed anchors during unrelated edits and reject conflicting BPM edits in `src/managed/Nota.App/TempoTrackControl.cs` and `src/managed/Nota.App/ArrangementView.Tempo.cs`.
- [X] T018 [US2] Verify Grid follows audio playback continues using original source frames and absolute timing while tempo-map changes synchronize transport, Arrangement, Piano Roll, and metronome in `src/native/nota.engine/src/engine/Engine_Render.cpp`, `src/native/nota.engine/src/engine/Engine_Tempo.cpp`, and `tests/Nota.SmokeTest/GridWarpTests.cs`.
- [X] T019 [US2] Verify committed anchors and tempo points survive project save/load and edits restore as one undo/redo action in `src/managed/Nota.Infrastructure/Persistence/ProjectService.cs`, `src/managed/Nota.Infrastructure/Persistence/AudioClipState.cs`, and `tests/Nota.SmokeTest/GridWarpTests.cs`.

**Checkpoint**: A Grid Warp gesture updates the actual project clock and Tempo Track, while the audio source and absolute playback remain stationary.

## Phase 5: User Story 4 - Continue using Audio follows grid (Priority: P1)

**Goal**: Preserve the existing stretch-marker workflow as an independent mode with a fixed tempo map.

**Independent Test**: Move an Audio follows grid warp marker, verify its established nondestructive stretch, and confirm tempo points and Grid Warp anchors do not change.

### Tests for User Story 4

- [X] T020 [US4] Add regression coverage for Audio follows grid marker edits, warp-cache playback, unchanged tempo events, and mode isolation in `tests/Nota.SmokeTest/GridWarpTests.cs` and `specs/079-grid-warp/ui/Program.cs`.

### Implementation for User Story 4

- [X] T021 [US4] Keep Audio follows grid gestures on the existing warp-marker/cache path and ensure None/Grid follows audio cannot invoke that path in `src/managed/Nota.App/AudioClipEditorView.cs` and `src/managed/Nota.App/AudioClipEditorView.Transients.cs`.
- [X] T022 [US4] Ensure selecting or leaving Audio follows grid does not enable, disable, or rebuild clip warp state until an explicit warp gesture is committed in `src/managed/Nota.App/AudioClipEditorView.cs`.

**Checkpoint**: Existing audio stretch behavior remains intact and project tempo is unchanged by Audio follows grid edits.

## Phase 6: User Story 3 - Tune transient detection (Priority: P2)

**Goal**: Replace numeric detection controls with bounded sliders and asynchronously refresh shared transient results without losing committed anchors.

**Independent Test**: Exercise endpoints/defaults, verify lower/higher threshold behavior and 5–250 ms minimum spacing, then rapidly adjust values and confirm only latest analysis results appear.

### Tests for User Story 3

- [X] T023 [US3] Add tests for 0–100% sensitivity mapping, 5–250 ms spacing validation, spacing enforcement, and stale-job cancellation in `tests/Nota.SmokeTest/TransientAnalysisTests.cs`.
- [X] T024 [US3] Extend `specs/079-grid-warp/ui/Program.cs` to verify slider ranges/default values/value labels, debounced refresh, latest-result display, and preservation of committed anchors.

### Implementation for User Story 3

- [X] T025 [US3] Replace the sensitivity and minimum-spacing `DragNumber` controls with horizontal sliders showing 0–100% (default 50%) and 5–250 ms (default 30 ms) in `src/managed/Nota.App/AudioClipEditorView.Transients.cs`.
- [X] T026 [US3] Update transient settings validation and tests so sensitivity remains normalized internally and minimum spacing is constrained to 0.005–0.250 seconds in `src/managed/Nota.Application/Analysis/ITransientAnalysisService.cs` and `tests/Nota.SmokeTest/TransientAnalysisTests.cs`.
- [X] T027 [US3] Debounce settings changes, cancel obsolete per-source work, re-analyse active sources asynchronously, and expose only snapshots matching current settings in `src/managed/Nota.App/TransientWorkspace.cs` and `src/managed/Nota.App/AudioClipEditorView.Transients.cs`.
- [X] T028 [US3] Verify waveform transient markers/count follow the latest completed analysis and committed tempo anchors are not regenerated or moved during re-analysis in `src/managed/Nota.App/AudioClipEditorView.Transients.cs` and `tests/Nota.SmokeTest/TransientMappingTests.cs`.

**Checkpoint**: Slider-driven analysis is responsive, bounded, and independent from committed Grid Warp anchors and real-time audio.

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Keep Nota's user-facing documentation and quality gates in sync.

- [X] T029 [P] Update `CHANGELOG.md` with the Grid Warp and transient-control capability.
- [X] T030 Update `FEATURES.md` using the Nota Features workflow for the new Grid Warp capability.
- [X] T031 Run focused UI and smoke-test selectors documented in `specs/079-grid-warp/quickstart.md`; resolve regressions.
- [X] T032 Run `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\build-win.ps1` and confirm native, managed, and smoke-test gates pass.
- [X] T033 Confirm every new source file has the required AGPL SPDX header and no dependency was added; update `LICENSES/third-party.md` only if a dependency is added.

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1) precedes test work.
- Foundational transaction work (Phase 2) is required before Grid Warp can commit map/clip state; it does not block mode-only UI tests.
- User Story 1 (Phase 3) is the smallest user-visible increment.
- User Story 2 (Phase 4) depends on the transaction foundation and the selected-mode routing in US1.
- User Story 4 (Phase 5) depends on US1 mode routing; it must not share Grid Warp's edit path.
- User Story 3 (Phase 6) can be implemented after the P1 editing paths; it uses the shared transient service and feeds transient candidates to US2.
- Polish (Phase 7) follows all desired stories.

### User Story Dependencies

- **US1 (P1)**: Independent after setup; no project edits when selecting a mode.
- **US2 (P1)**: Depends on the foundational transaction and US1 mode selection.
- **US4 (P1)**: Depends on US1 mode selection; its warp path is independent of US2 tempo edits.
- **US3 (P2)**: Uses the existing analysis service; US2 can initially use current default analysis settings, then US3 supplies the new controls.

### Parallel Opportunities

- T001–T002 UI harness work can be done without touching application code.
- T004 native regression tests and T006 US1 UI tests cover different projects and can be developed in parallel once declarations exist.
- T009 localization can proceed independently from mode rendering after strings are selected.
- T023 service tests and T024 UI slider tests can proceed in parallel before slider/service implementation.
- T029 changelog and T030 feature-list update are independent documentation changes after behavior is implemented.

## Implementation Strategy

1. Establish the headless UI harness and the atomic validated engine transaction.
2. Deliver US1 as the first visible increment: exclusive mode toggles with no side effects.
3. Implement and verify the core US2 grid/anchor/tempo workflow, including source-time and save/undo invariants.
4. Verify US4 does not regress the existing audio-stretch workflow.
5. Add US3 bounded sliders and debounced background re-analysis.
6. Update documentation, run focused regression checks, then run the full Windows build gate.

## Nota Closing Tasks

- User-visible capability update: T029–T030 cover `CHANGELOG.md` and `FEATURES.md`.
- No new dependency is planned; confirm this in T033.
- Engine behavior changes are covered by `tests/Nota.SmokeTest/GridWarpTests.cs` and transient test extensions.

## Phase 8: Convergence

- [X] T034 [US2] Render a distinct draggable handle when a musical grid line is hovered and verify its visibility and hit alignment in `src/managed/Nota.App/AudioClipEditorView.GridWarp.cs` and `specs/079-grid-warp/ui/Program.cs` per FR-003 (partial).


## Phase 9: Completion foundation and source preservation

- [X] T035 Update spec/plan/research/contracts for the full attachment and original-tempo drop workflow; preserve prior tasks and capture current root cause evidence.
- [X] T036 Add failing GridWarpTests source-marker identity/trim/origin/pitch/seam/stable-ID regressions before native fixes.
- [X] T037 Decouple grid source window/absolute origin from audio warp markers in Engine_Tempo.cpp, Engine_Tracks.cpp and renderer; expose authoritative geometry through IAudioEngine/tempo ABI and persist it in AudioClipState.
- [X] T038 Assign stable anchor IDs and explicit tempo-point links using existing reserved POD slots; preserve move/undo/reload identity in GridWarpMapping, TempoTrackControl, native tempo/ABI and tests.
- [X] T039 Reject invalid pitch/origin and unsupported ramp seams atomically; preserve unaffected Step events and external fixed anchors with explicit boundary policy/tests.

## Phase 10: Original-source beat extraction (US5)

- [X] T040 Add deterministic pulse/rubato/subdivision/missing/ambiguous/fixed/silence/resource/cancellation fixtures in tests/Nota.SmokeTest/SourceBeatTests.cs.
- [X] T041 Implement bounded Application SourceBeatInference and source result/settings records using original source frames, confidence and distinct interpolated proposals.
- [X] T042 Extend the shared transient analysis service with cancellable bounded beat inference/caching after source capture; retain original BPM import seed and optional content-addressed source timing cache through save/reload without preview dirtying authoring state.
- [X] T043 Add inline Snap All Beats/start/min/max/sensitivity/Preview/Apply/Cancel controls and distinct draggable/numeric-correctable proposals in AudioClipEditorView extraction/GridWarp partials; preserve fixed manual anchors and revalidate async source/region identity.
- [X] T044 Route captured Arrangement audio drag onto Tempo row before move commits, retain original clip/source location and open the existing editor for extraction; cover MainWindow integration and cancellation.
- [X] T045 Verify extraction before/after auto-warp/project BPM changes, preview/cancel no mutation, one-step Apply, original PCM/warp marker preservation and source cache reload in smoke/UI tests.

## Phase 11: Complete manual controls and Reset (US6)

- [X] T046 Add discoverable Add anchor, selected Beat/Time, fixed-anchor selection and first/last boundary feedback in existing editor; remove hover interception and preserve handle hit areas.
- [X] T047 Add graph-owned reference-region reset baseline and native Reset/state read/restore ABI; serialize baseline with clip grid metadata and retain unrelated map/protected anchors atomically.
- [X] T048 Wire Reset/Undo into existing editor and test repeated mapping/reset/undo/save/reload, source waveform identity and external tempo preservation.

## Phase 12: Verification and delivery

- [X] T049 Extend actual-app 079 headless harness for proposals, correction, Apply/Cancel, Add/Reset/numeric, real captured Tempo-row drop, zoom/scroll and mode isolation in dark/light.
- [X] T050 Localize EN/fr controls/status and update actual shortcuts, FEATURES, CHANGELOG and EN/RU manual/screenshots using Nota skills.
- [X] T051 Run focused native/source/GridWarp/UI tests, full Windows build/smoke/scanner gate and actual Windows app; record all 18 attachment workflow checks, root cause, numerical evidence and piano/hardware limitations in verification.md.
- [X] T052 Run speckit-converge against the expanded spec/plan/tasks, append and resolve real gaps, and report concrete supported behavior.

Dependencies: T035–039 → T040–045 and T046–048 → T049–052. Tests precede the corresponding
behavior. Source inference is pure and can be verified separately from native transactions.
The current attachment supersedes prior completeness claims; old checked tasks alone do not
satisfy the new automatic extraction, reset or source-marker preservation requirements.

## Phase 13: Convergence

- [X] T053 Preserve unique Grid Warp anchor identity across deletion/recreation and loaded baselines with a monotonic authoring allocator in Engine.h, Engine_Tempo.cpp and Engine_GridReset.cpp; add a delete/add regression in GridWarpTests.cs per FR-021 (partial).
