# Bug Fix: Clear only active hosted callback frames

- **Slug**: kontakt-buffer-overhead (from the confirmed assessment)
- **Fixed**: 2026-11-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Instrument and effect adapters now clear `storage_.clear(0, frames)`, retaining maximum
capacity while initializing all active channels. Four 56-channel fixture instances drop
from 0.1877/0.2030 ms to 0.0390/0.0397 ms median at 128 frames for instrument/insert paths.

## Changes

| File | Change | Notes |
| --- | --- | --- |
| Native `pluginhost/hosting/PluginInstrument.cpp` | modified | Clear active frame prefix on every channel. |
| Native `pluginhost/hosting/PluginEffect.cpp` | modified | Same prefix initialization, retaining input/sidechain handling. |
| Native `pluginhost/tests/RenderProbe.cpp` | extended | 28 stereo outputs and finite auxiliary data exercise multi-output clearing and stale sidechain input across changing callback sizes. |
| `tests/Nota.SmokeTest/HostedRenderPoolTests.cs` | extended | Two correctness checks and two active-frame scaling guards, plus variable-frame measurement support. |
| `tests/Nota.SmokeTest/{HostedPluginProfile.cs,Program.cs}` | added/modified | Opt-in commercial-plugin/project profiles; scratch catalog, message-pumped initialization, read-only saved states/MIDI and timing/RMS records. Not part of normal smoke. |
| `CHANGELOG.md`, `ARCHITECTURE.md`, `BUILD.md` | modified | Performance fix and diagnostic contracts. |

Abbreviated native paths are under `src/native/nota.engine/`.

## Local Verification

- Before DLL preserved in the temporary baseline directory; both new scaling guards fail
  before the two clear changes and pass after. Changing-size input correctness passes in
  both builds. No claimed correction to already-correct active audio.
- Full Windows gate passes: exit 0; 5,365 PASS lines, zero FAIL; native/managed Release,
  scanner self-test, smoke/performance guards and scan-scale check.
- Native CTest: 2/2 pass. All twenty hosted fixture checks pass in the full smoke suite.
- Existing exact-output, sidechain, automation, bypass/removal/undo and concurrent graph
  tests pass. No active channel/layout/capacity or public ABI changed.
- Whitespace checks pass; new sources have AGPL headers. No dependency added.

## Deviations from Assessment

The user supplied the saved project and clarified 512 samples at 48 kHz. The raw default
SWAM profile initially rendered silence until the diagnostic serviced initialization
messages; those silent timings are explicitly excluded. Saved-state project profiles
produce nonzero RMS. The buffer correction alone changes that four-SWAM window from about
60% to 59%, so it does not explain the larger reported gap. A separate, user-confirmed
[worker-headroom assessment](../swam-worker-headroom/assessment.md) and fix address that
measured scheduling issue; they are not attributed to buffer clearing.

The diagnostic code grew to support the supplied project's real states/MIDI, temporary
muting and a useful-helper vs all-workers comparison. It writes neither the source project
nor installed catalog. Studio One and physical ASIO playback are not instrumented.

## Documentation and Follow-ups

Nota Docs was checked: this is internal performance work with no UI, shortcut or manual
workflow change, so its documented pure-performance exception applies. FEATURES capabilities
are unchanged. The development/build reference documents the opt-in diagnostic selectors.
See [benchmark.md](benchmark.md) and [test.md](test.md). No commit, push or PR was made.
