<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Implementation Plan: Piano roll workspace improvements

**Branch**: `dev/windows-fixes` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

## Summary
Reuse MainWindow's maximized detail layout and existing editor controls. Snapshot the
previous docked/floating layout on Arrangement MIDI activation; temporarily host the same
editor in the main window and restore on exit. Add missing floating Devices close control.
Make primary and extra controller lane heights independently draggable and persistent;
remove velocity onset offsets and extend MaxPpb from 400 to 3200.

## Technical Context

- C# / .NET 10, existing Avalonia controls and settings service. No dependencies added.
- Storage: application Settings for lane heights; transient workspace snapshot for layout.
- Audio-thread impact: none. No native, DSP, routing or project MIDI format changes.
- Testing: real Windows Avalonia regression probe plus existing Windows build/smoke gate.
- Platform: Windows 11, retaining cross-platform layout primitives and logical DPI units.
- Performance: retained playhead layers; resize only layout, bounded visible grid rendering.
- Scope: MainWindow.Detail/DetailWindow, PianoRoll partials, Settings, tests and docs.

## Constitution Check

I/II: no audio-thread or ABI changes. III: AGPL headers, no dependencies.
IV: Windows build/smoke plus targeted UI regressions required. V: logical changes isolated; latest user instruction keeps all changes uncommitted.
VI: changelog, FEATURES, shortcuts and EN/RU manual updates/handoff. VII: spec precedes
implementation. VIII: no guard weakening. Gates pass before and after design.

## Project Structure

- `src/managed/Nota.App/MainWindow.Detail*.cs`, `DetailWindow.cs`, `MainWindow.axaml`:
  workspace snapshot, host restoration and panel controls.
- `src/managed/Nota.App/PianoRollView*.cs`: lane dividers, exact onset and zoom range.
- `src/managed/Nota.Application/Settings.cs`: normalized persistent lane heights.
- `specs/072-piano-roll-workspace/validation/`: real UI regression probe and results.
- `tests/Nota.SmokeTest/`: applicable persistence/geometry coverage.

## Implementation Order

Workspace activation → Devices close/reopen → independent lane sizing → velocity onset
fix → horizontal zoom → documentation, Windows build and convergence. Existing 070 meter
and 071 CC changes are snapshotted separately in the system temporary directory; do not
stage unrelated pending work. No automatic push.

