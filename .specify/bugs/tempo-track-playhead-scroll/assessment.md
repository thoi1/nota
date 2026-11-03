# Bug Assessment: Tempo track does not follow playhead scroll

- **Slug**: tempo-track-playhead-scroll
- **Created**: 2026-11-03
- **Source**: pasted text
- **Verdict**: likely valid, needs reproduction
- **Severity**: medium

## Report (verbatim or summarized)

> the tempo track doesnt scroll with the playhead

## Symptom

When playback Follow scrolls the Arrangement to keep the playhead centered, the Tempo
Track may remain visually at its previous horizontal position while the other lanes move.
Expected: its curve and grid follow the same horizontal scroll as the Arrangement.

## Reproduction

1. Enable Follow in the Arrangement.
2. Start playback and let the playhead move far enough to trigger horizontal scrolling.
3. Observe whether the Tempo Track curve moves with the rest of the Arrangement.

Exact playback setup and whether Follow is enabled are [NEEDS CLARIFICATION].

## Suspected Code Paths

- `src/managed/Nota.App/ArrangementView.cs:731` — `UpdatePlayheadDisplay` moves the shared
  scroll position during Follow and invalidates `_lanes`, but not `_tempoLane`.
- `src/managed/Nota.App/ArrangementView.cs:1007` — `Redraw` does invalidate `_tempoLane`
  for ordinary scroll/zoom changes, indicating the missing invalidation is specific to the
  playback-follow path.
- `src/managed/Nota.App/TempoTrackControl.cs:78` — the track renders its grid and tempo
  points using the owner's shared scroll/beat-to-X mapping.

## Root Cause Hypothesis

High confidence: `CenterOnPlayhead()` changes `_scrollBeats` and `_hScroll.Value`, but the
playback-follow path repaints `_lanes` without repainting the retained `TempoTrackControl`.
The track therefore can keep showing the previous scroll until some other action calls
`Redraw()`. Reproduction in the running app has not yet been performed.

## Proposed Remediation

**Preferred**: In `UpdatePlayheadDisplay`, invalidate `_tempoLane` whenever the Follow
scroll actually changes, alongside `_lanes`. This keeps the tempo grid and curve synchronized
without repainting it on every playhead tick when the view is stationary. Check whether
other programmatic horizontal-scroll paths need the same invalidation.

**Files likely to change**:
- `src/managed/Nota.App/ArrangementView.cs`
- `tests/Nota.SmokeTest/` or the existing tempo-track UI validation harness

**Tests to add or update**:
- Verify that the tempo lane redraws against the new shared scroll position when Follow
  moves the Arrangement, and that non-follow/manual scrolling continues to redraw it.

## Risks & Considerations

- The Arrangement source files are already modified in the worktree; preserve all existing
  edits and make only the focused invalidation change.
- Repainting the tempo curve during Follow adds work only when the scroll position changes;
  its curve sampling is bounded by the visible lane width.

## Open Questions

- [NEEDS CLARIFICATION: Does this occur only while Follow is enabled, or also during manual
  horizontal scrolling?]
