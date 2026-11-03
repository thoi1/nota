# Feature Specification: Piano Roll Snap Toggle

**Feature Branch**: `078-piano-roll-snap-toggle`

**Created**: 2026-11-03

**Status**: Draft

**Input**: User description: "add snap to grid button on piano roll"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Toggle piano-roll snapping directly (Priority: P1)

A musician editing MIDI notes wants to temporarily place or move notes freely without
opening the grid menu. They use a dedicated snap button in the piano-roll header, then
continue editing with snapping restored when they turn it back on.

**Why this priority**: Direct access to the snap state reduces interruptions during note
editing and makes the current state visible at a glance.

**Independent Test**: Open a MIDI clip, toggle snap off from the piano-roll header, place
or move a note between grid positions, then toggle snap on and repeat. Verify both the
resulting note positions and the button state.

**Acceptance Scenarios**:

1. **Given** a MIDI clip open with snap enabled, **When** the user activates the piano-roll
   snap button, **Then** snapping turns off, the button visibly indicates the off state,
   and note editing no longer rounds positions to the grid.
2. **Given** snap is off, **When** the user activates the button again, **Then** snapping
   turns on and note editing uses the current grid step.
3. **Given** the snap state changes through the existing grid menu or Toggle Snap command,
   **When** the piano roll is visible, **Then** the dedicated button updates to show the
   current state.
4. **Given** the button is toggled in the piano roll, **When** the user opens another MIDI
   clip or project, **Then** the app-wide piano-roll grid setting remains in effect.

### Edge Cases

- Toggling snap must not change the selected grid step, adaptive/fixed mode, triplet
  setting, or the independent Quantize value.
- The button must remain synchronized when grid preferences change while the piano roll
  is open, including changes made through keyboard commands or the grid menu.
- The toggle must remain usable and clearly labeled for assistive technology and localized
  UI.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The piano-roll header MUST provide a dedicated one-click control to turn grid
  snapping on and off without opening the grid menu.
- **FR-002**: The control MUST communicate whether snapping is currently enabled or
  disabled through a visible, distinguishable state.
- **FR-003**: Activating the control MUST update the piano-roll snap setting used by note
  placement, movement, resizing, and other existing grid-snapped editing gestures.
- **FR-004**: The control MUST preserve the current grid mode, step, density, and triplet
  setting when toggling snap.
- **FR-005**: The control MUST stay synchronized with changes made using the existing grid
  menu and Toggle Snap command.
- **FR-006**: The control MUST follow the existing app-wide piano-roll grid preference
  across clips and projects; it MUST NOT introduce a separate snap preference.
- **FR-007**: The control MUST have localized, accessible identification and an explanatory
  tooltip.
- **FR-008**: The existing grid chip MUST continue to open the grid-options menu, and the
  Quantize value and behavior MUST remain independent of this toggle.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can change piano-roll snap state with one click and no menu interaction.
- **SC-002**: In a focused note-editing check, 100% of edits made with snap on land on the
  selected grid step, and edits made with snap off preserve off-grid positions.
- **SC-003**: After a snap-state change from any existing control, the piano-roll button
  shows the matching state before the next user interaction.
- **SC-004**: Toggling snap leaves the grid step, triplet mode, adaptive/fixed mode, and
  Quantize setting unchanged.

## Assumptions

- The new button is separate from the existing grid chip and sits beside it in the
  piano-roll header.
- The control changes only the existing Piano Roll snap-on setting; the established
  piano-roll and audio clip editor shared grid behavior is retained.
- The Arrangement's separate snap control is unchanged.
