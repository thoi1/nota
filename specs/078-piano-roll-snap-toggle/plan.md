# Implementation Plan: Piano Roll Snap Toggle

**Branch**: `078-piano-roll-snap-toggle` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/078-piano-roll-snap-toggle/spec.md`

## Summary

Add a one-click magnet toggle beside the existing Piano Roll grid chip in the MIDI clip
header. It reads and changes the existing app-wide Piano Roll snap setting, remains
synchronized with grid-menu and command changes, and does not change grid denomination or
Quantize behavior.

## Nota: Read Before Planning

- UI work follows [`DESIGN.md`](../../DESIGN.md) and existing Nota chip/toggle patterns.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) governs localization, licensing, and testing.

## Technical Context

**Language/Version**: C# / .NET 10.

**Primary Dependencies**: Existing Avalonia controls and Nota application grid helpers; no
new dependencies.

**Storage**: Existing app-wide `PianoRollGrid` setting in settings.json; no schema change.

**Audio-Thread Impact**: None. This changes only an app UI preference and its existing
managed editing behavior.

**Testing**: Headless Avalonia UI regression harness for click/state synchronization and
note snapping, plus the focused existing grid smoke tests.

**Target Platform**: Existing desktop platforms supported by Nota.

**Project Type**: Desktop audio application.

**Performance Goals**: A toggle updates synchronously with no visible delay; no work is
added to the audio processing path.

**Constraints**: Preserve the existing adaptive/fixed grid, triplet, keyboard command,
and shared Piano Roll/audio-editor setting semantics. Follow Studio tokens and
localization conventions.

**Scale/Scope**: One additional header control in the MIDI clip editor; no new model,
engine, or public API.

## Constitution Check

- Real-time audio safety: PASS — UI-only.
- Boundary discipline: PASS — no managed/native boundary changes.
- Licensing hygiene: PASS — no new dependency or source file requiring a third-party
  license review.
- Verification gate: PASS — run focused UI and grid tests; no engine behavior changes.
- One logical change: PASS — direct Piano Roll snap toggle only.
- Documentation: PASS — update CHANGELOG.md and FEATURES.md for the user-visible control.
- Spec-first: PASS — specification and plan precede implementation.

## Project Structure

### Documentation (this feature)

```text
specs/078-piano-roll-snap-toggle/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ui.md
└── tasks.md
```

### Source Code

```text
src/managed/Nota.App/
├── ClipEditorView.cs       # MIDI clip editor header controls
├── EditorGrids.cs          # existing shared, persisted grid setting
└── SnapChip.cs             # existing grid menu and shared setting behavior

tests/Nota.SmokeTest/
└── SnapGridTests.cs        # existing grid-setting transition coverage

specs/078-piano-roll-snap-toggle/ui/
└── PianoRollSnapToggleUi.csproj and Program.cs  # focused UI regression harness
```

**Structure Decision**: Keep the feature in the existing managed app and grid settings.
Use a focused headless UI harness for the new control; do not introduce a domain entity or
engine API.

## Design and Validation

The Piano Roll header gets a dedicated `ToggleButton` adjacent to the existing grid chip.
The button reads `EditorGrids.Shared.Get(EditorKind.PianoRoll).SnapOn`, updates that
setting by applying the existing snap-toggle operation, and listens to the existing grid
change event while attached. Use the existing `GlyphKind.Snap` glyph, chip styling, theme tokens,
localized tooltip, and accessible control name. Keep the existing chip as the menu entry
point and leave Quantize independent.

The UI harness verifies initial state, click-on/click-off, immediate synchronization after
an external setting change, and representative snapped versus free note editing. Run
`SnapGridTests` to ensure the toggle operation still preserves other grid fields.

## Complexity Tracking

No constitution violations or added complexity.
