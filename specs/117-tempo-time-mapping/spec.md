<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

# Feature Specification: Interactive Tempo Time Mapping

**Feature Branch**: `feat/tempo-time-mapping-1129`

**Created**: 2026-11-28

**Status**: Draft

**Input**: [GitHub issue #1129](https://github.com/nota-daw/nota/issues/1129)

## User Scenarios & Testing

### User Story 1 - Align a beat with a performance (Priority: P1)

An arranger Ctrl-drags a beat or bar in the master Tempo automation lane to align
it with a transient in a freely recorded performance.

**Why this priority**: Aligning the grid to the recording is the core workflow.

**Independent Test**: Open an arrangement with an audio recording and a MIDI note
on the beat to be moved. Drag that beat to a visible audio transient.

1. **Given** the master Tempo lane is visible, **when** Ctrl is held over an eligible
   boundary, **then** the lane shows the time mapping cursor.
2. **Given** an eligible boundary, **when** it is dragged, **then** the alignment guide,
   timeline position, resulting BPM, tempo curve and grid update during the drag.
3. **Given** a completed drag, **when** the arrangement plays, **then** the reference
   audio plays at its original times and beat based events follow the edited tempo map.

### User Story 2 - Edit one region and recover the original map (Priority: P2)

An arranger makes several local adjustments, keeping previously aligned anchors in place.

**Why this priority**: A local correction must preserve the timing already established
elsewhere in the song.

**Independent Test**: Establish fixed anchors on both sides of a boundary, then drag
the middle boundary with snapping on and off. Cancel one edit and undo and redo another.

1. **Given** fixed anchors around a boundary, **when** the boundary is moved, **then**
   those anchors retain their original absolute positions.
2. **Given** a snapped or free drag, **when** the user presses Escape, **then** the original
   map is restored and no undo step is added.
3. **Given** a completed drag, **when** the user undoes and redoes it, **then** the original
   and edited timing return in one step each.

### Edge Cases

- The song start is fixed. A drag cannot cross a fixed anchor or require an unsupported BPM.
- A click without movement creates no edit or anchor. Losing the gesture before release cancels it.
- A flat tempo lane or a boundary with no existing tempo point can be mapped.
- Tempo curves spanning the edit must retain their original behavior outside the affected region.

## Requirements

### Functional Requirements

- **FR-001**: Ctrl plus horizontal drag MUST map an eligible beat or bar boundary in the
  master Tempo automation lane. Ordinary automation editing and other lane targets MUST
  retain their current behavior.
- **FR-002**: The gesture MUST adjust surrounding tempo values to place the selected
  boundary at the requested legal timeline position and retain the positions of surrounding
  fixed anchors.
- **FR-003**: The lane MUST provide a contextual cursor, temporary vertical guide, BPM
  readout and timeline position. The grid and tempo curve MUST update during the gesture.
- **FR-004**: Audio clip placement and playback in absolute time MUST be preserved,
  without introducing time stretching. MIDI and beat based events MUST keep their
  musical positions and follow the edited tempo map.
- **FR-005**: Both snapped and unsnapped destinations MUST be supported. An invalid
  destination MUST stop at a legal position or leave the last legal preview in place.
- **FR-006**: Releasing MUST commit the whole change as one undoable action. Redo MUST
  restore the committed map, and cancellation MUST restore the starting state.
- **FR-007**: The feature MUST use the existing master Tempo lane without permanent controls.

### Key Entities

- **Mapped boundary**: A beat or bar whose absolute timeline position is moved.
- **Fixed anchor**: A beat with an established absolute position that constrains nearby edits.
- **Tempo map**: The tempo curve that connects musical beats to absolute playback time.

## Success Criteria

- **SC-001**: A legal free drag aligns the chosen beat with the guide within one screen pixel
  at the current zoom. In snap mode the guide lands on the active snap target.
- **SC-002**: In a project with reference audio and fixed anchors on both sides, the audio
  and those anchors stay at their original absolute times, within one sample at the project rate.
- **SC-003**: One undo restores the pre-drag map, one redo restores the committed map, and a
  cancelled drag creates zero undo steps.
- **SC-004**: The cursor and guide appear only in the relevant hover and drag states, and
  existing automation gestures remain available.

## Assumptions

- The Arrangement snap setting is the default for the gesture. Snap targets are resolved
  from the map at the start of the drag so they do not move with the preview.
- The song start and neighboring established mapping boundaries constrain the edit.
  The plan must define how to derive those anchors from the existing tempo curve and
  how to handle an open ended region.
- Existing warped audio is in scope for preserving its audible timing. The plan must
  establish how to achieve that without changing the existing warp settings.
