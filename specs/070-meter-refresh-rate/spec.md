<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

# Feature Specification: Meter refresh rate

**Feature Branch**: current working branch
**Created**: 2026-11-01
**Status**: clarified
**Input**: Configurable 30/60 Hz audio level meters in Settings → Appearance.

## User Scenarios & Testing

### User Story 1 — Choose smoother meters (P1)

A musician chooses Meter refresh rate in Appearance without changing audio setup.
**Independent test**: switch between exactly 30 Hz and 60 Hz during playback.
**Acceptance**:
1. Fresh or older preferences select 30 Hz; 60 Hz is the only alternative.
2. Switching applies immediately to visible transport, arrangement and mixer level meters.
3. The selection is restored after a new launch.

### User Story 2 — Monitor without changing timing (P1)

The musician keeps accurate audio, transport, metric readings and smooth playheads.
**Independent test**: measure meter and service cadence, cursor movement and audio setup.
**Acceptance**:
1. Both rates retain existing audio callback configuration and transport scheduling.
2. CPU/DSP sampling remains independent; meters also work for live input while stopped.
3. Both arrangement and piano-roll cursors retain frame-timed motion.

### User Story 3 — Spend rendering work only where visible (P2)

Meters work in dark themes and floating windows without repainting hidden views.
**Independent test**: hide/minimize views, restore them and compare both rates/themes.
**Acceptance**:
1. Hidden/minimized meters are skipped; reopening resumes without duplicate clocks.
2. Studio Dark and Logic Grey use the existing native settings controls and meter colours.
3. Record actual update rate and rendering overhead at each setting on Windows x64.

### Edge Cases

Missing/invalid saved rates fall back to 30. Slow displays cap observed presentation.
Export suspension and shutdown must cancel queued work before engine disposal. A floating
mixer remains active while the main window is minimized. Constant/silent levels need no repaint.

## Requirements

- **FR-001**: Appearance exposes exactly 30 Hz (default) and 60 Hz (smoother meters).
- **FR-002**: Selection is immediate, persistent and safe for older/invalid preferences.
- **FR-003**: Apply the rate to primary transport, arrangement/return/master, Mixer and
  Session audio level meters; reuse existing UI/scheduling patterns and prevent duplicate feeds.
- **FR-004**: Preserve independent CPU/DSP sampling, other device telemetry and all audio,
  sample-rate, buffer and transport behavior. Do not introduce a new performance-monitor UI.
- **FR-005**: Preserve the prior Windows arrangement/piano-roll cursor fixes.
- **FR-006**: Skip hidden/minimized level meters, support floating Mixer and live input while stopped.
- **FR-007**: Keep native styles, existing themes and peak-hold/scale semantics unchanged.
- **FR-008**: Measure both requested rates and overhead and build/test with Windows x64 tools.

### Key Entities

Refresh preference (30 or 60), visible level meter, and independent presentation cadence.

## Success Criteria

- **SC-001**: Actual visible meter changes average within 10% of the selected rate on
  this machine with a varying signal and sufficient display callbacks; report distributions.
- **SC-002**: A new process restores either choice; a missing/invalid value selects 30.
- **SC-003**: Hidden-meter state/drawing counts remain unchanged and restore works.
- **SC-004**: Cursor and service cadence remain independent of meter choice; no reported
  audio dropouts attributable to the change in the measured samples.
- **SC-005**: Full Windows build/smoke tests pass; report UI overhead at both rates and
  actual theme/lifecycle coverage without claiming physical presentation measurement.

## Clarifications

- User answered “audio level meter” when asked which Performance Monitor was meant.
  This applies to the existing primary audio level meters previously measured, plus
  Session's equivalent level bars. CPU/RAM graphs are not present and will not be added.
- Device-specific analyzers/scopes and output rails remain on their existing telemetry
  clocks; this setting changes the primary mixer/track/master level-meter presentation.
- No changes to keyboard bindings or project format. Continue in the user's current checkout.

## Nota Compliance

No native/DSP/ABI/dependency changes. Follow the spec workflow and Studio design system;
update changelog/features/manual guidance and run Windows regression checks.
