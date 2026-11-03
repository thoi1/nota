# Bug Assessment: Grid follows audio anchor interaction

- **Slug**: grid-anchor-interaction (user confirmed)
- **Created**: 2026-11-03
- **Source**: pasted user report
- **Verdict**: valid
- **Severity**: high — blocks core manual Grid Warp workflow

## Report

“cannot add anchors when grid follows audio is on and cannot move the anchors to snap it to the transient”

## Symptom

Adding and repositioning musical timing anchors is inaccessible or ineffective in the actual
editor. Existing tests chiefly drag below the ruler on an origin-zero unwarped fixture.

## Reproduction

1. Place an audio clip after beat zero, open its Sample Editor, select Grid follows audio.
2. Click Add anchor without first selecting another anchor. Inspect default Beat/Time versus
   the selected clip's origin and verify whether native commit succeeds.
3. Drag a musical line from the upper beat ruler. It currently seeks instead of editing.
   Double-clicking that ruler fails the Grid Warp Y guard.
4. Select an interior Grid Warp anchor and use Snap selected anchor. Its enablement and
   operation currently inspect only audio-warp marker selection.
5. For an auto-warped clip, inspect waveform/transient alignment before first Grid commit:
   Reload currently retains warped overview/marker coordinates in Grid tool mode.

## Suspected Code Paths

- AudioClipEditorView.cs: constructor calls BuildGridExtractionSection/SyncGridExtraction
  before Reload populates _startBeat; SelectWarpTool/Reload; OnPointerPressed seeks first.
- AudioClipEditorView.GridWarp.cs: BeginGridWarpGesture rejects ruler Y; hit/hover and
  source-boundary behavior; local drag validation and commit.
- AudioClipEditorView.BeatExtraction.cs: SyncGridExtraction initializes beat from stale editor
  placement once per source; selection callback does not update snap-button availability.
- AudioClipEditorView.Transients.cs: CanSnapSelectedAnchor and SnapSelectedAnchor only use
  _selMk/_mk, rather than selected Grid anchors.

## Root Cause Hypothesis

High confidence for the code-grounded cases above. Grid editing was added beside existing
ruler-seek and audio-marker interactions without completing their routing/selection logic.
Initialization caches source identity before placement is loaded, so nonzero-origin Add can
use an out-of-region beat. Auto-warped first edits also retain a warped overview even though
Grid coordinates are natural source coordinates. The exact user's pointer location and clip
are unavailable; reproduce the supported interactions rather than claim an exact file match.

## Proposed Remediation

Prefer Grid gestures over seeking only when a musical line/anchor is hit, including the
ruler; retain ordinary ruler seeking between lines and in other tools. Enable consistent
Grid selection/snapping, constrain nearest-transient snapping to legal neighboring intervals,
and leave physical boundary constraints intact. Seed Add fields from authoritative engine
placement after source geometry is available. Render the original-source overview/transients
while Grid tool is active, including the first edit of auto-warped clips. Surface commit
errors in the visible Grid status bar. No native/audio algorithm changes are needed.

**Files likely to change**:
- src/managed/Nota.App/AudioClipEditorView.cs
- src/managed/Nota.App/AudioClipEditorView.GridWarp.cs
- src/managed/Nota.App/AudioClipEditorView.BeatExtraction.cs
- src/managed/Nota.App/AudioClipEditorView.Transients.cs
- specs/079-grid-warp/ui/Program.cs
- CHANGELOG.md; feature verification and directly affected documentation/shortcut records

**Tests to add**:
Actual pointer ruler add/drag with Seek installed; nonzero-origin Add by actual button;
Grid selection and actual Snap selected anchor; source-coordinate display on auto-warped
clip; map/source preservation, one-action undo and existing mode/gesture regressions.

## Risks & Considerations

Keep seek behavior between grid lines and in other tools; protect first/last source anchors,
existing fixed anchors and original PCM/warp arrays. Changes remain on authoring/UI paths.
No dependency, C ABI or format change. Preserve existing dirty workspace and running project.

## Open Questions

No implementation blocker. User's exact audio and pointer position were not supplied;
reproductions will explicitly state fixture coverage.
