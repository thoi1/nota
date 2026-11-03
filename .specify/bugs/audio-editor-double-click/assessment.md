# Bug Assessment: Audio editor does not open on double-click

- **Slug**: audio-editor-double-click
- **Created**: 2026-11-03
- **Source**: pasted text
- **Verdict**: likely valid, needs reproduction
- **Severity**: medium

## Report (verbatim or summarized)

The user reports that double-clicking an audio clip does not show the audio editor.

## Symptom

Double-clicking an audio clip in the Arrangement is expected to open its audio editor, but the editor is not shown. The report does not specify which editing tool or mode is active when the double-click occurs.

## Reproduction

1. Open a project containing an audio clip in the Arrangement.
2. Double-click the audio clip.
3. Observe whether the audio editor appears in the detail panel.

[NEEDS CLARIFICATION: Whether the issue occurs with the Select tool and automation mode off, or only with another editing tool/mode active.]

## Suspected Code Paths

- `src/managed/Nota.App/ArrangementView.LaneControl.cs:237-240` — automation mode and non-Select editing tools are dispatched before the normal clip double-click handler.
- `src/managed/Nota.App/ArrangementView.LaneControl.cs:299-306` — the normal double-click path selects the clip and calls `OnClipDoubleClicked`.
- `src/managed/Nota.App/ArrangementView.ToolClips.cs:28-43` — tool-specific handling can consume pointer presses; Nota Warp explicitly opens an audio clip editor, while other tool paths may not.
- `src/managed/Nota.App/ArrangementView.cs:1282-1286` — dispatches audio activation through `AudioClipActivated`.
- `src/managed/Nota.App/MainWindow.axaml.cs:213-216` — wires the Arrangement activation event to the audio editor opener.
- `src/managed/Nota.App/MainWindow.Detail.cs:600-640` — builds and displays the audio editor after activation.

## Root Cause Hypothesis

The normal Select-mode path is wired to open the audio editor, so the report is not explained by a missing activation route. However, the lane pointer handler returns early for automation mode and for non-Select tools before reaching the double-click branch. In those modes the expected double-click may be consumed by the active tool instead. Confidence: medium; reproduction with the user's active mode/tool is still needed.

## Proposed Remediation

**Preferred**: Reproduce with the Select tool and automation mode off, then with other tools and automation mode on. If the editor should open on a clip double-click regardless of the active tool, handle a double-click on an existing clip before tool-specific early returns, while preserving explicit tool gestures such as Nota Warp and preventing accidental clip creation or edits. Add a regression test covering the normal audio activation route and any mode/tool combinations the intended behavior supports.

If reproduction fails in Select mode with automation off, inspect event delivery and detail-panel visibility in the existing UI harness or a focused headless test before changing dispatch order.

**Files likely to change**:

- `src/managed/Nota.App/ArrangementView.LaneControl.cs`
- `src/managed/Nota.App/ArrangementView.ToolClips.cs` (only if tool-mode dispatch needs adjustment)
- A focused Arrangement UI regression harness or existing test project

**Tests to add or update**:

- Verify an audio clip double-click raises audio activation and shows the audio editor in the detail panel.
- Verify the chosen behavior when automation mode or a non-Select editing tool is active, without breaking the tool's existing gesture behavior.

## Risks & Considerations

- Changing dispatch order may conflict with automation editing or other active-tool gestures.
- The main-window event wiring and editor construction are present in code; avoid adding a duplicate activation path.
- No engine or audio-thread changes appear necessary.

## Open Questions

- [NEEDS CLARIFICATION: Which editing tool and mode are active when the failure occurs?]
- [NEEDS CLARIFICATION: Does double-clicking an audio clip still fail with the Select tool and automation mode off?]
