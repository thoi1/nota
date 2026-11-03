<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

# Grid Warp completion verification

Date: 2026-11-03. Platform: Windows x64, .NET 10, Avalonia, native Release build.

The manual Grid Warp and original-source extraction workflows are implemented in the
existing Sample Editor, native TempoMap and Tempo Track. The rebuilt desktop app was
opened successfully (`Nota — Untitled`, responding). A real freely performed piano recording
was not supplied or auditioned; the 18-step acceptance sequence below distinguishes test
coverage from listening and physical-device verification.

## Traced cause and repairs

The earlier Grid follows audio control fitted a map without a separate musical-grid
handle/gesture workflow. The existing 079 foundation had already added that workflow before
this completion pass. Its actual path is Sample Editor pointer press → BeginGridWarpGesture
→ local pointer preview → release → validated Grid Warp transaction; capture loss/Escape
cancel it before ordinary Audio Warp operations. The missing-overlay/handler diagnosis does
not describe the current completed code.

The completion audit found concrete remaining faults:

- `Engine_Tempo.cpp::refreshTempoReferences` rewrote musical coordinates in the reference's
  audio warp markers. It now keeps independent source-window/absolute-origin geometry and
  leaves those markers untouched. Clip beat position is derived from its fixed absolute origin.
- The editor used raw region bounds while native playback could use trimmed warped bounds.
  The editor, transient guides, waveform peaks and playback now share authoritative geometry.
- Stopped-playhead hover handling could intercept a grid handle. Grid handles now receive
  their own hover/hit path first, including a visible hover affordance.
- Tick matching did not give anchors durable identity or explicit Tempo Track links.
  Anchors now have stable IDs and tempo points explicit IDs in existing reserved ABI slots.
  A final convergence regression exposed deleted-ID reuse; the monotonic authoring allocator
  fixes it, including baseline restore/import.
- Automatic pulse proposals, original-tempo retention, clip-to-Tempo drop, discoverable Add
  anchor/numeric editing and region Reset were incomplete. They now use existing analysis,
  editor, graph undo and persistence services.
- Region insertion could change neighboring ramp curves. Unsupported non-Step seams now
  reject atomically with an explanation rather than altering unrelated curve shapes.

## Delivered behavior and main files

| Area | Files and behavior |
| --- | --- |
| Editor interaction | `src/managed/Nota.App/AudioClipEditorView{,.GridWarp,.Transients,.BeatExtraction}.cs`: exclusive tool-only mode selection, orange handles/fixed anchors, transient snapping, Shift bypass, pointer/numeric edits, Add/Delete, Reset/Undo and local proposals |
| Tempo drop | `ArrangementView.LaneControl.cs`, `ArrangementView.Tempo.cs`, `TempoTrackControl.cs`, `MainWindow.axaml.cs`: captured Arrangement clip drop targets Tempo before clip movement, restores preview positions and opens the existing editor |
| Original pulse analysis | `Nota.Application/Analysis/SourceBeatInference.cs`, `ITransientAnalysisService.cs`, `Nota.Infrastructure/Analysis/TransientAnalysisService{,.Beats}.cs`: original-source attack evidence, bounded pulse search, tempo continuity, subdivisions/missing beats, fixed constraints, cancellation and editable confidence |
| Source retention | `MainWindow.AudioImport.cs`, `Nota.Infrastructure/Persistence/{SourceTempoCache,AudioClipState,ProjectModel,ProjectService}.cs` and Session restore: original BPM seed and content-addressed original-source results survive project tempo edits/reload; derived analysis is excluded from the authoring manifest |
| Native tempo/reference | `src/native/nota.engine/src/engine/{Engine.h,Track.h,TempoMap.h,TempoMap.cpp,Engine_Tempo.cpp,Engine_Tracks.cpp,Engine_GridReset.cpp}`: exact Step intervals, separate natural source geometry, atomic constraints, stable identity and graph-owned region baseline |
| Boundary/persistence | `include/nota/nota_engine.h`, `src/abi/nota_engine_core.cpp`, native CMake list, managed `Engine/IAudioEngine.cs`, tempo records and Interop tempo partials: pure C ABI geometry/reset functions and additive saved clip metadata; existing 24/48-byte point/anchor ABI sizes retained |
| Tempo editing | `Nota.Application/Editing/GridWarpMapping.cs`, Tempo Track and clipboard: linked move/delete is atomic; conflicting BPM changes reject; pasted tempo shape does not copy source constraints |
| Verification/docs | `tests/Nota.SmokeTest/{GridWarpTests,SourceBeatTests}.cs`, existing tempo/transient suites, `specs/079-grid-warp/ui/`; FEATURES, CHANGELOG, ARCHITECTURE, app shortcuts/fr strings and sibling EN/RU user manual/screenshots |

Tempo is calculated as `60 × deltaQuarterBeats / deltaSeconds`, without rounding stored
BPM. The native TempoMap/Transport remains the only project clock. Grid Warp reference audio
uses natural source playback; other tracks use their established musical timebase. Analysis
runs through the existing two-worker limit, never in the audio callback. Pointer previews do
not publish maps or build stretch caches. Commit/Apply/Reset each publish one undoable graph.

The existing SuperFlux detector supplies attacks, not assumed beats. Threshold is 0–100%
(default 50%); minimum spacing is 5–250 ms (default 30 ms). Debounced obsolete results are
cancelled/ignored. Snap all beats adds starting/min/max BPM and beat sensitivity controls.
Proposals remain separate from committed anchors and are correctable before Apply.

## Verification results

| Check | Result / evidence |
| --- | --- |
| Native CTest | 2/2 pass: tempo and SuperFlux reference tests; `src/native/nota.engine/build/Testing/Temporary/LastTest.log` |
| Focused Grid Warp + source inference | 45 assertions pass, zero failures; `grid-completion-focused.log` in the native build directory |
| Actual Avalonia interaction harness | Six grouped checks pass, including captured clip drop, actual proposal pointer drag with Shift, numeric correction, Cancel/Apply, Add/Reset, manual grid gestures, zoom/scroll, mode isolation and slider debounce; `grid-completion-ui-final.log` |
| Full Windows gate | `scripts/build-win.ps1` exit 0, native + managed Release, SMOKE TEST PASSED, SCAN SCALE PASSED; 4,979 main smoke assertions, 5,344 PASS lines across the complete gate, zero FAIL; `grid-completion-windows-final.log` |
| Desktop binary | App opened and responding; app-bin/native-build DLL SHA-256 both `5225fb0e1b8c9400b87a5589d50d2d3c80dec9b090fe7df50872156419828193` |
| Documentation | EN/RU pages and shortcuts regenerated; three actual-app screenshot scenes in both themes pass; site builds 213 pages; source check 106 pages, zero errors, 64 existing warnings |
| Hygiene | `git diff --check` passes; new production sources have SPDX headers; no production dependency added |

Logs named above are generated local build evidence. Committed evidence includes
[screenshots](screenshots/) and the reproducible [UI harness](ui/Program.cs).
The harness emits obsolete Bitmap.Save API warnings; these do not fail the checks.

Numerical evidence:

- Trimmed source at nonzero origin maps adjacent intervals to **70.588235 / 77.922078 BPM**;
  anchor times are origin + 0.85 / 1.62 seconds, with unchanged source markers and placement.
- Three seconds of original reference playback, including reversed playback, remain
  sample-identical across Step boundaries. Source PCM and source/beat warp arrays match.
- Constant, accelerando, rubato, subdivision and missing-attack fixtures have 0 ms maximum
  labelled alignment error; ritardando has 10.7 ms (the criterion is 30 ms).
- Real SuperFlux analysis of a deterministic audio fixture gives identical source-beat results
  before/after import-style auto-warp and project BPM changes to 155.
- Original BPM 97.5 and source-beat results survive sidecar/project reload without preview
  dirtying the authoring manifest. Geometry, explicit IDs and Reset baseline survive disk reload.
- Existing clock gate crosses/loops Step events at 44.1/48/96 kHz without accumulated drift.
  Actual MIDI onset tests at nonzero clip origin are within one sample; export uses the map,
  including a nonzero-origin 48 kHz export with stale scalar request BPM. Variable-tempo
  freeze/live maximum difference is 1.29e-13. A 10,000-point native transaction measured 0.15 ms
  in this run; this is a local observation, not a general latency guarantee.

## The attachment's 18 acceptance steps

All rows use deterministic audio through real engine/editor code. None substitutes for
listening to the user's freely performed recording.

| Step | Coverage/result |
| --- | --- |
| 1. Import piano recording | Actual MainWindow audio fixture import/drop path tested; real piano file pending |
| 2. Open Sample Editor | Actual existing editor opened in the MainWindow harness and desktop app available |
| 3. Enable Grid follows audio | Toggle/mode transitions pass without project mutation |
| 4. See orange beat handles | Render/hit tests and dark/light screenshots pass |
| 5. Drag Beat 2 to attack | Actual musical-line drag and transient snap exercised; freely performed attack placement remains a user check |
| 6. Waveform stays stationary | Source samples, source warp arrays and absolute rendered playback comparisons pass |
| 7. Real BPM event appears | Native map and existing Tempo Track updated through the real transaction |
| 8. Drag another beat | Repeated drag/fixed-neighbor handling and local-preview/commit checks pass |
| 9. Adjacent intervals recalculate | Exact interval formula, fixed neighbors, numeric example and invalid/crossing cases pass |
| 10. Tempo Track displays Step events | Native Step assertions and actual Tempo Track/editor screenshots pass |
| 11. Grid follows performance | Fixture anchors agree with authoritative map; real recording/listening remains pending |
| 12. Undo/redo movement | One-action manual/Apply/Reset/linked-point undo/redo passes |
| 13. Save/reopen | Actual project bundle save/load tested |
| 14. Anchors/map persist | Geometry, IDs, point links, map and Reset baseline reload comparisons pass |
| 15. Snap All Beats preview | Actual original-source analysis and preview tested before/after auto-warp/BPM edits |
| 16. Correct proposals | Actual pointer drag with Shift plus numeric correction stay local; deletion route available |
| 17. Apply proposed mapping | Real Step map/anchors in one undo action; source markers unchanged |
| 18. Playback/MIDI sync | Automated renderer, MIDI/controller, transport loop, freeze/export clocks pass; audible metronome, external MIDI and third-party plug-in audition pending |

## Supported limits and remaining manual checks

- Automatic pulse inference is an editable heuristic, not a guarantee for arbitrary piano.
  Starting BPM helps choose half/double tempo. Weak, sparse, unmetered or long-silent material
  may need trimming, manual anchors and corrected proposals; insufficient evidence rejects.
- Grid Warp requires neutral clip pitch to preserve original playback speed. It rejects
  pitched references instead of silently changing pitch. Reverse and trimmed source geometry
  are supported and tested. The tool disables pitch/reverse edits while it is selected.
- Inserting/resetting a region across unsupported non-Step seams rejects safely. Existing
  ramp playback remains supported; automatic ramp-fitting is outside this Step workflow.
- Reset rejects protected external-anchor conflicts and an individual fragment of a shared
  split reference. It does not erase sibling mappings or unrelated tempo automation.
- Existing global time signature and denominator-based pulses are used (including 6/8 tests).
  Nota currently has no time-signature-automation model; no new meter track was added.
- Drop extraction targets an imported Arrangement audio clip, retaining its source location;
  direct sample-browser-to-Tempo import is not part of this workflow.
- Physical audio-device listening, real piano accuracy, audible metronome/MIDI alignment,
  external hardware and individual VST3 hosts were not auditioned. CLAP hosting is not added.
- No desktop-wide screenshot was retained. UI evidence uses the actual app under Avalonia
  headless rendering; the ordinary Windows app was separately launched and checked.

## Try a freely performed piano recording

1. Import the original recording into Arrangement. Keep an untouched copy. Open its Sample
   Editor and choose **Grid follows audio**. Selecting this tool alone makes no audio/map edit.
2. For manual mapping, align successive musical handles with the intended beat attacks.
   Inspect the live BPM; use the threshold/spacing sliders, Shift to bypass transient snap,
   Add anchor and selected Beat/Time fields. Check the expanded Tempo Track after committing.
3. For extraction, drag the Arrangement clip onto the Tempo Track, or choose **Snap all beats**
   in Grid mode. In **Beat settings**, choose a plausible starting BPM and suitable min/max.
   Inspect the distinct proposed beats; correct misplaced beats before choosing **Apply**.
   Cancel should leave the original map/undo state intact.
4. Listen from before the mapped region with the metronome and a simple MIDI beat track.
   Check early/late beats and pauses. The recording should retain its absolute placement,
   natural speed and attack times while the musical grid/MIDI follows the chosen beats.
5. Undo/redo one anchor move and Apply. Save, close/reopen, compare anchors/Tempo Track,
   then try Reset grid and undo it. For unsupported seams or protected references, read the
   rejection message and use a simpler Step region or explicitly edit the constraints.

## Convergence

Final read-only review covered 24 functional requirements, 9 success criteria, 6 stories
(28 acceptance scenarios), the eight architecture decisions and all eight constitution
principles. T053 closed the identified stable-ID gap with a failing-before/passing-after
regression. No further buildable gap was found in this specified scope. Real-piano and
physical-device acceptance remains explicitly unverified above. The convergence pass leaves
existing tasks unchanged; completion checkboxes are recorded by the implementation workflow.


## Follow-up: anchor interaction (2026-11-03)

A subsequent user report exposed interactions the earlier harness did not cover: the upper
musical ruler with real Seek routing, first Add on a nonzero-origin clip, the selected Grid
anchor's transient-snap button, and the first Grid edit on auto-warped audio. The four new
reproductions fail before the correction and pass afterward. The earlier success claims
apply to the narrower fixtures documented above, not those missing cases. See
[the bug assessment](../../.specify/bugs/grid-anchor-interaction/assessment.md),
[fix report](../../.specify/bugs/grid-anchor-interaction/fix.md) and
[verification](../../.specify/bugs/grid-anchor-interaction/test.md).

### Continuation validation (2026-11-03)

Re-ran the complete actual-editor harness: all ten reported groups pass, including all four
anchor-interaction reproductions. Re-ran `--grid-warp`: all 45 engine/source-beat assertions
pass. Reviewed the post-fix full Windows gate log (5,344 PASS lines, smoke/scanner passed);
that gate was not repeated because this continuation required no production-code changes.

The sibling manual already contains the repaired EN/RU workflow. Regenerated its 212
shortcuts, created the missing `editing/grid-warp-shortcuts` screenshot, and re-shot it plus
`editing/grid-warp-fixed`, `editing/grid-warp-preview` and `editing/warp-shortcuts` in both
themes. Visually checked the Grid Warp shortcut rows in dark/light. Documentation builds
213 pages; checks report 106 source pages, zero errors and 64 existing warnings. App/docs
whitespace checks pass. The completed bug verification is linked above; real-piano listening
and hardware limitations remain as documented in this report.
