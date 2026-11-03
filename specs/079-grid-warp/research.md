# Research: Grid Warp

## Existing Architecture Observed

- `AudioClipEditorView` owns Sample Editor controls and wires `WaveformView` marker edits to the existing clip-warp APIs. Its current Audio follows grid button is a plain button that can enable transient warp, while Grid follows audio immediately calls the clip-tempo workflow. These side effects must be separated from the new UI-only mode selector.
- `WaveformView` already renders source-relative transient markers and warp markers. It supports zoom/scroll transforms, source-region trims, and existing marker drag/add/delete operations. Grid Warp must use a separate gesture branch so it cannot change the established warp-marker behavior.
- `ITransientAnalysisService` returns immutable source-relative results and `TransientAnalysisService` captures engine state on the authoring thread, runs analysis on background workers, and supports cancellation/progress/cache reuse. `TransientWorkspace` currently configures analysis on a completed control gesture, but does not debounce/re-run analysis for new settings. UI currently uses `DragNumber`; the service accepts sensitivity 0–1 and minimum spacing 5–1000 ms, with defaults 50% and 30 ms.
- `TempoMap` is the authoritative native musical clock. It stores ordered points and timing anchors, supports Step/Linear/Smooth curves, prepares beat/seconds knots outside rendering, and provides forward/inverse conversion. `TempoAnchor` can hold beat tick, absolute seconds, source frame, reference identity, and lock state.
- `Engine::fitClipTempo` already builds piecewise-constant BPM intervals from source-frame/beat anchors, rejects invalid interval ordering and BPM limits, preserves points outside the fitted interval, and validates protected absolute-time anchors. `refreshTempoReferences` re-maps Grid follows audio clips under the new tempo map.
- The native renderer has a separate Grid follows audio source playback path and does not use the audio stretch cache for those clips. Audio follows grid uses the existing prepared warp cache. This is the key protection for source audio; regression tests should compare rendered source positions/times, not only clip metadata.
- `TempoTrackControl` previews tempo-point drags locally and commits on release through the engine map API. It already supports add/move/delete, limits, selection, and anchor-unlock menu actions; BPM value labels and Grid Warp anchor synchronization need verification/addition.
- `ProjectService` captures and restores the tempo map, while `AudioClipState` persists clip workflow/reference identifiers and warp markers. Existing tests cover map curves, tempo undo/redo, protected anchors, clip-reference persistence, transient analysis, and tempo cache behavior.

## Decisions

### Keep one tempo and time model

- **Decision**: Use `TempoMap` and `Transport` for every beat/time query and commit. Grid overlay positions, transient targets, and tempo events use beat ticks and absolute seconds from the existing map.
- **Rationale**: The engine already provides monotonic forward/inverse conversion and is the clock for playback, MIDI, automation, and transport.
- **Alternatives considered**: A Sample Editor-only beat clock or a second mapping engine. Rejected because either would diverge from project playback and violate the user's no-independent-model requirement.

### Reuse existing timing anchors and preserve audio

- **Decision**: Extend existing anchor relationships for manually committed Grid Warp correspondences where possible. Preview candidates locally; on release validate and commit one tempo-map/anchor transaction. Keep Grid follows audio on the native source playback path; do not build a stretch cache for it.
- **Rationale**: Native tempo fitting and absolute-time anchors already implement the essential invariant. Publishing a map on each pointer event would run graph and tempo-reference work unnecessarily.
- **Alternatives considered**: Encode Grid Warp as Audio follows grid warp markers or ordinary automation. Rejected because either stretches audio or fails to drive transport time.

### Keep mode choice separate from project state

- **Decision**: Use a three-state editor-local mode selection. Switching modes does not call audio/tempo mutation APIs. Actual Audio follows grid and Grid follows audio edits are committed only by their mode-specific gestures.
- **Rationale**: This meets the explicit no-side-effects-on-switch requirement and avoids running both workflows at once.
- **Alternatives considered**: Persist the selected mode as clip data or toggle `GridFollowsAudio` immediately. Rejected because mode switching would then change project state, and the latter currently fits anchors into the tempo map.

### Synchronize explicitly linked Tempo Track points

- **Decision**: Moving or deleting a Tempo Track point explicitly linked to a Grid Warp anchor updates or removes that anchor in the same undoable transaction. Unrelated tempo-point edits preserve all fixed anchors. A BPM edit that conflicts with a fixed anchor is constrained or rejected, with explicit unlinking required where necessary.
- **Rationale**: The user requires bidirectional synchronization while also requiring Grid Warp anchor timing to remain authoritative during edits elsewhere.
- **Alternatives considered**: Allow Tempo Track edits to silently drift linked anchors or audio alignment. Rejected because it would break the source-time/beat relationship and violate the requested constraint.

### Use shared transient worker and cache

- **Decision**: Keep threshold normalized to 0–1 internally and display 0–100%; constrain minimum spacing to 5–250 ms in the existing service and editor. Debounce setting changes, cancel older jobs, and update views only from the current settings' snapshot.
- **Rationale**: The existing service already shares results, handles background work, and avoids the real-time thread.
- **Alternatives considered**: Add a Grid Warp detector/cache or re-run analysis synchronously per slider event. Rejected due duplicate work, UI stalls, and cancellation complexity.

### Use step events for generated tempo

- **Decision**: Generate `TempoCurve.Step` intervals for Grid Warp anchors initially and keep existing non-affected point segments unchanged.
- **Rationale**: The user's initial scope explicitly requires piecewise-constant segments; the engine supports those exactly.
- **Alternatives considered**: Smooth/linear interpolation between anchor-derived BPM values. Rejected for the initial implementation because it changes the stated calculation and can violate exact anchor times unless a separate correct fitting algorithm is added.

## Risks Requiring Implementation Verification

- Current source is already heavily modified and contains uncommitted work in the exact files in scope. All implementation must inspect the current buffers and preserve those changes.
- Audio Editor currently uses a fixed four-beat bar in several waveform/ruler calculations. Grid Warp must source meter from Nota's project time-signature data and handle meter-change boundaries rather than hard-coding 4/4.
- Existing tempo anchors include clip source frames and a shared reference ID. Tests must verify multiple clips/references do not cause one Grid Warp edit to move unrelated anchors.
- The existing `TrySetTempoMap` and `TrySetClipTempoMode` operations are separately exposed. If they cannot atomically commit the clip reference plus generated events, implement a narrowly scoped validated transaction at the existing C ABI boundary instead of accepting an intermediate inconsistent graph.
- The user has Nota open from the previous test build (PID 14288, if still running); close that specific process before a Release build if it locks output files.

## Completion audit — 2026-11-03

Read-only planning research traced current manual pointer press/move/release/capture routing:
Grid Warp already branches before Audio Warp in AudioClipEditorView.cs. The original missing
anchor workflow had no independent grid-line gestures/model; the current manual foundation
adds them, but earlier checked tasks did not cover the full attached request.

Remaining concrete gaps: Snap All Beats/proposals/Apply/Cancel, Add anchor, selected-anchor
numeric editing, Reset baseline, durable per-anchor IDs and internal clip-to-tempo drop.
Current refreshTempoReferences rewrites audio warp-marker beats/window; this violates the
latest source-preservation requirement. UI grid range uses raw clip offset/length instead of
the native trimmed reference window. Native/managed builders force Step at the region tail
and can change a preceding ramp endpoint. Unsupported non-Step seams should reject safely
until an exact shape-preserving splice exists. There is no time-signature automation model;
use the existing meter, test 3/4 and denominator units, and report that existing limitation.

Decision: independent persisted source window/origin on references, original warp data untouched.
Expose reference geometry through the engine port; natural source playback reads this geometry.
Reject non-neutral pitch in original-speed Grid Warp rather than secretly changing source speed.
Use existing reserved POD slots for positive stable anchor IDs and explicit linked point IDs,
keeping struct layout unchanged; zero means legacy/unlinked. Native publication allocates IDs.

Decision: pure bounded SourceBeatInference in Application consumes original-source SuperFlux
snapshots. Starting period provides half/double prior; a bounded beam scores pulse continuity,
attack strength, subdivisions and missing attacks. No new DSP dependency or callback work.
Persist original-source seed/results separately from musical project timing through the existing
analysis port and AudioClip DTO. Derived source beats are facts about a source, not a project clock.
Preview fixes manual anchors exactly, distinct proposals remain editable, and Apply reuses the
validated atomic Grid Warp transaction. Confidence is evidence, not calibrated certainty.

Decision: an existing-engine source-analysis service captures immutable source data on authoring,
then runs transient analysis/inference off-thread with cancellation/stale-result guards. Capture
original BPM seed at import before auto-warp. Source result persistence uses original sample identity
and source frames; tempo/map/warp edits never retime these stored source observations.

Decision: reset stores a reference-region baseline (old points/anchors and original clip workflow),
splices only that region back via one native edit and validates other fixed anchors. Persist it in
clip state. Grid source geometry remains separate from preserved audio warp markers.

Decision: Arrangement clips use captured-pointer internal drag, not OS Drop. Route release over
Tempo row before ordinary clip movement; restore preview positions, retain source track/clip IDs,
open existing editor and start original-source inference. Do not export/reimport an audio cache.
