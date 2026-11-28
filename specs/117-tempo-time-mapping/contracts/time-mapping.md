# Time Mapping Contract

- Hold Ctrl over an eligible boundary in the master Tempo lane for the contextual
  cursor. Capture the pointer only once the gesture is claimed. Other targets
  and ordinary point/curve editing follow the existing paths.
- The gesture uses the pre-edit screen-to-time conversion and snap targets.
  Draw grid, automation and guide from the latest valid preview. Show absolute
  position and the resulting BPM at the mapped boundary, using existing typography.
- Drag bounds are strictly inside neighboring anchors and respect 20–300 BPM.
  Preview returns the accepted time and BPM, including when a target is constrained.
- Start requires a valid boundary and an available engine edit slot. Update
  prepares a detached candidate for display without writing history or retiming
  playback. Finish accepts or cancels the edit, and is idempotent for an
  already-closed handle. An unrelated graph publication invalidates the handle.
  Cancel on Escape, capture loss, lane change and project replacement.
- Commit records one undo entry including all timing changes. Undo/redo restore
  audio timing references together with tempo. No additional permanent controls
  or MCP commands are introduced.
