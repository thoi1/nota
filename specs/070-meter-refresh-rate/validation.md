<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

# Meter refresh rate — implementation and measurements

Date: 2026-11-02. Settings → Appearance → Meter refresh rate offers exactly 30 Hz
(default) and 60 Hz (smoother meters). Applies on the next visible frame and persists
through the existing SettingsService. The primary transport, arrangement/return/master,
Mixer and Session level meters share the existing frame callback; no additional timer.

## Actual visual cadence

One generated, quiet, varying stereo audio track, normal main window and docked Mixer;
one second warmup, ten seconds measured per case. All five sampled MeterBar controls
(transport master, arrangement track/master, Mixer track/master) matched these rates.

| Case | Requested | Actual level/drawing updates/s | Level-gap p95 | Service ticks/s | Cursor changes/s | Reported dropouts |
|------|----------:|-------------------------------:|--------------:|----------------:|-----------------:|------------------:|
| Studio Dark / WASAPI 48k, 480 frames | 30 | 30.00 | 33.54 ms | 29.98 | 238.7 | 0 |
| Studio Dark / WASAPI 48k, 480 frames | 60 | 59.80 | 17.06 ms | 29.12 | 236.3 | 0 |
| Logic Grey / Yamaha Steinberg USB ASIO 48k, 64 frames | 30 | 30.00 | 34.02 ms | 28.12 | 230.1 | 0 |
| Logic Grey / Yamaha Steinberg USB ASIO 48k, 64 frames | 60 | 59.90 | 16.95 ms | 29.96 | 238.4 | 0 |

Raw JSON: validation/studio-30.json, studio-60.json, grey-30.json, grey-60.json.
Composition draw-list replacements were counted independently from level field changes;
counts were 300/597/300/599 per meter during the respective ten-second samples. This
measures actual UI updates/drawing commands, not physical compositor presentation.

## Rendering and process overhead

| Case | Process CPU over 10s | Total allocated bytes (MB) | Record five meter drawings, mean |
|------|--------------------:|--------------------------:|--------------------------------:|
| Studio 30 | 5875.00 ms | 100.69 | 45.90 µs |
| Studio 60 | 4562.50 ms | 107.00 | 36.16 µs |
| Grey 30 | 6781.25 ms | 131.94 | 33.03 µs |
| Grey 60 | 5187.50 ms | 102.58 | 51.43 µs |

Process CPU/allocation includes the engine, whole UI, renderer and diagnostic sampling;
these single sequential samples vary and do not isolate a total-CPU regression percentage.
The drawing microbenchmark records the five real meter controls into 1000 DrawingGroups
after cadence sampling; it includes command recording/allocation and excludes GPU scanout.
The direct observed overhead is approximately twice as many meter drawing updates at 60.
The recorded drawing cost spans roughly 1–3 ms per second at these target rates, but is
not an end-to-end display/compositor cost estimate. No ballistics/animation were added.

## Behavior and regression checks

- Real Appearance ComboBox has exactly two choices and applies both via the real settings.
- Separate new processes restore 30 and 60 from their respective scratch directories.
- Deterministic smoke checks pass: default/invalid/missing preferences, JSON roundtrips,
  exact phase-preserving 30/60 counts, immediate rate switches and no catch-up bursts.
- All four actual-App runs pass: hidden Mixer levels stay unchanged; minimized main
  meters stay unchanged; floating Mixer owns frame delivery; export suspension/all
  minimized windows stop callbacks; restore resumes; visible stopped meters retain updates.
- Separate actual piano-roll/arrangement regression: 1186 changes each / 1186 callbacks,
  237.21/s, zero note-grid/ruler draw-list replacements, zero dropouts. Seek, loop,
  floating editor, suspension and close checks pass. A paused detail window without
  primary level meters correctly stops its loop; a visible Mixer retains live-input updates.
- The 33ms MainWindowViewModel service clock, ProcessCpuSampler 500ms cache, 1s CPU tooltip
  throttle, DSP sampling, native engine/audio callbacks and transport interpolation are
  unchanged. No RAM sampler or new Performance Monitor was added (user clarified audio meters).
- Device-specific analyzers/output rails retain their existing telemetry cadence. The
  numerical frequency probe covers primary MeterBars; Session's split update routing is
  covered by source review and the full smoke suite, not a separate Session cadence sample.

## Build and reproduction

Windows x64 Release app build passes (existing unrelated warnings). Deterministic tests:
`dotnet tests/Nota.SmokeTest/bin/Release/net10.0/Nota.SmokeTest.dll --meter-refresh`.
Final `scripts/build-win.ps1` in the installed VS x64 developer environment: exit 0,
`SMOKE TEST PASSED` and `SCAN SCALE PASSED`; scanner self-test and the original overloaded
render-pool guards pass. Log: validation/build-win.log. The earlier spec 071 run's two
timing failures are retained in its report; no limits or engine code were changed.
Appearance captures in both themes: validation/appearance-studio.png and appearance-grey.png.

Build validation/MeterProbe.csproj after Release. Invocation:

```powershell
dotnet specs/070-meter-refresh-rate/validation/bin/Debug/net10.0/MeterProbe.dll <results.json> "" 30 studio-dark
dotnet specs/070-meter-refresh-rate/validation/bin/Debug/net10.0/MeterProbe.dll <results.json> "Yamaha Steinberg USB ASIO" 60 logic-grey
dotnet specs/070-meter-refresh-rate/validation/bin/Debug/net10.0/MeterProbe.dll <same-results.json> restart 60
```

Scratch data lives beside results under probe-data/. The probe uses existing Release
assemblies and adds no dependencies. UI-side probe fixes included following rebuilt
startup controls and using visual-tree lookup (Preferences has no parent NameScope).
An initial style check caught regular spaces before Hz; native thin spaces fixed it.
An initial playhead probe expected meter work in a detail window without any primary
level meters; the corrected expectation matches intentional hidden-meter suppression.

## Feature file list

- src/managed/Nota.Application/Settings.cs and new MeterRefreshRate.cs.
- src/managed/Nota.Presentation/ViewModels/MeterRefreshCadence.cs (new).
- src/managed/Nota.App/PreferencesWindow.cs, MainWindow.Playhead.cs, MainWindow.axaml.cs.
- src/managed/Nota.App/Controls/MeterBar.cs (visibility predicate only).
- src/managed/Nota.App/ArrangementView.cs, MixerView.cs, SessionView.cs, SessionView.Mixer.cs.
- src/managed/Nota.App/Languages/fr.json; CHANGELOG.md; FEATURES.md.
- tests/Nota.SmokeTest/MeterRefreshTests.cs (new) and Program.cs.
- specs/070-meter-refresh-rate/: spec, plan, research, data model, contracts, tasks,
  quickstart, checklist, manual handoff, validation report/.gitignore and validation
  probe sources plus small JSON evidence. Local PNGs/logs and scratch data are ignored.
- .specify/feature.json is the local active-feature pointer.

The separate multiple-CC-lane changes are listed in spec 071. The absent sibling manual
repository is handled by manual-handoff.md. No commit/push or external application writes.
