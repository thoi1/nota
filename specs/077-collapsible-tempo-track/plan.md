# Implementation Plan: Collapsible Tempo Track

**Branch**: dev/windows-fixes (preserve existing worktree) | **Date**: 2026-11-03
**Spec**: [spec.md](spec.md)

## Summary
Extend Transport with an immutable project tempo map carried by existing Graph snapshots.
Expose points/conversions through the C ABI/Application port. Add a pinned Arrangement lane
using Studio tokens, graph undo and existing persistence. Clip warp markers stay source-to-beat
relationships, never another project clock.

## Technical Context
C++20, pure C ABI, .NET10, Avalonia12.1.4; no new production dependency. Windows x64.
Existing smoke/native/headless tests. Format v29 migrates to v30; track-view.json keeps layout.
44.1/48/96kHz timing, two-hour/10,000-point maps; callback work bounded by output frames.

## Constitution Check
PASS real-time/boundary: authoring prepares immutable tables; graph retention keeps callback
raw pointers valid; no callback integration, allocation, locks or I/O. PASS licensing: headers,
no new production dependency. PASS verification: ABI/native/smoke/actual UI and Windows gate.
PASS scoped change/spec-first: keep old work, dependent tasks below. PASS documentation:
Studio, Loc/fr, shortcuts, FEATURES/changelog and EN/RU manual. PASS no weakened safety rule.

## Phase 0: Research
[research.md](research.md), [clock-audit.md](clock-audit.md), [ui-audit.md](ui-audit.md).
Transport has only scalar BPM. Absolute beat*spb and sample/spb assumptions in render,
recording, warp caches, managed display/export/freezing must migrate. No CLAP host exists;
validate actual VST3/AU bridge and report unavailable CLAP rather than claim support.

## Phase 1: Design
- TempoPoint: 960 ticks/quarter, double BPM, outgoing Step/Linear/Smooth. Strict ordered ticks,
  initial zero point, configurable limits20–300 default within1–999; last tempo extrapolates.
- Authoring integrates Step/Linear analytically and Smooth by bounded adaptive quadrature.
  Prepare monotone beat/seconds knots with <1/8-sample error at192kHz and hard resource cap.
  Runtime forward/inverse share this representation: bounded search plus interpolation.
- Graph owns map/constraints; Transport adopts nonowning pointer on audio block boundary,
  preserves musical position on edit, remains sole clock. Existing scalar API stays compatible.
  Multi-point transport BPM edits update/insert at the current musical position.
- Slice process at prepared tempo knots and loops, at most one slice/output sample; use map
  for absolute placement and scalar local slope only for DSP. Master plugins share slice timing.
- Warp cache identity includes map and clip start. Worker bakes synthetic output-time markers
  into existing stretcher; no new worker pool. Retain stale caches during background preparation.
  Unwarped sources preserve seconds; recording takes capture beat/source correspondences.
- Pinned lane below ruler shares header width, BeatToX, scroll, snap and Studio drawing.
  Candidate-only pointer preview commits once or cancels. Dedicated tempo semantics, no gain lane.
- Explicit Grid follows audio fits source-time/beat anchor pairs to project Step segments and
  leaves reference audio unstretched; Audio follows grid preserves musical warp coordinates.
  Validate locked absolute-time correspondences within one sample; reject with explanation.
- Save points/anchors in project format30, view state in sidecar; legacy BPM creates constant
  map. Export/bounce duration is map(end)-map(start); displays query same conversion port.

## Structure
Native engine/TempoMap.{h,cpp}, Engine_Tempo.cpp, Transport/Graph/render/warp/record/takes,
ABI header/core. Managed Application tempo port, Infrastructure interop/project/export,
Presentation clock display, App TempoTrackControl/ArrangementView.Tempo and Warp workflow.
Tests: native TempoMap, smoke TempoMapTests, actual-app headless gestures. See contracts,
data-model and quickstart. Same constitution gates pass after design.
