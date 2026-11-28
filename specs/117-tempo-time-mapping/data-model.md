# Data Model: Interactive Tempo Time Mapping

- **Boundary**: Selected musical beat, finite and greater than zero. Choose from
  the lane's visible beat/bar grid at gesture start.
- **Anchor**: A neighboring tempo knot or the song start, with finite musical
  beat and frozen absolute seconds. Anchors must remain ordered. A committed
  mapped boundary becomes a tempo knot and can constrain later gestures.
- **Candidate**: A master Tempo point list. Preserve original points and curve
  sides outside the affected interval. BPM must stay within 20–300, point
  positions must remain nonnegative and sorted, and the existing tempo point
  limit applies. Same-beat pairs represent incoming/outgoing BPM.
- **Edit session**: Start graph, selected boundary, adjacent anchors, drag-start
  axis and last valid candidate. Idle → preview → commit or cancel. Restore
  the starting graph when cancelled. A no-op creates no undo entry.
- **Audio timing reference**: Original absolute clip start/end and, for warped
  playback, the render timing associated with its established source-to-time
  mapping. The previously authored warp markers remain available to the editor.
