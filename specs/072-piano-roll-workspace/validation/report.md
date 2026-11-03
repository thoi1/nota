<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Piano roll workspace — implementation and verification

Date: 2026-11-02. Branch: `dev/windows-fixes`. All changes remain uncommitted and
nothing was pushed, following the user's latest instruction. Existing meter-refresh
and multiple-CC work is preserved.

## Result and root causes

- Arrangement MIDI activation previously opened only the split detail panel. It now
  uses the existing full-workspace layout, focuses the same roll after layout and shows
  **Back to Arrangement**. Repeated activation of the same MIDI selection reuses its roll.
- The detached editor owned a separate Auto-height device row. Hiding only main-window
  controls could not remove it. Workspace mode temporarily hides that existing window,
  reparents its editor using the existing cross-window-safe host helper and restores the
  same window, Devices visibility and editor. Pop-out also returns to that existing window.
- Docked Devices already had a close button. The detached device row now has one too;
  existing View commands reopen it. Selection-follow no longer reopens a manually hidden
  panel. Neither closing nor maximizing changes the audio graph or device instances.
- Velocity/MPE and extra CC lanes had fixed 72/112-pixel heights and a fixed overflow cap.
  Each lane now has a captured resize divider, a 48–480 logical-pixel range and a saved
  preference. Extra lane keys include lane slot and controller, so two views of the same
  CC remain independent. Short views retain room for notes and scroll extra lanes.
- Velocity rendering added 3 pixels to BeatToX and picking added 4, while notes used the
  exact onset. Stems and caps are now centered on BeatToX; picking uses that same position.
- Max horizontal zoom was 400 pixels/beat. It is now 3,200 (8x); minimum remains 6.
  Existing anchored transforms, scroll clamps and visible-grid enumeration are reused.

## Modified files for this request

UI and layout:

- `src/managed/Nota.App/MainWindow.MidiWorkspace.cs` (new snapshot/restore partial)
- `src/managed/Nota.App/MainWindow.Detail.cs`
- `src/managed/Nota.App/MainWindow.DetailWindow.cs`
- `src/managed/Nota.App/MainWindow.Menu.cs`
- `src/managed/Nota.App/MainWindow.axaml` (detail return label only)
- `src/managed/Nota.App/DetailWindow.cs`
- `src/managed/Nota.App/PianoRollView.cs`
- `src/managed/Nota.App/PianoRollView.ControllerLanes.cs` (extends existing pending CC work)
- `src/managed/Nota.App/PianoRollView.Controllers.cs`
- `src/managed/Nota.App/PianoRollView.Expression.cs`

Preferences, localization and documentation:

- `src/managed/Nota.Application/Settings.cs` (lane preferences only for this request)
- `src/managed/Nota.Application/Editing/MidiLaneHeight.cs` (new normalization helper)
- `src/managed/Nota.App/PreferencesWindow.cs` (updated gesture reference)
- `src/managed/Nota.App/Languages/fr.json`
- `CHANGELOG.md`, `FEATURES.md`
- `specs/072-piano-roll-workspace/` (specification, design, tasks, manual handoff and probes)

Regression tests:

- `tests/Nota.SmokeTest/MidiLaneHeightTests.cs` (new)
- `tests/Nota.SmokeTest/Program.cs` (targeted option and full-suite registration)
- `validation/WorkspaceProbe.csproj`, `validation/Program.cs`
- `validation/FrameProbe/` (full-workspace retained-drawing/cursor lifecycle probe)

## Validation

- Release managed build: PASS, zero errors; existing compiler warnings remain.
- Targeted `--midi-lane-heights`: PASS, including architecture/design/localization guards,
  old-settings defaults, invalid/boundary values and independent JSON persistence.
- Actual Windows UI probe: **180 checks PASS**. Covers docked/floating activation and
  restoration, focus, Mixer restoration, complete close/reopen, hidden-device sound,
  audio-clip split behavior, real pointer resize gestures, duplicate-controller independence,
  editor reopening, narrow/short windows, zero MIDI edit callbacks and zoom bounds/anchor.
- Velocity drawing/picking: exact recorded geometry across 6/400/1600/3200 pixels/beat,
  fractional horizontal scroll, fixed 1/32, 1/16 and 1/4 grids, and actual Avalonia layout
  transforms at 100/125/150/200 percent. Lane and note-grid origins coincide.
- Fresh process reads saved lane heights: PASS (122 / 157 / 197 logical pixels).
- Extreme zoom ruler: 200 renders in **29 ms** in the final UI run.
- Full-workspace cursor on the available display: **1195 updates in 1195 callbacks**,
  **239.0 updates/s**, median 4.16 ms, p95 4.35 ms; **zero static note-grid/ruler redraws**
  during measured cursor movement and **zero dropouts**. WASAPI, 480-frame buffer.
  Pause/seek/loop, floating/minimized ownership, suspension and shutdown checks pass.
- Logic Grey geometry/theme source SHA-256 unchanged:
  `5A2C36570BD8E70D241C01098DFBBE177F91069763DD6FF15B51C8BF1F938D7C`.
  Theme palette files have no diff; first 580 lines of MainWindow.axaml (menu and upper
  layout) match the pre-task snapshot exactly. Screenshots were inspected.
- Pending unrelated Arrangement/MeterBar/playhead/main-window polling/Mixer/Session files
  match their pre-task snapshots. No native source or audio ABI changes.
- `git diff --check`: PASS.
- Complete Windows build/smoke/scanner gate: **PASS**, exit 0, including native scanner self-test, managed build, `SMOKE TEST PASSED` and `SCAN SCALE PASSED`; see
  `validation/build-win-verified.log`. Earlier runs caught a duplicate existing French
  return label and a retired shortcut translation; both were corrected and the targeted
  localization guard subsequently passed.

Reproduce the full gate from an x64 Visual Studio developer shell:
`pwsh -NoProfile -File scripts/build-win.ps1`.
Build/run `validation/WorkspaceProbe.csproj` after the Release build; pass the validation
folder to the probe, then use `--read-lanes validation/probe-data` in a fresh process.
Run FrameProbe with `after validation/frame-results.json`.

## Limits

- Physical mixed-DPI multi-monitor moves and a separate 144 Hz screen were not tested.
  The available display exercised approximately 240 Hz; scaled Avalonia layouts were tested.
- `../nota-docs` is absent. Paired English/Russian manual changes are prepared in
  `manual-handoff.md`; the external documentation site was not built.
- Added CC lane views retain the existing explicit +/− workflow. Their saved heights
  return when the corresponding lanes/controllers are reopened.
- No commits were created. Suggested commit splitting is superseded by the user's
  instruction to leave **all** changes uncommitted.

