# Tasks: SuperFlux, warp and tempo mapping

## Phase 1: Setup

- [x] T001 Inspect actual engine/editor/clock architecture and record reuse in specs/076-superflux-warp-tempo/research.md.
- [x] T002 Specify and clarify scope, lifecycle and constraints in specs/076-superflux-warp-tempo/spec.md, plan.md and contracts/transients.md.

## Phase 2: Foundation

- [x] T003 Preserve upstream BSD-2-Clause notice in LICENSES/SuperFlux.txt and register adaptation in LICENSES/third-party.md.
- [x] T004 Define immutable Application settings/results/job/service port in src/managed/Nota.Application/Analysis/ITransientAnalysisService.cs.

## Phase 3: US1 - Source transients

Independent test: deterministic source attacks with bounded working storage, cancellation and shared cached snapshots; no playback changes.

- [x] T005 [US1] Add synthetic C ABI and service fixtures in tests/Nota.SmokeTest/TransientAnalysisTests.cs for sharp/soft/piano/chords/arpeggios/vibrato/silence/noise/stereo/rates and invalid inputs.
- [x] T006 [US1] Implement streaming original log-filterbank, delayed frequency-maximum difference, adaptive local peaks and spacing in src/native/nota.engine/src/media/SuperFlux.h and SuperFlux.cpp.
- [x] T007 [US1] Add optional conservative local attack refinement with confidence/uncertainty in src/native/nota.engine/src/media/SuperFlux.cpp; retain uncertain frame timing.
- [x] T008 [US1] Capture source independently of engine lifetime in src/native/nota.engine/src/engine/Engine_Tracks.cpp; expose step/cancel/read POD ABI in include/nota/nota_engine.h and src/abi/nota_engine_transients.cpp; register in CMakeLists.txt.
- [x] T009 [US1] Bind ABI through src/managed/Nota.Infrastructure/Interop/NativeMethods.Transients.cs and NotaEngine.Transients.cs; keep FFT off authoring/audio threads.
- [x] T010 [US1] Implement shared cache/background service in src/managed/Nota.Infrastructure/Analysis/TransientAnalysisService.cs; source/settings key, 16 entries/200000 records, at most two workers, invalidation, progress, cancellation and O(log N) lookup.
- [x] T011 [US1] Verify reference agreement, cancellation/invalidation/retry, settings separation, query boundaries, concurrent requests and captured-source lifetime in tests/Nota.SmokeTest/TransientAnalysisTests.cs.
- [x] T012 [US1] Compile native/managed and run focused + complete smoke tests using scripts/build-win.ps1; record actual results in specs/076-superflux-warp-tempo/verification.md.

## Phase 4: US2 - Audio follows grid

Independent test: visible guides track trim/rate/warp, selected anchors snap nondestructively with undo/reload.

- [x] T013 [US2] Generalise/reuse src/managed/Nota.App/ClipEditor/ClipTempo.cs and ClipTimeMap.cs for source-to-clip/arrangement transforms including rate/reversal/trim; add tests/Nota.SmokeTest/TransientMappingTests.cs.
- [x] T014 [US2] Add cached transient overlays and nearest-guide drag highlighting to src/managed/Nota.App/AudioClipEditorView.cs and ArrangementView.LaneControl.cs using shared analysis service; avoid playback/pointer geometry rebuilds.
- [x] T015 [US2] Add Analyse/Show/Snap, sensitivity/spacing controls via existing toolbar/editor in src/managed/Nota.App, Loc and Languages/fr.json; preserve muted red Controls/WarpRuler.cs and keyboard mappings.
- [x] T016 [US2] Integrate explicit selected-attack anchors with existing warp validation/stretch/undo/persistence in AudioClipEditorView.cs and Engine_Tracks.cpp; manual placement and snapping toggle, no automatic quantisation of all attacks.
- [x] T017 [US2] Verify trim/move/anchor crossing/undo/reload/rendered boundaries through tests/Nota.SmokeTest and actual app; record evidence in specs/076-superflux-warp-tempo/verification.md.

## Phase 5: US3 - Grid follows audio

Independent test: rubato map changes grid and synchronises every musical clock without stretching source.

- [ ] T018 [US3] Add separate beat inference/tempo candidates, first-beat/meter, half/double ambiguity and smoothing in src/native/nota.engine/src/media; test variable-tempo, accelerando/ritardando and beat-vs-attack distinction.
- [ ] T019 [US3] Add immutable strictly monotonic beat/time map and forward/inverse tests in src/native/nota.engine/src/engine; publish via graph/command rules.
- [ ] T020 [US3] Migrate Transport, Engine_Render MIDI/controllers/automation/Session/audio/metronome/recording, plugin TransportInfo and warp-cache conversion to shared map in src/native/nota.engine/src/engine and pluginhost/hosting; verify block timing end-to-end.
- [ ] T021 [US3] Persist/undo map via src/managed/Nota.Infrastructure/Persistence/ProjectModel.cs, ProjectMigrations.cs and existing graph history.
- [ ] T022 [US3] Add Detect tempo/Smoothing/Grid follows audio/Audio follows grid/Preview/Apply/Reset controls to existing src/managed/Nota.App editors with Loc/fr translations and app verification.

## Phase 6: Polish and verification

- [x] T023 Sync accurate delivered capabilities in CHANGELOG.md, FEATURES.md and ../nota-docs EN/RU pages using nota-features/nota-docs skills.
- [ ] T024 Run Windows scripts/build-win.ps1 and convergence; record full modified-file list, actual performance/reference evaluation, license and limitations in specs/076-superflux-warp-tempo/verification.md. Annotated real recordings required before claiming real-world precision/recall.

## Dependencies and implementation strategy

T001-004 → US1 → US2; US3 depends on US1 but can be designed independently of waveform UI. US3 clock migration precedes any working Grid follows audio control. Tests precede corresponding implementation. Within US1 native DSP and managed service files can be worked independently after contracts; within US2 coordinate tests and UI design can proceed independently; within US3 inference/map tests can proceed independently. No duplicate worker pool or time authority. Deliver verified US1 first as explicitly requested if entire scope does not fit one pass; unchecked tasks remain open and convergence must not claim full completion.

## Phase 7: Convergence

Audit 2026-11-02: checked 12 functional requirements, 6 success criteria, 10 story acceptance scenarios, design/lifecycle decisions and all 8 constitution principles. Outcome: tasks appended; whole feature **not converged**. Five findings: four high (missing/partial integration), one medium (partial verification). No constitution violations. These tasks trace and consolidate the still-open T013-T024 rather than marking their work complete.

- [x] T025 Generalise existing ClipTimeMap/ClipTempo source-to-arrangement mapping, including source offset, trim, rate, reverse and current warp anchors; validate to one-sample precision in tests/Nota.SmokeTest/TransientMappingTests.cs per FR-005/SC-005 (missing; HIGH, F1; continues T013).
- [x] T026 Integrate shared analysis with AudioClipEditorView.cs and ArrangementView.LaneControl.cs overlays, nearest guide highlighting, explicit selected-anchor snapping/manual placement and existing undo/stretch/persistence; expose localised Analyse/Show/Snap/sensitivity/spacing controls using nota-design without changing toolbar keys per FR-006/007/011 and US2 (partial; HIGH, F2; continues T014-T017).
- [ ] T027 Implement separate beat inference, confidence/ambiguity, first beat/meter, half/double tempo alternatives and smoothing in src/native/nota.engine/src/media with piano/rubato fixtures per FR-008 and US3/AC1-2 (missing; HIGH, F3; continues T018).
- [ ] T028 Implement immutable monotonic beat/time map, migrate Transport and all scalar clock consumers including MIDI/Session/automation/plugin transport/audio/recording, persist/undo it, then add preview/apply/reset and two workflow controls in src/managed/Nota.App per FR-009/010, SC-006 and US3/AC3 (missing; HIGH, F4; continues T019-T022).
- [ ] T029 Complete workflow/mapping/render-boundary/synchronisation tests and app verification, annotated real-piano evaluation and CPU/reference performance measurements; update delivered UI capabilities in CHANGELOG.md/FEATURES.md and EN/RU manual when ../nota-docs is available; rerun Windows build and convergence per FR-012/SC-005/SC-006 (partial; MEDIUM, F5; continues T023-T024). Current stage has no user workflow/manual change; native analysis documentation is complete.

## Phase 8: Convergence

Second-increment audit: checked all 12 functional requirements, 6 success criteria, 10 story acceptance scenarios, design decisions and 8 constitution principles. US1/US2 and their UI/manual/shortcut work are implemented. T013-T017, T023, T025-T026 are verified complete; ClipTimeMap was moved into Application and its original editor consumers share the extracted WarpPositionMap. Native and UI anchor validation, complete marker reads beyond 128 anchors, save/load and real-app headless gestures are covered. No constitution violations found. Full feature outcome remains **tasks appended / not converged**: two HIGH missing findings and one MEDIUM partial evaluation finding.

- [ ] T030 Implement beat inference separately from transients in src/native/nota.engine/src/media, with manual first beat/meter, initial BPM, half/double ambiguity, confidence and rubato-preserving smoothing; add labelled variable-tempo fixtures per FR-008/US3/AC1-2 (missing; HIGH; continues T018/T027).
- [ ] T031 Implement immutable strictly monotonic beat/time map in src/native/nota.engine/src/engine, migrate Transport/MIDI/Session/automation/plugin/audio/recording clocks and warp-cache conversions, persist/undo it through Nota.Infrastructure/Persistence, then expose Grid follows audio and preview/apply/reset/smoothing in Nota.App with localisation and EN/RU docs per FR-009/010, SC-005/006 and US3/AC3 (missing; HIGH; continues T019-T022/T028). Existing Audio follows grid and selected-attack controls are complete.
- [ ] T032 Finish US3 forward/inverse/synchronisation and full-workflow playback checks in tests/Nota.SmokeTest, evaluate annotated real piano and reference CPU usage, sync the new tempo workflow's EN/RU manual, rerun Windows gate and convergence, and record results in this spec directory per FR-012/SC-005/006 (partial; MEDIUM; continues T024/T029). Current US2 Windows/UI/docs checks pass; historical initial-stage manual omissions do not describe the current state.



