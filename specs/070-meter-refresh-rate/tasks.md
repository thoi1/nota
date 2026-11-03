# Tasks: Meter refresh rate

## Phase 1: Setup
- [x] T001 Complete specification, clarification and plan in specs/070-meter-refresh-rate/.

## Phase 2: US1 — choice and persistence
- [x] T002 [US1] Add normalized 30/60 preference in Application/Settings.cs and MeterRefreshRate.cs; native Appearance picker in App/PreferencesWindow.cs.
- [x] T003 [US1] Test default, invalid and persisted values in tests/Nota.SmokeTest/MeterRefreshTests.cs.

## Phase 3: US2 — independent display cadence
- [x] T004 [US2] Add Presentation/ViewModels/MeterRefreshCadence.cs and integrate shared frame scheduling in App/MainWindow.Playhead.cs and MainWindow.axaml.cs.
- [x] T005 [US2] Split pure level reads from control/scene sync in App/ArrangementView.cs, MixerView.cs, SessionView.cs and SessionView.Mixer.cs.
- [x] T006 [US2] Add deterministic cadence/rate-switch/stall tests in tests/Nota.SmokeTest/MeterRefreshTests.cs and register in Program.cs.

## Phase 4: US3 — visibility and measurements
- [x] T007 [US3] Extend an actual-app probe in specs/070-meter-refresh-rate/validation/ for UI selection, process persistence, themes, 30/60 updates/overhead and lifecycle.

## Phase 5: Documentation and completion
- [x] T008 Update CHANGELOG.md, FEATURES.md, App/Languages/fr.json and manual handoff.
- [x] T009 Run Windows x64 build/smoke/scanner tests; record results in validation.md and run speckit-converge.

Dependencies: preference before scheduler, splits before switching feed, tests before final gate.
Independent stories: real picker/persistence; cadence and unchanged metrics; hidden/floating views.
Parallel opportunities: documentation and diagnostic design after T002; execute shared files sequentially.


