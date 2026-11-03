# Tasks: Arrangement editing tools

**Input**: spec.md, plan.md, research.md, data-model.md, contracts/ui.md, quickstart.md

## Phase 1: Setup

- [x] T001 Inspect actual toolbar, pointer handlers, Glyph, snap, automation and undo in `src/managed/Nota.App` and `Nota.Application`; capture the existing dirty-file baseline.
- [x] T002 Complete specification/clarification and planning artifacts in `specs/074-arrangement-editing-tools/`; no critical clarification remains, hooks are empty.

## Phase 2: Foundation

- [x] T003 Add tool/waveform types and bounded pure drawing/clip interval helpers in `src/managed/Nota.Application/Editing/ArrangementTools.cs`.
- [x] T004 Add per-Arrangement tool selection, cancellation, shared automation preview and Glyph-based cursors in `src/managed/Nota.App/ArrangementView.Tools.cs`.

## Phase 3: US1 — Clip tools

Independent test: select every tool twice; Pencil click/drag empty instrument space, undo/redo; Eraser remove MIDI/audio, undo/redo; Select move/trim/Alt-copy and navigation unchanged.

- [x] T005 [US1] Add inline tool group/settings affordance in `MainWindow.axaml`, synchronize in `MainWindow.ArrangementTools.cs`, preserve narrow-window access and add missing icons in `Controls/Glyph.cs`.
- [x] T006 [US1] Add Pencil interval preview and Eraser click routing in `ArrangementView.LaneControl.cs` and a focused partial; preserve right-click/Select paths.

## Phase 4: US2 — Automation drawing

Independent test: all five shapes plus Pencil/Eraser in track, return, master; reverse sweeps, range clamping, outside-point preservation, one undo/redo and cancellation.

- [x] T007 [US2] Route existing track/return `ArrangementView.AutoEditor.cs` and footer master `ArrangementView.Controls.cs` through shared stroke lifecycle; protect previews from live refresh.
- [x] T008 [US2] Implement waveform/amplitude/cycle/phase secondary menu in `MainWindow.ArrangementTools.cs` with localized strings and no new MIDI CC infrastructure.

## Phase 5: US3 — Nota Warp

Independent test: selecting Nota Warp explains unavailable tempo-grid editing and leaves audio/tempo/ruler navigation untouched.

- [x] T009 [US3] Add horizontal native WarpGrid glyph and explicit unavailable state/status in `Controls/Glyph.cs` / `ArrangementView.Tools.cs` / toolbar help.

## Phase 6: Validation and documentation

- [x] T010 Add meaningful range/shape/reverse/grid/bounds and native undo tests in `tests/Nota.SmokeTest/ArrangementToolTests.cs`, register in `Program.cs`.
- [x] T011 Update `Languages/fr.json`, `PreferencesWindow.cs` gesture reference, `CHANGELOG.md`, `FEATURES.md` and EN/RU manual (or record absent sibling dependency).
- [x] T012 Build managed and full Windows script/smoke; inspect UI if available; record concrete results in `specs/074-arrangement-editing-tools/verification.md`.
- [x] T013 Review local-only delta for preservation, AGPL headers/no dependencies/audio-thread impact; run convergence against spec/plan/tasks with no commit/push/PR.

## Dependencies and implementation strategy

T001–T002 → T003–T004 → T005–T006 (US1 MVP) → T007–T008 (US2) → T009 (US3) → T010–T013.
Independent icon/document/test tasks can be checked separately; implementation remains sequential because the partials share state. No parallel editing agents. Incrementally build after wiring, then run meaningful integration checks once stable.

## Phase 7: Convergence

- [ ] T014 Sync the implemented toolbar/drawing workflow into the EN/RU Arrangement manual, main-window overview and generated shortcuts in `../nota-docs`, then retake screenshots and run its build/check per FR-008 and Constitution VI (partial). External dependency: `C:/Dev/nota-docs` does not exist. Ready-to-apply bilingual content is in `manual-update.md`; implementation and Windows validation are complete. No application-code work is blocked by this item.
