# UI Contract: Grid Warp

## Warp mode selector

- The Sample Editor shows three mutually exclusive toggle choices: **None**, **Audio follows grid**, and **Grid follows audio**.
- The active choice uses Nota's orange accent; inactive choices use the normal theme.
- Selecting an active choice clears it. Selecting another mode replaces the active choice.
- Mode selection alone does not write clip warp data, tempo-map points, or timing anchors.
- None preserves existing non-warp Sample Editor interactions. Each warp mode routes gestures only to its own editing workflow.

## Grid follows audio waveform

- Show project bar/beat lines over the waveform when Grid follows audio is active.
- Lines and hit targets track the current horizontal zoom, scroll, and project meter.
- Hover shows a distinct draggable affordance. A drag preview identifies the musical beat, target transient when snapped, and resulting interval BPM.
- Transient snapping is on by default for the gesture. Holding Shift bypasses it for the duration of that drag.
- Double-click an eligible line to create a fixed timing anchor; Alt-click a committed anchor to remove it; drag a committed anchor to change its alignment.
- A gesture previews locally and commits once on release. Escape/capture loss cancels the preview.
- The waveform itself and its source-time playback remain unchanged in this mode.

## Transient controls

- Threshold is a horizontal slider from 0% to 100%, default 50%, with a visible numeric percentage.
- Minimum spacing is a horizontal slider from 5 ms to 250 ms, default 30 ms, with a visible numeric value.
- New slider settings debounce analysis; stale results do not replace the current result set.
- The analysis status reports progress/result count and existing errors visibly.

## Tempo Track

- Committed tempo points and their BPM values are visible when the Tempo Track is expanded.
- Moving or deleting a tempo point linked to a Grid Warp anchor updates or removes that anchor in the same edit; unrelated tempo edits preserve fixed anchors.
- A BPM edit that would break a fixed anchor is constrained or rejected, or requires the musician to explicitly unlink the anchor first.
- Grid Warp commit, Tempo Track point edit, deletion, undo, redo, and project reload keep the tempo map and associated anchor state consistent.
- Invalid/crossing/protected edits are rejected without partial updates and report an explanatory status using Nota's existing error surface.

## Audio follows grid

- Its existing warp marker gestures and cache-based nondestructive stretch remain unchanged.
- It does not create or edit project tempo points.
- Switching to or from the mode alone does not apply or undo a warp.

## Completion controls and transactions

Inline editor only: Add anchor, selected Beat/Time, Reset, Snap All Beats, starting/min/max BPM,
beat sensitivity, preview status, Apply and Cancel. Fixed/preview/transient visuals are distinct.
Proposal correction changes local source-frame positions; Apply is one native publication.
Drop an Arrangement audio clip onto pinned Tempo row: route through captured LaneControl,
leave clip placement unchanged, open its existing editor and start original-source preview.
Existing source speed or locked boundary conflicts report actionable rejection, not partial edits.

Native reference-state getter/restore carries source window/origin independently of audio markers.
Anchor/point IDs occupy existing reserved slots, retaining binary sizes. Reset and Apply use
validated graph transactions and retain source PCM, source speed and original audio warp data.
