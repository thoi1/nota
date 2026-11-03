# Tempo Track: managed UI, persistence and warp audit

Research only, 2026-11-03. Existing working tree changes were preserved; no production
source was changed by this audit.

## Authority and existing behavior

`Transport` is the native playback clock. No project-wide tempo-point collection is
currently exposed through `IAudioEngine` or persisted in `ProjectDocument`. The current
clip `WarpPositionMap` is strictly a source-frame↔clip-beat interpolation helper, not a
project clock. `Controls/WarpRuler.cs` explicitly documents that it supplies feedback
only and has no tempo-map engine. Do not turn this helper into a second project clock.

`AudioClipEditorView` currently implements Audio follows grid: the nested WaveformView
holds `_mk` source/beat pairs, previews pointer movement locally and calls
`MarkersCommitted` → `WarpEdit` → `IAudioEngine.SetClipWarpMarkers` on commit. The
existing `WarpEdit` defers cache preparation and raises `WarpBuildRequested`; it must
remain cheap. `WaveformView.ApplySegmentBpm` changes clip marker beats, not project BPM.
The transient section (`AudioClipEditorView.Transients.cs`) moves only the selected
interior marker source to a detected attack; no global tempo fitting exists there yet.

## Minimal coherent layout integration

`ArrangementView.cs` constructor assembles a DockPanel with pinned Overview, Sections
and ruler above the vertical track scroller. Add a dedicated Tempo row immediately after
the ruler and before `centerArea`; it then stays above ordinary tracks while vertical
track scrolling remains unchanged. This avoids injecting a fake audio/MIDI TrackVM or
changing `_rowTops`, row selection, header reorder, group sizing, returns or master.

Implement a partial `ArrangementView.Tempo.cs`, analogous to
`ArrangementView.Sections.cs`, owning the row/control and editor working copy:

- `BuildTempoRow`: header + graph using existing `HeaderPanel` and `AddSideRow`.
- `HeaderPanel` automatically registers the width/divider; any extra fixed-width child
  also joins `_headerColumnParts`. `AddSideRow` uses the existing width and column-side
  layout policy (`ArrangementView.Sizing.cs`), so resizing the header stays aligned.
- Reuse owner `_pixelsPerBeat`, `_scrollBeats`, `BeatToX`, `SnapMaybe` and
  `SnapGridState`; do not introduce a second horizontal ScrollViewer or zoom factor.
- Add tempo-control invalidation to `Redraw()`; it already runs on wheel, h-scroll,
  follow, zoom and grid changes. `Refresh()` must reload accepted tempo points unless
  a tempo gesture is active, as `ReloadSections` does for sections.
- Compact row about existing 24–30 px chrome; expanded row uses bounded user height
  and a bottom seam grip. Keep the same height for its header, scale and graph.
- Header includes existing Glyph caret, current BPM, and compact/expanded action.
  Use `DragNumber`/field controls for numeric selected-point beat/BPM editing.
- Keep normal curve neutral (`NotaPalette.VizInk`/text ink), selection/hover accent,
  seams and well colors from existing tokens; use `Loc` and French translations.

`SectionsControl` is the closest project-level lane example: working-copy previews,
single commit on release, reload after accepted commit and engine-backed undo. It is a
better structural starting point than adding Tempo as parameter automation.

## Editing and command routing

`ArrangementView.AutoEditor.cs` is private and tied to `TrackVM`, `AutoPt` float values,
parameter targets and ordinary automation playback. Reuse its hit-radius, capture and
fine-drag concepts, but do not add a Tempo AutomationTarget or reuse its engine commit.
Tempo points need integer ticks, double BPM, dedicated interpolation and constraints.

Prefer local preview during drag, then one validated whole-map commit on release. With
10,000 points, binary-search visible/hit ranges and draw a bounded number of strokes;
never enumerate source samples or rebuild stretch caches from Render/PointerMoved.
`Escape`/capture loss restore the original working copy. Preserve the initial tick-zero
point and clamp group movement to neighboring unselected ticks before committing.

`MainWindow.Menu.cs` routes global Undo/Redo to Engine and `RefreshAfterUndoRedo()`
refreshes Timeline and open editors. A graph-backed tempo map then gains native undo
without another managed undo stack. Add tempo reload/warp-cache preparation to that
refresh path as necessary. Do not let a protected-anchor rejection checkpoint undo.

Keyboard commands currently favor Session/piano roll/tracks/clips in MainWindow menu
handlers. Route copy/cut/paste/delete/select-all to the Tempo editor when its graph or
inspector has focus, and handle tempo arrows/Escape locally. Otherwise existing global
clip deletion or computer-keyboard note handling can consume these keys. Update
`nota-shortcuts`/Preferences.ShortcutGroups for every actual new binding or gesture.

## Shared warp workflow and anchor policy

Expose explicit Audio follows grid / Grid follows audio controls; changing the existing
Warp Tool selection alone must not reinterpret the song. Route both workflows through
native project-map authoring APIs, then broadcast a tempo revision/changed signal so
Timeline, open audio editor, Session and transport reread one accepted snapshot.

Suggested policy:

- Audio follows grid retains existing clip source↔musical-beat markers and musical
  clip/MIDI/automation coordinates; only their mapping to elapsed time changes.
- Grid follows audio fits native project map from explicit absolute/source time versus
  project tick correspondences. Convert clip-local marker beat to arrangement beat
  using clip start/trim/reverse mapping; source time uses source sample rate, never
  device sample rate. It must leave the original waveform/sample unchanged.
- Protect an explicit absolute anchor by storing its absolute seconds and tick plus a
  durable source relationship (saved track/clip index and sample asset identity if it
  refers to a clip). Reject a whole candidate if conversion would violate a protected
  correspondence; a UI message states which protected anchor caused rejection.
- Do not silently reinterpret all existing clip markers as absolute locked anchors.
  Their current semantics are musical source mappings and must remain backward compatible.

`ClipWarpMarkers.Read` already captures complete marker lists, including >128 markers.
`ClipTimeMap` and `WarpPositionMap` already handle trim/rate/reverse/source↔arrangement
geometry; use them when creating explicit source correspondences, not an approximate
`sourceFrames / currentBpm` formula.

## Persistence and old projects

`ProjectService.CurrentFormatVersion` is 29. `ProjectDocument` currently contains scalar
`TransportDto.Bpm`, tracks, Session, sections and mixer, with extension data retained.
Add project tempo points/interpolation/bounds and explicit anchor relationships to the
document; capture from the engine, not the transport UI's instantaneous BPM readout.

`ProjectService.Apply` calls `engine.Reset`, then `engine.SetBpm(doc.Transport.Bpm)`
before creating clips. Load the native map baseline at that point, before clip warp/take
geometry; avoid feeding the old scalar back into the map after it has been restored.
Append format migration 29→30 and bump CurrentFormatVersion when serialized schema
changes. Legacy absence means one tick-zero point at the saved scalar BPM. Validate
malformed/newer data through existing migration/error conventions.

`ArrangementView.Sizing.cs` already persists cosmetic project layout in bundled
`track-view.json`: `TrackViewDto`, `SaveTrackView`, `LoadTrackView`, `ResetTrackView`.
Extend it with tempo visible/expanded/expanded-height fields. `MainWindow.Files.cs`
already calls Reset on New, Load before refresh and Save with the bundle. This keeps
layout cosmetic and outside musical undo. Ensure the early 'delete default sidecar'
condition considers new fields, else tempo-only layout changes will disappear.

The existing application Settings has ArrangementShowSections and other global view
preferences, but per-project Tempo layout belongs in the sidecar required by this spec;
do not add a conflicting global preference unless explicitly defining its precedence.

### Concrete Grid follows audio fitting recommendation

For an explicit reference clip, convert each accepted marker to `(projectTick,
absoluteSeconds)` using the clip's source-rate/trim/reverse playback transform and its
chosen absolute start. Require strictly increasing ticks and seconds. For adjacent
correspondences `(b0,s0)`, `(b1,s1)`, a Step segment with
`BPM = 60 * (b1-b0) / (s1-s0)` is an exact initial fitting policy. Splice only the
selected span into the one native project map, preserving the elapsed-time origin and
both boundary correspondences. Reject impossible BPM bounds or ordering atomically.
This establishes correct synchronization without claiming automatic rubato inference
or inventing an independent piecewise map in the clip editor.

Reference audio must then play at its original source time/rate (absolute placement),
not additionally stretch through the fitted map. Preserve source sample + marker
relationships as authoring data even if the stretch playback flag is disabled. Existing
musically anchored clips/MIDI/automation continue to use project ticks. Editing a
reference correspondence recomputes the adjacent native tempo segments and redraws the
Tempo lane; editing Tempo instead evaluates all explicit protected correspondences
against the candidate. For unlocked references, the documented map edit wins and their
derived displayed musical grid changes. Locked correspondences preserve their original
absolute seconds and ticks and reject a conflict; they do not own a second mutable map.

## Fixed-BPM managed assumptions to migrate

| File / symbol | Current assumption | Required treatment |
|---|---|---|
| `ArrangementView.Controls.cs::RulerControl.RenderTime` | `RulerLabels.Seconds/BeatsAt` uses a scalar BPM | Query shared map at global beat/time; musical grid x stays beat-based. |
| `PianoRollView.cs::RenderTime` | Converts `origin + local beat` with scalar BPM | Query global map then subtract clip origin only for x. |
| `AudioClipEditorView.cs` time ruler (~1934) | `_bpm` maps all beats to seconds | Use arrangement-global map for musical played time; distinguish source-time view. |
| `TransportViewModel.UpdatePositionText` (~274) | `beats * 60 / Bpm` | Engine elapsed-time conversion; instantaneous BPM cannot recover elapsed time. |
| `TransportViewModel.OnBpmChanged/FollowEngineBpm` | scalar setter + 1s acknowledgment | Multiple-point edit/insertion must preserve unrelated points; following current BPM must not echo writes. |
| `MainWindow.TempoWarp.cs` | Only BPM property changes trigger 120 ms coalesced worker | Trigger same worker from accepted map/anchor edits and undo; preserve close/export/project guards. |
| `MainWindow.AudioImport.cs` pending length | frames/rate × current BPM/60 | Duration endpoints need map difference at import start for absolute audio placement. |
| `MainWindow.Convert.cs` fallback clip length | seconds × current BPM/60 | Convert elapsed duration around clip start through shared map. |
| `AudioClipEditorView::AdsrTime` | envelope beat duration × scalar BPM | Use map endpoint difference at actual clip origin when showing elapsed duration. |
| `AudioClipState`/`ProjectService` | Scalar transport saved alongside clip markers | Persist shared map and anchor relationship baseline; source data remains unchanged. |

Not every BPM calculation is a project-clock bug: `ClipTempo.Bpm` estimates a source
clip's musical duration, `ClipTempo.Scale` rescales clip warp coordinates, and sample
analysis/import beat inference may legitimately report a scalar source BPM. Preserve
those semantics rather than replacing them indiscriminately.

### Additional complete managed timing fan-out

- **Master and stem export**: `Nota.Infrastructure/Audio/AudioExporter.cs::FramesFor`
  computes `TotalBeats * SampleRate * 60 / Bpm`. Both `ExportMaster` and `ExportStems`
  use it. Replace with sample-rate times the native map difference between
  `StartBeats + TotalBeats` and `StartBeats`; do not merely evaluate instantaneous BPM
  at the range start. `ExportRequest.TotalBeats` includes the requested beat-based tail.
  `MainWindow.Files.cs::DoExportAsync` and `Nota.Mcp/Tools/ExportTools.cs::Request`
  currently supply scalar BPM; it may remain metadata/fallback but cannot determine length.
- **Export UI**: `ExportWindow.RefreshRange` and `RefreshEstimate` use scalar `_bpm`
  for duration, WAV/FLAC/MP3 size and tails. Pass a shared-map range-duration callback
  or an accepted snapshot. The dialog's musical range coordinates remain unchanged.
- **Freeze**: `Nota.Infrastructure/Audio/TrackFreezer.Freeze` delegates frame allocation
  to native `BeginFreeze(trackId,lengthBeats)`, then renders the returned count. Audit
  that native count and frozen-buffer playback use the same map revision.
- **Bounce at nonzero origin**: `TrackFreezer.BounceRange` delegates
  `BeginBounce(trackId,endBeat-startBeat)` before seeking startBeat. With variable tempo,
  duration alone loses origin. Extend that call to carry startBeat or an exact map-derived
  frame count. `MainWindow.PasteBounced.cs` and `MainWindow.LiveFreeze.cs` use this helper;
  the live-freeze linked audio placement must not be stretched twice or move on map edits.
- **AI separated stems**: `MainWindow.Stems.cs` delegates to
  `ClipAi.ReadStemSource/ApplyStems`; the raw separation is absolute audio and should not
  adopt a current-BPM time conversion. Preserve source placement/warp relationships when
  applying each stem. This is distinct from `AudioExporter.ExportStems`.
- **Smooth playhead**: `Nota.Presentation/ViewModels/TransportDisplayPosition.At`
  currently computes `StartBeat + ElapsedSeconds * snapshot.Bpm / 60` and wraps beats.
  This is wrong inside ramps and when the presentation window spans a tempo boundary.
  Supply the exact same immutable map/revision or a bounded prepared presentation
  interval, never maintain a separate display clock. `MainWindow.Playhead.cs` consumes
  this result for Arrangement, piano roll and detached editors; their geometric drawing
  coordinates remain in beats. Do not update recording/edit positions from interpolation.
- **Elapsed transport readout**: `TransportViewModel.UpdatePositionText` must map beat
  to elapsed seconds; instantaneous `Bpm` continues to be appropriate as a visible BPM
  value. `RemoteHub.State`/MCP transport/project overview also legitimately report the
  instantaneous engine BPM plus beat position; no new independent conversion there.
- **MIDI import**: `Nota.Application/Midi/MidiFileReader.cs` converts PPQ ticks to
  quarter-note beats and deliberately drops imported tempo (documented current policy).
  `MainWindow.MidiImport.cs` preserves these musical positions. Do not map them through
  current BPM or silently add tempo-import behavior as part of this UI change.
- **Audio→MIDI fallback lengths**: `MainWindow.Convert.cs` and
  `Nota.Mcp/Tools/AiTools.cs` convert decoded seconds to beats using current BPM when
  no explicit clip length is returned. Use the map duration endpoints at clip origin.
- **MIDI effect/visual estimates**: `Nota.Mcp/Tools/MidiFxTools.cs` converts
  `msPerBeat = 60000 / E.Bpm`; `Controls/LengthGateViz.cs` and
  `Controls/RandomOutputViz.cs` perform local ms↔beat display conversions. These are
  instantaneous device parameter/readout estimates, not event timestamp authorities.
  Native scheduling must be audited separately. Do not claim these estimates are exact
  elapsed durations over a ramp unless upgraded to origin-aware map conversion.
- **Scope windows**: `ScopeTrace.WindowSeconds` and `ModularView.Scope` convert a
  beat-synced window at current BPM. This is a live visual time-window estimate; use map
  interval if exact historical beat coverage is required, but do not confuse it with
  playback/MIDI time scheduling.
- **Preset/sample audition**: `PresetAudition`, `PreviewPlayer`, generated Grain fixtures
  and `SampleInfo.Beats` use explicit source/preview BPM. They are independent audition
  fixtures/sample facts and should remain constant-tempo unless auditioning the project.
- **Load/recovery traps**: `MainWindow.Files.cs::OpenProject` sets
  `_vm.Transport.Bpm` after ProjectService.Apply, and `MainWindow.Recovery.cs`
  captures scalar TransportState. Do not let those updates overwrite an already-restored
  multi-point map. Store/query map separately and use a follow-without-echo refresh path.

## Documentation and validation

Affected EN/RU manual pages: `basics/transport`, `views/arrangement`,
`editing/audio-clips`, `basics/undo`, `reference/shortcuts`; `editing/recording` and
Session tempo text should be reviewed once their timing policy is implemented.
`../nota-docs/coverage.yml` already maps these features. Use real headless app screenshots
for expanded/compact states, and update coverage sources for the new partial/control.

Extend the actual headless MainWindow harness pattern used by
`.specify/bugs/tempo-change-lag/ui`: pointer-add/drag/delete, selection/box/numeric,
collapse/resize, h-scroll/zoom, undo and close with pending preparation. Add engine +
project round-trip tests separately. Keep the tempo-lag regression green; no UI pointer
path should call synchronous full-source cache preparation.
