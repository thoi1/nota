# Research: Interactive Tempo Time Mapping

## Gesture axis

**Decision**: Use the start map for pointer deltas and snap targets, and use the
preview map for drawing. Capture a stable pointer-to-time conversion independent
of the display axis. Anchor the destination in seconds rather than feeding the
updated beat under the pointer back into the solver.

**Rationale**: The existing `FreezeTempoAxis` also freezes display positions.
That is appropriate for ordinary point drags and would hide the grid movement
needed here. The underlying transport conversion remains authoritative.

## Local tempo solver

**Decision**: Use adjacent distinct existing tempo knots as fixed anchors, with
song start as the fallback on the left. When there is no right knot, use the next
bar boundary strictly after the mapped beat as the right anchor. Use two constant
tempo spans inside the affected interval. For each span, BPM is 60 times the beat
distance divided by the requested seconds distance. Intersect the feasible time
intervals for both spans before solving. If a snap target is required, choose the
nearest legal frozen grid target or leave the preview unchanged.

**Rationale**: This preserves both anchor times analytically. Existing same-beat
Tempo lane point pairs encode the incoming/outgoing tempo at each splice, so the
curve on either side remains intact. The affected interval can change shape.
Scaling arbitrary partial curves was considered, but splitting Nota's current
curve shape mid-segment is not guaranteed to reproduce the original curve.

## Audio and preview transaction

**Decision**: Keep the timing basis of audio through the mapping edit. Natural
rate clips already reproject placement in `Engine::deriveTempoMap`, but clip-local
envelopes still use beat time. For warped audio, retain its original effective
local timebase and use the existing `tempoWarpPlan` to reconstruct the same
source-to-output mapping. Reuse an equivalent cache on preview. Persist the basis
for save/load and restore it via the graph on undo/redo. Project the saved
absolute edges into the new map for scheduling and display.

**Rationale**: Reprojecting a warped clip's start or markers alone can change
sample timing inside a nonlinear segment. The scheduler and timeline index
also currently derive warped duration in beats. The render lookup, clip-local
envelopes, index, clip display and cache identity must agree on the preserved
timing. Rebuild caches off the render thread after load or sample rate change.
The basis persists across repeated mapping gestures. An explicit clip timing or
warp edit may establish a new basis. Ordinary tempo editing keeps its existing
behavior for clips that have no retained basis.

**Alternative considered**: Flattening warped clips to rendered audio would
preserve output but discard the editable warp relationship. A gesture-specific
timing basis keeps that relationship.

**Transaction**: Recompute the detached display preview from the immutable
starting graph. Playback keeps the original graph until release. Cancel
discards the candidate without changing history. Commit publishes the
validated candidate and checkpoints the baseline once. An unrelated
structural edit invalidates the candidate so it cannot overwrite that edit.
