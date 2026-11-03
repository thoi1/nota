<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->
# Implementation Plan: Meter refresh rate

Date: 2026-11-02. Spec: spec.md. Existing branch/checkouts retained.

## Summary and Technical Context
C#/.NET 10/Avalonia; existing JSON SettingsService; no dependencies or native changes.
Add MeterRefreshHz (normalized 30/60, default 30) and native segmented Appearance picker.
Reuse the existing MainWindow RequestAnimationFrame callback with a phase-preserving
cadence gate. Keep it alive while visible primary meters can show live input even when
transport is stopped. Keep cursor updates at display cadence while playing; stopped
seeks retain the existing service path. Add floating Mixer as an eligible frame owner.

## Research Decisions
Split pure level updates from ArrangementView.UpdateMeters, MixerView.UpdateMeters and
SessionView.UpdateStates so fader sync, scene blinking, CPU/DSP sampling and other work
remain on their original service clocks. Never feed a meter from both paths. Gate hidden
panels and individual controls before engine reads. Same-valued MeterBar.Push remains a
no-op. A reusable pure cadence helper uses monotonic deadlines and skips missed periods
without catch-up bursts; resetting/reconfiguring permits an immediate next frame.
Research agent confirmed the combined methods and the minimized-floating-mixer trap.

## Constitution Check
PASS: presentation-only reads of existing meter snapshots; no DSP/audio callback/ABI,
transport clock or dependencies changed. AGPL headers. Existing semantic brushes/native
controls and localization used. CPU sampler cache (500ms) and tooltip throttle (1s) stay.

## File Scope
Application/Settings.cs, new Application/MeterRefreshRate.cs and Presentation/ViewModels/MeterRefreshCadence.cs;
App/PreferencesWindow.cs, MainWindow.Playhead.cs, MainWindow.axaml.cs, ArrangementView.cs,
MixerView.cs, SessionView.cs, SessionView.Mixer.cs and Controls/MeterBar.cs (visibility predicate). Existing meter control styles and
native code remain. Smoke cadence/preference tests, validation probe, changelog/features,
French strings and manual handoff.

## Validation
Deterministic rate/invalid preference/delay/rate-switch tests. Actual app/picker persistence
in a new process, visible meter state/draw-list cadence and per-process CPU time/allocated
bytes at 30/60; both Studio Dark and Logic Grey and WASAPI/Yamaha ASIO where available.
Check hide/minimize, floating mixer, stopped input path and export suspension. Compare
service and cursor cadence to preserve existing fixes. Full Windows x64 build/smoke once
source is stable; scope and measurements in validation.md.

