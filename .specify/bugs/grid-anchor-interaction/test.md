# Bug Verification: Grid follows audio anchor interaction

- **Slug**: grid-anchor-interaction (from the selected Grid Warp continuation and existing assessment/fix)
- **Tested**: 2026-11-03
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

The four fixture reproductions pass through actual Avalonia pointer/button handlers:
immediate Add on a clip at beat 4, ruler anchor creation/drag with Seek installed,
selected musical-anchor snapping, and original-source display/first edit on auto-warped
audio. The complete UI harness and focused engine/source-beat suite also pass. No further
production changes were needed in this continuation.

## Checks Performed

| Check | Command / Action | Result | Notes |
| --- | --- | --- | --- |
| Reproduction (post-fix) and UI regressions | `dotnet run --project specs/079-grid-warp/ui/GridWarpUi.csproj -c Release --artifacts-path src/native/nota.engine/build/grid-anchor-artifacts` | pass | Re-run during this continuation: all ten reported groups pass, including the four original failing cases, preserved ruler seeking between handles, source preservation, one-step undo, proposals/Apply/Cancel/Reset and slider debounce. |
| Engine/source-beat regression suite | `dotnet run --project tests/Nota.SmokeTest -c Release --no-build -- --grid-warp` | pass | Re-run during this continuation: 45 assertions, zero failures; covers atomic rejection, source playback, anchor identity, linked tempo edits, undo/redo, persistence, Reset and original-source beat inference. |
| Full Windows gate | Existing `src/native/nota.engine/build/grid-anchor-windows-final.log` from `scripts/build-win.ps1` | pass (prior run) | Reviewed existing evidence, not rerun: native build, scanner self-test, managed Release build with zero errors, SMOKE TEST PASSED and SCAN SCALE PASSED; 5,344 PASS lines. Log completed after the final production-source edits on 2026-11-03. |
| Shortcut reference | Reconcile `PreferencesWindow.ShortcutGroups` with Grid pointer routing, then `npm run shortcuts` in `../nota-docs` | pass | Both EN/RU reference pages regenerated: 18 groups, 212 shortcuts. Actual-app screenshot harness also compiled the app in Debug. |
| Screenshots and visual inspection | `npm run shots -- editing/grid-warp-shortcuts editing/grid-warp-fixed editing/grid-warp-preview editing/warp-shortcuts` in `../nota-docs` | pass | Four scenes rendered in both themes, zero failures. Inspected the new Grid Warp shortcut scene in dark/light: all four gesture rows are readable. |
| Manual synchronization | Inspect EN/RU `editing/audio-clips.mdx` against the repaired handlers | pass | Both already describe ruler editing, seek between lines, nonzero-origin Add, selected-anchor snap and original-source display. Created their missing Grid Warp shortcut screenshot. |
| Documentation build/check | `npm run build`, then `npm run check` in `../nota-docs` | pass | 213 pages built; 106 source pages checked, zero errors, 64 pre-existing warnings concerning older source paths and pending coverage. |
| Whitespace | `git diff --check` in app and docs repositories | pass | Only existing line-ending conversion notices. |

## Output Excerpts

```text
PASS Grid anchor regression: add
PASS Grid anchor regression: ruler
PASS Grid anchor regression: snap
PASS Grid anchor regression: source
PASS Grid Warp grid gestures, transient snapping, anchors and audio preservation
4 shot(s) · 0 failed
213 page(s) built
106 pages · 0 error(s) · 64 warning(s)
```

The pre-fix evidence remains in `src/native/nota.engine/build/grid-anchor-before.log`:
all four cases failed before the repair. This continuation did not remove the production
fix to repeat that negative control.

## Residual Risks

- The exact user audio and pointer coordinates were unavailable. Reproduction uses
  deterministic audio in the actual editor, including nonzero placement and nonlinear warp.
- Listening with a freely performed piano recording, physical MIDI devices, external
  plug-ins and an audible metronome was not performed. Automatic pulse inference remains
  an editable estimate; this report verifies the repaired interactions.
- Existing nullable/member-hiding/obsolete API compiler warnings remain.

## Recommendation

Close this interaction bug as verified by the automated reproductions and regression
checks. The documentation follow-up is complete locally, including EN/RU shortcuts and
dark/light images. Real-recording listening and hardware acceptance remain manual checks
described in `specs/079-grid-warp/verification.md`.
