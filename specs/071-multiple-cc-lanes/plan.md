<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->
# Implementation Plan: Multiple CC lanes

Date: 2026-11-01. Spec: spec.md. Continue in the current checkout.

## Summary and Technical Context
C#/.NET 10, existing Avalonia controls and controller engine contracts. No dependencies.
Keep the existing primary velocity/MPE/controller lane. Add a small native + toolbar
and a bounded scrollable stack of extra controller lanes. Every extra lane has its own
controller identity, picker and - button. Reuse ControllerLane drawing/gestures and
StoreController rather than duplicate editing logic. Expose no new engine API.

## Research and Decisions
The existing ControllerLane reads one shared _laneCtl; add an optional per-instance
controller override while keeping the primary lane's fallback. Picker takes a callback
so it can select either the primary controller or an extra lane. Invalidate every extra
lane when the shared controller cache or horizontal view changes. Remove/change cancels
pending pointer work. Add grid gutter columns to the existing _keyColumns registry and
remove them when the row goes away. The extra stack is limited to 224px and scrolls.

## Constitution Check
PASS: no audio callback/ABI/dependencies, AGPL source headers, native token brushes and
Glyph controls. Existing undo commits are reused. Changelog/features/translations updated;
manual handoff because C:/Dev/nota-docs is absent. Existing playhead code remains untouched.

## File Scope
PianoRollView.cs, PianoRollView.Controllers.cs, PianoRollView.Expression.cs (height fit), new PianoRollView.ControllerLanes.cs,
Languages/fr.json, CHANGELOG.md, FEATURES.md, and validation sources/evidence here.

## Validation
Actual Avalonia diagnostic exercises add/pick/remove, continuous and switch controller
rendering, callback-based commit routing, reload/cache/zoom and many-lane scrolling.
Capture Studio Dark and Logic Grey. Build Windows x64 and run smoke/localization checks.
Data and manual guidance live in data-model.md, contracts/lanes.md and quickstart.md.

