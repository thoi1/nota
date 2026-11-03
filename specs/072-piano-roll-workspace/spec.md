<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Feature Specification: Piano roll workspace improvements

**Feature Branch**: `dev/windows-fixes`  
**Created**: 2026-11-02  
**Status**: Implemented and verified

**Input**: Piano Roll Improvements attachment: full workspace, closable Devices,
independent persistent lane heights, precise velocity onsets and deeper zoom.

## User Scenarios & Testing

### User Story 1 - Open a focused MIDI workspace (Priority: P1)

Double-click an Arrangement MIDI clip to edit it across the application workspace.
The menu, transport and editing tools remain accessible. Return to Arrangement with
one visible control and restore the previous panel layout.

**Independent Test**: Double-click while playing, return, repeat with a detached editor.

**Acceptance Scenarios**:

1. Given a MIDI clip, when double-clicked, its selected notes appear in the full workspace.
2. Given Devices or Mixer were visible, when maximizing, their containers disappear;
   returning restores their previous visibility and sizes without changing sound.
3. Given Devices were manually closed, returning keeps them closed.
4. Given an audio clip, double-click retains its existing behavior.

### User Story 2 - Control Devices visibility (Priority: P2)

Close the whole Devices container using its compact close button; reopen it using
existing controls. Closing never unloads instruments, effects or routing.

**Independent Test**: Close and reopen docked and detached Devices while playing.

**Acceptance Scenarios**:

1. Closing removes the container and its unused vertical space immediately.
2. Reopening restores the same device chain, with sound continuing throughout.
3. A workspace transition preserves the user's visibility choice.

### User Story 3 - Size individual expression lanes (Priority: P3)

Drag the divider above Velocity or any displayed CC lane to resize only that lane.
Reopening the editor or restarting Nota retains the chosen heights.

**Independent Test**: Resize Velocity and two different CC lanes, reopen and restart.

**Acceptance Scenarios**:

1. Each divider shows a resize cursor and enforces a readable minimum height.
2. Resizing scales stems and curves without changing MIDI data or undo history.
3. Small windows retain usable note editing and allow scrolling through controller lanes.

### User Story 4 - Align velocity onsets (Priority: P4)

Velocity stems share the exact timeline position of their MIDI notes' left edges.

**Independent Test**: Compare quantized note and stem positions while scrolling at
several grid steps, zoom levels and display scales.

**Acceptance Scenarios**:

1. Drawing and hit testing both use the same musical onset as the note.
2. Alignment remains correct at fractional scrolling and non-integer display scaling.

### User Story 5 - Edit at deeper zoom (Priority: P5)

Zoom in up to eight times the former maximum while retaining the existing zoom-out range.

**Independent Test**: Zoom at the pointer to the maximum, edit a short note and CC curve.

**Acceptance Scenarios**:

1. The maximum increases by at least four times, targeting eight times.
2. Pointer anchoring, scrolling, placement, snapping and resizing remain accurate.
3. Grid labels remain legible and interaction stays responsive.

### Edge Cases

- Existing detached editor, hidden Devices, visible docked Mixer and switching views.
- Narrow/short windows, multiple CC lanes and extreme zoom near clip boundaries.
- Corrupt or out-of-range saved lane heights and Windows fractional DPI scaling.
- Playback, undo/redo and high refresh cursor updates during layout changes.

## Requirements

### Functional Requirements

- **FR-001**: Arrangement MIDI activation MUST fill the available editing workspace in
  the main window, select the clip and focus editing without changing playback or snapping.
- **FR-002**: Workspace mode MUST hide complete Devices/Mixer containers and splitters,
  retain menu and transport, and restore the prior layout with an obvious return control.
- **FR-003**: Existing audio activation and operating-system fullscreen MUST remain unchanged.
- **FR-004**: Devices MUST close and reopen in normal layouts without modifying processing;
  manual visibility MUST survive workspace transitions.
- **FR-005**: Velocity and each CC lane MUST have independent draggable height dividers,
  resize cursors, minimum heights and persistent sizes across editor/app reopening.
- **FR-006**: Layout resizing MUST preserve controller values, notes, selection, snapping
  and undo history; stems and curves MUST scale to actual lane height.
- **FR-007**: Velocity drawing and picking MUST share the exact note onset timeline position.
- **FR-008**: Maximum horizontal zoom MUST increase at least 4x, targeting 8x, retaining
  zoom-out range, pointer anchoring, accurate scrolling and bounded grid rendering.
- **FR-009**: Logic Grey and the integrated menu design MUST remain unchanged; no unrelated
  engine changes, duplicate editors or unnecessary recreation/redraws are permitted.
- **FR-010**: Changes MUST have regression coverage and Windows build/test evidence,
  documentation updates and no automatic push. Latest user instruction supersedes the requested commits: leave all changes uncommitted.

### Key Entities

- **Workspace snapshot**: previous editor hosting, panel visibility and desired sizes.
- **Lane height preference**: independently chosen height for Velocity, expression and
  each MIDI controller, remembered as an application preference.

## Success Criteria

### Measurable Outcomes

- **SC-001**: One MIDI double-click opens the workspace; one return action restores it.
- **SC-002**: Hidden panels consume zero editing height and all devices continue sounding.
- **SC-003**: Velocity plus two CC lanes retain three independently chosen heights after restart.
- **SC-004**: Note and stem centers differ by zero musical time at tested zooms and scales.
- **SC-005**: Maximum zoom reaches 8x the old maximum with the old minimum unchanged.
- **SC-006**: Windows regression/build checks pass; theme source remains byte-identical.

## Assumptions

- Reuse existing full-workspace, close, panel toggle and detached-window hosting controls.
- Lane sizes are application preferences keyed by controller, not project MIDI edits.
- Returning restores a previously detached editor to its previous window arrangement.
- Existing multiple CC lanes are a prerequisite already present in the working tree.
- Clarification review: scope, persistence, safety, UI and acceptance criteria are clear;
  no user questions required. Extension hooks are empty.


