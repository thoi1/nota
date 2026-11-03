# Bug Verification: Four-instance SWAM callback headroom

- **Slug**: swam-worker-headroom (from assessment/fix context)
- **Tested**: 2026-11-03
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial — saved-project headroom verified; physical ASIO popping needs audition

## Summary

The confirmed 512-sample / 48 kHz configuration is used throughout the saved-project
comparison. Actual saved SWAM states and MIDI produce nonzero audio, and four-track mean
callback-budget use improves from 59.06% to 30.92%. A single SWAM remains approximately
28%; the complete saved project improves from 60.25% to 32.22%. The supplied project is
unchanged. Neither before nor after in-memory windows reproduce the original audible pops.

## Checks Performed

| Check | Command / action | Result | Evidence |
| --- | --- | --- | --- |
| Deterministic before reproduction | Real VST3 fixture, four large 512-frame jobs versus one | pass (gap reproduced) | Headroom assertion fails: 6.086 ms versus 2.916 ms. `headroom-before.log`. |
| After headroom/work bounds | `--hosted-render-pool`, focused and full smoke | pass | Four jobs 3.161 ms versus one 2.918 ms focused; 3.167/2.923 ms in the full suite. Work-proxy guard passes. |
| Actual saved-project comparison | `--profile-project 'D:/Projects/Untitled.nota' 512 48000` before/after target change | pass | Exact saved states/MIDI; no restore warnings/fallback; each case renders nonzero RMS. Temporary catalog/muting, no source save or hardware output. See benchmark. |
| Callback deadlines | 400 paced callbacks per saved-project case after initialization/warm-up | pass | 0/400 late for one SWAM, four SWAM and complete project in both builds. This is an in-memory timing check, not a physical-driver dropout reproduction. |
| Full Windows regression gate | `scripts/build-win.ps1` in the VS developer environment | pass | Exit 0; native/managed Release, scanner, smoke/performance and scan scale; 5,365 PASS lines, zero FAIL. All twenty hosted checks pass. |
| Idle/light load | Existing pool guards in full smoke | pass | Idle and stopped helper CPU 0.0 ms/second; light playback helpers 0.0% of a core; render CPU 12.8% versus 13.0% single-threaded. |
| Overload/bounce/export | Existing performance guards | pass | 0/4,125 late overload blocks, no bursts; bounce/export work and timing remain within budgets. |
| Deterministic audio/lifecycle | Existing full smoke and hosted regressions | pass | Serial/parallel audio exact; MIDI, automation, previous-block sidechains, routing, undo, bypass, replacement and concurrent graph edits pass. |
| Native tests | `ctest --test-dir src/native/nota.engine/build --output-on-failure` | pass | 2/2 pass. |
| Delivery/hygiene | App/native hash comparison, whitespace and source review | pass | Matching DLLs; no dependency, ABI, format, priority, driver setting or UI/shortcut change. Core stays JUCE-free. |
| Original audible popping | Physical Yamaha Steinberg USB ASIO playback/live MIDI | not-run | The diagnostic deliberately does not open the physical output device. User audition remains required. |
| Direct Studio One/platform comparison | Instrumented other DAW, Linux/macOS, Valgrind | not-run | Not performed on this Windows host. |

## Output Excerpts

```text
[PASS] 512-frame large VST3 jobs reserve headroom: four 3.167 ms vs one 2.923 ms (<= 1.6x)
[PASS] idle engine: 16 render threads use 0.0 ms CPU over 1 s
[PASS] overloaded: 0 of 4125 blocks late with render threads after warm-up
SMOKE TEST PASSED
SCAN SCALE PASSED
100% tests passed out of 2
```

[Saved-project measurements, charts and method](benchmark.md), [raw records](benchmark.json).

## Residual Risks

More useful simultaneous workers are an intentional tradeoff: the saved four-track
caller-elapsed-plus-worker-CPU proxy rises about 4.8% while callback time nearly halves.
It remains substantially below the initial whole-pool wake-up comparison. This proxy
includes caller waiting and is not a precise process-CPU total. Physical driver/DPC
jitter, live input, later musical regions and external plug-in scheduling can differ.
SWAM output varies between runs; deterministic fixtures establish host output equivalence.

The original default SWAM probe produced silence before message-pumped initialization;
those silent results were discarded and are not used as performance evidence. The saved
state/MIDI comparisons are audible renders with nonzero RMS. Source manifest SHA-256 stays
`F95E30CF1E5849EA3EDAB0407BA4BF6E687E68767B14ED91E0368BDED02D95A7` before/after profiling.

## Recommendation

Use the rebuilt desktop app at the same 512/48000 settings to audition the saved project.
The headroom regression is verified; close the audible-popping report only after physical
playback confirms it. If pops remain, capture the exact timeline/live gesture and examine
the ASIO callback and worker scheduling with the matching project rather than infer a
cause from Studio One's meter alone. No commit, push or PR was made.
