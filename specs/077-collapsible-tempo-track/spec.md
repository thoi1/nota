# Feature Specification: Collapsible Tempo Track

**Feature Branch**: existing working branch (preserve uncommitted warp work)
**Created**: 2026-11-03
**Status**: Implemented and verified on Windows; see verification.md for supported scope and limits
**Input**: User's “NOTA DAW — Implement Collapsible Tempo Track with Editable Tempo Automation” attachment.

## User Scenarios & Testing

### User Story 1 — See and edit project tempo (Priority: P1)

The musician opens a Tempo Track above the Arrangement, expands its compact curve,
adds BPM points and draws a step change or accelerando without leaving the song.

**Why this priority**: makes the existing project clock visible and directly editable.
**Independent Test**: add points, drag them, change interpolation, collapse/expand,
zoom/scroll, undo and reopen the project; verify the same tempo shape and positions.

**Acceptance Scenarios**:
1. Given an older constant-tempo song, showing Tempo displays one protected starting point
   at its existing BPM; existing playback and clip locations remain unchanged.
2. Given an expanded lane, double-click adds a snapped point; dragging changes its beat/BPM,
   Shift refines movement and Alt-click removes a noninitial point. Undo restores one gesture.
3. Given a collapsed or resized lane, curve, header BPM, horizontal position and saved
   visibility/height remain consistent with the Arrangement after reopening.

### User Story 2 — Hear the same tempo everywhere (Priority: P1)

Transport, MIDI, audio, metronome, automation, loops and hosted plugins follow the
same tempo curve, including transitions within a processing block and exported audio.

**Why this priority**: tempo is the time coordinate system, not a gain lane.
**Independent Test**: render known beat-position events across step/ramp boundaries and
loop seams, inspect plugin transport, record timestamps and compare against elapsed time.

**Acceptance Scenarios**:
1. Given step/linear/smooth segments, beat→time→beat round trips stay within one sample
   and elapsed time never jumps or reverses.
2. Given MIDI/automation/metronome events at known beats, audible events, cursor and plugin
   PPQ/BPM agree through boundaries, seeking and looping; export follows the same map.
3. Given repeated curve edits during playback, the interface remains responsive, previous
   prepared warped audio remains available until replacement, and no callback work blocks.

### User Story 3 — Keep warp and tempo connected (Priority: P1)

The musician explicitly chooses Audio follows grid or Grid follows audio. Changing one
authoritative map refreshes both the Tempo Track and Nota Warp grid without altering sources.

**Independent Test**: move an unlocked grid/audio timing anchor, observe the tempo lane,
then edit tempo and verify the documented musical versus absolute anchor behavior.

**Acceptance Scenarios**:
1. Audio follows grid preserves clip source↔musical warp anchors and stretches to the edited
   project timing; musical MIDI/automation events keep their beat coordinates.
2. Grid follows audio derives project timing from explicit source-time/beat correspondences;
   original audio stays unstretched and both grid and lane show that same timing.
3. A protected absolute-time anchor cannot silently move. Conflicting edits are rejected
   atomically with an explanation and leave tempo, anchors and undo history intact.

### User Story 4 — Refine tempo efficiently (Priority: P2)

The musician uses smooth ritardando, segment context controls, numeric position/BPM,
multi-selection, box selection and copy/paste with keyboard access and one undo per gesture.

**Independent Test**: edit a selected group, paste at a chosen beat, undo/redo and reload.

**Acceptance Scenarios**:
1. Selected/hovered points are distinct; group edits preserve order and the initial point.
2. Numeric and pointer edits share validation; interpolation is stored on the outgoing segment.
3. Editing limits never admits zero, negative, nonfinite or out-of-range tempo values.

### Edge Cases

- Empty/malformed/duplicate/unsorted tempo data; points at zero; deleting the last point.
- Point crossing, duplicate ticks, clipboard collisions, dragging outside the lane and Escape.
- Flat ramps, near-equal BPM, extreme limits, long songs and dense maps.
- Tempo/warp edits while playing, recording, preparing audio, exporting or opening another song.
- Sample-rate changes, loops across several segments, seeks backward and plugin block boundaries.
- Hidden/collapsed Tempo Track, extreme zoom, narrow window, dark/light themes and French UI.

## Requirements

### Functional Requirements

- **FR-001**: One authoritative musical-time map MUST drive Tempo Track, Nota Warp and all
  clock consumers; no independent UI playback clock or ordinary parameter automation substitute.
- **FR-002**: A dedicated lane above ordinary tracks MUST offer visibility, compact/expanded
  modes, adjustable expanded height, current BPM, vertical BPM scale and shared horizontal view.
- **FR-003**: Points MUST have musical tick position and precise BPM; outgoing segments MUST
  support Step, Linear and Smooth with positive, continuous, strictly increasing elapsed time.
- **FR-004**: Beat/time conversion MUST be stable and invertible; practical exact cases use
  analytical conversion, others deterministic bounded-error conversion prepared outside playback.
- **FR-005**: Point editing MUST support addition, selection, horizontal/vertical/fine drag,
  deletion, multiselect, box selection, copy/paste, numeric edits and segment context controls.
- **FR-006**: The initial point MUST remain at zero and cannot be deleted; validated edits MUST
  prevent invalid BPM/order and make each gesture one undoable transaction with cancellation.
- **FR-007**: Grid follows audio and Audio follows grid MUST use the same map and document
  absolute versus musical anchoring; locked conflicts MUST reject/constrain with an explanation.
- **FR-008**: Playback, playhead, rulers, piano roll, MIDI scheduling/recording, automation,
  looping, metronome, plugin transport and clip placement MUST agree with that map within a sample.
- **FR-009**: Playback MUST consume immutable prepared timing without locks, allocation, I/O
  or expensive integration in its callback; rapid edits MUST preserve tempo-edit responsiveness.
- **FR-010**: Projects MUST retain tempo points/interpolation, lane visibility/mode/height,
  limits and anchor relationships; old projects MUST load with their existing constant BPM.
- **FR-011**: UI MUST follow Nota Studio tokens, existing gestures/undo and localization;
  new bindings, capabilities and EN/RU manual pages MUST be documented.
- **FR-012**: Verification MUST cover the user's 18 scenarios, actual Windows UI/build and
  engine integration; completion reports MUST distinguish verified behavior and remaining gaps.

### Key Entities

- **Tempo point**: tick, BPM and outgoing interpolation mode.
- **Tempo map**: ordered points, BPM bounds, musical↔elapsed mapping and immutable revision.
- **Tempo Track state**: visible, expanded and expanded height.
- **Timing anchor**: related source/absolute time and musical tick, workflow and protection.
- **Edit transaction**: original/candidate points and anchor constraints, accepted as one undo.

## Success Criteria

- **SC-001**: All 18 requested scenarios have recorded checks; passing claims include evidence.
- **SC-002**: At 44.1/48/96 kHz, conversions and scheduled test events agree within one sample,
  including tempo boundaries, ramps, seeking and looping over a two-hour song.
- **SC-003**: A 10,000-point song supports visible-range editing and zoom/scroll without
  scanning audio or preparing stretch caches in pointer handlers; measured edits stay under 50 ms.
- **SC-004**: A complete edit/undo/redo/save/reload round trip restores map and lane state;
  rejected locked-anchor edits change neither timing nor source audio.
- **SC-005**: Existing constant-tempo smoke scenarios and Windows build pass; the tempo-lag
  regression remains green. Callback timing work is bounded and measured separately.

## Assumptions

- Reuse/extend Transport, currently the sole playback clock; existing clip warp maps express
  source↔beat relationships, not a second project tempo map. New work completes that missing
  shared-map foundation from spec 076; automatic beat inference remains separate.
- Use explicit manual source/beat anchors for Grid follows audio; no unverified automatic
  rubato inference is implied. Audio follows grid keeps musical warp coordinates unchanged.
- Default editing bounds are the current transport's 20–300 BPM; configurable bounds stay
  inside the engine's supported positive range. Tick resolution and numerical scheme are design details.
- The top BPM control edits the starting tempo for a constant map; with multiple points it
  edits/inserts at the current musical insertion position, without replacing unrelated points.
- Clip starts, MIDI and automation are musical by default. Absolute anchors are explicit and
  never inferred/locked silently. Visibility defaults to visible compact in new/legacy projects.
- First deliver visible lane, editing, Step/Linear and shared warp synchronization; then Smooth,
  advanced editing and complete playback/persistence validation in the same implementation.
