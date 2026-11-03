# Tasks: Piano Roll Snap Toggle

**Input**: Design documents from `specs/078-piano-roll-snap-toggle/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui.md

## Phase 1: Setup

**Purpose**: Establish a focused headless UI test project.

- [X] T001 Create the Avalonia headless UI test project referencing the app in `specs/078-piano-roll-snap-toggle/ui/PianoRollSnapToggleUi.csproj`.

## Phase 2: Foundational

**Purpose**: Define a regression check for the direct-toggle behavior before implementation.

- [X] T002 Add a headless test in `specs/078-piano-roll-snap-toggle/ui/Program.cs` that finds the Piano Roll snap toggle, verifies its initial state, clicks it on and off, and asserts only `PianoRollGrid.SnapOn` changes.

## Phase 3: User Story 1 - Toggle piano-roll snapping directly (Priority: P1)

**Goal**: Let the musician toggle grid snapping from the Piano Roll header and keep its state synchronized with existing controls.

**Independent Test**: In a visible Piano Roll, click the dedicated toggle and verify off-grid edits are preserved while off and snap to the current step while on; change snap through the grid menu and verify the toggle follows.

### Tests for User Story 1

- [X] T003 [US1] Extend `specs/078-piano-roll-snap-toggle/ui/Program.cs` to verify state synchronization after an external grid-setting change and representative note placement with snapping enabled and disabled.

### Implementation for User Story 1

- [X] T004 [US1] Add a localized, accessible Snap-glyph `ToggleButton` beside the grid chip in `src/managed/Nota.App/ClipEditorView.cs`; initialize and update it from `EditorGrids` and apply only the existing Piano Roll snap toggle.
- [X] T005 [P] [US1] Add or verify required French translations for the button name and on/off tooltip in `src/managed/Nota.App/Languages/fr.json` (existing translations cover all reused strings).

**Checkpoint**: The Piano Roll toggle works independently, preserves all other grid settings, and stays in sync with existing controls.

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Keep Nota's user-facing feature list and change record current, then validate the finished behavior.

- [X] T006 [P] Add a `[Unreleased]` entry describing the Piano Roll snap toggle in `CHANGELOG.md` and update `FEATURES.md` using the Nota Features workflow.
- [X] T007 Run the focused UI harness and grid tests from `specs/078-piano-roll-snap-toggle/quickstart.md`; resolve any regressions.
- [ ] T008 Run `scripts/build-win.ps1` and confirm the native build, managed build, and smoke-test gate pass.

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1) precedes test and implementation work.
- Foundational test task (Phase 2) depends on the UI test project setup and precedes implementation.
- User Story 1 depends on the UI test project and its initial regression assertion.
- Polish follows implementation and focused test completion.

### User Story Dependencies

- User Story 1 (P1) has no dependency on another story and is the complete MVP.

### Parallel Opportunities

- T005 can proceed independently from the C# control implementation after the required user-facing strings are selected.
- T006 documentation updates can proceed independently from test execution after the feature behavior is implemented.

## Implementation Strategy

1. Establish the UI test project and add the failing toggle contract check.
2. Implement the header toggle using the existing persisted `PianoRollGrid` state.
3. Add localization, update user-facing documentation, and run focused then full Windows validation.

## Nota Closing Tasks

- User-visible capability update: T006 covers `CHANGELOG.md` and `FEATURES.md`.
- No new source file is planned; no new dependency, engine behavior, or C ABI surface is required.
