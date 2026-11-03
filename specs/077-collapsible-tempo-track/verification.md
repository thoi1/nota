# Tempo track verification — Windows x64

Date: 2026-11-03. Feature 077; existing 076 warp/transient work and tempo-change-lag
fix preserved. No commit, push or release performed.

## Implemented structure

- Native `engine/TempoMap.{h,cpp}`, `Engine_Tempo.cpp`, `Transport`, `Graph` and
  `nota_engine.h`/ABI: one immutable musical-time map, points at 960 ticks/quarter,
  outgoing Step/Linear/Smooth, bounds, inverse queries and atomic protected-anchor edits.
  Step/Linear integrate analytically during preparation; Smooth uses bounded quadrature.
  Playback reads prepared monotone knots (<1/8 sample interpolation error at 192 kHz),
  slices at timing boundaries and does no map integration, owning atomic, allocation,
  locking or source analysis. UI queries retain the same map through a nonaudio snapshot.
- Render/Tracks/Recording/Takes/Session/Modulation: mapped event samples and loop positions,
  capture coordinates, cache duration/keys/markers, source lengths, freeze capture metadata,
  plugin timing including master. Natural Session playback retains a source-seconds cursor;
  its musical phase still comes from Transport. Existing warp worker/cancellation is reused.
- Application tempo port, Infrastructure interop/persistence/export/freezer and Presentation
  transport display: native timing is authoritative. Format 30 stores maps and source reference
  IDs; format 29 migrates without changing its scalar tempo. Captured freezes include their map.
- App `TempoTrackControl`, `ArrangementView.Tempo`, main-window focus/tempo/cache integration
  and audio editor workflow: pinned compact/expanded lane, resize, numeric inspector, bounds,
  selection/box/fine/group edits, clipboard, keyboard, outgoing interpolation and explicit
  unlock. Pointer preview is local, with one native edit on release. Top BPM drags latch their
  insertion beat and undo as one gesture while preserving other points.
- Grid follows audio fits explicit source-frame/beat anchors and preserves natural source
  timing. Audio follows grid prepares stretched Arrangement audio. Protected absolute pairs
  reject conflicts atomically. Musical notes/automation retain beats. Durable reference IDs
  survive reindex/split; copies detach. A fixed reference must leave Grid follows audio before
  it can move musically. No source sample data is rewritten.

## Requested scenarios

| # | Scenario | Evidence and result |
|---|---|---|
| 1 | Constant tempo | PASS native two-hour exact map, pre-command scalar compatibility and existing smoke scenarios. |
| 2 | Multiple steps | PASS accumulated native time, actual MIDI onset and transport at 44.1/48/96 kHz. |
| 3 | Linear accelerando | PASS analytical duration, prepared error bound, actual MIDI/controller samples, forward/reverse warped pitch. |
| 4 | Smooth ritardando | PASS monotone inverse over two hours, midpoint BPM, actual MIDI/controller samples. |
| 5 | Insert points | PASS actual MainWindow double-click adds a snapped point; initial zero remains protected. |
| 6 | Move points | PASS real pointer drag changes tick/BPM in one undo; Shift fine, Escape and numeric edit command. |
| 7 | Delete points | PASS actual Alt-click deletion; attempted initial-point deletion retains zero. |
| 8 | Multi-point editing | PASS actual box/group edit, fine key nudge and system clipboard copy/paste at playhead. |
| 9 | Undo/redo | PASS graph restoration, one pointer gesture, reference-anchor transaction and moving-playhead top BPM gesture. |
| 10 | Save/reload | PASS actual bundle disk round-trip with reference IDs and remapped track IDs; lane view sidecar reload; serialized freeze audio identity. |
| 11 | Warp Grid sync | PASS explicit source fit updates the same map; reference edit/undo/reindex/split preserve relationship/source samples. |
| 12 | Locked anchors | PASS conflicting tempo/warp edits reject without map revision, partial publication or undo entry; explicit protection/unlock. |
| 13 | MIDI timing | PASS actual sampler note onset and controller state transition within one sample through Step/Linear/Smooth at nonzero clip origins. Input-ring recording/replay and recorded natural duration are tested; physical MIDI device latency is unverified. |
| 14 | Looping | PASS step seams at 44.1/48/96 kHz and repeated presentation-window elapsed-time wrapping across a ramp. |
| 15 | Plugin tempo sync | PASS native TransportInfo PPQ/seconds/local-slope BPM contract; reviewed VST3/AU adapter and instrument/track/master delivery paths. An installed plugin audition was not performed. CLAP hosting is absent in this repository. |
| 16 | Long/dense projects | PASS two-hour inverse/monotonicity, native 10,000-point map and actual 10,000-point lane rendering. Step-map transaction measured 0.18 ms; lane reload/render 31.9 ms including a deliberate 30 ms dispatcher pump (one local run, not a hardware guarantee). |
| 17 | Extreme zoom | PASS actual lane renders at min/max horizontal zoom without losing map points. |
| 18 | Collapse/expand | PASS actual compact default, expanded height, visibility/height persistence and Studio Dark/Light screenshots. |

Additional tests compare variable-tempo freeze/live samples (max difference 1.29e-13),
serialized freeze identity, nonzero-origin 48 kHz WAV export frame count against native
endpoint times (request BPM deliberately stale), and warped 440 Hz sine pitch/bounds in
both directions (440/440 Hz in early/late windows, boundaries within one sample).

The convergence audit found an unwarped Session source-position error: the discriminating
comparison failed with max sample difference 1.13 before correction and passes with
8.77e-12 afterward. It is tracked/completed as T025. Musical source phase/envelopes and loop
boundaries remain map-based; natural source audio progresses in seconds.

## Reproduction and gates

- Native: `cmake --build src/native/nota.engine/build`, then
  `ctest --test-dir src/native/nota.engine/build --output-on-failure`: 2/2 pass,
  including 11 native tempo assertions and the existing transient self-test.
- Focused managed: `dotnet .../Nota.SmokeTest.dll --tempo-map`: 48/48 current checks pass;
  see ignored build log `tempo-map-tests.log` for exact observations.
- Actual application headless harness:
  `dotnet run --project specs/077-collapsible-tempo-track/ui -c Release`:
  12/12 gesture/layout/theme/dense-map checks pass. Screenshots under `screenshots/` include
  expanded, compact, Studio Light and the actual Tempo track shortcut group in Preferences.
- Windows full gate: `scripts/build-win.ps1` exits 0. Native and solution builds, scanner
  self-test, 4,932 full smoke assertions and plugin scan scale pass (`tempo-windows-gate-final.log`).
  Overload gate passes unchanged: 31/4,125 late blocks (<1%), maximum burst 3 (limit 4).
  Interactive tempo setter 0.341 ms; obsolete-worker cancellation 0.001 ms in this run.
  An earlier full run passed functional tests but failed the existing render-pool timing
  gate (46/4125 late blocks, limit 1%). No timing limit or safety check was weakened.
- EN/RU manual: updated transport, Arrangement, audio clip workflow and generated shortcuts;
  real expanded lane, Arrangement, audio editor and Shortcuts screenshots retaken in both
  themes. `npm run build`: 213 pages pass. `npm run check`: 106 pages, zero errors,
  64 existing warnings (mostly moved source paths plus one older pending documentation item).
- Actual Windows executable launched from `src/managed/Nota.App/bin/Release/net10.0/Nota.App.exe`;
  running as process 30856, window “Nota — Untitled”, responding. Engine DLL SHA-256 matches
  the native build (`BB60F0CE98809A590AF91BB4B4C3E5AE54A349236EB0E4536641287EAC328929`).
  The app is left open for interactive testing.
- `git diff --check` passes (line-ending notices only). No new production dependency.

## Practical limits

- No CLAP host exists; no claim of CLAP synchronization. No installed VST3/AU plugin or
  physical audio/MIDI loopback audition was performed. Native host timing is tested.
- Warped Session clips launched at a different part of an automated tempo map read a
  prepared cache at musical phase; this may change pitch. Arrangement map-aware prepared
  stretching preserves pitch in the tested ramp. Natural Session audio preserves source time.
- Grid follows audio uses explicit source/beat correspondences. Automatic rubato beat
  inference remains separate feature-076 work; transient detection is not beat inference.
- Resource guards reject maps exceeding prepared-table limits (100,000 points,
  2,000,000 knots). Dense extreme curves can reach that limit before the point limit.
- Timing/performance observations are local Windows measurements, not measured hardware
  round-trip latency. The local Windows script passes; no PR/release or other platform build was performed.

## Final convergence assessment

The follow-up speckit-converge assessment finds no remaining actionable implementation
items for feature 077 within the documented repository support. Checked: 12 functional
requirements, 5 success criteria, 12 story acceptance scenarios, 8 architecture decisions,
8 constitution principles and all 25 task results. T025 closes the Session contradiction.
Build/evidence/documentation gates are complete; unavailable host formats and physical
hardware/plugin auditions are explicitly limited above. Outcome: **Converged**.
