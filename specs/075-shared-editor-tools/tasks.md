# Tasks: Shared editor tools

## Phase 1 — Inspect/specify/clarify/plan

- [x] T001 Inspect actual Arrangement/Piano Roll tool, keyboard, note/controller/ruler and undo architecture; save dirty-file baseline outside repo.
- [x] T002 Write spec/checklist, resolve ambiguities from pasted brief, complete plan/research/data-model/contracts/quickstart in this directory. Hooks are empty; no clarification question needed.

## Phase 2 — Shared UI and state (US1/US2)

- [x] T003 Add `Nota.Application/Editing/EditorToolState.cs` for separate tool state, shared waveform/settings, cycle transitions and command mapping.
- [x] T004 Extract `Nota.App/Controls/EditingToolBar.cs` and shared cursor/icon definitions; bind Arrangement and Piano Roll to it, preserving layout and resize.
- [x] T005 Add `tool.*` commands to `CommandCatalog.cs`, bind in `MainWindow.Commands.cs`, route 1–5 using focused/last-clicked editor in `MainWindow.Input.cs` and shared/floating input paths, with typing/modal guards and repeat suppression.
- [x] T006 Add dynamic shape Glyphs and synchronized menus/icons/shortcut labels in `Controls/Glyph.cs` / shared toolbar; maintain independent active tools.

Independent validation: both toolbars match; 1–5 select in each context, 4 cycles once per press and wraps; dropdown/shape shared, modifiers/typing/keypad unaffected.

## Phase 3 — Context drawing (US3)

- [x] T007 Add cancellable Pencil note preview and Eraser routing in `PianoRollView.Tools.cs` / NoteGrid, using existing note ownership/default length/velocity/bounds/commit.
- [x] T008 Extend existing ControllerLane in `PianoRollView.Controllers.cs` with shared waveform adapter, Eraser and snap/locked-target guards, preserving Select behavior and one undo on release.
- [x] T009 Wire Escape/tool change/window-loss cancellation and clear missing-CC instructions; preserve Arrangement adapters/navigation and existing mouse modifiers.

Independent validation: note create/erase; CC1/CC11 all shapes/range/order/outside data; reverse draw, undo/redo, cancellation and locked/missing targets. No note mutation from waveform drawing.

## Phase 4 — Warp feedback (US4)

- [x] T010 Add shared `Controls/WarpRuler.cs` rendering and wire both ruler renderers/hover invalidation; muted light-red tint/readable WARP badge and explicit unavailable-engine help. No fake tempo or sample/note edits.

Independent validation: only the selected editor's ruler changes; hover/navigation and restore work; timing/audio/MIDI data unchanged.

## Phase 5 — Validation/docs/convergence

- [x] T011 Add meaningful state/cycle/command/controller/undo tests in `tests/Nota.SmokeTest/SharedEditorToolTests.cs` and register them; exercise actual UI focus/typing/repeat/drawing/rulers using Windows headless harness.
- [x] T012 Update `PreferencesWindow.cs`, `Languages/fr.json`, `CHANGELOG.md`, `FEATURES.md`, and EN/RU manual if available (otherwise prepare bilingual update and record missing repo).
- [x] T013 Run Windows build/smoke and appropriate focused/UI checks; inspect dark/light and narrow/detached layouts; document exact modified files/evidence/limitations in `verification.md`.
- [x] T014 Review local-only delta, headers, no new dependencies, safety, state/gesture ownership and run convergence. No commit/push/PR.

## Dependencies and strategy

T001–T002 → T003–T004 → T005–T006 → T007–T009 → T010 → T011–T014. UI/state before editor-specific drawing as requested. Research is read-only delegated per plan skill; implementation remains sequential. Independent tests/docs may be prepared separately; no parallel file edits. Existing working editor paths remain authoritative.
