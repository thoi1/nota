# Shared editor tools — implementation and verification

Date: 2026-11-02. Local work only; no commits, pushes, PRs, destructive Git operations or new repository dependencies.

## Architecture and behavior

- `EditingToolBar` is one native Avalonia control, used by the main Arrangement transport and Piano Roll. It uses existing `tp-icon` controls, shared 15px Glyphs, spacing and selected/hover styling. Waveform's dropdown sits directly beside its key, before Warp. Tooltips/accessibility include names and keys 1–5.
- `EditorToolState` owns one tool per editor. MainWindow retains its Piano Roll state across clip-editor reconstruction and reparenting; Arrangement retains its own state. One `SharedWaveformState.Shared` owns shape and amplitude/cycle/phase for both. UI subscriptions attach/detach with the toolbar, avoiding subscriptions from discarded clip views.
- Five `tool.*` commands are declared in `CommandCatalog`, bound through `CommandRegistry`, and dispatched by the existing main/detached transport-key tunnel. Bare top-row 1–5 select Select/Pencil/Eraser/Waveform Draw/Nota Warp in the focused editor, with a last-clicked fallback for custom canvases that leave focus at the window. Last editor context is retained for command-palette actions. Key-up and window loss release tool latches.
- Typing/TextBox/NumericUpDown ancestors, popup/value editors, dialogs, command palette and foreign/plugin input roots are excluded. A held digit begun in a text field cannot become a tool repeat after focus moves. Keypad and modified shortcuts are not remapped. There is no existing configurable-shortcut storage to override.
- Fresh 4 activates Waveform with its last shape; another distinct press while active cycles Sine → Triangle → Sawtooth → Square → Freehand → Sine. Dropdown changes use that same state. Four new Glyph paths provide Triangle/Sawtooth/Square/Freehand alongside the existing sine curve; both icons update immediately and scale through the existing 16-unit vector/stroke system.
- Arrangement retains the existing MIDI-clip Pencil, clip/point Eraser and track/return/master automation adapters. Drawing settings now come from the shared waveform state. Existing Select/ruler/navigation handlers remain.
- Piano Roll Pencil creates a local note preview using `DrawTarget`, `NewNoteLen`, remembered velocity, current grid and clip ownership/bounds. Release settles overlaps before its single commit; Escape/tool changes restore the original notes without an engine write. Eraser deletes clicked editable notes through the existing commit path. Original Select note movement/resizing/modifiers remain.
- Existing `ControllerLane` owns waveform previews in `_orig`/`_work` and commits once via `StoreController`. It reuses `ArrangementDrawing.WavePoints` and grid sampling; editor beats are mapped back into the active clip's local timeline, clamped to its length/value range, ordered and thinned. The same adapter serves existing extra CC lanes, including CC1/CC11. Freehand uses captured pointer samples and the shared grid sampler. Switch CCs retain normalized on/off changes.
- Waveform Draw over notes/Velocity/MPE lanes makes no unrelated edit and gives a CC-lane instruction. Lane tabs/pickers stay available while the tool is active. Locked/reference notes are protected. Existing controller and new note previews cancel on Escape, tool changes, reload or window loss as supported by their host.
- Shared `WarpRuler` provides muted red tint, readable WARP badge and hovered-grid feedback on each editor's selected ruler. Other tools restore the original appearance immediately. No fake warp anchors or sample-stretching calls were added.

## Known limits

- **Tempo editing is unavailable**: no authoritative tempo-map/Tempo Track engine supports ruler anchors with reference audio fixed in absolute time. Warp therefore provides truthful selection/ruler feedback only; it changes no tempo, audio, MIDI timing or pitch. Transient-assisted warp dragging is unavailable. Existing ruler navigation remains its existing seek/loop behavior.
- Arrangement still has no MIDI CC lanes. Piano Roll CCs use their existing infrastructure; no parallel CC system was invented.
- Existing Arrangement Alt-drag uses free positioning and has no duplication handler; no Alt + right-click erase handler exists there. Conditional preservation requests retain the actual existing behavior.
- Periodic shapes are sampled at the active grid and use existing lane interpolation. A coarse grid can undersample short cycles; choose finer resolution or a longer cycle. Tool/shape choices are session UI state, not new project settings. MIDI notes/controllers retain existing save/reload behavior.
- `../nota-docs` is absent. Website EN/RU edits, shortcut-page generation, site screenshots and site build/check could not run. Local changelog/features/Preferences/French translations are updated, with [ready-to-apply bilingual instructions](manual-update.md) prepared as the specified fallback.
- Windows was built and exercised here. macOS/Linux native cursor and capture timing were not executed.

## Build and test evidence

| Check | Result |
| --- | --- |
| `scripts/build-win.ps1` from VS x64 developer environment | Pass, exit 0: native engine, scanner self-test, Release solution build, full smoke and scan-scale check |
| Full smoke suite | `SMOKE TEST PASSED` |
| Scanner scale | `SCAN SCALE PASSED` |
| `dotnet run --project tests/Nota.SmokeTest -c Release -- --shared-editor-tools` | Pass: 33 shared-state/command/waveform/range/native undo checks, including the existing Arrangement tool tests; design/localization guards also pass |
| Real app/native-engine Avalonia.Headless harness | 88 checks pass, zero failures |
| Project persistence | Existing full-smoke MIDI controller save/load checks pass, including missing-controller compatibility; no new project format/settings writer |
| Visual inspection | Real dark Arrangement + Piano Roll toolbars, waveform curves/rulers and light detached editor inspected |
| Whitespace and local preservation | `git diff --check` passes; session-start snapshot comparison shows only intended source/doc hunks |

The harness loads the real App/DI services and engine, opens the actual clip-editor host, dispatches real routed mouse/key input, and uses the real detached editor. It covers toolbar equality, all number mappings, exact cycle/wrap/held-key behavior, dynamic icon/dropdown synchronization, typing/numeric/foreign/modal guards, held digits after focus changes, separate tools, Select move/resize/Alt positioning, note/CC creation/deletion/undo/cancellation, overlap settling in one undo step, reference protection, reverse CC sweeps, CC1/CC11 independence, clip-local mapping, incompatible note/velocity/MPE safety, lane-picker access, Warp data safety and a narrow detached window.

The full script passed before final input/overlap/picker refinements; the latest app was rebuilt by the headless harness and the focused smoke/design/localization run passed afterward. Compiler warnings are existing warnings; no build errors remain. No extra production/test dependency was added to the solution: Avalonia.Headless 12.1.4 was used only by a temporary external harness.

## Test build and temporary evidence

Builds used `BaseOutputPath=bin/SharedTools/` to keep the user's already-open previous test process available. Run the updated build at:

`C:/Dev/nota/src/managed/Nota.App/bin/SharedTools/Release/net10.0/Nota.App.exe`

Logs/harness/screenshots are outside the source tree:

- `C:/Users/Dexter/AppData/Local/Temp/nota-shared-full-build.log`
- `C:/Users/Dexter/AppData/Local/Temp/nota-shared-tests.log`
- `C:/Users/Dexter/AppData/Local/Temp/nota-shared-ui.log`
- `C:/Users/Dexter/AppData/Local/Temp/nota-shared-ui-check/Program.cs` and `UiCheck.csproj`
- `C:/Users/Dexter/AppData/Local/Temp/nota-shared-tools-dark.png`
- `C:/Users/Dexter/AppData/Local/Temp/nota-shared-tools-light-detached.png`
- `C:/Users/Dexter/AppData/Local/Temp/nota-shared-tools-baseline-rp9dynxq/` — session-start copies and changed-file list.

## Exact files created for this feature

- `src/managed/Nota.Application/Editing/EditorToolState.cs`
- `src/managed/Nota.App/Controls/EditingToolBar.cs`
- `src/managed/Nota.App/Controls/EditorToolCursors.cs`
- `src/managed/Nota.App/Controls/WarpRuler.cs`
- `src/managed/Nota.App/PianoRollView.Tools.cs`
- `tests/Nota.SmokeTest/SharedEditorToolTests.cs`
- `specs/075-shared-editor-tools/spec.md`
- `specs/075-shared-editor-tools/checklists/requirements.md`
- `specs/075-shared-editor-tools/plan.md`
- `specs/075-shared-editor-tools/research.md`
- `specs/075-shared-editor-tools/data-model.md`
- `specs/075-shared-editor-tools/contracts/ui.md`
- `specs/075-shared-editor-tools/quickstart.md`
- `specs/075-shared-editor-tools/tasks.md`
- `specs/075-shared-editor-tools/manual-update.md`
- `specs/075-shared-editor-tools/verification.md`

## Exact existing files modified in this session

- `src/managed/Nota.App/ArrangementView.Controls.cs`
- `src/managed/Nota.App/ArrangementView.cs`
- `src/managed/Nota.App/ArrangementView.Tools.cs`
- `src/managed/Nota.App/Controls/Glyph.cs`
- `src/managed/Nota.App/DetailWindow.cs`
- `src/managed/Nota.App/MainWindow.ArrangementTools.cs`
- `src/managed/Nota.App/MainWindow.axaml`
- `src/managed/Nota.App/MainWindow.axaml.cs`
- `src/managed/Nota.App/MainWindow.Commands.cs`
- `src/managed/Nota.App/MainWindow.Detail.cs`
- `src/managed/Nota.App/MainWindow.Input.cs`
- `src/managed/Nota.App/PianoRollView.Controllers.cs`
- `src/managed/Nota.App/PianoRollView.cs`
- `src/managed/Nota.App/PianoRollView.Expression.cs`
- `src/managed/Nota.App/PreferencesWindow.cs`
- `src/managed/Nota.App/Languages/fr.json`
- `src/managed/Nota.Application/Palette/CommandCatalog.cs`
- `tests/Nota.SmokeTest/LocalizationAllowList.txt` — search aliases marked as data, not translated interface copy.
- `tests/Nota.SmokeTest/Program.cs`
- `CHANGELOG.md`
- `FEATURES.md`
- `.specify/feature.json` — active workflow pointer.

Existing uncommitted work from earlier features was retained. No native engine/DSP/ABI source, user settings, project or release version was changed.

## Convergence

FR-001–FR-009, SC-001–SC-004 and twelve story acceptance criteria reviewed against current code and evidence, along with the plan/edge cases and all eight constitution principles. The requested safe missing-tempo-engine behavior and absent-docs-repo fallback are explicitly scoped and reported. No remaining implementation findings or safety/boundary/dependency violations; no convergence tasks required. **Converged** for this feature's specified scope, including its stated limitations.
