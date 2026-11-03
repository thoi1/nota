# Feature Specification: Shared transient analysis, warp and tempo mapping

**Feature Branch**: existing working branch (preserve concurrent editor work)
**Created**: 2026-11-02
**Status**: Specified
**Input**: Native SuperFlux transient detection shared by Nota Warp, waveform overlays and musical tempo mapping; deliver in verified increments, starting with the detector and shared service.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reliable source transients (Priority: P1)

An editor analyses a recording once and reuses stable attack positions across clips and editing tools, without interrupting playback or changing source audio.

**Why this priority**: Both warp workflows depend on reliable attacks; transients alone do not establish beats.
**Independent Test**: Analyse generated mono/stereo recordings, inspect source positions and confidence, repeat with identical settings, cancel and invalidate analyses.
**Acceptance Scenarios**:
1. **Given** isolated sharp and soft attacks, **When** analysed, **Then** results are sorted source sample positions with strength, confidence, timing uncertainty and version.
2. **Given** vibrato, silence or noise, **When** analysed, **Then** sustained pitch motion is suppressed and silence produces no attacks.
3. **Given** shared audio, **When** requested by two consumers, **Then** completed analysis is reused; settings or source revisions produce separate results.
4. **Given** an obsolete job, **When** cancelled or invalidated, **Then** partial results never replace a completed snapshot and playback remains responsive.

### User Story 2 - Edit audio against the grid (Priority: P2)

An editor sees transient guides, snaps selected warp anchors to attacks or places anchors manually, then stretches audio nondestructively using the existing warp metadata.

**Why this priority**: Attack guides make existing warp editing precise without treating every attack as a beat.
**Independent Test**: Trim, move and stretch a clip; toggle transient display/snapping, drag an anchor, undo and reload.
**Acceptance Scenarios**:
1. **Given** analysed audio, **When** shown in the waveform, **Then** guides follow source offsets, trim and warp mappings, and differ visually from confirmed anchors.
2. **Given** a selected anchor, **When** dragged with snapping enabled, **Then** the nearest transient is highlighted, anchors cannot cross, and undo restores the previous mapping.
3. **Given** Warp mode, **When** selected, **Then** the ruler shows muted red feedback using the existing toolbar and shortcuts.

### User Story 3 - Let the grid follow a performance (Priority: P3)

A musician previews beat inference on expressive piano, chooses a first beat, meter and half/double tempo interpretation, then applies or resets a tempo map without stretching the recording.

**Why this priority**: A shared variable clock must synchronise the whole application before this mode can be offered.
**Independent Test**: Apply a synthetic rubato performance map and verify beat/time round trips, MIDI events, automation and plugin transport.
**Acceptance Scenarios**:
1. **Given** attacks and a manual first beat/meter, **When** tempo is detected, **Then** candidate beats, initial tempo, confidence and competing interpretations are previewed separately from transients.
2. **Given** accelerando/ritardando, **When** smoothing is adjusted, **Then** a strictly increasing map preserves expressive timing, with accurate forward/inverse conversions.
3. **Given** an applied map, **When** playback runs or the project reloads, **Then** transport, MIDI, automation, piano roll and plugin time share the map, while the source recording remains unchanged.

### Edge Cases

Silence; clipped/non-finite input; antiphase stereo; attacks at file boundaries; recordings shorter than one analysis window; different sample rates; very long sources; overlapping attacks; sustained vibrato; invalid settings; cancellation during analysis; source replacement during a job; trimmed/moved/rate-adjusted and warped clips; no reliable beat; half/double ambiguity; crossing anchors; project close/reopen.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Detect attacks using SuperFlux with vibrato suppression, then optionally refine attack time locally with explicit uncertainty; do not modify audio.
- **FR-002**: Return immutable sorted source-relative sample positions, seconds, strength, normalised confidence and analysis version; band contributions are optional.
- **FR-003**: Share analyse/get/nearest/range/invalidate operations keyed by immutable source identity, revision and settings; support background execution, cancellation and progress.
- **FR-004**: Bound working analysis memory independently of source duration, apart from returned transients; reuse existing processing and scheduling facilities. No analysis runs on the audio callback.
- **FR-005**: Transform positions through actual clip offsets, trims, rates and warp anchors; hide out-of-window positions.
- **FR-006**: Expose Analyse/Show/Snap transients, sensitivity and minimum spacing; default to expressive piano settings. Avoid analysis and geometry rebuilds during pointer movement/playback.
- **FR-007**: Reuse existing warp anchors, stretch cache, undo/redo and persistence; allow manual placement and disabling snapping; prevent invalid/crossing anchors.
- **FR-008**: Infer beats separately; report confidence/ambiguity, initial BPM, half/double interpretations, manual first beat and meter; support changing tempo and smoothing.
- **FR-009**: Use one strictly monotonic beat/time map for all musical timing; preview/apply/reset are explicit undoable operations and survive save/load.
- **FR-010**: Grid follows audio leaves audio unstretched; Audio follows grid stretches only explicitly selected attacks nondestructively. Do not automatically quantise every transient.
- **FR-011**: Integrate controls into Nota design/localisation, preserve current toolbar/keyboard mappings and use muted red Warp ruler feedback.
- **FR-012**: Verify deterministic audio, timing transformations, threading/lifetime, editing and synchronisation with automated tests and actual Windows builds; record unavailable evaluations without invented metrics.

### Key Entities

- **Transient**: source sample/time, onset strength, confidence, uncertainty, refinement status, analysis version.
- **Analysis settings/snapshot**: source identity/revision, sensitivity, spacing, algorithm version and sorted results.
- **Warp anchor**: source sample paired with local musical beat; distinct from a transient.
- **Tempo proposal/map**: candidate musical beat/time pairs, confidence, ambiguity, meter, smoothing and application state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Deterministic isolated attacks at 22.05, 44.1, 48 and 96 kHz are detected once each within 20 ms; refinement improves sharp attack error to at most 2 ms when reliable.
- **SC-002**: Silence returns zero attacks; vibrato fixtures produce no repeated false attacks after their initial attack. Stereo polarity reversal does not erase attacks.
- **SC-003**: Cached queries return the same completed snapshot; invalidated or cancelled work cannot publish stale results. Nearest and range results match a sorted exhaustive search.
- **SC-004**: Working analysis memory remains constant as recording duration grows, excluding audio already held by Nota and the returned list; progress is monotonic and cancellation is checked at least once per analysis frame.
- **SC-005**: Clip transformations and tempo-map round trips differ by less than one source sample on deterministic fixtures.
- **SC-006**: Both warp workflows preserve source samples, survive undo/reload, and maintain MIDI/plugin clock synchronisation within one render block.

## Assumptions

- Existing user edits remain in place; no branch switch or reset is required.
- First verified increment is the native detector and shared service, as explicitly authorised in the request; later stories remain open until actually built and verified.
- Sources are Nota's decoded immutable sample buffers. Long-file decoding is an existing separate limitation; analysis must not duplicate the full recording or retain a full spectrogram.
- Confidence expresses local detection evidence, not a calibrated musical probability. Unreliable refinement retains hop-based timing and uncertainty.
- Licensed real piano recordings and annotations have not been supplied; synthetic evaluation is reproducible, and real-recording precision/recall remain unclaimed until suitable data is available.
