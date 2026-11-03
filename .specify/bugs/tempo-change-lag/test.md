# Bug Verification: Responsive project tempo edits

- **Slug**: tempo-change-lag (resolved from the preceding fix)
- **Tested**: 2026-11-03
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

The original transport view-model reproduction no longer blocks on full-source stretching.
Actual pointer-driven BPM edits remain responsive, the latest caches finish through the
window's own polling loop, and the full Windows build/regression suite passes.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Original reproduction | `dotnet run --project .specify/bugs/tempo-change-lag/repro/TempoLag.csproj -c Release --no-build` | pass | Same two 20-second Complex-warp clips and four VM setters as before; verified timing record below. |
| Focused regressions | `dotnet run --project tests/Nota.SmokeTest -c Release -- --tempo-change` | pass | 12 checks; also run by full smoke suite. Desired tempo before command drain, active-worker cancellation/latest wins, Session, no undo/source changes, retained audio, offline sample-rate preparation, invalid BPM. |
| Actual transport UI | `dotnet run --project .specify/bugs/tempo-change-lag/ui/TempoUi.csproj -c Release --no-build` | pass | 25 real headless pointer moves, debounce/poll completion, close with pending callbacks. No manual worker polling substitutes for MainWindow behavior. |
| Full Windows regression | `scripts/build-win.ps1` under VS x64 developer environment | pass | Native build, scanner self-test, managed build, smoke and scan-scale invocations; 5,154 passing assertions total (4,884 in the main smoke invocation); zero failures, exit 0. |
| Native CTest | `ctest --test-dir src/native/nota.engine/build --output-on-failure` | pass | Native transient self-test, 1/1. |
| Whitespace | `git diff --check` | pass | Existing line-ending warning only. |
| Rebuilt app | Compared SHA-256 of native build and Release app engine DLL | pass | Identical engine binaries. |
| Physical device / reporter project | Play reporter's exact project and drag transport BPM | not-run | Automated reproduction uses deterministic generated clips and offline/headless output; no claim of physical-device audition. |

## Reproduction Timing

Measured setter latency in milliseconds; fixture setup and cache preparation are outside
the timing. These local measurements demonstrate removal of synchronous DSP, not a
guarantee for arbitrary machines/projects. Preparation still takes time in the background.

| Project BPM | Before | Verification rerun |
|-------------|-------:|-------------------:|
| 121 | 214.548 | 0.506 |
| 132 | 201.196 | 0.017 |
| 145 | 189.623 | 0.004 |
| 110 | 239.136 | 0.004 |

Records: `repro-before.log`, `repro-verified.log`. Native cache inputs now also reflect
the requested BPM before its clock command drains.

## Output Excerpts

```text
PASS actual transport drag: 25 BPM changes; slowest input 24.086 ms
PASS MainWindow debounce/poller completed latest tempo (355 ms)
PASS closed-window continuation does not access the disposed engine
[PASS] tempo: Arrangement + Session audio equals fresh synchronous rebuild (max diff 0.0E+000)
SMOKE TEST PASSED
SCAN SCALE PASSED
100% tests passed out of 1
```

Focused log: `targeted-final.log`; UI verification: `ui-verified.log`; CTest: `ctest.log`.
Full raw regression log is in the ignored build directory:
`src/native/nota.engine/build/tempo-change-lag-build.log`.

## Residual Risks

- Existing cached audio remains audible while replacements prepare. Large projects take
  longer to settle; no streaming stretch was added to the callback.
- Physical-device output and the reporter's exact project were not auditioned.
- This repair preserves constant project-tempo semantics; it does not implement a variable
  tempo map or change transient/marker editing.

## Recommendation

Close the reproduced blocking-tempo bug. The rebuilt Release app is ready for the reporter
to test against their project. No extension hooks are registered in `.specify/extensions.yml`.
