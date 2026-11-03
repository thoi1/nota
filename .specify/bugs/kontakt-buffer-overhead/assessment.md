# Bug Assessment: Hosted adapters clear unused maximum-block storage

- **Slug**: kontakt-buffer-overhead (user confirmed)
- **Created**: 2026-11-03
- **Source**: user follow-up and source inspection
- **Verdict**: valid
- **Severity**: high — avoidable per-instance callback work reduces headroom at small buffers

## Report

The preceding VST3 render-pool repair improves Kontakt playback, but the user reports
approximately 40% higher DSP than Studio One with matching audio driver, sample rate,
buffer, patches and Kontakt multiprocessing. Four SWAM String Sections instances also
turn Nota's DSP red and cause popping; a single instance performs similarly in other DAWs.
The two reports warrant measuring four-instance host overhead and coordination rather
than assuming the proprietary instruments have identical bottlenecks.

## Symptom and Reproduction

`Engine_Devices.cpp` prepares hosted processors for `kMaxBlock` (16,384 frames).
`PluginInstrument::render` and `PluginEffect::process` clear all of that storage on every
callback, although the view passed to JUCE exposes only `frames`. At 128 frames this
clears 128 times the active sample range, multiplied by the channel and instance counts.

Automated reproduction: extend the existing deterministic VST3 fixture to expose 28
stereo outputs, then measure the same four zero-work instances at small and large buffer
sizes before/after the correction. Test stereo/main, sidechain and auxiliary buffer
initialization across changing callback sizes and compare rendered audio exactly.

The exact SWAM patches/voice pattern and current buffer/sample rate have not been supplied.
Installed SWAM section VST3s are available locally. A separate opt-in, isolated offline
profile can compare one versus four actual instances without altering the open project
or claiming to reproduce its precise musical workload.

## Suspected Code Paths

- `src/native/nota.engine/pluginhost/hosting/PluginInstrument.cpp:75`: full storage clear.
- `src/native/nota.engine/pluginhost/hosting/PluginEffect.cpp:81`: same full storage clear.
- `src/native/nota.engine/src/engine/Engine_Devices.cpp:438,454,463`: maximum-block preparation.
- `PluginCommon.cpp::pluginChannelPtrs`: exposes only the actual callback frame range.
- `AudioThreadPool.cpp` and `Engine_Render.cpp`: additional hypotheses, not changes in this
  assessment: normal-priority helper scheduling, conservative overload-only activation,
  indivisible track cost and per-instance VST3 processing. Profile before changing them.

## Root Cause Hypothesis

High confidence in wasted memory clearing; unknown share of the observed commercial-plugin
gap. Both JUCE clear overloads are allocation-free. `clear(0, frames)` initializes every
channel's active range while retaining capacity and avoiding work on samples the plug-in
cannot access this callback. Growing later callbacks clear their own entire active range.
This does not prove that buffer clearing is the sole cause of four-instance SWAM popping.

## Proposed Remediation

**Preferred**: replace the two full clears with `storage_.clear(0, frames)`. Preserve channel
layouts, all bus activation, maximum preparation capacity, sidechain semantics, sample
rate, latency and the previous scheduling repair. No pool, priority, latency policy, graph,
ABI, project format, product control or third-party dependency changes.

**Files likely to change**:
- `src/native/nota.engine/pluginhost/hosting/{PluginInstrument.cpp,PluginEffect.cpp}`
- `src/native/nota.engine/pluginhost/tests/RenderProbe.cpp`
- `tests/Nota.SmokeTest/{HostedRenderPoolTests.cs,HostedPluginProfile.cs,Program.cs}`
- `CHANGELOG.md`, and `ARCHITECTURE.md`/`BUILD.md` for the performance contract/diagnostic command

**Tests to add/update**:
- Four real 56-channel VST3 instances: small callback cost scales with actual frames, with
  before/after timings saved separately from the first repair's evidence.
- Variable 16/64/128/1024/8192-frame callbacks: silent inputs and auxiliary channels do not
  inherit prior data; test both instrument and insert adapters.
- Existing exact-output, sidechain, automation, lifecycle, idle, overload and bounce guards.
- Full Windows build/smoke gate, native tests and whitespace checks.
- Optional actual SWAM one/four-instance profile in a temporary catalog, with explicit
  limits on sample/patch and audio-device equivalence.

## Risks & Considerations

Never leave an active input/sidechain channel uninitialized. Do not reduce allocation
capacity or disable auxiliary outputs merely to improve a benchmark. The deterministic
fixture must expose stale data through its main output so master NaN protection cannot
hide failed clearing. Preserve all unrelated dirty-worktree edits and the user's running
project. Benchmark copied pre-change and post-change DLLs against the same revised fixture.

## Open Questions

The user's exact four-instance SWAM configuration and Studio One internal processing
buffer/dropout-protection mode remain unavailable. They do not block the proven clearing
repair, but resolving the entire residual gap needs the matching workload and measurements.
