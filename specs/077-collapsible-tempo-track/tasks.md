# Tasks: Collapsible Tempo Track

**Input**: spec.md, plan.md, research.md, data-model.md, contracts/tempo.md.
Tests explicitly requested. Existing dirty worktree and tempo-lag fix must be preserved.

## Phase 1 — Setup and architecture

- [x] T001 Inspect native/managed clocks, warp, layout, persistence and undo; record specs/077-collapsible-tempo-track/{clock-audit,ui-audit,research}.md.
- [x] T002 Specify stories, contracts and bounded immutable map design in specs/077-collapsible-tempo-track/{spec,plan,data-model,quickstart}.md and contracts/tempo.md.

## Phase 2 — Shared foundation

- [x] T003 Add native analytical/reference/inverse/bounds/dense-map tests in src/native/nota.engine/src/engine/tests/TempoMapTest.cpp; register CMakeLists.txt.
- [x] T004 Implement immutable prepared Step/Linear/Smooth TempoMap with bounded integration/table resources in src/native/nota.engine/src/engine/TempoMap.{h,cpp}.
- [x] T005 Add map and protected anchor state to Graph.{h,cpp}; publish validated map transactions and conversions in Engine_Tempo.cpp/Engine.h with C ABI in nota_engine.h and nota_engine_core.cpp.
- [x] T006 Bind POD tempo points/anchors/map queries through Nota.Application/Engine/TempoMap.cs, IAudioEngine.cs and Nota.Infrastructure/Interop/NativeMethods.Tempo.cs, NotaEngine.Tempo.cs.

## Phase 3 — US1 first delivery: visible editable tempo

Independent check: actual Arrangement lane add/move/delete/numeric/interpolation/collapse/resize, undo and reload.

- [x] T007 [US1] Add pinned TempoTrackControl.cs and ArrangementView.Tempo.cs, sharing ruler/scroll/header width in ArrangementView.cs and Studio tokens.
- [x] T008 [US1] Implement Step/Linear point add/select/drag/fine/delete/numeric/context editing, protected initial point, local previews and single graph-undo commits in TempoTrackControl.cs.
- [x] T009 [US1] Persist map/bounds/anchors in ProjectModel.cs, ProjectService.cs and ProjectMigrations.cs; retain visible/expanded/height in ArrangementView.Sizing.cs.
- [x] T010 [US1] Wire native map edits/top BPM/refresh/background caches through MainWindow.TempoWarp.cs, TransportViewModel.cs and undo/load lifecycle; test constant compatibility in tests/Nota.SmokeTest/TempoMapTests.cs.

## Phase 4 — US2 shared playback

Independent check: known event samples through step/ramp/loop/seek at multiple rates agree with one native map.

- [x] T011 [US2] Migrate Transport.{h,cpp} seek/position/loop/plugin snapshots/presentation to map conversions; preserve constant-tempo fast path.
- [x] T012 [US2] Slice Engine_Render.cpp at prepared timing boundaries; migrate absolute MIDI/expression/controllers/automation/metronome/Session/recording conversions and master plugin timing.
- [x] T013 [US2] Prepare map-aware warp cache keys/output-time markers and absolute audio placement in Engine_Tracks.cpp, Engine_Render.cpp and media/Warp.h using existing worker.
- [x] T014 [US2] Migrate recording/take and freeze/bounce geometry in Engine_Recording.cpp, Engine_Takes.cpp and Engine_Freeze.cpp (actual source paths from audit), preserving absolute source time.
- [x] T015 [US2] Migrate export/stem/bounce lengths and managed transport/playhead/time rulers through AudioExporter.cs, TrackFreezer.cs, TransportViewModel.cs and Arrangement/piano-roll display helpers.
- [x] T016 [US2] Add native/C ABI scheduling/loop/render/plugin snapshot and performance checks in tests/Nota.SmokeTest/TempoMapTests.cs; retain TempoChangeTests responsiveness regression.

## Phase 5 — US3 shared warp workflows

Independent check: explicit source anchors update same Tempo Track/map, unchanged source, protected conflict rejects atomically.

- [x] T017 [US3] Add explicit source-time/beat Grid follows audio fitting and Audio follows grid transactions plus protected-anchor validation in Engine_Tempo.cpp/Engine_Tracks.cpp and tempo ABI/port.
- [x] T018 [US3] Expose workflow/protection/status controls in AudioClipEditorView.Transients.cs and MainWindow/Arrangement tempo integration, preserving existing warp gestures and complete marker lists.
- [x] T019 [US3] Test marker↔map synchronization, absolute/musical anchor policy, locked rejection, undo/source identity and save/reload in tests/Nota.SmokeTest/TempoMapTests.cs.

## Phase 6 — US4 advanced editing

- [x] T020 [US4] Add Smooth curve/context support, multiselect/box/group clamping, copy/paste, keyboard nudging/cancellation and numeric/bounds inspector in TempoTrackControl.cs; route focus before global clip commands.
- [x] T021 [US4] Exercise all gestures, zoom/scroll, dark/light, collapsed/expanded and lane persistence in specs/077-collapsible-tempo-track/ui actual-app headless harness.

## Phase 7 — Documentation, build and convergence

- [x] T022 Localize new UI via Loc and Languages/fr.json; sync actual bindings in PreferencesWindow.Shortcuts.cs (actual group source), FEATURES.md, CHANGELOG.md and EN/RU manual using nota skills.
- [x] T023 Run Windows native/managed/full smoke/scan and focused map/tempo-lag/native tests; build/run actual Windows app and record all 18 scenarios and limits in specs/077-collapsible-tempo-track/verification.md.
- [x] T024 Run speckit-converge against spec/plan/tasks, append real gaps and complete required work; never claim unavailable CLAP hosting or unverified hardware timing.

## Dependencies and execution

T001–002 → T003–006 → T007–010 → T011–016 → T017–019 → T020–021 → T022–024.
T003 and managed contract tests can be prepared independently; UI drawing can be developed
after the port while native playback migration continues, but neither is marked complete
without integrated behavior. Use existing layer boundaries, no duplicate clocks/workers.
First checkpoint is the real lane + Step/Linear + shared warp synchronization; continue
with Smooth, advanced editing and full playback/persistence validation as requested.

## Phase 8: Convergence

- [x] T025 Correct unwarped Session source phase across tempo changes in SessionPlayer.h/.cpp and Engine_Render.cpp; compare real rendered samples with a constant-clock source and retain musical envelope/loop phase per FR-001, FR-008 and plan: natural audio preserves seconds (contradicts).
