# Feature Specification: Arrangement editing tools

**Created**: 2026-11-02  
**Status**: Ready for implementation  
**Input**: Add five mutually exclusive editing tools to the main Arrangement toolbar, reuse existing editing infrastructure, preserve local changes, and do not commit or publish.

## User Scenarios & Testing

### User Story 1 — Choose and use clip tools (Priority: P1)

A musician chooses Select Tool, Pencil Tool, Eraser Tool, Waveform Draw Tool or Nota Warp Tool from one group beside the existing editing controls.

**Why this priority**: Editing modes must be discoverable and unambiguous.

**Independent Test**: Select every tool, including the active tool again; exactly one stays selected. Draw a MIDI clip in empty instrument-track space with Pencil, erase a MIDI/audio clip with Eraser, and undo/redo each action.

**Acceptance Scenarios**:

1. Given the main Arrangement window, choosing a tool displays its selected state and its exact requested English tooltip and accessible name, using existing icon/button styling.
2. Given Pencil and an instrument track, dragging empty space creates a snapped MIDI clip bounded by neighboring clips; a click creates one grid cell. Existing clips are not overwritten.
3. Given Eraser, clicking a MIDI/audio clip deletes that clip with undo/redo. Alt + right-click erase is preserved wherever it exists.
4. Given Select, clip selection, move, Alt-drag duplication, looping/resizing and context menus keep their existing behavior. Ruler navigation, scrolling and zoom remain available in every tool.

### User Story 2 — Draw automation (Priority: P1)

A musician uses Pencil to draw an envelope, Eraser to remove points, or Waveform Draw to draw Sine, Triangle, Sawtooth, Square or Freehand curves in existing Arrangement automation lanes.

**Why this priority**: The tool selection must perform useful edits through existing lanes.

**Independent Test**: Show automation, draw all five shapes across a lane, undo/redo the entire gesture, and verify values stay within the lane range and points outside the edited interval survive.

**Acceptance Scenarios**:

1. Pencil adds/replaces points on the current snap grid; Eraser removes only a hit point. A drawing gesture is one undoable action.
2. A secondary menu selects all five waveform shapes and practical amplitude, cycle-length and phase settings. Dragging generates an envelope across the swept interval, including reverse drags; Freehand follows the pointer.
3. Track, return and master automation use their supported value ranges. No audio sample data is edited.
4. Arrangement MIDI CC drawing is included only if such lanes exist; the separate piano-roll CC system is not duplicated.

### User Story 3 — Select Nota Warp honestly (Priority: P2)

A musician selects the native horizontal warp-grid icon. If ruler-based tempo-map editing is absent, the tool clearly reports that it is unavailable and does not modify timing or audio.

**Independent Test**: Select Nota Warp and interact with the ruler; without the required engine, normal navigation remains and no tempo or audio changes occur.

**Acceptance Scenarios**:

1. If a shared tempo-map/Tempo Track engine supports ruler editing with audio fixed in absolute time, reuse it and transient data on the selected reference track.
2. Otherwise expose only selection state with a visible unavailable explanation; never substitute audio stretching for tempo-grid editing.

### Edge Cases

- Empty project, group/audio tracks under Pencil, occupied clip space, reverse/zero-width drag, grid disabled, pointer outside the starting row, and very long drags.
- Escape, window deactivation and tool changes cancel an uncommitted preview and restore the appropriate cursor.
- Right/middle buttons do not start drawing. Lane target menus and row-height seams retain priority.
- Narrow windows retain access to the tool group. Existing toolbar controls remain functional.

## Requirements

- **FR-001**: One inline toolbar group MUST expose exactly the five requested tools with vector icons, exact English tooltips, accessible names, mutually exclusive state and Select as default.
- **FR-002**: Pencil MUST create snapped, non-overlapping MIDI clips on instrument tracks and draw supported automation; Eraser MUST delete clicked MIDI/audio clips and hit automation points with an eraser cursor.
- **FR-003**: Waveform Draw MUST offer Sine, Triangle, Sawtooth, Square and Freehand with amplitude, cycle length and phase settings where practical; generated values MUST respect snapping and lane ranges.
- **FR-004**: Every edit MUST use existing undo/redo. Drawing gestures MUST be one undo step and retain data outside the swept interval.
- **FR-005**: Select behavior, Alt-drag duplication, existing erase shortcuts, clip trim/loop, ruler navigation, zoom, scroll and context menus MUST be preserved.
- **FR-006**: Nota Warp MUST reuse a suitable tempo-map engine if present; otherwise selection MUST clearly report the missing capability and perform no timing/sample edits.
- **FR-007**: Tool changes/cancellation MUST clear previews and restore cursors. Local changes MUST be preserved; no commit, push or PR.
- **FR-008**: Changelog, feature list, translations, shortcut reference and EN/RU manual MUST describe the implemented scope and dependencies truthfully.

### Key Entities

- Editing tool: one selected mode for this Arrangement view, independent of automation visibility.
- Waveform settings: shape, normalized amplitude, cycle length in beats and phase in cycles.
- Drawing gesture: starting lane/grid, anchor, original points and temporary replacement preview.

## Success Criteria

- **SC-001**: All five tools can be selected in the main window with exactly one selected at all times.
- **SC-002**: Clip create/delete and all five automation shapes survive undo and redo; cancelling a preview changes no project data.
- **SC-003**: Values remain within the target range, clip creation does not overlap neighbors, and existing interaction checks pass.
- **SC-004**: A Windows managed build and appropriate smoke/headless checks pass, or concrete environmental/test limitations are recorded.

## Assumptions

- Tool selection is transient UI state, not persisted project data. No new keyboard tool bindings are needed.
- Automation visibility remains the existing independent toggle; selecting a drawing tool does not silently hide clips.
- Waveform amplitude defaults to 100%, cycle length to one bar and phase to zero. A dropdown offers practical presets.
- Existing Arrangement automation covers track/return/master envelopes, not piano-roll MIDI CC lanes.
- Existing clip Warp is not a tempo-grid engine. UI-only Nota Warp is explicitly authorized when dependencies are absent.

## Nota Compliance Checklist

- [x] No new dependency; new source files carry the standard AGPL header.
- [x] UI-thread-only work; existing engine commands, pure C ABI and immutable graph behavior retained. No audio-thread allocations/locks/I/O introduced.
- [x] Studio tokens/Glyph system reused; requested exact tool names override sentence-case defaults for these names.
- [x] Build/smoke, documentation and convergence planned before completion.
