<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

# Multiple CC lanes — validation

Date: 2026-11-02. + under the piano roll adds independently selected controller lanes;
each lane's picker reuses the full existing controller list, and - hides only the view.
The velocity/MPE/primary-controller lane remains. Extra lanes use a bounded scroll area
that leaves notes visible in short editors. Layout is editor-local, not project-persisted.

## Checks

Actual Avalonia control diagnostic: 12 checks pass, exit 0; raw validation/cc-results.json.
Three distinct controller lanes, including Sustain's switch blocks; minus/re-add preserves
all CC points and makes no commit; one existing commit targets only the selected controller;
reload cancels old gesture work and retains lane layout; ten lanes scroll within 224px;
short (300px) editor retains a note viewport; every gutter tracks the note-grid column;
theme/zoom/view changes make no data writes. Studio Dark and Logic Grey PNGs are local.
The commit-routing check exercises the existing callback boundary with generated data;
it does not claim automated physical pointer drawing or an end-to-end engine undo gesture.
Existing controller engine tests run in the smoke suite.

Actual capture inspected: multiple visible controller curves/sustain blocks, native glyph
add/remove controls, readable full controller label on the lane and shared horizontal axis.
Screenshots: validation/studio-dark-cc.png, validation/logic-grey-cc.png.

Release app builds with 0 errors and existing unrelated warnings. The first Windows full
run passed functional/design/localization checks but failed two timing-sensitive overloaded
render-pool guards (71/4125 late blocks; burst 6 versus limit 4). No guard was weakened and
no engine code changed. The shared final Windows run for specs 070/071 is recorded below
when complete. git diff --check passes.

Final combined Windows x64 run: **PASS**, exit 0; `SMOKE TEST PASSED`, `SCAN SCALE PASSED`,
scanner self-test and the unchanged overload timing guards pass. Evidence:
../070-meter-refresh-rate/validation/build-win.log. No source changes after this passing
build other than documentation/diagnostic capture refinements.

## Implementation notes

ControllerLane has an optional instance controller ID, retaining the original primary
lane fallback. Shared caches and existing StoreController undo boundary are reused.
Pending gestures/pointer capture are canceled on reload/removal/selection changes;
delayed Clear/picker callbacks are guarded against changed clips/removed lanes.
Source review verifies this routing; the diagnostic directly tests reload cancellation.

An initial diagnostic closed the app before queued theme-change work drained; shutdown
was moved to a background dispatcher job, after which the probe exits cleanly. No app
change was made for that diagnostic-only shutdown ordering.

## Files

- src/managed/Nota.App/PianoRollView.ControllerLanes.cs (new toolbar, stack, independent lanes).
- src/managed/Nota.App/PianoRollView.Controllers.cs (picker routing, identity, cache/gesture guards).
- src/managed/Nota.App/PianoRollView.cs (rows and invalidation).
- src/managed/Nota.App/PianoRollView.Expression.cs (recompute available extra-lane height).
- src/managed/Nota.App/Languages/fr.json; CHANGELOG.md; FEATURES.md.
- specs/071-multiple-cc-lanes/: spec/checklist/plan/research/data model/contract/quickstart/
  tasks/manual handoff/validation/.gitignore; validation/CCProbe.csproj, Program.cs and
  cc-results.json. Images/logs/scratch data are local ignored artifacts.

Reproduce after Release: build validation/CCProbe.csproj, then run
`dotnet specs/071-multiple-cc-lanes/validation/bin/Debug/net10.0/CCProbe.dll specs/071-multiple-cc-lanes/validation`.
The sibling manual repository is absent; EN/RU guidance is in manual-handoff.md.
No new keyboard bindings, dependencies, native processing, MIDI data format or playhead
code changes were made by this CC feature. Meter scheduling is a separate spec 070 change.
