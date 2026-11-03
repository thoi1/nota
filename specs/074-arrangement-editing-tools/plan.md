# Implementation Plan: Arrangement editing tools

**Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

## Summary

Add one persistent five-tool group beside Snap/Automation in MainWindow's transport. Keep the automation visibility toggle independent. Use Select/Edit glyphs where appropriate; add Pointer, Eraser, WaveDraw and WarpGrid only as needed to Glyph.cs. Use existing tp-icon styling and 15px glyphs. Add a secondary waveform settings menu.

## Technical Context

C# / .NET 10 / existing Avalonia. No new dependencies or persistence. Windows validation here; portable managed behavior. UI-thread-only previews, existing IAudioEngine edit commands on release; no native, DSP, audio-thread or C ABI changes. Bound generated point counts/density. Tests: pure editing math, engine undo/redo, managed build, full Windows build/smoke and available UI verification.

## Constitution Check

All eight principles pass before and after design. Real-time safety and ABI unchanged; AGPL headers on new sources; no dependencies. Spec precedes implementation. Studio styling reused. Exact requested tool names are an explicit user exception to sentence case. Documentation and verification are closing tasks. Existing dirty files are edited minimally; no commits or publishing.

## Research decisions

See [research.md](research.md). No suitable tempo-map/Tempo Track exists: Nota Warp is selection-only, with visible unavailable status/help. Arrangement has no MIDI CC lanes; do not connect or duplicate piano-roll lanes. Use snap model and existing clip create/delete APIs. Local automation previews preserve outside points and commit under one undo group (including first-lane creation). Cancellation restores source points. Reuse this preview implementation for track, return and master rather than rewriting their Select handlers.

## Project Structure

- `Nota.Application/Editing/ArrangementTools.cs`: tool/shape types and pure interval/wave generation.
- `Nota.App/MainWindow.ArrangementTools.cs`, `MainWindow.axaml`: tool group, settings menu, synchronization.
- `Nota.App/ArrangementView.Tools.cs`: shared state, cursor and automation stroke lifecycle.
- `Nota.App/ArrangementView.LaneControl.cs`, new partial tools file: clip pencil preview and tool routing.
- `Nota.App/ArrangementView.AutoEditor.cs`, `ArrangementView.Controls.cs`: existing automation editor routing including master.
- `Nota.App/Controls/Glyph.cs`: missing vector icons only.
- `Nota.App/Languages/fr.json`, `PreferencesWindow.cs`: localization and gesture reference.
- `tests/Nota.SmokeTest/ArrangementToolTests.cs`: math, bounds and undo integration.
- `CHANGELOG.md`, `FEATURES.md`, EN/RU user documentation if sibling repo available.

## Implementation strategy

Tool state first, then clip gestures, then shared automation previews, then test/doc closure. Re-check interactions/cancellation and compare only session additions against captured local baseline. No external API contract; UI contract under contracts/.
