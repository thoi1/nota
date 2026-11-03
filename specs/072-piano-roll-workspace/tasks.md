<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Tasks: Piano roll workspace improvements

Input: [plan.md](plan.md), [spec.md](spec.md), research/data-model/contracts.

## Setup and foundations

- [x] T001 Inspect layout/activation/zoom and snapshot pending changes; preserve theme hash.
- [x] T002 Complete spec/clarification/design and identify reusable controls and constraints.

## US1 — Full workspace

- [x] T003 [US1] Add MainWindow workspace snapshot/restore, Arrangement-only MIDI activation,
  focus and visible return label in MainWindow.Detail*.cs, MainWindow.axaml, PianoRollView.cs.
- [x] T004 [US1] Add actual docked/floating activation/restore regressions under validation/.

## US2 — Devices visibility

- [x] T005 [US2] Reuse docked close; add floating Devices header/close in DetailWindow.cs;
  route existing reopen/toggle and preserve manual visibility in MainWindow.Detail*.cs/Menu.cs.
- [x] T006 [US2] Validate close/reopen, zero blank height and continued audio with hidden UI.

## US3 — Lane resizing

- [x] T007 [US3] Add normalized lane-height preferences in Nota.Application/Settings.cs.
- [x] T008 [US3] Add independent captured dividers and scrollable lane layout in
  PianoRollView.ControllerLanes.cs/Expression.cs; persist through settings host wiring.
- [x] T009 [US3] Validate independent heights, bounds, persistence, small windows and MIDI integrity.

## US4 — Onset alignment

- [x] T010 [US4] Remove drawing/picking offsets from VelocityLane in PianoRollView.cs.
- [x] T011 [US4] Add exact onset/hit regressions across zoom/grid/scroll/display scales.

## US5 — Zoom

- [x] T012 [US5] Extend MaxPpb to 3200 in PianoRollView.cs; retain minimum/anchor/scroll bounds.
- [x] T013 [US5] Validate extreme zoom editing, bounded labels/grid and accurate scrolling.

## Polish and delivery

- [x] T014 Update CHANGELOG.md, FEATURES.md, shortcuts and EN/RU manual/handoff; localize UI.
- [x] T015 Run Windows Release build, regression probe, smoke suite and theme/menu checks;
  record results and hardware limits in validation/report.md.
- [x] T016 Review the task-only diff; leave all changes uncommitted per the latest user instruction; no push.

Dependencies: T001–T002 → US1 → US2 → US3 → US4 → US5 → T014–T016.
Sequential implementation requested; read-only research may run independently.
Independent validation checkpoints are T004/T006/T009/T011/T013. No parallel mutations.



