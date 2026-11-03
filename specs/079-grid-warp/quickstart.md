# Quickstart: Grid Warp

## Prerequisites

- Windows development environment with the repository's .NET 10 and native build prerequisites.
- A project containing an imported audio recording on an Arrangement audio track.

## Manual acceptance scenario

1. Open a freely performed piano recording in the Sample Editor and note the current tempo map and waveform.
2. Verify None, Audio follows grid, and Grid follows audio are mutually exclusive. Switch between them and confirm the waveform and tempo map do not change merely from switching.
3. In Grid follows audio mode, set threshold to 50% and minimum spacing to 30 ms; start analysis and confirm attacks are marked.
4. Hover a visible beat/bar line, drag it onto a piano attack, and confirm the preview displays the beat, transient, and resulting BPM. Hold Shift during another drag and confirm it is not transient-snapped.
5. Commit two or more valid mappings. Confirm the Tempo Track shows editable BPM points, and the Arrangement, Piano Roll, transport and metronome follow the new tempo map.
6. Compare the recording's original sample identity and absolute playback times before and after mapping; neither should change.
7. Add a fixed anchor, edit a different interval, then move/delete the anchor. Verify invalid crossings are rejected and a re-analysis leaves committed anchors alone.
8. Undo and redo each committed edit. Save, close, reopen, and confirm tempo points and anchors restore.
9. Select Audio follows grid and verify its existing marker workflow still stretches audio while leaving the tempo map fixed.

## Automated validation

- Run focused tests with the repository smoke-test runner (see `tests/Nota.SmokeTest/Program.cs` selectors and `BUILD.md`).
- Run `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\build-win.ps1`.
- Expected: mode-only transitions are side-effect-free; preview changes no committed state; valid commits update the map once; invalid commits are atomic; Grid follows audio preserves the original playback timing; Audio follows grid remains independent; focused UI/engine tests and the Windows build gate pass.

## Complete original-tempo workflow

Run labelled source-beat tests and current Grid Warp smoke/UI harness. Test a clip already
warped at a different project BPM: drag it into Tempo lane, inspect original-source proposals,
correct one, Cancel and compare project. Repeat and Apply; compare PCM and audio-warp markers,
verify Step points/source-time constraints, undo/redo, save/reload and source metadata.
Test Add anchor numeric edits and Reset after several drags; unrelated tempo and manual anchors
must remain untouched. Test fixed-anchor constraints, unsupported ramp seams, 3/4, offsets/trim,
source replacement during analysis and closing editor/project with work pending. Build Windows,
re-shoot real dark/light editor and document all 18 attachment checks honestly.
