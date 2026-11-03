# Bug Verification: Active hosted buffer range

- **Slug**: kontakt-buffer-overhead (from assessment/fix context)
- **Tested**: 2026-11-03
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified for adapter overhead; commercial playback symptoms tracked separately

## Summary

The before DLL fails both new frame-scaling guards. The active-prefix correction passes
them while preserving changing-size input/sidechain initialization and exact deterministic
audio. The observed SWAM comparison gap is not attributed to this small-buffer memory fix;
see the separate [headroom verification](../swam-worker-headroom/test.md).

## Checks Performed

| Check | Command / action | Result | Evidence |
| --- | --- | --- | --- |
| Before reproduction | Preserved parallel-render DLL with the revised 56-channel VST3 fixture | pass (defect reproduced) | Both active-frame scaling guards fail; old correctness tests pass. `buffer-before.log`. |
| After fixture coverage | `dotnet run --project tests/Nota.SmokeTest -c Release -- --hosted-render-pool` | pass | Four added checks: two adapter paths, changing 16–16,384-frame callbacks, no finite auxiliary-data leak, and active-size timing. `buffer-after.log`. |
| Regression suite | `scripts/build-win.ps1` in the VS developer environment | pass | Exit 0; native/managed Release, scanner self-test, SMOKE TEST PASSED, SCAN SCALE PASSED; 5,365 PASS lines, zero FAIL. All twenty hosted checks pass. |
| Native tests | `ctest --test-dir src/native/nota.engine/build --output-on-failure` | pass | 2/2 tempo/SuperFlux tests. |
| Existing audio behavior | Full exact-output, MIDI/automation, sidechain, bypass/removal/undo, rack fallback and concurrent-edit guards | pass | Production layouts, capacity and routing unchanged. |
| Source/project preservation | Whitespace checks, app/native DLL hashes, read-only saved-project profiling | pass | DLL hashes match; project manifest remains `F95E30CF1E5849EA3EDAB0407BA4BF6E687E68767B14ED91E0368BDED02D95A7`. |
| UI/manual workflow | Nota Docs applicability review | not applicable | Internal performance change; no label, parameter, shortcut, flow or visible control changed. Its pure-performance exception applies. BUILD/architecture/changelog updated. |

## Numerical Evidence

Four test VST3 instrument instances at 128/48000: median 0.1877 → 0.0390 ms.
Four insert adapters: 0.2030 → 0.0397 ms. These are zero-work 56-channel test plug-ins,
not commercial instrument CPU measurements. [Benchmark, plots and raw records](benchmark.md).

## Residual Risks and Recommendation

The memory-clearing issue is verified. Keep the active-range regression guards. Audition
the commercial project through the physical ASIO driver for the reported popping; the
headroom follow-up gives the substantial saved-SWAM improvement. Other platforms and the
Linux instruction-count gate were not run on this Windows host. No dependency or ABI changed.
