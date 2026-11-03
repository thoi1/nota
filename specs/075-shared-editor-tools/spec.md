# Feature Specification: Shared editor tools and number shortcuts

**Created**: 2026-11-02  
**Status**: Ready for planning  
**Input**: User's pasted Shared Arrangement and Piano Roll Toolbar, Keyboard Shortcuts, and Waveform Cycling brief; top-row 1–5 select tools.

## User Scenarios & Testing

### US1 — Choose tools with the keyboard (P1)

A musician uses the same five-tool toolbar in Arrangement and Piano Roll. Top-row 1 selects Select, 2 Pencil, 3 Eraser, 4 Waveform Draw and 5 Nota Warp. Only the focused editor changes its active tool.

**Independent test**: Focus each editor (including the detached piano roll), press 1–5 and verify tool selection. Hold a key: auto-repeat changes nothing further. Type numbers in text/numeric fields, plugin windows and dialogs: tool state is unchanged. Existing modified grid shortcuts remain functional.

**Acceptance**:

1. The toolbars use identical icons, sizes, spacing, hover/selection and tooltip/shortcut conventions, in the exact requested order; the waveform dropdown sits beside Waveform Draw.
2. Each editor remembers its own active tool for the running session. Clicking a toolbar affects that editor, with exactly one tool selected.
3. Number shortcuts run through the existing command infrastructure and respect typing/modal/focus boundaries. Existing bindings are not overridden.

### US2 — Share and cycle waveform shapes (P1)

Pressing 4 activates Waveform Draw with the last chosen shape. When already active, another distinct press cycles Sine → Triangle → Sawtooth → Square → Freehand → Sine. Both toolbars immediately show the same dynamic shape icon and dropdown selection.

**Independent test**: Cycle all shapes in either editor, change the shape in its dropdown, switch editors and continue cycling from that shape. Changing tools preserves the shape. A held 4 never cycles repeatedly.

**Acceptance**:

1. Exactly one authoritative shape choice is shared by both editor instances and detached views.
2. Choosing another tool does not reset shape; returning to Waveform restores it. Dropdown changes and cycling agree immediately.
3. Tool choice is transient; existing project/controller data persists through the existing save/reload path. No new tool-setting project format is required.

### US3 — Edit notes and controller envelopes (P1)

Select preserves existing note editing. Pencil creates a note using the remembered default length/velocity and current snap. Eraser removes clicked editable notes or CC points. Waveform Draw changes only the chosen existing controller lane (including CC1/CC11), with bounded, ordered, range-clamped points and one undo step per completed gesture.

**Independent test**: Draw/delete notes, generate every waveform in CC1/CC11, undo/redo and cancel. Verify notes, other controllers and data outside the sweep remain unchanged, locked/reference clips stay read-only, and clip-relative coordinates map correctly.

**Acceptance**:

1. Drawing/erasing notes reuses existing clip ownership, bounds, default note settings and undo conventions. Select's modifiers/move/resize/multi-selection keep working.
2. Waveform generation shares normalized math with Arrangement automation, respects snapping and the lane's value/time range, and avoids unbounded/redundant event density.
3. Drawing without a compatible controller lane gives a visible instruction/inactive indication and changes no notes. Escape and tool changes leave no partial new drawing edits.
4. Existing Arrangement clip/automation tools continue to work, with normal ruler/scroll/zoom/context interactions preserved. Existing Alt behaviors are retained where implemented.

### US4 — Indicate Warp safely (P2)

Choosing Nota Warp tints only that editor's ruler muted light red, displays WARP and highlights hovered grid lines. Other tools restore the normal ruler immediately. Labels stay readable and ruler geometry/navigation is unchanged.

**Independent test**: Select Warp independently in each editor and inspect ruler appearance, hover, navigation and the visible unavailable explanation. No tempo/audio/note data changes when there is no suitable tempo-map engine.

**Acceptance**:

1. Reuse a suitable shared tempo map, absolute-time mapping and transient targets if present; never stretch/move audio or reposition/quantize notes as a substitute.
2. The known absent tempo-map/Tempo Track engine is an explicit limitation: implement truthful selection/ruler feedback only, with no fake anchors or separate clip tempo map.

### Edge cases

TextBox/NumericUpDown descendants, value-entry popovers, plugin windows, modal dialogs, command palette, held keys/released keys after focus loss, Session/Modular focus, small/detached windows, missing/locked clips, controller changes during a stroke, reverse/out-of-bounds sweeps, switch controllers, recording refresh, tool changes mid-gesture and odd-meter/triplet grids.

## Requirements

- **FR-001**: Both editors MUST use a matching, reusable five-tool toolbar and one set of tool identifiers/icons/labels.
- **FR-002**: Top-row keys 1–5 MUST target the focused editor through existing commands, prevent auto-repeat, preserve modified shortcuts and never intercept typing or unrelated windows.
- **FR-003**: The editors MUST have separate active tools and one shared remembered waveform, with exact cycling order, immediate dynamic icons and synchronized menus.
- **FR-004**: Piano Roll Pencil/Eraser MUST edit notes and editable controller points through existing bounds/ownership/undo. Existing Select workflows MUST remain.
- **FR-005**: Waveform Draw MUST edit supported controller lanes using shared waveform math, correct time/value mapping and one undo per completed gesture; it MUST leave notes/other lanes intact.
- **FR-006**: Missing targets MUST be explained and MUST NOT cause unrelated edits. Escape/tool changes MUST cancel new uncommitted gestures.
- **FR-007**: Warp MUST apply the requested ruler tint/WARP/hover visuals independently and report the absent tempo-map engine. It MUST NOT simulate timing edits with audio stretching or MIDI movement.
- **FR-008**: Arrangement tools and existing shortcuts/navigation MUST remain functional. No unrelated local changes, commits, pushes, PRs or unnecessary dependencies.
- **FR-009**: Local changelog/features/translations/shortcut reference MUST reflect the feature. Sync EN/RU website documentation if the sibling repo is available; otherwise prepare bilingual updates and report the missing dependency.

### Entities

Editor tool selection, shared waveform settings, focused editor context, toolbar command descriptors, cancellable note/controller gesture previews, existing project/controller data.

## Success Criteria

- **SC-001**: Both editors select all five tools using the same toolbar and top-row shortcuts; at most one tool is active in each.
- **SC-002**: Five successive cycle presses wrap the shared waveform once; held keys never produce additional cycles; dropdown changes update both icons.
- **SC-003**: Note/CC edits pass undo/redo, clip bounds, value ranges, outside-data retention and cancellation checks; protected contexts receive typed numbers normally.
- **SC-004**: Windows build/smoke and meaningful actual-UI checks pass, or concrete test/environment limitations are recorded. Local-change preservation is reviewed.

## Assumptions and clarifications

No critical ambiguities require a question: the attachment provides exact mappings, order, cycling and fallback behavior. Top-row only, without modifiers; keypad mappings stay unchanged. Existing Cmd/Ctrl+1–5 grid shortcuts stay intact. Shared shape/settings live for the app session; editor tool state belongs to its view and survives reparenting. Warp visuals are explicitly allowed without an engine but never imply working tempo editing. The missing sibling manual repo remains a reported dependency if still absent.

## Nota compliance

UI-thread-only work, no DSP/audio callback or ABI changes. Existing undo and controller ports only. No new dependency. AGPL headers on new source files. Studio tokens/Glyphs and localization everywhere. Exact requested WARP indicator is an explicit casing exception.
