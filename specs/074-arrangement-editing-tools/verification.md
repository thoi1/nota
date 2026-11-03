# Arrangement editing tools — verification

Date: 2026-11-02. Implementation is local and uncommitted. No push or pull request was created.

## Delivered behavior

- The main Arrangement toolbar has one inline group with Select Tool, Pencil Tool, Eraser Tool, Waveform Draw Tool and Nota Warp Tool. Exactly one is selected, including when clicking it again. Tooltips and accessible names use the requested English names; French translations are supplied.
- Existing `tp-icon` styling and 15px Glyph rendering provide the shared button geometry, hover and selected states. Pencil reuses `GlyphKind.Edit`; Pointer, Eraser, WaveDraw and WarpGrid extend the shared vector catalogue. No paths appear in views.
- Pencil click/drag creates a MIDI clip in an empty instrument-track gap. The active snap grid determines the interval, including triplets/odd meters; Alt or Snap off allows free positioning. Forward/reverse drawing stops at neighboring clips. The preview makes no engine changes until release.
- Eraser deletes the clicked MIDI/audio clip or hit automation point. Its cursor uses the shared eraser Glyph, is regenerated after colour-theme changes, and is restored when switching tools.
- Show the existing Automation mode to edit envelopes on tracks, returns and master. Pencil draws freehand; Waveform Draw offers Sine, Triangle, Sawtooth, Square and Freehand. The arrow beside the group opens shape, amplitude (25/50/75/100%), cycle length (1/4, 1/2, 1, 2, 4 bars) and phase (0/90/180/270 degrees) choices. Waveforms center on the press value and clamp to the target range. Cycle/phase apply to periodic shapes.
- Stroke previews preserve original points/curves outside the swept interval, bound generated point counts, protect the preview from live-recording refresh/follow scrolling, and commit once under an existing undo group. Creating the first lane and writing its points is one undo step. Escape, tool change, automation-mode change or existing window-deactivation cancellation discards uncommitted drawing.
- Select retains the existing clip move, selection, trim/loop paths. Row seams, target selectors, right-click actions, ruler navigation, zoom and scrolling retain priority/their existing handlers. The group remains accessible at tested widths 1600, 1280 and 1100.
- Nota Warp selects its native grid icon and visibly explains that ruler tempo-grid editing is unavailable. It invokes no stretching, sample or tempo-changing API.

## Dependencies and limitations

- There is no shared tempo-map/Tempo Track engine to edit ruler beat/bar positions while keeping reference audio fixed in absolute time. Existing audio-clip Warp renders stretched clips and is deliberately not used. Transient-assisted ruler editing therefore remains unavailable.
- Arrangement has no MIDI CC lanes. The piano-roll CC infrastructure remains separate and unchanged. Automation drawing covers the supported Arrangement envelopes only.
- Arrangement clip dragging currently uses Alt to bypass snapping; it has no Alt-drag duplication handler. No Arrangement Alt + right-click erase shortcut was found. Existing behavior is preserved; these conditional preservation requests do not add new bindings.
- Square and other shapes are sampled at the active grid resolution and follow the existing lane interpolation. A coarse grid can undersample a short cycle; choose a finer grid or longer cycle.
- `C:/Dev/nota-docs` is absent. EN/RU website edits, generated shortcut-page updates and documentation screenshots/build/check could not run. Local user-facing docs, the Preferences shortcut reference and translations are updated; [manual-update.md](manual-update.md) contains ready-to-apply EN/RU instructions. This is the remaining convergence item.
- Only Windows was built/executed here. OS-level cursor appearance and real pointer interactions on macOS/Linux were not tested.

## Validation results

| Check | Result |
| --- | --- |
| `scripts/build-win.ps1` under VS x64 developer environment | Pass: native engine, scanner self-test, Release solution build, complete smoke suite and scan-scale check; exit 0 |
| Full smoke suite | `SMOKE TEST PASSED` |
| Scanner scale | `SCAN SCALE PASSED` |
| `dotnet run --project tests/Nota.SmokeTest -c Release -- --arrangement-tools` | Pass: 21 drawing/range/grid/wave/native undo checks, plus design/localization guards |
| Temporary Avalonia.Headless harness against the real app | 48 checks passed; zero failures |
| Headless UI coverage | Tool exclusivity/exact tooltips; Pencil create/cancel/undo; Eraser deletion; all five automation shapes; track/return/master envelopes; first-lane one-step undo; return point erase; Select movement/resize and existing Alt behavior; ruler use with Warp selected; dark/light cursor regeneration/restoration; narrow widths |
| Visual inspection | Real rendered dark toolbar and light toolbar/waveform menu inspected; no new floating toolbar |
| `git diff --check` | Pass |
| Session baseline comparison | Existing local edits preserved; only intended hunks added to shared dirty files |

The full build passed before the final cursor/menu polish and audio-Erase test addition. The final app was rebuilt by the headless harness, and the focused smoke/design/localization checks passed again afterward. Existing compiler warnings remain; no build errors.

Temporary evidence (kept outside the source tree; no test package was added to the app or solution):

- `C:/Users/Dexter/AppData/Local/Temp/nota-tools-full-build.log`
- `C:/Users/Dexter/AppData/Local/Temp/nota-tools-tests.log`
- `C:/Users/Dexter/AppData/Local/Temp/nota-tools-ui.log`
- `C:/Users/Dexter/AppData/Local/Temp/nota-arrangement-ui-check/Program.cs` and `UiCheck.csproj` (Avalonia.Headless 12.1.4 validation-only harness)
- `C:/Users/Dexter/AppData/Local/Temp/nota-arrangement-tools.png`, `nota-arrangement-tools-light.png`, `nota-arrangement-tools-menu.png`
- `C:/Users/Dexter/AppData/Local/Temp/nota-arrangement-baseline-k_k5vfos/` (session-start snapshots)

## Files changed for this request

New source files:

- `src/managed/Nota.Application/Editing/ArrangementTools.cs` — tool/shape types, bounded grid sampling, waveform/interval math.
- `src/managed/Nota.App/ArrangementView.Tools.cs` — shared state, automation stroke lifecycle, cursor.
- `src/managed/Nota.App/ArrangementView.ToolClips.cs` — clip drawing preview and Eraser routing.
- `src/managed/Nota.App/MainWindow.ArrangementTools.cs` — exclusive toolbar selection and waveform settings menu.
- `tests/Nota.SmokeTest/ArrangementToolTests.cs` — drawing and native undo tests.

Modified existing files:

- `src/managed/Nota.App/MainWindow.axaml` — inline tool buttons and settings arrow.
- `src/managed/Nota.App/MainWindow.axaml.cs` — view-specific visibility/cancellation.
- `src/managed/Nota.App/ArrangementView.cs` — cancel previews before automation visibility/refresh changes.
- `src/managed/Nota.App/ArrangementView.AutoEditor.cs` — track/return tool routing and preview protection.
- `src/managed/Nota.App/ArrangementView.Controls.cs` — master/footer routing and cursor behavior.
- `src/managed/Nota.App/ArrangementView.LaneControl.cs` — clip handlers, preview render and gesture protection.
- `src/managed/Nota.App/Controls/Glyph.cs` — four missing native vector glyphs.
- `src/managed/Nota.App/PreferencesWindow.cs` — tool gesture reference via the Nota shortcuts workflow.
- `src/managed/Nota.App/Languages/fr.json` — new interface translations.
- `tests/Nota.SmokeTest/Program.cs` — register focused/full smoke checks.
- `CHANGELOG.md`, `FEATURES.md` — implemented capabilities and limitations.

Workflow artifacts live under `specs/074-arrangement-editing-tools/`; `.specify/feature.json` points at this feature. Unrelated existing work was not reverted. No dependencies, native engine/DSP code or ABI were changed.

## Convergence review

Reviewed FR-001–FR-008, SC-001–SC-004, ten story acceptance scenarios, edge cases, plan decisions and all eight constitution principles. Implementation/validation requirements are met except the external EN/RU manual sync under FR-008. No safety/boundary/dependency violations found. One partial documentation finding is recorded as T014 in `tasks.md`; no code work remains for the explicitly allowed fallback scope. Convergence is pending that external documentation item, not claimed complete.
