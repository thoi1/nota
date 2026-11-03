# Verification: native SuperFlux and shared analysis service

Historical first-stage report. The subsequent implemented Warp UI and current verification
are documented in [warp-verification.md](warp-verification.md).

Date: 2026-11-02. Platform: Windows x64, MSVC Release, .NET 10.

This is the first verified increment explicitly allowed by the request. It delivers the
native detector and source analysis service (US1). The complete Warp/tempo feature is
**not converged**; see the unchecked tasks and Phase 7 convergence findings.

## Repository inspection

See [research.md](research.md) for the architecture inventory and reuse decisions.
Existing Signalsmith FFT/stretch, immutable source buffers, peak/RMS tables, warp
anchors, graph history, marker persistence and managed scheduling are reused. No Essentia,
additional runtime dependency or independent worker pool was introduced. Existing
energy-rise detectors remain unchanged for their legacy consumers; they are not described
as SuperFlux. Transport remains the existing constant-tempo authority.

## Delivered

- Native centred STFT, logarithmic quarter-tone triangular filterbank, logarithmic
  magnitude, delayed three-band frequency maximum subtraction, positive differences,
  adaptive mean threshold, local maxima and minimum spacing.
- Original 44.1 kHz/2048-sample/200 fps reference configuration. Rate-scaled FFT sizes
  support decoded mono/stereo sources from 8 to 192 kHz. Stereo magnitude pooling avoids
  antiphase cancellation. Silent padding, finite-input sanitation and short-input safety.
- Separate optional local attack refinement. Unreliable refinement leaves the spectral
  estimate with conservative window uncertainty. Results contain source-relative int64
  sample positions, seconds, strength, heuristic confidence, uncertainty/refinement and
  analysis version. Optional frequency-band contributions are not populated.
- Pure C ABI jobs capture source ownership on the authoring thread and remain valid
  after engine disposal. First background step prepares FFT state; cancel checks every
  frame. Partial/cancelled results are unavailable. Exceptions stay inside the ABI.
- Application job/service ports, SafeHandle P/Invoke wrapper, read-only snapshots and
  shared background service. Source replacement IDs represent revisions. Cache keys include
  source/settings; maximum two active jobs on the existing .NET pool, 16 completed cache
  entries and 200,000 retained records. Oversize results return uncached without displacing
  useful entries. Binary-search nearest/range queries; invalidation rejects stale work.
- Engine-owned service disposal cancels outstanding work without waiting on the callback.
  Source buffers outlive cancelled work safely. Any consumer cancellation cancels its
  shared job; a subsequent request can retry after that job exits.

## Actual verification

- Initial test build failed on the missing API before implementation, as expected.
- Native Release build: passed with `cmake --build src/native/nota.engine/build` inside
  the matching VS BuildTools developer environment.
- Managed build and focused `--transients` suite: **43 checks passed**, zero failures.
  Latest focused suite measured **0.21 seconds** locally including fixture work. This is
  a test-run elapsed measurement, not a hardware-independent performance guarantee.
- `ctest --test-dir src/native/nota.engine/build -R '^nota-transient-selftest$'
  --output-on-failure`: **1/1 passed**; latest measured test time **0.18 seconds**.
- Native test compares all 38 ODF frames against committed NumPy/SciPy golden values
  using the original reference spectral equations: maximum absolute error
  **8.541934e-06**, below the **1e-4** tolerance. No refinement participates in this test.
  Test-vector regeneration used NumPy 2.5.4 and SciPy 1.18.1 in the ignored build directory;
  neither is required by the app or automated test executables.
- Measured non-FFT DSP scratch: **39,988 bytes** for both **0.186-second** and
  **60-second** sources at 44.1 kHz. This explicitly excludes existing decoded source
  audio, returned detections and fixed-size FFT internals. The FFT dimensions also depend
  only on sample rate, never duration. This is not a whole-process memory benchmark.
- Sharp attacks at 22.05/44.1/48/96 kHz, including attacks between hops, passed the
  **2 ms** error limit. Synthetic soft-piano, chord and arpeggio fixtures passed one-to-one
  detection counts within **20 ms**. Silence, short input, low-level noise, clipped
  attacks and antiphase stereo passed. Smoothly released sustained vibrato had no repeated
  false attacks.
- Lifecycle tests passed: shared tasks/snapshots; settings separation; monotonic progress;
  nearest/range/extreme positions; native partial-result suppression; pre-cancellation;
  running/queued invalidation; retry; two-worker bound; cache eviction and oversize policy;
  capture thread; shared consumer cancellation; disposal; source surviving engine disposal.
- Full `scripts/build-win.ps1`: **passed, exit 0**, including native, managed/app, scanner
  self-test, complete engine smoke suite and scan-scale check. The final run includes the
  oversized-cache regression and binary BSD notice. Both smoke and scan-scale report passed,
  with **5,094 passing checks** across their invocations and **zero failing checks**.
- App rebuilt after adding the BSD distribution notice: passed, zero errors. Output
  `licenses/SuperFlux.txt` is byte-identical to the repository notice; publish metadata
  also includes it. Existing compiler warnings remain in app/test files.
- `git diff --check` on touched tracked files: passed.

## Licensing

SuperFlux.py at upstream commit `0a85f898af69188324b649a4c640052a5443078e`
contains the BSD-2-Clause notice, copyright 2012-2014 Sebastian Böck. It is preserved
verbatim in [LICENSES/SuperFlux.txt](../../LICENSES/SuperFlux.txt), registered in
third-party.md and attributed in THIRD-PARTY-NOTICES.md, the C++ implementation and
test-vector generator. The app copies the full notice to its build/publish output.
Nota additions use the required AGPL-3.0-only headers. Algorithm citation: Böck & Widmer,
*Maximum Filter Vibrato Suppression for Onset Detection*, DAFx 2013; sources linked in
research.md. No upstream Python framework ships with Nota.

## Remaining work and limits

- No new waveform transient overlays, snapping/highlight integration or Analyse/Show/Snap
  controls are exposed yet. Existing muted-red Warp ruler/editor behavior is preserved.
  Shared source-to-clip/arrangement transforms for trimmed/rate-adjusted/reversed/warped
  clips remain T025; source-relative API results must not be drawn directly as beat positions.
- No new beat inference, BPM ambiguity, variable tempo map, smoothing, preview/apply/reset,
  Grid follows audio or selected-attack Audio follows grid workflow is implemented.
  These need coordinated Transport/MIDI/automation/plugin/persistence changes (T026-T028).
- There is no transient-result disk persistence yet; cache reuse is within the engine
  lifetime. Existing full-file decode memory usage remains unchanged; analysis scratch is
  bounded without making source decoding itself streamed.
- Faithful reference initial lag can miss attacks at the very beginning or in very short
  files. Abrupt file truncation can generate a spectral cutoff candidate; the vibrato test
  uses a smooth release to isolate pitch-modulation suppression. Soft polyphonic attacks
  can remain early/uncertain and are not arbitrarily forced to a sample boundary.
- Confidence is not calibrated and does not mean beat/downbeat likelihood. Transients are
  not beats. No actual recording or annotation dataset was supplied; real-piano precision,
  recall, CPU comparison with upstream and broad musical evaluation are **not claimed**.
- No macOS/Linux build, installer build, interactive UI workflow validation or final
  variable-clock synchronisation test was performed for this increment.
- nota-features/nota-docs review: capability log describes only engine API groundwork.
  No user workflow/manual page changed. `../nota-docs` is absent; no EN/RU edits, screenshots
  or manual-site build were needed/performed. Later UI work must sync both languages.

## Files changed by this increment

Pre-existing editor work was preserved. Files shared with that work received only additive,
targeted edits; the full working-tree diff includes unrelated changes that predate this task.

- `CHANGELOG.md`, `FEATURES.md`
- `LICENSES/SuperFlux.txt`, `LICENSES/third-party.md`, `LICENSES/THIRD-PARTY-NOTICES.md`
- `src/native/nota.engine/CMakeLists.txt`
- `src/native/nota.engine/include/nota/nota_engine.h`
- `src/native/nota.engine/src/engine/Engine.h`, `Engine_Tracks.cpp`
- `src/native/nota.engine/src/media/SuperFlux.h`, `SuperFlux.cpp`
- `src/native/nota.engine/src/media/tests/SuperFluxTest.cpp`
- `src/native/nota.engine/src/abi/nota_engine_transients.cpp`
- `src/managed/Nota.Application/Analysis/ITransientAnalysisService.cs`
- `src/managed/Nota.Application/Engine/IAudioEngine.cs`
- `src/managed/Nota.Infrastructure/Analysis/TransientAnalysisService.cs`
- `src/managed/Nota.Infrastructure/Interop/NativeMethods.Transients.cs`
- `src/managed/Nota.Infrastructure/Interop/NotaEngine.Transients.cs`, `NotaEngine.cs`
- `src/managed/Nota.App/Nota.App.csproj` (binary license notice only)
- `tests/Nota.SmokeTest/TransientAnalysisTests.cs`, `Program.cs`
- `tests/fixtures/superflux/generate.py`, `odf-golden.h`
- This spec directory: `spec.md`, `checklists/requirements.md`, `plan.md`, `research.md`,
  `data-model.md`, `contracts/transients.md`, `quickstart.md`, `tasks.md`, `verification.md`
- Ignored local `.specify/feature.json` points subsequent Spec Kit commands at this feature.

Build logs and the temporary NumPy/SciPy installation are local ignored build artifacts;
no generated audio, binary, new runtime package, commit, push or pull request is included.
Logs are retained locally under
`src/native/nota.engine/build/superflux-verification/` (including
`build-superflux-windows.log`, `build-superflux-tests.log` and initial failing test build).
