# Tempo interfaces

Pure C ABI POD points/anchors and complete map setter/getter, bounds, beat/seconds conversion
and BPM query. Reject nonfinite, unsorted, duplicate, missing-zero, out-of-range, protected
anchor conflicts and excessive resources. No native exceptions cross boundary. Managed owns
no clock. Graph undo is one gesture; initial point cannot move/delete.
Top BPM edits initial point for constant map, current position for multi-point map.
Grid follows audio fits explicit source-frame/beat relationships to the same map and disables
reference stretching. Audio follows grid retains musical markers and prepares stretched cache.
Locked absolute-time pairs constrain candidate edits; source audio is never overwritten.
Pointer: double-click adds; click/Shift-click select; blank drag box; point drag moves tick/BPM;
Shift fine; Alt-click/Delete remove except initial; arrows nudge; Escape cancels. Ctrl/Cmd C/V
apply only with tempo focus. Numeric inspector shares validation; outgoing context Step/Linear/
Smooth. Bounds and anchor unlock/workflow actions are explicit.

Native contract implemented in `include/nota/nota_engine.h`:

- `NotaTempoPoint`: tick (int64), BPM (double), outgoing curve (int32), reserved (int32), 24 bytes.
- `NotaTempoAnchor`: tick, seconds, locked/track/clip/reserved, source frame, durable reference
  ID (int64), 48 bytes. Track/clip fields are locator hints; reference ID survives reindex/split.
- Complete map replace accepts points, BPM limits, anchors and `checkpoint`. User edits use
  checkpoint=true; project baseline restoration uses false. Validation precedes publication.
- List getters return total count, including for capacity zero; managed code owns copies.
- `nota_tempo_seconds_at_beat`, inverse, BPM and revision expose the authoritative map.
  Conversion queries retain an atomic shared snapshot on nonaudio callers. Callback reads
  the Graph-owned raw map through Transport, with no owning atomic or reclamation.
- Source mode fit and protection are one graph transaction. Reference restore assigns saved
  identity without refitting the saved map; copied references become independent natural audio.
