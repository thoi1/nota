# Implementation Plan: Interactive Tempo Time Mapping

**Branch**: `feat/tempo-time-mapping-1129` | **Date**: 2026-11-28 | **Spec**: [spec.md](spec.md)

## Summary

Add a dedicated Ctrl-drag gesture to the master Tempo lane. Compute the destination
against the drag-start time axis and draw the grid from a detached tempo
preview. Publish the constrained edit when the gesture commits.

## Technical Context

- Avalonia gesture and guide in `ArrangementView.AutoEditor.cs` and
  `ArrangementView.Automation.cs`. Keep the ordinary automation gesture intact.
- C++ engine derives musical time from the master automation lane through
  `Engine::deriveTempoMap` and `TempoMap`. Use the engine for conversion and
  validation. Expose the edit lifecycle through the existing C ABI and managed interop.
- Build with the repository scripts. Verify timing and history through
  `tests/Nota.SmokeTest`, and verify pointer behavior in the app.

## Constitution Check

Issue #1129 is approved. Compile and validate candidate maps on the authoring
thread, then publish immutable graph snapshots. Render reads the snapshot without
locking, allocation or I/O. Preserve Transport as the source of musical time.
Cross the ABI with POD values only. Add no dependencies. Use Nota design tokens
and update shortcut guidance, localization, features, changelog and manual.

## Phase 0: Research

See [research.md](research.md) for the axis, tempo solver and audio decisions.

## Phase 1: Design

- [Data model](data-model.md)
- [Gesture and engine contract](contracts/time-mapping.md)
- [Quickstart and verification](quickstart.md)

## Source Layout

- Engine edit and validation: `Engine_Automation.cpp`, `TempoMap.h`, `Engine.cpp`.
- Clip timing and render: `Track.h`, `Track.cpp`, `Engine_Tracks.cpp`,
  `RenderGraphExecutor.cpp`, `TimelineIndex.h`.
- ABI and project data: `nota_engine.h`, `IAudioEngine.cs`, native interop and project
  persistence in `Nota.Infrastructure`.
- UI: `ArrangementView.AutoEditor.cs`, `ArrangementView.Automation.cs`,
  `ShortcutLayout.cs`, and `Localization/en.json`.

## Post-design Gate

The local tempo solver, preview transaction and preservation of warped playback
must be demonstrated before wiring the gesture. Preview must never create an undo
entry or regenerate audio on the render thread. Verify the persisted audio timing
across save/load as well as undo/redo.
