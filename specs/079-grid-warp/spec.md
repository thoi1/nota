# Feature Specification: Grid Warp

**Feature Branch**: `079-grid-warp`

**Created**: 2026-11-03

**Status**: Implemented and automatically verified — real-piano audition pending (see verification.md)

**Input**: User description: "Implement Cubase-Style Grid Warp in Nota DAW", extending the existing Grid follows audio workflow with transient-guided musical grid editing, Tempo Track integration, adjustable transient detection, and mutually exclusive warp modes.

## Clarifications

### Session 2026-11-03

- Q: When editing a Tempo Track point linked to a fixed Grid Warp anchor, how should the linked anchor behave? → A: Directly moving or deleting a linked point updates or removes its anchor and recalculates affected tempo segments. Unrelated tempo edits preserve fixed anchors; conflicting BPM edits must be constrained, rejected, or require explicit unlinking. Grid Warp remains the authoritative source of beat-to-audio alignment, with bidirectional synchronization and full undo/redo.

## Completion request, 2026-11-03

Complete the attached production Grid Warp specification. Prior checked tasks are existing
work, not proof of the new acceptance criteria. Reuse the manual Grid Warp foundation;
finish automatic original-tempo extraction, preview/apply/cancel, reset, explicit add-anchor,
stable anchor identity and source-preservation invariants. The earlier user request also
requires dragging an audio clip onto the Tempo Track to extract and apply its original tempo.
The unavailable interactive demo supplies no additional requirement beyond the attachment.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose a warp editing mode (Priority: P1)

A musician chooses None, Audio follows grid, or Grid follows audio from the Sample Editor. The active mode is immediately visible, only one mode can be active, and choosing or leaving a mode does not itself change the audio or project tempo.

**Why this priority**: Mode clarity prevents accidental audio stretching and ensures the two workflows cannot run at once.

**Independent Test**: Exercise every mode transition, including clicking the active mode to return to None; compare audio and tempo-map state before and after each mode-only transition.

**Acceptance Scenarios**:

1. **Given** no mode is active, **When** the musician selects either warp mode, **Then** only that mode is active and uses the orange accent.
2. **Given** one warp mode is active, **When** the musician selects the other, **Then** the old mode becomes inactive and no audio or tempo-map edit is applied by the mode switch.
3. **Given** a warp mode is active, **When** the musician selects it again, **Then** both warp modes become inactive.
4. **Given** Audio follows grid is active, **When** the musician performs a Grid Warp gesture, **Then** no Grid Warp operation is performed.

### User Story 2 - Fit the musical grid to a performance (Priority: P1)

A musician analyses an audio performance, then drags musical bar or beat lines onto detected attacks. Nota changes the project tempo map so the selected musical beats coincide with those attacks while leaving the source waveform and its absolute playback timing unchanged.

**Why this priority**: This is the core Grid Warp outcome: make the musical clock follow the performance instead of stretching the performance to a fixed clock.

**Independent Test**: Use a recording with known attacks, map at least two beat lines to attacks, and verify calculated tempo events, unchanged source samples and unchanged absolute audio playback times.

**Acceptance Scenarios**:

1. **Given** Grid follows audio is active, **When** the musician hovers a visible musical grid line, **Then** a distinct handle appears at the correct zoomed and scrolled position over the waveform.
2. **Given** detected transients are available, **When** the musician drags a grid line, **Then** snapping targets a transient and the preview shows the beat and resulting BPM before commit.
3. **Given** a valid drag is committed, **When** the tempo map is read or played, **Then** editable tempo points represent the new beat-to-time mapping and the Tempo Track displays it.
4. **Given** an invalid, crossing, zero-duration, or out-of-range mapping, **When** the edit is committed, **Then** Nota rejects it without partially changing the map, anchors, undo history, or source audio.
5. **Given** a drag is in progress, **When** the musician holds Shift, **Then** transient snapping is temporarily disabled; fixed anchors elsewhere remain aligned.
6. **Given** a fixed anchor, **When** the musician Alt-clicks it, **Then** that anchor is removed; double-clicking an eligible grid line creates an anchor.
7. **Given** a committed Grid Warp edit, **When** the project is undone, redone, saved, or reopened, **Then** its tempo map and anchors restore consistently.
8. **Given** a Tempo Track point linked to a Grid Warp anchor, **When** the musician moves or deletes that point, **Then** its linked anchor updates or is removed in the same undoable edit; edits to unrelated points preserve fixed anchors.
9. **Given** a BPM edit would break a fixed anchor's beat-to-audio alignment, **When** the edit is attempted, **Then** Nota constrains or rejects it, or requires the anchor to be explicitly unlinked first.

### User Story 3 - Tune transient detection (Priority: P2)

A musician adjusts detection threshold and minimum spacing with horizontal sliders, sees their current values and transient count, and gets refreshed waveform markers without blocking playback or losing manually committed tempo anchors.

**Why this priority**: Reliable, responsive attack detection makes Grid Warp practical on expressive and noisy recordings.

**Independent Test**: Change each slider through its endpoints and intermediate values on deterministic audio, then verify detection strength, spacing, cancellation, preview markers, and preserved anchors.

**Acceptance Scenarios**:

1. **Given** the Sample Editor is open, **When** the musician changes threshold, **Then** the displayed percentage is in the 0–100% range, where lower values admit weaker candidates and higher values retain stronger candidates.
2. **Given** the Sample Editor is open, **When** the musician changes minimum spacing, **Then** the displayed value is 5–250 ms and accepted transients are never closer than the selected interval.
3. **Given** slider movement is rapid, **When** a new analysis supersedes an older one, **Then** obsolete work is cancelled or ignored and only results for the latest settings are shown.
4. **Given** analysis is refreshed, **When** committed Grid Warp anchors exist, **Then** they remain unchanged.

### User Story 4 - Continue using Audio follows grid (Priority: P1)

A musician continues to use the established Audio follows grid workflow to stretch audio against the existing musical grid. Its transient snapping, warp markers, undo/redo and persistence remain independent from Grid follows audio.

**Why this priority**: The new timing workflow must not regress existing audio-warp editing.

**Independent Test**: Add and move Audio follows grid warp markers, verify the expected nondestructive stretch, and confirm the project tempo map is unchanged.

**Acceptance Scenarios**:

1. **Given** Audio follows grid is active, **When** a warp marker is moved, **Then** only the existing audio-warp workflow operates and project tempo events remain unchanged.
2. **Given** the musician switches between Audio follows grid and Grid follows audio, **When** either mode is used, **Then** it cannot invoke the other mode's editing operation.
3. **Given** an existing project with audio warp markers, **When** it is opened and edited, **Then** existing marker behavior is preserved.

### User Story 5 - Extract and apply original performance tempo (Priority: P1)

A musician drops an audio clip on the Tempo Track or chooses **Snap All Beats** in the
Sample Editor. Nota analyses the original source, estimates beats separately from attacks,
and previews a stepped tempo map. The musician corrects proposed beats and applies the
result to the real Tempo Track without stretching or moving the reference recording.

**Independent Test**: A known rubato recording that was auto-warped on import still yields
its original timing. Preview and correction leave the project unchanged until Apply;
Apply is one undo step, and original timing survives tempo edits and save/reload.

**Acceptance Scenarios**:
1. Given a dropped Arrangement audio clip, analysis reads original source frames rather than
   stretched playback, and the clip remains in its original Arrangement location.
2. Given subdivisions, missing attacks and syncopation, proposed beats represent an estimated
   pulse with confidence, not one musical beat per transient. Starting/minimum/maximum BPM
   and beat-detection sensitivity are adjustable; uncertain material explains the result.
3. Given proposed beats, preview distinguishes them from fixed anchors; the musician can
   reposition/delete proposals before Apply. Cancel changes no audio, tempo or undo history.
4. Given existing fixed anchors, proposals respect those constraints and Apply preserves them;
   conflicting proposals are rejected without overwriting manual work.
5. Given a valid preview, Apply creates committed anchors and Step tempo events in one undoable
   transaction. Subsequent BPM edits, imports and save/reopen do not destroy original source data.

### User Story 6 - Add anchors and reset a mapped region (Priority: P1)

A musician uses a discoverable **Add anchor** command or grid handles to align beats, then
uses **Reset** to restore the selected reference region's saved mapping baseline without
erasing tempo events elsewhere.

**Independent Test**: Add/move/delete anchors with pointer and numeric commands, Reset,
undo/redo and reopen; verify both the baseline and unaffected regions remain intact.

**Acceptance Scenarios**:
1. Given Grid follows audio, a visible Add anchor command creates a selected, numerically
   editable fixed anchor with stable identity, in addition to line dragging/double-click.
2. Given a region with several completed edits, Reset restores its pre-edit mapping and
   reference relationship as one undoable action; unrelated tempo automation remains intact.
3. Given reanalysis or zoom/scroll, committed anchor identity and source timing stay unchanged.

### Edge Cases

- Clips with no detected attacks, insufficient attacks, silence, or analysis failure.
- Attacks at clip boundaries, repeated/nearby attacks, reversed or pitched clips, and trimmed source regions.
- The first beat, beat zero, clip placement, non-4/4 time signatures, and clips crossing time-signature changes.
- Anchors at the same time/tick, crossing anchors, locked anchors, dense anchors, and tempos at configured limits.
- A transient re-analysis while anchors exist or while a drag is active.
- Rapid slider changes, cancellation, project close, source replacement, playback, recording, and a pending tempo-map preparation.
- Horizontal zoom and scroll extremes, high-DPI rendering, and editing while the Tempo Track is collapsed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Sample Editor MUST expose mutually exclusive None, Audio follows grid, and Grid follows audio toggle modes. Selecting the active mode MUST return to None. Active controls MUST use Nota's orange accent and inactive controls MUST use the normal theme.
- **FR-002**: Selecting or leaving an editing mode MUST NOT itself stretch, resample, move, or otherwise alter audio, nor change the tempo map.
- **FR-003**: Grid follows audio MUST overlay musical bar and beat lines on the audio waveform and provide a hover affordance and draggable line for editing their absolute-time alignment.
- **FR-004**: A Grid Warp drag MUST preview the beat, target transient, and resulting tempo before commit. By default, grid lines MUST snap to accepted detected transients; Shift MUST temporarily disable this snapping.
- **FR-005**: Double-clicking an eligible grid line MUST create a fixed timing anchor. Alt-click MUST remove an eligible fixed anchor. Dragging an anchor MUST update its alignment without crossing or moving other fixed anchors.
- **FR-006**: Grid Warp MUST derive tempo from musical beat positions and absolute audio timeline times, using the existing project time signature and the relation BPM = 60 × beat difference / elapsed-time difference for each constant-tempo interval.
- **FR-007**: A committed Grid Warp edit MUST create or update actual project tempo-map events and editable timing anchors, display the resulting BPM automation in the Tempo Track, and synchronize transport, Arrangement, Piano Roll, and metronome timing.
- **FR-008**: Grid Warp MUST use piecewise-constant tempo intervals initially. It MUST preserve tempo events outside the affected mapping region and reject invalid ordering, crossing anchors, non-positive intervals, and values outside the supported BPM limits atomically.
- **FR-009**: Tempo Track edits MUST remain editable and undoable. Moving or deleting a point explicitly linked to a Grid Warp anchor MUST update or remove that anchor in the same edit; unrelated tempo edits MUST preserve fixed anchors. A conflicting BPM edit MUST be constrained, rejected, or require explicit unlinking. Grid Warp and Tempo Track MUST remain bidirectionally synchronized, with each complete user action undoable and redoable.
- **FR-010**: Grid Warp MUST NOT stretch, resample, move, or otherwise alter the original audio, its waveform samples, or its absolute playback timing. Tempo-map preview MUST avoid expensive full-map or audio-cache preparation on every pointer event; final edits MUST use Nota's existing commit and undo mechanisms.
- **FR-011**: Detection threshold MUST be controlled by a horizontal slider with a 0–100% display and a 50% default; lowering it MUST make weaker transient candidates eligible and raising it MUST favor stronger candidates.
- **FR-012**: Minimum transient spacing MUST be controlled by a horizontal slider with a 5–250 ms range and a 30 ms default; accepted transients MUST respect the selected minimum spacing.
- **FR-013**: Slider changes MUST debounce and re-analyse asynchronously, cancel or disregard obsolete jobs, update waveform markers and transient count, and preserve committed tempo anchors. Transient analysis MUST never run on the real-time audio thread.
- **FR-014**: Audio follows grid MUST continue to stretch/compress audio only through its existing explicit warp-marker workflow, keep the grid fixed, leave the tempo map unchanged, and remain independent of Grid follows audio.
- **FR-015**: Grid Warp data and resulting tempo events MUST persist through project save/load and participate in project undo/redo. Existing projects and audio warp markers MUST remain compatible.
- **FR-016**: Grid Warp controls and statuses MUST follow Nota's existing dark visual language, remain aligned during zoom and scroll, distinguish grid lines, hover, transients, and committed anchors, and show a BPM preview during drag.
- **FR-017**: The implementation MUST reuse Nota's existing audio-editor, transient-analysis, tempo-map, Tempo Track, undo, and project persistence systems; it MUST NOT add a duplicate tempo engine or independent musical-time model.

- **FR-018**: Snap All Beats and audio-clip drop onto the Tempo Track MUST analyse original
  source timing off-thread and estimate a musical pulse using attack strength, interval
  evidence and tempo continuity, with subdivision/missing-beat handling and adjustable
  starting/minimum/maximum BPM and beat sensitivity. Project BPM MUST NOT substitute for
  original tempo. Automatic proposals MUST remain separate from transients and fixed anchors.
- **FR-019**: Extraction MUST provide visible Preview, Apply and Cancel in the existing
  Sample Editor. Proposed beats MUST be manually correctable before Apply. Preview/cancel
  MUST NOT publish project changes; Apply MUST create Step tempo events and fixed anchors
  together as one undoable transaction, preserving existing manual fixed anchors.
- **FR-020**: Original PCM, source timing and extracted original-source beat data MUST remain
  available after import auto-warp, musical warp edits, project BPM changes, duplication and
  save/reload. Extraction MUST use the selected source region and correct absolute origin.
- **FR-021**: Committed fixed anchors MUST have stable unique identities independent of tick,
  optional source transient/confidence provenance, and explicit linkage to tempo events.
  Moving an anchor MUST preserve its ID; proposed beats MUST NOT masquerade as fixed anchors.
- **FR-022**: The editor MUST provide a discoverable Add anchor command and selected-anchor
  position editing. Reset MUST restore the selected mapping region's pre-edit baseline as
  one undoable action, preserve unrelated tempo events and reject protected conflicts.
- **FR-023**: Grid follows audio commits MUST preserve original PCM, source speed, source
  warp-marker data and absolute clip placement. Source-frame/time constraints MUST be
  independent from mutable musical warp-marker coordinates.
- **FR-024**: Completion MUST include actual editor gesture tests, extraction fixtures,
  source/undo/save/reload comparisons, supported clock/export checks, Windows build and a
  report of the traced original missing-anchor cause and any unverified piano/hardware cases.

### Key Entities

- **Warp mode**: the current Sample Editor editing choice: None, Audio follows grid, or Grid follows audio.
- **Transient**: a detected source attack with source-relative time, strength, confidence, and analysis settings.
- **Grid Warp anchor**: a musical beat associated with an absolute audio timeline time; fixed anchors constrain adjacent edits.
- **Tempo event**: a BPM value at a musical beat, forming a piecewise-constant interval in the project tempo map.
- **Detection settings**: threshold and minimum accepted spacing used to produce an immutable transient result set.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every pair of supported mode choices results in exactly the selected mode or None; mode-only transitions leave audio and tempo-map state unchanged.
- **SC-002**: On deterministic fixtures, every committed beat/time anchor pair agrees with the resulting tempo map within one audio sample, including after save/load and undo/redo.
- **SC-003**: Grid Warp edits preserve the original source samples and the audio clip's absolute playback times; Audio follows grid still produces its established stretch behavior without changing tempo events.
- **SC-004**: Across 5–250 ms spacing settings, no two accepted transients are closer than the selected setting; slider endpoints and the 50% / 30 ms defaults display correctly.
- **SC-005**: During drag previews, no full audio stretch-cache build is initiated; rapid slider updates show only the latest settings' results, and analysis does not execute on the audio callback.
- **SC-006**: Tempo points, anchors, and their relationships survive save/reopen, and one undo/redo restores/removes each committed edit as one action.
- **SC-007**: A recorded expressive piano performance can be analysed and mapped to beat lines while the Tempo Track, transport, Arrangement, Piano Roll, and metronome remain synchronized.

- **SC-008**: On labelled constant and changing-tempo attack fixtures, proposed beats
  align within 30 ms and recover the intended pulse, including stronger/weaker subdivisions
  and missing attacks. Silence, inadequate evidence and out-of-range requests reject safely.
- **SC-009**: Preview/cancel preserve the complete pre-edit project state; Apply, Reset and
  anchor gestures each undo/redo atomically. Drop extraction produces the same original beat
  result before and after project-tempo changes, and retained results survive disk reload.

## Assumptions

- The three modes are editing-tool selections, not project tempo/audio operations; changes occur only after a user performs a relevant edit.
- Grid Warp maps source attacks through the selected clip's existing source region, direction, pitch/rate, and project placement into absolute audio timeline time.
- Project BPM limits and time-signature changes are owned by Nota's existing tempo system; Grid Warp does not introduce new limits or a separate time signature.
- Existing audio-warp markers and projects retain their current meaning and data format unless an additive compatible field is required for Grid Warp anchors.
- Committed Grid Warp anchors are separate from automatically detected transient candidates; re-analysis never promotes, moves, or deletes committed anchors.

- Automatic inference is an editable estimate. A supplied starting BPM resolves half/double
  ambiguity; the interface must allow correction and must not claim certainty for free-time,
  sparse or unmetered audio. Meter guides bar labelling; it is not evidence of every beat.
- Drag-to-Tempo Track targets the existing Arrangement clip at its current absolute origin;
  horizontal drop position does not move the recording. Sample-browser import remains intact.
- Reset is scoped to a persisted baseline for the reference region; overlapping/externally
  protected edits must be validated rather than silently discarded.
