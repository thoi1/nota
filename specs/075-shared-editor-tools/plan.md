# Implementation plan: Shared editor tools

**Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

## Summary

Extract the existing Arrangement five-tool UI into a native reusable EditingToolBar. Add EditorToolState per editor and one authoritative shared waveform/settings object. Route bare top-row 1–5 through CommandCatalog/CommandRegistry using focused or last-clicked editor context, existing key-held suppression and strict typing/modal/plugin guards. Add the same toolbar above the existing PianoRollView body without altering its internal resizable rows. Reuse normalized ArrangementDrawing waveform math for a clip-local controller adapter.

## Technical context

C#/.NET 10, existing Avalonia 12.1.4. No dependencies, project schema changes or persistence of ephemeral tool mode. Existing controller/note persistence and pure-C engine commands remain. UI-thread-only state, previews and generation; no audio-callback, DSP or ABI changes. Windows build plus full smoke and actual headless input tests. Build to a separate bin/SharedTools output while the user's test process stays open.

## Constitution check

All eight principles pass before and after design: no realtime/boundary/dependency change; AGPL headers required; spec precedes code; Studio tokens/shared Glyphs; local docs/shortcut/translation sync; EN/RU site sync only if the sibling repo exists. No commits/push/PR. Requested WARP and exact tool-name casing are explicit UI exceptions.

## Research and design

See research.md. MainWindow owns CommandRegistry and the shared main/floating keyboard route. Per-editor tool selection uses a common application model; the shared toolbar observes local tool changes and one shared waveform object, only while attached. Tool buttons choose their own editor; registry commands target the focused editor. A fresh press of 4 cycles only if that target already has Waveform active. A shared cursor helper and one shape-to-Glyph mapping serve both editors.

Piano NoteGrid gets a focused partial for new-tool routing: Pencil creates a local preview using DrawTarget/NewNoteLen/s_lastVel and commits once at release; Eraser uses existing deletion. Preserve Select's original double-click, modifiers, move/resize and live-edit path. Cancel new previews independently from existing generator previews.

ControllerLane gains a waveform drag mode using _orig/_work/StoreController and existing Normalize/Thin utilities. Generate in editor coordinates on the snap grid, then map into the active clip's local beats and clamp to its length/range. Gate all new tool actions on editable clip ownership. Freehand reuses controller stroke infrastructure, with snapping enabled for tool-driven drawing. New waveform draws commit once; Escape/tool changes reset previews.

Both ruler renderers receive a shared muted-red wash, readable WARP badge and hovered-grid stroke. The tempo-map/Tempo Track engine remains absent; toolbar/ruler help explicitly explains that no tempo editing is active. Existing clip stretching is never used.

## Project structure

- Application/Editing/EditorToolState.cs: authoritative shared shape/settings and per-editor tool/cycle transitions, command mapping.
- App/Controls/EditingToolBar.cs, EditorToolCursors.cs, WarpRuler.cs: shared native UI and rendering.
- MainWindow.ArrangementTools.cs, MainWindow.Commands.cs, MainWindow.Input.cs: shared toolbar binding and focus-aware number command dispatch.
- ArrangementView.Tools.cs and Controls.cs: model binding/ruler feedback, retaining existing clip/envelope adapters.
- PianoRollView.Tools.cs and existing PianoRollView*.cs: toolbar, tool routing, note/controller preview adapters and ruler feedback.
- Application/Palette/CommandCatalog.cs: registered tool commands and numeric shortcut descriptions.
- PreferencesWindow.cs, Languages/fr.json, CHANGELOG.md, FEATURES.md: user-facing references.
- tests/Nota.SmokeTest/SharedEditorToolTests.cs plus temporary external headless harness: transitions, shapes, commands, edit/undo and focus/cancellation checks.

## Strategy

Shared model/toolbar first; shortcuts/dynamic icons second; piano note/controller adapters third; honest Warp visuals fourth; docs, Windows build, headless tests and convergence last. Inspect the session baseline to avoid overwriting earlier local work. No new tempo-map engine or unrelated editing behavior.
