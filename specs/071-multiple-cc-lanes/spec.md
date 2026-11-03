<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

# Feature Specification: Multiple MIDI CC lanes

**Created**: 2026-11-01
**Status**: clarified; user requested this before finishing spec 070
**Input**: “can i add multiple cc lane with a plus and minus button”

## User Scenarios & Testing

### US1 — Edit several controllers together (P1)

A musician uses + below the piano roll to add visible controller lanes, picks a controller
for each, and edits curves without switching the existing velocity/MPE lane.
**Acceptance**: two or more lanes display different controllers simultaneously; each uses
the existing controller picker, continuous-curve or switch-block editing and one-step undo.

### US2 — Remove clutter without losing music (P1)

The musician clicks − on a lane to hide that lane.
**Acceptance**: removing any lane leaves all controller data, other lanes and notes intact;
adding it again shows its data. The editor remains usable with many lanes and when zoomed.

## Requirements

- **FR-001**: Add native + and per-lane − controls and independent controller selection.
- **FR-002**: Reuse the existing controller editor for each extra lane and its commit/undo path.
- **FR-003**: Extra lanes share the existing horizontal beat axis, active clip and cache;
  edits, undo/reload, zoom/scroll and track-mode clip selection update every visible lane.
- **FR-004**: Removing a lane changes visibility only, never project controller data.
- **FR-005**: Bound the extra-lane area with scrolling so notes and existing velocity/MPE
  editing remain available; no audio/native/ABI, transport or playhead changes.
- **FR-006**: Follow Studio native UI, current localization and both Studio Dark/Logic Grey.
- **FR-007**: Build and run meaningful regression tests using Windows x64.

## Success Criteria

- **SC-001**: Add/select/remove at least three independently editable controller lanes.
- **SC-002**: Removing/re-adding a lane preserves its points byte-for-byte; edits affect
  only the chosen controller and retain the existing undo callback semantics.
- **SC-003**: Multiple lanes follow the shared zoom/scroll and reload after a clip change;
  many lanes scroll without consuming the whole note editor.
- **SC-004**: Native settings/design/localization and Windows smoke checks pass.

## Assumptions and Edge Cases

− hides a view rather than clearing CC data; clearing remains the existing context-menu
action. Lane layout is editor-local and survives note reloads in that editor, but this
request does not require persisting it in project/settings. Reserved controllers remain
excluded by the existing picker. An empty lane is valid. Removing/changing a lane cancels
its pending gesture. Existing velocity/MPE and fifth controller tab remain available.

## Clarification

User explicitly chose “Prioritize multiple CC lanes, then finish Meter Refresh Rate”.
Spec 070 remains pending; this feature adds no new keyboard bindings.
