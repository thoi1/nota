# Tasks: Multiple CC lanes

## Phase 1: Setup
- [x] T001 Inspect controller/lane architecture and complete specification, clarification and plan in specs/071-multiple-cc-lanes/.

## Phase 2: US1 — independent controller lanes
- [x] T002 [US1] Add independent identity and callback picker reuse in src/managed/Nota.App/PianoRollView.Controllers.cs.
- [x] T003 [US1] Add native plus toolbar, per-lane picker/minus and bounded stack in src/managed/Nota.App/PianoRollView.ControllerLanes.cs and PianoRollView.cs.

## Phase 3: US2 — safe removal and synchronized views
- [x] T004 [US2] Wire cache/view invalidation and gesture cancellation in PianoRollView.Controllers.cs and PianoRollView.ControllerLanes.cs.
- [x] T005 [US2] Verify data retention, independent commits/reload and scrolling with actual-window diagnostics in specs/071-multiple-cc-lanes/validation/.

## Phase 4: Validation and documentation
- [x] T006 Update CHANGELOG.md, FEATURES.md, Languages/fr.json and local manual handoff; validate both themes.
- [x] T007 Build/test Windows x64, record evidence and run speckit-converge.

Dependencies: T001 → T002 → T003 → T004 → T005; docs can follow T003, final validation last.
Strategy: reuse primary controller editor first, then extra-lane UI, verify data safety.


