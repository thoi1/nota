# Research decisions

- Reuse existing CommandCatalog/CommandRegistry. Bare top-row 1–5 have no current binding; modified Cmd/Ctrl+1–5 belong to grid commands. No configurable shortcut storage exists. Key latches must clear on key-up and focus loss. Reject TextBox/NumericUpDown ancestors and unrelated top-level/modal/plugin input.
- Share waveform state, keep tool selection per editor. Existing ArrangementTool enum and ArrangementDrawing normalized waveform functions are already reusable; avoid a second enum or oscillator. Temporary state does not belong in project files.
- Extract one native toolbar and shape-icon mapping rather than copying XAML into Piano Roll. Attach/detach shared-state subscriptions to avoid leaks when clip editors are rebuilt.
- ControllerLane already has _orig/_work preview, Normalize, ApplyStroke/Thin and a single StoreController on release. Generate on the editor grid, map through _set.ToLocal(active,...), use MidiController.Range (ordinary CC 0..1). Include _set.IsEditable(active) lock protection.
- NoteGrid Select double-click/live drag publishes immediately; new Pencil needs a separate local preview to make cancellation real. Reuse DrawTarget/NewNoteLen/default velocity and CommitNotes on release.
- No shared tempo-map/Tempo Track/absolute-time warp engine exists. Add ruler feedback and truthful unavailable help only. Existing audio Warp stretches samples and cannot satisfy this request.
- nota-docs is absent. Prepare local bilingual manual updates and report the unavailable website build/screenshot regeneration.
