<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Feature Specification: MIDI expression swell

**Feature Branch**: `dev/windows-fixes`  
**Created**: 2026-11-02  
**Status**: Ready for planning

**Input**: Native MIDI Expression Swell Generator attachment. Add the seventh Generate
tool below Shape in the existing piano-roll Tools sidebar; generate editable controller
swells from selected notes/chords/phrases with safe multiple-controller targets.

## User Scenarios & Testing

### User Story 1 - Swell selected notes and chords (Priority: P1)

Select notes, choose Tools → Generate → Expression Swell, adjust the existing Parameters
section and preview rising CC11 expression. Apply once to commit an editable swell while
keeping the MIDI notes unchanged.

**Independent Test**: A single sustained note and a three-note chord each produce one
ramp per target; no selection produces an explicit instruction and no edits.

**Acceptance Scenarios**:

1. Expression Swell appears immediately below Shape; Transform and existing tools are unchanged.
2. Defaults are CC11, Start 20, Peak 100, Rise 600 ms, S-Curve, offset 0, Hold/Fall off,
   Retrigger, Replace within generated range.
3. Simultaneous/slightly staggered notes within adjustable tolerance form one chord event.
4. Preview is distinct and temporary; Apply is one undo action; Revert discards preview;
   Defaults restores all swell settings and never alters notes.

### User Story 2 - Choose safe independent CC targets (Priority: P2)

Search a compact scrolling controller selector by number, CC number or partial standard
name. Select multiple continuous/assignable controllers and set each Start and Peak.

**Independent Test**: Search 11/CC11/Expression/mod/breath; select CC1 + CC11 + arbitrary
assignable CC; inspect all 128 entries and their safety explanations.

**Acceptance Scenarios**:

1. All CC0–CC127 appear with accurate standard names or Undefined / Assignable.
2. Search, keyboard navigation, selected highlighting and closed-selector summary work.
3. Each target has independent 0–127 Start/Peak values; default selection is CC11.
4. Channel-mode CC120–127 cannot be used for a continuous swell. Switch/data-entry/bank
   controls show a clear reason and cannot silently generate a continuous ramp.

### User Story 3 - Shape expressive phrases (Priority: P3)

Choose Retrigger, Continuous or Phrase-based behavior for consecutive/connected notes.
Set rise in milliseconds, beats or duration percent, one of five curve shapes, offset,
optional peak hold, and optionally enable a fall.

**Independent Test**: Generate consecutive chords, overlapping/legato notes and phrases
using each curve/unit/mode, with negative offsets and clip-edge clipping.

**Acceptance Scenarios**:

1. Retrigger creates each onset; Continuous maintains continuity for connected notes;
   Phrase-based creates one envelope across each connected group.
2. Linear, Ease In, Ease Out, S-Curve and Exponential are supported; S-Curve is default.
3. Millisecond timing follows the project's available tempo information; timing calculations
   correctly handle known tempo changes when such a map is supplied.
4. Offset can anticipate an onset; output stays inside the active clip without shifting notes.
5. Hold is optional. Fall stays off until enabled; exactly 0 ms gives an ordered instantaneous
   step, while longer falls are smooth and bounded by the clip.
6. Sampling is smooth but bounded, values stay 0–127 and no duplicate beat positions remain.

### User Story 4 - Preserve existing CC and project workflows (Priority: P4)

Choose Replace within generated range, Merge, or Skip occupied ranges. Generate into
existing piano-roll CC lanes and continue manual editing, playback, saving and undoing.

**Independent Test**: Apply multiple controllers over existing automation; undo/redo once,
reload a saved project and verify playback against the generated values.

**Acceptance Scenarios**:

1. Replace only affects generated ranges, Merge preserves distinct existing events and Skip
   leaves occupied ranges unchanged. Unrelated controllers/ranges and all notes stay intact.
2. Target lanes are revealed using existing controls, retain resizing and timeline alignment,
   and show editable generated data after Apply.
3. Preview never permanently changes the project or adds undo history, and is invalidated
   safely by selection changes, undo, clip changes, rail hiding or another edit.
4. All targets apply/undo/redo as one operation and survive save/reload and existing MIDI
   playback/export paths where those paths are supported by Nota.

### Edge Cases

- Empty/locked/other-clip selections in multi-clip or track mode; short/zero-length notes.
- Chords of unequal note lengths, chained onset tolerance, overlapping generated ranges,
  negative offsets, a fall at the clip end, exact 0 ms fall and conflicting existing points.
- Very long dense passages, extreme parameter values and repeated preview edits.
- Unsafe CCs, empty target list and target parameter state while searching/reselecting.
- Current project tempo changes after preview; pending controller edits and editor closure.

## Requirements

### Functional Requirements

- **FR-001**: Add Expression Swell as the seventh Generate tool below Shape, using the
  existing Parameters/Apply/Revert/Defaults infrastructure and preserving Transform.
- **FR-002**: Provide searchable/scrollable, keyboard-accessible multiple selection of all
  128 standard-labelled CC entries; disclose and block unsafe continuous-ramp targets.
- **FR-003**: Configure independent integer Start/Peak per target, defaulting to CC11/20/100.
- **FR-004**: Process only selected editable notes of the active clip, group chords by
  tolerance and show a clear message for an empty applicable selection.
- **FR-005**: Support Retrigger, Continuous and Phrase-based grouping without unwanted
  resets across connected notes in continuity modes.
- **FR-006**: Support rise ms/beats/percent, five curves, signed offset, optional hold and
  explicitly enabled fall with target/duration/curve and exact zero-duration steps.
- **FR-007**: Use project tempo context, bound adaptive event density, clamp values and
  clip times, preserve event ordering and avoid duplicate positions.
- **FR-008**: Support range replacement, merge and occupied-range skipping without
  erasing unrelated notes/controllers/automation.
- **FR-009**: Provide distinct temporary CC preview, one-action multi-target Apply/undo/redo,
  Revert and Defaults; protect against stale previews and preserve MIDI notes.
- **FR-010**: Integrate real MIDI CC lanes, manual editing, playback and save/reload;
  preserve lane sizes, horizontal transforms and applicable existing export behavior.
- **FR-011**: Preserve Logic Grey and unrelated UI/Generate/Transform behavior; generate
  outside the audio callback and keep long-passage preview responsive.
- **FR-012**: Add automated generator/UI/integration coverage, run Windows build/tests,
  update documentation and commit only this feature locally without pushing.

### Key Entities

- **Target**: controller number, Start and Peak, safety classification.
- **Swell settings**: timing units, curves, offset, hold/fall, chord/phrase mode and collision policy.
- **Musical event**: chord onset/end or connected phrase boundaries from selected notes.
- **Generated range**: bounded time interval plus ordered controller values.
- **Preview**: generated controller replacement state tied to one active clip/selection snapshot.

## Success Criteria

### Measurable Outcomes

- **SC-001**: One selected note or three-note chord yields one swell per selected target.
- **SC-002**: Selector contains 128 searchable entries; no unsafe channel-mode ramp can Apply.
- **SC-003**: Default generation has no fall; enabling 0 ms fall creates a true step.
- **SC-004**: Preview/Revert produce zero permanent edits; all targets Apply/undo/redo once.
- **SC-005**: Generated data remains editable, plays back and round-trips a saved project.
- **SC-006**: All timing/curve/group/collision tests and Windows build pass; theme files unchanged.
- **SC-007**: Long-passage generation has explicit point budgets and measured responsive execution.

## Assumptions and clarification review

- Active-clip selection only; no implicit whole-clip operation. An explicit whole-clip
  action is optional and omitted to prevent accidental passage-wide changes.
- Unsuitable bank/data-entry/channel-mode controls are listed with reasons but disabled
  for this continuous generator; ordinary switch controls are likewise disabled here.
- Defaults for unspecified values: 20 ms onset tolerance, 30 ms phrase connection gap,
  0 ms offset, 200 ms optional hold and 400 ms optional fall toward Start.
- Retrigger ramps truncate at the next onset to avoid overlapping contradictory CC data.
  Short events compress the envelope to their available musical span.
- Tempo/export capabilities must be confirmed against the current engine during planning;
  do not create an unrelated tempo editor or export subsystem.
- This request authorizes a local commit for the swell feature. Previous unrelated work
  must remain uncommitted; no push is authorized.
- Clarification review: user behavior is specified; routine defaults documented above.
  No unanswered user question is required before design. Extension hooks are empty.
