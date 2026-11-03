<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Research

- Decision: use SetDetailMaximized and SetHost, not a second PianoRollView.
  Rationale: main layout already hides Mixer/main/browser, preserving wanted heights;
  SetHost drains other windows' layout queues before reparenting. Rejected a new floating
  workspace because it loses the integrated transport/menu and duplicates hosting.
- Decision: snapshot old docked content/visibility and existing detached window. Hide
  the detached window during the main workspace, then restore it with the same controls.
  Research agent confirmed activation is isolated to OpenClipEditor; ShowClipPanel is
  shared with Session/audio and must retain its normal behavior.
- Decision: reuse docked close; add close header to floating ChainHost wrapper only.
  Rationale: detached editor currently always shows a separate Auto-height device row.
- Decision: drag local lane dividers in logical units, save on release, cap heights,
  keep controller overflow scrollable. Existing curve rendering already reads Bounds.Height.
- Decision: center velocity stem/cap on BeatToX(note.StartBeat) and pick that coordinate.
  Root cause: Render adds 3px, Nearest adds 4px, while notes use the exact transform.
- Decision: 3200 maximum pixels/beat (8x), existing 6 minimum and anchored ZoomAt.
  Existing grid density is derived from visible scale; verify extreme zoom in UI probe.
- No unresolved clarification. No extension hooks. No audio-thread impact.
