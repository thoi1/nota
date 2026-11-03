# Research: Piano Roll Snap Toggle

## Decision: Reuse the existing Piano Roll snap setting

`EditorGrids` owns the app-wide `PianoRollGrid` setting and publishes a `Changed` event
whenever it changes. `SnapGrid.ToggleSnap` flips only the `SnapOn` field. The new control
will read, update, and observe that same setting.

**Rationale**: The Piano Roll already applies this setting to note placement, movement,
resizing, and the grid label. Reusing it keeps the one-click control, grid chip, keyboard
command, and audio clip editor's shared setting consistent without a new preference.

**Alternatives considered**:
- Add a separate control-specific setting: rejected because it could disagree with the
  actual snapping behavior and the existing grid chip.
- Route the button through a new command abstraction: rejected because the existing grid
  settings event and toggle operation already provide the required behavior.

## Decision: Keep the grid chip and add a neighboring toggle

The existing Piano Roll header grid chip displays the step and opens a menu; the menu
already includes “Snap to grid.” Add a dedicated Snap-glyph toggle beside it rather than
changing the chip's click behavior.

**Rationale**: Preserves existing grid menu discovery and provides the requested direct
one-click control.

**Alternatives considered**:
- Change the grid chip into an on/off toggle: rejected because it would remove or complicate
  its current menu action.
- Add the control to the Arrangement or audio editor: outside the requested Piano Roll
  scope; their shared setting will still reflect changes through existing controls.

## Decision: Use a managed headless UI regression check

Exercise the rendered Piano Roll header control and its setting synchronization in a
headless Avalonia harness; retain existing `SnapGridTests` for grid-state semantics.

**Rationale**: A pure grid unit test cannot prove that the UI control is present, toggles,
and follows external changes.

**Alternatives considered**:
- Rely only on manual inspection: rejected as insufficient regression coverage.
- Modify engine smoke tests: rejected because no engine or real-time behavior changes.
