# Native clock audit — Collapsible Tempo Track

Research for `speckit-plan`, 2026-11-03. No production changes made by this audit.

## Decision

Extend the existing `Transport`, not the automation engine. Put one immutable prepared
`TempoMap` in each `Graph` and carry it in `inheritGraphLevel`. `Transport` observes that
snapshot on the audio thread and remains the sole playback clock. The authoring graph
provides the same map to UI queries, edits, cache jobs and persistence. The existing
retired-graph lifetime scheme keeps an observed raw map pointer alive; do not create or
release the final `shared_ptr` owner on the audio callback.

Replacing `samplesPerBeat()` with an instantaneous BPM is **insufficient**: absolute
`beat * spb` / `sample / spb` formulas become wrong as soon as any earlier segment has a
different tempo. Retain `spb` only for a local DSP rate inside a prepared timing slice.

## Prepared coordinate representation

- Points are ordered ticks, positive BPM and outgoing Step/Linear/Smooth mode. Step
  changes BPM at the point and never discontinuously changes cumulative elapsed time.
- Keep seconds as the sample-rate-independent prepared coordinate, with cumulative
  prefixes. Define `beatToSeconds`, `secondsToBeat`, `bpmAtBeat`, `nextKnotSeconds`.
- Constant/step integration is `60 * deltaBeat / bpm`. For a linear BPM ramp with
  slope `k`, use `60/k * log1p(k * deltaBeat / bpm0)`; inverse uses `expm1`. Use a
  constant branch as `k → 0` to avoid cancellation.
- Smooth can use monotone cubic smoothstep BPM between positive endpoints, integrated
  outside playback with deterministic adaptive quadrature. No overshooting cubic BPM.
- A prepared monotone beat/seconds table gives callback binary search + linear
  interpolation, and the inverse is exactly the inverse of that same approximation.
  Build a conservative error budget in seconds at the maximum supported sample rate
  (e.g. <0.1 sample at 192 kHz); allocate table and compute logarithms/quadrature only
  on the authoring/preparation thread. Do not recompute cumulative sums per query.
- For interpolation error, `|time''| = 60 |BPM'| / BPM²`; a linear chord's maximum
  error is bounded by `h² max|time''| / 8`. For smoothstep `max|BPM'| <= 1.5
  |BPM1-BPM0| / segmentBeatLength`. Numerical integration needs its own error budget.
- Bound point count, tick range and prepared table storage; reject a candidate requiring
  excess capacity atomically. Dense points may imply sub-sample knots: use binary
  search to skip already-rounded knots, not a callback loop over arbitrarily many points.
- Keep one-point constant maps on the original fast path. Negative/count-in positions
  extrapolate with initial tempo; beyond the final point use final tempo.

### Slice-cost caveat

Processing at every adaptive table knot makes the local affine mapping exact for that
table and lets built-ins/MIDI FX retain local `spb`. However, dense/extreme ramps can
approach one `mixGraph` per frame, harming CPU even though no allocation occurs. Measure
this case, enforce bounded per-block slice count, and use direct coordinate lookups for
event placement even if DSP tempo updates are coarser. A fixed 64-frame approximation by
itself does not meet the sample-error contract for arbitrary steep ramps. Skip multiple
sub-sample knots with an `upper_bound`; round boundaries consistently with MIDI events.

## Exact render migration

| Existing symbol/file | Fixed-BPM assumption | Required change |
|---|---|---|
| `Transport.h::seekBeats`, `positionBeats`, `wrapToLoopStart`, `loopStartSamples`, `loopEndSamples` | Origin-based multiply/divide | Use the map's forward/inverse mapping, scaled by sample rate. |
| `Transport.h::setBpm` | Scalar BPM controls entire timeline | Authoring BPM operation changes the intended tempo point/map; preserve constant-map legacy behavior. Audio observes the published map, not a second tempo authority. |
| `Transport.cpp::transportInfo` | `ppqPosition=blockStart/spb`, scalar BPM | Map block-start samples to exact PPQ; expose curve BPM at that position and unchanged absolute seconds/samples. |
| `Transport.cpp::publishPresentation` | History origin, correction and start beat multiply/divide | Publish absolute sample/seconds window endpoints with revision; compute display start/end through the map. Loop corrections need elapsed intervals, not a beat correction times current spb. |
| `Engine_Render.cpp::processBlock` (~1749) | One `spb` captured for the entire device block | Observe graph/map before mapping commands/seek; split by loop, timing knots, count-in and quantized-launch boundaries. Compute each slice's actual start beat, end beat and local slope. |
| `processBlock::onLoopWrap` call | `loopStartSamples/spb`, `seamEndSamples/spb` | Pass stored musical loop bounds or map those actual samples; keep exact beat jump for held note recording/MIDI FX. |
| `gatherInstrumentNotes` (~1137) | Clip/note origin and duration times spb | Compute clip start/end and note on/off via `beatToSamples(clip.startBeat+localBeat)`; clamp note off in beat coordinates first. Keep musical `MidiEv` duration as note.lengthBeats, not sample duration divided by local spb. |
| `noteExprEvents` (~224) | Note-local curve beat is `(offset-onS)/spb` | Supply note start in musical coordinates plus an explicit slice clock; evaluate `clock.beatAtSample(blockStart+offset)-noteStartBeat`; map expression points from their musical offsets to sample offsets. |
| `clipCtlEvents` / `clipCtlEndResets` (~246) | Local controller points `clipStartS+p.beat*spb` | Supply clip musical origin and slice clock. Map each controller point's global beat to samples; evaluate curves from inverse-map beat minus clip origin. Preserve pedal/off ordering and chase logic. |
| `trackRoutedDown`, `captureRoutedNotes`, `captureRoutedHeld`, MPE capture (~1087–1128, ~1406) | `(blockStart+offset)/spb` | Inverse-map each event's actual sample, then apply count-in/session musical offset. Held-note chase uses mapped block-start beat. |
| `computeInstrumentMidi` / swing (~1259) | Sixteenth length is global `spb*.25` | Swing moves musical event position first, then maps to sample. Do not quantize absolute sample coordinates with one local sixteenth length. |
| `renderInstrumentRaw`, `playEvents` | Absolute `blockStart/spb` fed to instruments/MIDI FX; clip envelopes use `(p-clipStart)/spb` | Feed actual slice beat and local slope to local DSP. Clip envelopes use inverse-map beat minus clip beat origin. |
| `mixGraph` (~2081) | Automation/modulation/global Session clock=`blockStart/spb` | Map start beat once per slice, pass it explicitly. Automation remains existing block/slice-rate parameter evaluation, separate from tempo-map integration. |
| `processLeafDevices`, group/return devices (~2060, ~2574, ~2639) | Device transport uses `blockStart/spb` | Use the same slice beat/local slope and `TransportInfo` as instrument paths. |
| `renderMetronome` (~2727) | Integer beat is `floor(sample/spb)` | Derive integer beat from inverse mapping or precompute next integer-beat sample per slice; preserve `lastBeatEmitted_` across boundaries. |
| `renderCountIn` (~2751) | Constant inc is `1/spb` | Explicit policy: count in at tempo at the insertion marker; it precedes playing timeline and can remain constant if documented/tested. Do not accidentally start the map at beat zero for a count-in into a later segment. |
| `renderAudioClipsRaw` / `mixAudioClip` (~1558) | Clip start=`clip.startBeat*spb`; warped length=`warpPlayLen*spb` | Musical start→absolute samples through map. Warped end=`map(clip.startBeat+warpPlayLen)`; prepared cache length is that elapsed interval. Unwarped source duration remains absolute seconds. |
| `mixAudioClip` envelopes | Local beat/ADSR length from sample/spb | Evaluate inverse-map beat minus clip.startBeat. For unwarped end compute beat at `startSample+sourceDurationDeviceFrames`. |
| `fillFrozen` (~1676) | Frozen buffer index=`currentBeat*frozenSpb` | Store the capture map/range with frozen audio or invalidate/re-freeze on map edits; one scalar cannot index variable-tempo frozen audio correctly. |

### Recommended expression/controller clock contract

Do not pass a different independent timing engine into helpers. A lightweight value
`BeatClockView` can reference the authoritative prepared map plus a musical offset:

```text
sampleAtBeat(localBeat) = transport.beatToSamples(localBeat + musicalOffset)
beatAtSample(sample) = transport.samplesToBeat(sample) - musicalOffset
```

Arrangement offset is zero. Session offset at the slice start is globalStartBeat minus
sessionLocalStartBeat. For expression use explicit `noteStartBeat`, `noteEndBeat`,
`blockStartSamples`, `frames`, `BeatClockView`. For controllers use `clipStartBeat`,
`clipEndBeat` with the same clock. Keep existing constant-spb wrappers only for isolated
self-tests if necessary; do not use them for variable-tempo playback.

## Session phase and launch

`renderSessionSlotRaw`, `renderSessionAudioSlotRaw`, `sessionPreApply`, `sessionPostRender`,
the muted-track branch in `mixGraph`, and `SessionPlayer::publish` advance by
`frames/spb`. Use `deltaBeat = map(blockEndSamples)-map(blockStartSamples)` exactly.
Local Session clock is a phase offset from global musical advancement, not a new tempo
map. Map an event at Session local beat `b` using global beat
`globalStartBeat + b - sessionLocalStartBeat`. Audio-slot loop-end splits must use that
same conversion; unwarped slot audio retains natural elapsed duration.

`SessionPlayer.cpp::maybeApply` currently applies a launch at the **start** of a block
that contains a quantization boundary. To claim sample-accurate launches across tempo
changes, compute the pending request's next quantized global beat, split processing at
its mapped sample, and apply only at that boundary. Preserve `pendingLegato`, follow
actions, Repeat mode and loop-seam launch behavior. Tempo transitions need not reset
Session phase. Scene tempo policy must not silently destroy the Arrangement map.

## Master chain: easily missed

`processBlock` currently processes master devices **after** all slices and transport
advancement. It recovers time as `positionBeats - numFrames/spb` and chunks by kMaxBlock;
unlike leaf/group/return paths, it does not set `TransportInfo` for master plugins here.
That cannot represent a loop or tempo transition inside the block.

Preferred: run the master chain on each mixed slice before advancing Transport, with
the identical `TransportInfo`, slice start beat and local slope. Keep preview/tone
placement and metronome pre/post-master policy unchanged deliberately. Alternative:
record fixed-capacity per-slice metadata (`outputOffset, frames, startSamples,
startBeat, localSpb, TransportInfo`) and replay that exact schedule for master devices.
Capacity must be bounded by the chosen maximum callback frame count, not tempo points.

## Warp-cache preparation

Current `media/Warp.h::WarpInputs` includes markers, trim, mode, pitch, `spb`, device
rate. `Engine_Tracks.cpp::warpInputsOf`, `warpCacheFrames`, `renderWarpWindow`, and
`reuseWarpCache` assume every output beat has identical elapsed length. `WarpStream.cpp`
converts marker beats to output frames by multiplying by spb.

Keep source↔musical markers as authored data. Off-thread cache preparation can bake a
**derived** source↔elapsed-output marker list for the existing stretcher:

1. Capture the immutable project map, clip musical start/trim/reverse and source.
2. Form a sorted union of source warp marker beat positions and project timing table
   knots inside the played window. At an added tempo knot, interpolate its source frame
   through the original source↔beat marker map.
3. Map each union position to output seconds (or device frames). Feed these synthetic
   coordinates to `ClipWarpStream` with a nominal `spb=sampleRate` (seconds coordinate),
   or `spb=1` (device-frame coordinate). This list is preparation output, not another
   editable map. Preserve existing constant-map rendering fast path.
4. Include map revision/content and absolute clip musical origin in cache inputs.
   Moving a clip under a nonconstant map invalidates its output even with identical
   local markers; completion must revalidate both before publishing.
5. Use full-material origin `clip.startBeat - warpPlayStart` when deriving output trim
   offset; map interval subtraction replaces `warpPlayStart*spb`. Cache slice reuse is
   valid only for the same absolute map interval, not just a narrower beat range.

**Reverse gotcha:** simply reversing a forward variable-tempo cache also reverses the
tempo distribution. To retain current musical reverse semantics, bake forward output
coordinates with reversed project-time traversal, then reverse-read as today, or prepare
reversed source explicitly. For an original material beat `m`, trim `[a,z]`, and project
start `c`, reverse output distance before final reverse is proportional to
`T(c+z-a) - T(c+z-m)`, not `T(c+m-a)-T(c)`. Add a nonuniform-tempo reversed transient test.

A Session clip may launch at any global beat: a cache baked at arrangement origin zero
cannot automatically be correct for every launch under a variable curve. Either prepare
against explicit captured launch/loop interval or use a documented musically indexed
cache plus bounded resampling policy; do not claim sample-accurate pitch-preserving
Session warp using one global scalar-cache key.

## Authoring audio / recording / freeze migration

- `Engine_Tracks.cpp::audioBeatsToSrcFrames`: source frames for musical delta at clip
  origin are `(T(start+delta)-T(start))*sourceRate*pitchRatio`, not `delta*spb`.
- `audioLenBeats` / `audioDisplayLenBeats`, `clipInfo`, `Engine_Session::audioClipBeats`:
  natural audio end is `inverse(T(start)+sourceDuration/pitchRatio)-start`.
- `audioReverseHead`, `audioCarveLeft/Right`, `audioSlice`, `audioOverwriteRange`,
  `placeAudioNonOverlap` and their callers (`moveClip`, range move/duplicate/copy/paste,
  insert time, trim/resize/split, consolidate, deep clone) need origin-aware interval
  conversion. Replacing only playback leaves visible lengths and edited source regions wrong.
- `beginFreeze`, `consolidateAudioClips`, freeze playback/capture state: duration is a
  mapped interval plus real-second tails; store capture origin/map or invalidate freezes.
- `Engine_Recording.cpp::activateLoopPunch`: loop buffer frames are mapped loop interval
  times recording sample rate; origin is elapsed map interval from loop start to record beat.
- `audioRecordLengthBeats`, Session take lengths and Arrangement materialization use the
  capture start map and elapsed duration, not the current BPM at stop time.
- `latencyCompensatedStart`: subtract latency in sample/seconds coordinates, then inverse
  map. The existing static constant-spb helper can remain as legacy test convenience.
- `AudioTake` (`Track.h`) stores only `beatsPerFrame`; `Engine_Takes.cpp::atTakePosition`,
  `beatsPerSourceFrame`, `placeTakesForTempo`, `takeInfo`, comp placement assume affine
  source↔beat. Variable tempo needs captured source-time↔beat mapping/shared capture map;
  no single replacement scalar handles it. Existing take/comp seam tests must stay green.
- `Engine_Modulation.cpp::modulatorValue` derives displayed beat as samples/spb; use map.

## Plugin and managed-clock boundaries

`TransportInfo` is already the JUCE-free POD adapter contract. `NotaPlayHead.cpp`
translates it into JUCE `AudioPlayHead` for hosted instruments/effects; racks forward it.
VST3 gets correctly timed per-process-slice context if every path, including master,
uses this snapshot. Bar start formula uses PPQ/time signature and is already appropriate.

There is **no CLAP host/transport implementation** in the current tree. Plugin discovery
uses JUCE default formats (VST3/AU); matches for “Clap” elsewhere are drum voices.
Report CLAP as unavailable/unverified rather than inventing a passing CLAP sync test.
Adding a CLAP host is a separate dependency/hosting capability decision.

Managed consumers also contain scalar assumptions:
`TransportViewModel.UpdatePositionText` uses `beats*60/Bpm` for elapsed display;
`TransportDisplayPosition.At` extrapolates snapshot elapsed time using one BPM.
Both need native shared-map time queries / presentation coordinates. Arrangement and
piano-roll seconds rulers, audio editor `ClipTimeMap`, export/freeze durations, take
overlays and waveform geometry must query that same authoritative mapping.

## API, publication, undo and validation

- Add POD tempo point/anchor structs and bounded list read/replace, conversion and
  lane-state APIs to `nota_engine.h`; expose through existing engine port/interop.
- Authoring validates finite positive BPM, strictly ordered unique ticks, protected zero,
  count/range limits and absolute locked anchors before any publish or undo checkpoint.
- Replace points/anchors atomically in one graph transaction, using existing
  `beginUndoGroup`/`endUndoGroup` or one replace at gesture commit. Preview can be local
  geometry until commit to avoid expensive snapshot/table preparation per pointer move.
- Add tempo map to `Graph.cpp::inheritGraphLevel`; otherwise unrelated track edits erase it.
  Undo/redo already swaps graph snapshots, but audio must observe the restored map, and
  authoring cache/take queries must use the restored graph map rather than authoringBpm_.
- Map adoption should preserve the current musical beat (recompute elapsed sample
  coordinate under the new map) and reset presentation epoch, without allocating or
  accidentally retriggering all held voices as a seek would.
- The existing deferred BPM path must remain cheap. No eager all-clip rebuild in pointer
  handlers, no worker join of unfinished jobs from a poll, no new worker pool.

## Minimum discriminating verification

Test actual event samples across a nonzero-origin clip and a BPM change preceding it;
this fails a superficially “instantaneous spb” implementation. Add a linear ramp note
and CC/MPE test spanning several table knots, a loop spanning a step and a ramp,
Session phase/quantized launch, unwarped trim/resize under ramp, reverse warped hits,
take comp seams, master plugin context, and export at a different rate. Map round-trip
tests alone do not prove scheduling, cache, recording or plugin integration.
