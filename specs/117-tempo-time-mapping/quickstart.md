# Quickstart: Verify Interactive Tempo Time Mapping

Run `scripts/build-win.ps1` from a Visual Studio developer PowerShell on Windows,
or the repository build sibling on macOS/Linux. Open the managed app and prepare
an Arrangement with a natural-rate audio clip, a warped clip, a MIDI clip and
master Tempo points to both sides of the chosen boundary.

1. Display the master Tempo lane. Hold Ctrl and drag the boundary to a transient.
   Verify the cursor, guide, BPM, absolute position and moving grid during the drag.
2. Release and play the region. Compare audio onsets/endpoints and the two fixed
   anchor times against the original. Check that MIDI retains its beat positions.
3. Repeat with snap enabled, then disabled. Try crossing an anchor and dragging
   far enough to exceed the engine BPM range.
4. Undo once and redo once. Try Escape, capture loss and a click with no motion.
   Compare both the tempo curve and audio timing before/after each action.
5. Repeat from a flat tempo project, then with a curved segment and multiple edits.
   Save, reopen and compare the audio timing again.
6. Verify normal Tempo point editing, other automation lanes, seek and playhead
   drawing. Check Settings → Shortcuts for the new gesture.
