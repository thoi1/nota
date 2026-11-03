# Validation

1. Build `dotnet build Nota.sln -c Release --nologo`; run `pwsh scripts/build-win.ps1` from a VS developer environment.
2. In Arrangement choose each tool twice: exactly one stays selected. Hover each: exact English names. Check narrow window access and theme selection/hover consistency.
3. Pencil click/drag empty instrument space (both directions), grid on/off/triplets and near neighboring clips; verify no overlap. Undo/redo. Audio/group space creates no MIDI.
4. Eraser clicks MIDI/audio clips; undo/redo; verify eraser cursor. Select restores normal move/trim/loop and Alt-drag duplication. Right-click menus, wheel/zoom and ruler loop/navigation remain.
5. Show automation. Pencil draw, Eraser remove points; choose each waveform in the secondary menu and sweep a track/return/master lane. Change amplitude/period/phase; check values, untouched outside points, reverse drags, one undo/redo. Freehand follows the pointer.
6. Escape/deactivate mid-draw: no data mutation. Change tool: appropriate cursor, no stale previews. Playback refresh must not replace the preview.
7. Nota Warp reports the missing tempo-grid engine and makes no timing/audio changes. Piano-roll CC lanes remain separate.
