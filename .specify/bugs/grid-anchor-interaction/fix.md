# Bug Fix: Grid follows audio anchor interaction

- **Slug**: grid-anchor-interaction (from assessment context)
- **Fixed**: 2026-11-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Fixed four reproduced interaction gaps: first Add on nonzero-origin clips, ruler add/drag,
selected musical-anchor snapping, and original-source display before the first Grid commit.

## Changes

| File | Change | Notes |
| --- | --- | --- |
| AudioClipEditorView.cs | modified | Route Grid hits before transport seeking; keep seeks between lines; show natural source overview in Grid mode |
| AudioClipEditorView.GridWarp.cs | modified | Accept ruler hits/hover, select committed drag result, expose errors in visible Grid status |
| AudioClipEditorView.BeatExtraction.cs | modified | Seed Add beat/time from shared map and original geometry, update snap-button state on musical selection |
| AudioClipEditorView.Transients.cs | modified | Snap selected musical anchor to nearest valid original transient; preserve neighboring anchor constraints and one commit |
| specs/079-grid-warp/ui/Program.cs | regression coverage | Four separately reported cases, all exercised by default harness; actual pointer buttons/drags with Seek installed |
| PreferencesWindow.cs, Languages/fr.json | modified | Document Grid ruler/wave gestures in actual shortcut list and translate new labels |
| CHANGELOG.md | modified | Unreleased Fixed entry |

No native, dependency, C ABI or format changes. Physical source-window boundaries remain
fixed. Snap validates both neighboring BPM intervals and respects protected fixed proposals.

## Tests Added or Updated

RunAnchorInteractionRegression cases add/ruler/snap/source exercise a clip at beat 4.
All four fail before production changes (`grid-anchor-before.log`) and pass after
(`grid-anchor-after.log`). The source case also drags the first anchor on a clip with
nonlinear audio warp markers. Default harness runs these plus the existing six grouped
mode, slider, preview, original-tempo drop, Apply/Cancel and Reset checks.

## Local Verification

`dotnet run --project specs/079-grid-warp/ui/GridWarpUi.csproj -c Release
--artifacts-path src/native/nota.engine/build/grid-anchor-artifacts` → exit 0, all ten
reported groups PASS (`grid-anchor-ui-final.log`). Actual Avalonia pointer paths exercised;
source PCM/warp arrays retained; Add/snap one undo; ruler between handles still seeks.
`git diff --check` → pass. Full Windows gate and delivery checks recorded in test.md.

## Deviations from Assessment

None. Preferences and translation files are the directly affected shortcut records.
The test build used isolated outputs while the user's open project locked the app DLL.
User saved/closed the desktop app before rebuilding the normal Release outputs.

## Follow-ups

Complete bug-test validation and synchronize the EN/RU manual and generated shortcuts using
nota-docs/nota-shortcuts; no new user-visible capability is added beyond repairing these paths.

Completed on 2026-11-03: see [test.md](test.md) for the repeated UI/engine checks,
regenerated bilingual shortcuts, four screenshot scenes in both themes, and successful
documentation build/check. Listening and physical-device limitations remain explicit there.
