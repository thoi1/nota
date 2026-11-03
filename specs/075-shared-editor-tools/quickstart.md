# Validation

1. Build the Windows solution/smoke with existing scripts; use BaseOutputPath=bin/SharedTools/ while the older test process is open.
2. In Arrangement and docked/detached Piano Roll click all five tools, then press bare top-row 1–5. Check separate active modes, repeated 4 cycling/wrap, shared dropdown/icon and held-key suppression. Cmd/Ctrl+1–5 must still operate the grid; keypad unchanged.
3. Type numbers in text/numeric/value editors, command palette and modal/plugin windows. No tool changes. Click another editor, then the key targets that editor only.
4. Piano Pencil click/drag creates a bounded note at remembered length/velocity; Eraser removes editable notes. Undo/redo. Escape/tool change cancels an unfinished new preview. Check Select move/resize/selection and existing modifiers.
5. Add CC1 and CC11 lanes; draw all five shapes both directions at different grids/zoom. Check normalized values, local clip bounds, sorted points, unchanged other CCs/notes/outside points, one undo per gesture and cancellation. Locked/reference clips do not change.
6. Check Arrangement existing drawing/erasing/automation still works. Warp tints only the chosen editor's ruler, shows WARP/hover and restores on another tool, while explaining the missing tempo-map dependency and leaving musical/audio data untouched.
7. Inspect actual screenshots of toolbars, menus and rulers in dark/light themes and narrow/detached layouts. Review local diff against baseline. Do not commit or publish.
