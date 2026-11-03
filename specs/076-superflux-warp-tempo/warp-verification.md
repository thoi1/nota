# Warp integration verification

Date: 2026-11-02. Windows x64, MSVC Release, .NET 10 / Avalonia 12.

US1 (native SuperFlux/service) and US2 (transient-guided audio warp) are implemented.
The full feature is **not converged**: beat inference and Grid follows audio still require
the shared variable-tempo Transport migration. No unfinished tempo-map controls are exposed.

## Test it

1. Open/import an audio clip in the Arrangement. With the Arrangement focused, press **5**
   (Nota Warp), then click the audio clip. The audio editor opens and analysis starts.
2. In the Sample inspector, **Transients** offers **Analyse transients**, **Show transients**,
   **Snap to transients**, sensitivity and minimum spacing. Defaults: 50%, 30 ms.
   Settings changes clear obsolete guides; press Analyse again for the new settings.
3. Enable **Audio follows grid** (or the existing Warp switch). Double-click near a thin
   transient guide to pin that source attack on the beat grid, then drag its triangle.
   **Snap selected anchor** adjusts only a selected interior anchor's source to the nearest
   detected attack while preserving its beat. Nothing quantises all detected attacks.
4. Disable transient snapping or hold Alt for manual placement. Esc during a drag restores
   the previous markers. Undo/Redo and project save/load use existing engine paths.

Analysis alone changes no audio or project tempo. Thin lines are guides, triangles are
confirmed anchors; the selected/nearest item uses the existing accent. Warp ruler feedback
is muted red. Source-relative results map through trims, placement, reverse, playback rate
and existing warp anchors. Attacks outside a clip's played window are hidden.

## Implementation and audio safety

- Extracted the existing ClipTimeMap into Application and ClipTempo interpolation into
  shared WarpPositionMap. The audio editor still uses those same formulas. TransientClipMap
  composes them with native clip length, which already includes playback rate and source
  rate conversion. No second tempo clock was added.
- TransientWorkspace shares UI settings and notifications per engine; analysis/caching
  remains in the existing background service. Capture happens on the UI/authoring thread,
  native FFT work stays on existing .NET workers, results/progress return through the UI
  dispatcher, and obsolete settings cancel their jobs. Detached editors/Arrangement views
  unsubscribe from workspace notifications.
- Arrangement caches played-fraction guide layouts per clip/snapshot, skips detections
  outside trim via binary search and draws at most one guide per three pixels. Playback
  ticks do not rebuild these layouts. Editor guides use existing live marker arrays and
  bounded visible strokes without creating transient geometry on pointer movement.
- Existing offline stretch worker, cache boundaries, marker commit, undo and persistence
  are reused. Replaced the editor's all-hit “To transients” action with selected-attack work.
  The legacy engine BeatWarp API remains for its existing consumers.
- Removed fixed 128-anchor reads from the editor, Arrangement mapping and project capture.
  ClipWarpMarkers queries the complete native count before reading; long marker edits no
  longer lose their later anchors when displayed or saved. A 141-anchor save/load test passes.
- Native marker updates reject non-finite, duplicate and crossing anchors before cloning
  the graph. Invalid edits do not add undo entries. The managed boundary checks paired
  array lengths and the touched C ABI catches exceptions. No render/callback code changed.

## Actual checks

- `scripts/build-win.ps1`: **passed, exit 0**. Includes native and managed/app builds,
  scanner self-test, complete smoke test and scan-scale check. **5,130 passing checks,
  zero failures** across the script's smoke invocations; existing compiler warnings remain.
- Focused transient suite: **61 assertions** per invocation, covering the original 43
  detector/service tests plus nine coordinate tests and nine native edit/persistence tests.
- Native reference CTest: **1/1 passed**. Original numerical reference and constant-scratch
  tests remain green; see [first-stage results](verification.md).
- New tests verify offset/trim/movement/rate/reverse/piecewise-warp round trips, hidden
  out-of-window attacks, immutable metadata, invalid maps, leading silence, selected-only
  anchors, Undo/Redo, rejected-edit history, source sample preservation and save/reopen.
- Existing docs headless harness builds and renders the **real app controls** at 2×.
  `npm run shots -- editing/audio-clip-editor editing/warp-shortcuts` passes in Studio Dark
  and Studio Light. The scene checks Analyse starts work, a real double-click pins the
  chosen attack on the grid, snap-off preserves manual placement, show-off hides guides,
  changed sensitivity removes obsolete results, selected-source snapping works, a drag
  highlights a nearby guide, anchors cannot cross and cancellation leaves native timing
  unchanged. These are assertions against the editor/engine, not drawn mock anchors.
- Additional French run at width 1360 passes the same UI assertions. Dark/light, French
  and filtered Shortcuts screenshots were visually inspected. Clipped Warp action labels
  found during this review were fixed by giving each button its own full-width row.
- UI screenshots: `../nota-docs/src/assets/shots/{dark,light}/editing/audio-clip-editor.webp`
  and `editing/warp-shortcuts.webp`; updated Preferences screenshots are also in that repo.
- `npm run shortcuts`: **17 groups / 200 shortcuts** generated for EN/RU from the current
  app list. Its parser now accepts Loc.T wrappers instead of silently missing these rows.
- Manual: `npm run build` **passed**, 213 rendered pages; `npm run check` **passed**,
  106 EN/RU page pairs, zero errors. There are **64 existing warnings** (63 stale source
  paths elsewhere and one unrelated pending coverage entry). The checker now handles
  Windows path separators and CRLF frontmatter, so those warnings are visible accurately.
- `git diff --check` passed in the app and docs repositories.

Local logs, raw screenshots and the French job are retained under
`src/native/nota.engine/build/superflux-verification/` and `../nota-docs/shots/.cache/`.
There is no commit, push, PR or publication.

## Documentation and licensing

Applied nota-design, nota-shortcuts, nota-features and nota-docs. Updated app feature log,
capability list, Settings shortcut descriptions and French translations. Cloned the missing
`../nota-docs` checkout locally and updated EN/RU audio-clips, Arrangement and generated
shortcuts pages, coverage and real-app shots. No new runtime dependency was introduced.
The SuperFlux BSD attribution and binary notice from US1 are preserved unchanged.

## Changed files in this increment

Application:
- `src/managed/Nota.Application/Editing/ClipTimeMap.cs` (moved from App/ClipEditor)
- `src/managed/Nota.Application/Editing/WarpPositionMap.cs`
- `src/managed/Nota.Application/Editing/ClipWarpMarkers.cs`
- `src/managed/Nota.Application/Analysis/TransientClipMap.cs`, `TransientGuideLayout.cs`

App:
- `src/managed/Nota.App/ClipEditor/ClipTempo.cs`
- `src/managed/Nota.App/AudioClipEditorView.cs`, `AudioClipEditorView.Transients.cs`
- `src/managed/Nota.App/TransientWorkspace.cs`
- `src/managed/Nota.App/ArrangementView.cs`, `ArrangementView.Vms.cs`,
  `ArrangementView.LaneControl.cs`, `ArrangementView.Tools.cs`, `ArrangementView.ToolClips.cs`,
  `ArrangementView.Transients.cs`
- `src/managed/Nota.App/PreferencesWindow.cs`, `Languages/fr.json`

Engine/boundary/tests:
- `src/native/nota.engine/src/engine/Engine_Tracks.cpp`
- `src/native/nota.engine/src/abi/nota_engine_track.cpp`
- `src/native/nota.engine/include/nota/nota_engine.h`
- `src/managed/Nota.Infrastructure/Interop/NotaEngine.Tracks.cs`
- `src/managed/Nota.Infrastructure/Persistence/AudioClipState.cs`
- `tests/Nota.SmokeTest/TransientMappingTests.cs`, `TransientAnalysisTests.cs`, `Program.cs`

Records: `CHANGELOG.md`, `FEATURES.md`, this report, `tasks.md`, `quickstart.md`,
`research.md` and the historical first-stage report link.

Sibling docs repository:
- `coverage.yml`, `scripts/shortcuts.mjs`, `scripts/check.mjs`
- `shots/app/Demo.cs`, `shots/app/Scenes.cs`, `shots/manifest.yml`
- `src/content/docs/{editing/audio-clips,views/arrangement,reference/shortcuts}.mdx`
  and their `ru/` twins
- Studio Dark/Light audio-editor, warp-shortcuts and Preferences shortcut image assets

Pre-existing editor work is preserved; the complete working-tree diff also contains work
that predates this feature.

## Remaining work

Separate beat inference, confidence/half-double ambiguity, first beat/meter, smoothing,
monotonic variable tempo map, coordinated transport/MIDI/automation/plugin timing migration,
tempo-map persistence/undo, Grid follows audio and preview/apply/reset controls remain open.
Annotated real piano evaluation and reference CPU benchmarks remain unclaimed. No macOS/Linux
build or real-device interactive playback evaluation was performed in this increment.
