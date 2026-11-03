# Data model

TempoPoint: int64 tick (960/quarter), double bpm, int32 outgoing0Step/1Linear/2Smooth.
Initial tick0, subsequent distinct increasing ticks; BPM finite inside saved limits.
TempoMap: points, bounds, prepared monotone beat/seconds knots, revision and anchor constraints.
Last BPM extrapolates; negative times use initial BPM for count-in only.
TempoAnchor: tick, absolute seconds, protected flag and source relationship. A durable
reference ID identifies the source relationship across clip reindex/splits; track/clip
indices are refreshed locator hints. Copies detach from the reference. Ordinary edits
validate old protected pairs atomically; explicit unlock/reset is a separate action.
TempoTrackView: visible=true, expanded=false, expandedHeight140 clamped80–320; compact30.
Stored in track-view.json without engine undo. TempoEdit: original/candidate/selection,
commit once, cancellation/rejection leaves old map/history. Missing legacy data uses scalar BPM.
