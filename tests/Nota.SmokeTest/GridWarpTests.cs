// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Application.Editing;
using Nota.Application.Analysis;
using Nota.Infrastructure;

namespace Nota.SmokeTest;
internal static class GridWarpTests
{
    public static IEnumerable<(bool, string)> Run()
    {
        foreach (var result in RunMappingTests()) yield return result;

        string wave = Path.Combine(Path.GetTempPath(), $"nota-grid-warp-{Guid.NewGuid():N}.wav");
        string bundle = Path.Combine(Path.GetTempPath(), $"nota-grid-warp-{Guid.NewGuid():N}.nota");
        try
        {
            WavWriter.WriteSine(wave, 4, 440, 44100);
            using var e = new NotaEngine();
            e.RenderOffline(new float[128], 64, 44100);
            int track = e.AddAudioTrack(), clip = e.AddAudioClip(track, wave, 0);
            e.SetClipWarpMarkers(track, clip,
                [0, 44100, 110250, 176400], [0, 1, 2.5, 4]);
            e.TryGetAudioClipInfo(track, clip, out var clipInfo);
            float[] sourceBefore = e.ReadSample(clipInfo.SampleId);
            var audioMarkersBefore = ClipWarpMarkers.Read(e, track, clip);
            float[] playbackBefore = RenderReferenceAudio(e);
            var map = CreateMap(track, clip);

            var originalMap = e.GetTempoMap();
            e.ClearUndoHistory();
            bool committed = e.TryCommitGridWarp(track, clip, map, out var commitError);
            yield return (committed, $"grid warp: valid tempo/anchor/reference transaction commits ({commitError})");
            if (!committed) yield break;
            var audioMarkersAfter = ClipWarpMarkers.Read(e, track, clip);
            yield return (audioMarkersBefore.Source.SequenceEqual(audioMarkersAfter.Source)
                && audioMarkersBefore.Beats.SequenceEqual(audioMarkersAfter.Beats),
                "grid warp: committing grid timing preserves original audio-warp source and beat coordinates");

            var committedMap = e.GetTempoMap();
            yield return (committedMap.Anchors.All(a => a.AnchorId > 0)
                && committedMap.Anchors.Select(a => a.AnchorId).Distinct().Count() == committedMap.Anchors.Length
                && committedMap.Anchors.All(a => committedMap.Points.Any(p => p.LinkedAnchorId == a.AnchorId)),
                "grid warp: committed anchors have stable IDs and explicit tempo links");
            yield return (committedMap.Points.Select(p => p with { LinkedAnchorId = 0 }).SequenceEqual(map.Points)
                && committedMap.Anchors.Select(a => a with { ReferenceId = 0, AnchorId = 0 }).SequenceEqual(map.Anchors)
                && Math.Abs(e.TempoAtBeat(2.5) - 40) < 1e-10
                && Math.Abs(e.TempoAtBeat(4.5) - 120) < 1e-10,
                "grid warp: absolute anchor intervals derive the expected step BPM events");
            yield return (e.ClipGridFollowsAudio(track, clip)
                && e.ReadSample(clipInfo.SampleId).SequenceEqual(sourceBefore)
                && e.WarpBuildRemaining == 0,
                "grid warp: enabling the reference preserves source samples and never builds a stretch cache");

            float[] playbackAfter = RenderReferenceAudio(e);
            bool undo = e.Undo() && !e.ClipGridFollowsAudio(track, clip)
                && e.GetTempoMap().Points.SequenceEqual(originalMap.Points)
                && e.GetTempoMap().Anchors.SequenceEqual(originalMap.Anchors);
            bool redo = e.Redo() && e.ClipGridFollowsAudio(track, clip)
                && e.GetTempoMap().Points.SequenceEqual(committedMap.Points)
                && e.GetTempoMap().Anchors.SequenceEqual(committedMap.Anchors);
            yield return (undo && redo, "grid warp: tempo points, anchors, and reference undo and redo as one action");
            yield return (playbackBefore.SequenceEqual(playbackAfter),
                "grid warp: source audio playback samples are unchanged by tempo mapping");

            var saved = ProjectService.Capture(e,
                new TransportState(120, 1, false, false), new List<string>());
            string manifestBeforeCache = ProjectService.SerializeManifest(saved);
            e.TransientAnalysis.RememberOriginalBpm(clipInfo.SampleId, 97.5);
            e.TransientAnalysis.RestoreOriginalTiming(clipInfo.SampleId, new(44100, 0, 176400,
                new(97.5, 40, 240), new[] { new SourceBeat(0, 0, 1, true), new SourceBeat(88200, 3.25, 1, true), new SourceBeat(176400, 6.5, 1, true) }));
            yield return (ProjectService.SerializeManifest(ProjectService.Capture(e, new TransportState(120, 1, false, false), new List<string>())) == manifestBeforeCache,
                "source timing: derived original tempo cache never dirties the authoring manifest");
            ProjectService.Save(saved, bundle, e);
            using var reload = new NotaEngine();
            reload.RenderOffline(new float[128], 64, 44100);
            ProjectService.Apply(ProjectService.Load(bundle), reload, bundle);
            reload.TryGetTrackInfo(0, out var restoredTrack);
            var restoredMap = reload.GetTempoMap();
            yield return (restoredMap.Points.SequenceEqual(committedMap.Points)
                && restoredMap.Anchors.Length == committedMap.Anchors.Length
                && restoredMap.Anchors.All(a => a.ReferenceId > 0 && a.TrackId == restoredTrack.Id)
                && reload.ClipGridFollowsAudio(restoredTrack.Id, clip)
                && reload.ClipTempoReferenceId(restoredTrack.Id, clip) > 0,
                "grid warp: project save/reload restores tempo events, anchors, and the clip reference");

            reload.TryGetAudioClipInfo(restoredTrack.Id, clip, out var loadedSource);
            yield return (reload.TransientAnalysis.OriginalBpm(loadedSource.SampleId) == 97.5
                && reload.TransientAnalysis.OriginalTiming(loadedSource.SampleId)?.Beats.Length == 3,
                "source timing: content-addressed original BPM and source beats survive disk reload");
            reload.ClearUndoHistory();
            yield return (reload.GetGridWarpBaseline(restoredTrack.Id, clip).Length > 0
                && reload.TryResetGridWarp(restoredTrack.Id, clip, out _)
                && !reload.ClipGridFollowsAudio(restoredTrack.Id, clip)
                && reload.GetTempoMap().Points.SequenceEqual(originalMap.Points)
                && reload.Undo() && reload.ClipGridFollowsAudio(restoredTrack.Id, clip) && !reload.CanUndo && reload.Redo(),
                "grid warp: persisted Reset baseline restores its region in one undo/redo action");

            var unrelatedAnchor = new TempoAnchor(7680, 5, 1, -1, -1, 0, 0);
            var withUnrelated = committedMap with
            {
                Points = committedMap.Points.Append(new TempoPoint(7680, 120)).ToArray(),
                Anchors = committedMap.Anchors.Append(unrelatedAnchor).ToArray()
            };
            bool unrelatedInstalled = e.TrySetTempoMap(withUnrelated, out var unrelatedError);
            yield return (unrelatedInstalled, $"grid warp Tempo Track fixture installs an unrelated fixed anchor ({unrelatedError})");
            if (unrelatedInstalled)
            {
                e.ClearUndoHistory();
                var beforeMove = e.GetTempoMap();
                int linkedIndex = Array.FindIndex(beforeMove.Points, p => p.Tick == 1920);
                bool movedCandidateValid = GridWarpMapping.TryMoveTempoPoint(e, beforeMove, linkedIndex,
                    2400, out var movedCandidate, out var moveError);
                bool movedCommitted = movedCandidateValid && e.TrySetTempoMap(movedCandidate, out moveError);
                var afterMove = e.GetTempoMap();
                yield return (movedCommitted && afterMove.Anchors.Any(a => a.ReferenceId == committedMap.Anchors[0].ReferenceId
                        && a.Tick == 2400 && Math.Abs(a.Seconds - 1) < 1e-10 && Math.Abs(a.SourceFrame - 44100) < 1e-8)
                    && afterMove.Anchors.First(a => a.Tick == 2400).AnchorId == beforeMove.Anchors.First(a => a.Tick == 1920).AnchorId
                    && afterMove.Anchors.Contains(unrelatedAnchor)
                    && Math.Abs(e.TempoAtBeat(1) - 150) < 1e-8
                    && Math.Abs(e.TempoAtBeat(2.6) - 20) < 1e-8,
                    $"grid warp Tempo Track move updates its anchor and tempo intervals, preserving unrelated anchors ({moveError})");
                bool moveUndo = movedCommitted && e.Undo()
                    && e.GetTempoMap().Points.SequenceEqual(beforeMove.Points)
                    && e.GetTempoMap().Anchors.SequenceEqual(beforeMove.Anchors);
                bool moveRedo = moveUndo && e.Redo()
                    && e.GetTempoMap().Points.SequenceEqual(afterMove.Points)
                    && e.GetTempoMap().Anchors.SequenceEqual(afterMove.Anchors);
                yield return (moveUndo && moveRedo, "grid warp Tempo Track move is one undo/redo action");

                int movedIndex = Array.FindIndex(afterMove.Points, p => p.Tick == 2400);
                bool conflictRejected = !GridWarpMapping.TrySetTempoPointBpm(afterMove, movedIndex,
                    afterMove.Points[movedIndex].Bpm + 1, out _, out _);
                yield return (conflictRejected, "grid warp Tempo Track rejects a conflicting BPM edit on a linked point");

                e.ClearUndoHistory();
                bool deleteCandidateValid = GridWarpMapping.TryDeleteTempoPoint(e, afterMove, movedIndex,
                    out var deletedCandidate, out var deleteError);
                bool deleteCommitted = deleteCandidateValid && e.TrySetTempoMap(deletedCandidate, out deleteError);
                var afterDelete = e.GetTempoMap();
                yield return (deleteCommitted && afterDelete.Points.All(p => p.Tick != 2400)
                    && afterDelete.Anchors.All(a => a.Tick != 2400)
                    && afterDelete.Anchors.Contains(unrelatedAnchor),
                    $"grid warp Tempo Track delete removes the linked anchor and preserves unrelated anchors ({deleteError})");
                bool deleteUndo = deleteCommitted && e.Undo()
                    && e.GetTempoMap().Points.SequenceEqual(afterMove.Points)
                    && e.GetTempoMap().Anchors.SequenceEqual(afterMove.Anchors);
                bool deleteRedo = deleteUndo && e.Redo()
                    && e.GetTempoMap().Points.SequenceEqual(afterDelete.Points)
                    && e.GetTempoMap().Anchors.SequenceEqual(afterDelete.Anchors);
                yield return (deleteUndo && deleteRedo, "grid warp Tempo Track delete is one undo/redo action");

                e.ClearUndoHistory();
                var beforeAudioWarpMap = e.GetTempoMap();
                float[] beforeAudioWarpPlayback = RenderReferenceAudio(e);
                var markerSources = new double[16];
                var markerBeats = new double[16];
                int markerCount = e.GetClipWarpMarkers(track, clip, markerSources, markerBeats);
                var editedBeats = markerBeats[..markerCount];
                if (markerCount > 2) editedBeats[1] += 0.25;
                e.SetClipWarpMarkers(track, clip, markerSources[..markerCount], editedBeats);
                var afterAudioWarpMap = e.GetTempoMap();
                float[] afterAudioWarpPlayback = RenderReferenceAudio(e);
                bool audioWarpIsolated = !e.ClipGridFollowsAudio(track, clip)
                    && e.ClipTempoReferenceId(track, clip) == 0
                    && afterAudioWarpMap.Points.Select(p => p with { LinkedAnchorId = 0 }).SequenceEqual(beforeAudioWarpMap.Points.Select(p => p with { LinkedAnchorId = 0 }))
                    && afterAudioWarpMap.Anchors.Contains(unrelatedAnchor)
                    && afterAudioWarpMap.Anchors.All(a => a.ReferenceId != committedMap.Anchors[0].ReferenceId)
                    && e.ReadSample(clipInfo.SampleId).SequenceEqual(sourceBefore)
                    && !beforeAudioWarpPlayback.SequenceEqual(afterAudioWarpPlayback)
                    && e.WarpBuildRemaining == 0;
                yield return (audioWarpIsolated,
                    "Audio follows grid stretches the clip, unlinks its Grid Warp anchors, and preserves tempo events and source samples");
                bool audioUndo = audioWarpIsolated && e.Undo()
                    && e.ClipGridFollowsAudio(track, clip)
                    && e.GetTempoMap().Points.SequenceEqual(beforeAudioWarpMap.Points)
                    && e.GetTempoMap().Anchors.SequenceEqual(beforeAudioWarpMap.Anchors);
                bool audioRedo = audioUndo && e.Redo()
                    && !e.ClipGridFollowsAudio(track, clip)
                    && e.GetTempoMap().Points.SequenceEqual(afterAudioWarpMap.Points)
                    && e.GetTempoMap().Anchors.SequenceEqual(afterAudioWarpMap.Anchors);
                yield return (audioUndo && audioRedo, "Audio follows grid edit unlinks Grid Warp in one undo/redo action");
            }

            using (var trimmed = new NotaEngine())
            {
                trimmed.RenderOffline(new float[128], 64, 44100);
                int tt = trimmed.AddAudioTrack(), tc = trimmed.AddAudioClip(tt, wave, 4);
                trimmed.AutoWarpClipAtBpm(tt, tc, 90); trimmed.SetClipWarpTrim(tt, tc, 1, 5);
                var geometry = trimmed.GetGridReferenceGeometry(tt, tc)!;
                var audioMarkers = ClipWarpMarkers.Read(trimmed, tt, tc);
                var anchors = new[] {
                    new TempoAnchor(3840, geometry.OriginSeconds, 1, tt, tc, 0, geometry.SourceStart),
                    new TempoAnchor(4800, geometry.OriginSeconds + 0.85, 1, tt, tc, 0, geometry.SourceStart + 0.85 * 44100),
                    new TempoAnchor(5760, geometry.OriginSeconds + 1.62, 1, tt, tc, 0, geometry.SourceStart + 1.62 * 44100) };
                bool built = GridWarpMapping.TryBuildMap(trimmed, trimmed.GetTempoMap(), anchors, tt, tc, 0, false, out var mapped, out _);
                yield return (built && trimmed.TryCommitGridWarp(tt, tc, mapped, out _)
                    && trimmed.GetGridReferenceGeometry(tt, tc) == geometry
                    && Math.Abs(trimmed.SecondsAtBeat(5) - geometry.OriginSeconds - 0.85) < 1e-10
                    && Math.Abs(trimmed.SecondsAtBeat(6) - geometry.OriginSeconds - 1.62) < 1e-10
                    && ClipWarpMarkers.Read(trimmed, tt, tc).Beats.SequenceEqual(audioMarkers.Beats),
                    "grid warp: trimmed nonzero-origin source maps 70.588235/77.922078 BPM without changing audio markers or placement");
                var protectedMap = trimmed.GetTempoMap();
                var external = new TempoAnchor(11520, trimmed.SecondsAtBeat(12), 1);
                trimmed.TrySetTempoMap(protectedMap with { Anchors = protectedMap.Anchors.Append(external).ToArray() }, out _);
                trimmed.ClearUndoHistory();
                yield return (!trimmed.TryResetGridWarp(tt, tc, out _) && !trimmed.CanUndo,
                    "grid warp: Reset rejects an external fixed-time conflict without creating undo");
                trimmed.TrySetTempoMap(protectedMap with { Points = protectedMap.Points.Append(new TempoPoint(19200, 140)).ToArray() }, out _);
                yield return (trimmed.TryResetGridWarp(tt, tc, out _)
                    && trimmed.GetTempoMap().Points.Any(p => p.Tick == 19200 && p.Bpm == 140),
                    "grid warp: Reset preserves unrelated tempo events outside its region");
            }
            using (var identity = new NotaEngine())
            {
                identity.RenderOffline(new float[128], 64, 44100);
                int it = identity.AddAudioTrack(), ic = identity.AddAudioClip(it, wave, 0);
                identity.TryCommitGridWarp(it, ic, CreateMap(it, ic), out _);
                long reference = identity.ClipTempoReferenceId(it, ic);
                var initial = identity.GetTempoMap();
                var extra = initial.Anchors.Append(new TempoAnchor(3840, 3, 1, it, ic, 0, 132300, reference)).ToArray();
                GridWarpMapping.TryBuildMap(identity, initial, extra, it, ic, reference, false, out var withExtra, out _);
                identity.TryCommitGridWarp(it, ic, withExtra, out _);
                var beforeDelete = identity.GetTempoMap();
                int retiredId = beforeDelete.Anchors.Single(a => a.Tick == 3840).AnchorId;
                GridWarpMapping.TryDeleteTempoPoint(identity, beforeDelete, Array.FindIndex(beforeDelete.Points, p => p.Tick == 3840), out var deleted, out _);
                identity.TrySetTempoMap(deleted, out _);
                var remaining = identity.GetTempoMap();
                var replacement = remaining.Anchors.Append(new TempoAnchor(4320, 3.25, 1, it, ic, 0, 143325, reference)).ToArray();
                GridWarpMapping.TryBuildMap(identity, remaining, replacement, it, ic, reference, false, out var withReplacement, out _);
                yield return (identity.TryCommitGridWarp(it, ic, withReplacement, out _)
                    && identity.GetTempoMap().Anchors.Single(a => a.Tick == 4320).AnchorId > retiredId,
                    "grid warp: deleted anchor IDs are never reused for a different anchor");
            }
            using (var reversed = new NotaEngine())
            {
                reversed.RenderOffline(new float[128], 64, 44100);
                int rt = reversed.AddAudioTrack(), rc = reversed.AddAudioClip(rt, wave, 0);
                reversed.SetClipReverse(rt, rc, true);
                var before = RenderReferenceAudio(reversed);
                var reverseAnchors = CreateMap(rt, rc).Anchors.Select(a => a with { SourceFrame = 176400 - a.SourceFrame }).ToArray();
                GridWarpMapping.TryBuildMap(reversed, reversed.GetTempoMap(), reverseAnchors, rt, rc, 0, true, out var reverseMap, out _);
                yield return (reversed.TryCommitGridWarp(rt, rc, reverseMap, out _) && RenderReferenceAudio(reversed).SequenceEqual(before),
                    "grid warp: reversed original playback stays sample-identical across tempo boundaries");
            }
            using (var pitched = new NotaEngine())
            {
                int pt = pitched.AddAudioTrack(), pc = pitched.AddAudioClip(pt, wave, 0);
                pitched.SetClipPitch(pt, pc, 1); pitched.ClearUndoHistory();
                yield return (!pitched.TryCommitGridWarp(pt, pc, CreateMap(pt, pc), out var pitchError)
                    && pitchError.Contains("pitch") && !pitched.CanUndo,
                    "grid warp: original-speed fitting rejects non-neutral pitch atomically");
            }
            using (var seam = new NotaEngine())
            {
                seam.RenderOffline(new float[128], 64, 44100);
                seam.TrySetTempoMap(new(new[] { new TempoPoint(0, 120, TempoCurve.Linear), new TempoPoint(7680, 180) }, Array.Empty<TempoAnchor>()), out _);
                int st = seam.AddAudioTrack(), sc = seam.AddAudioClip(st, wave, 2);
                double origin = seam.SecondsAtBeat(2);
                var anchors = new[] { new TempoAnchor(1920, origin, 1, st, sc), new TempoAnchor(5760, origin + 4, 1, st, sc, 0, 176400) };
                GridWarpMapping.TryBuildMap(seam, seam.GetTempoMap(), anchors, st, sc, 0, false, out var candidate, out _);
                var original = seam.GetTempoMap(); seam.ClearUndoHistory();
                yield return (!seam.TryCommitGridWarp(st, sc, candidate, out var seamError) && seamError.Contains("Step")
                    && seam.GetTempoMap().Points.SequenceEqual(original.Points) && !seam.CanUndo,
                    "grid warp: unsupported ramp seams reject instead of changing unrelated curve shape");
            }

            using var invalid = new NotaEngine();
            invalid.RenderOffline(new float[128], 64, 44100);
            int invalidTrack = invalid.AddAudioTrack(), invalidClip = invalid.AddAudioClip(invalidTrack, wave, 0);
            invalid.SetClipWarpMarkers(invalidTrack, invalidClip,
                [0, 44100, 110250, 176400], [0, 1, 2.5, 4]);
            var invalidOriginal = invalid.GetTempoMap();
            invalid.ClearUndoHistory();
            var outOfRangeAnchors = Reindex(map.Anchors, invalidTrack, invalidClip);
            outOfRangeAnchors[1] = new TempoAnchor(1920, 0.25, 1, invalidTrack, invalidClip, 0, 11025);
            var outOfRange = map with
            {
                Points =
                [
                    new TempoPoint(0, 480),
                    new TempoPoint(1920, 60 / 2.25),
                    new TempoPoint(2880, 120),
                    new TempoPoint(5760, 120)
                ],
                Anchors = outOfRangeAnchors
            };
            bool rejectedRange = !invalid.TryCommitGridWarp(invalidTrack, invalidClip, outOfRange, out var rangeError);
            bool rangeAtomic = rejectedRange && !string.IsNullOrEmpty(rangeError) && !invalid.CanUndo
                && !invalid.ClipGridFollowsAudio(invalidTrack, invalidClip)
                && invalid.GetTempoMap().Points.SequenceEqual(invalidOriginal.Points)
                && invalid.GetTempoMap().Anchors.SequenceEqual(invalidOriginal.Anchors);
            yield return (rangeAtomic, "grid warp: out-of-range BPM is rejected without partial state or undo");

            var crossingAnchors = Reindex(map.Anchors, invalidTrack, invalidClip);
            crossingAnchors[2] = new TempoAnchor(5760, 2.5, 1, invalidTrack, invalidClip, 0, 110250);
            crossingAnchors[3] = new TempoAnchor(2880, 4, 1, invalidTrack, invalidClip, 0, 176400);
            var crossing = map with { Anchors = crossingAnchors };
            bool rejectedCrossing = !invalid.TryCommitGridWarp(invalidTrack, invalidClip, crossing, out var crossingError);
            yield return (rejectedCrossing && !string.IsNullOrEmpty(crossingError) && !invalid.CanUndo
                && !invalid.ClipGridFollowsAudio(invalidTrack, invalidClip)
                && invalid.GetTempoMap().Points.SequenceEqual(invalidOriginal.Points)
                && invalid.GetTempoMap().Anchors.SequenceEqual(invalidOriginal.Anchors),
                "grid warp: crossing absolute-time anchors are rejected atomically");
        }
        finally
        {
            if (File.Exists(wave)) File.Delete(wave);
            if (Directory.Exists(bundle)) Directory.Delete(bundle, recursive: true);
        }
    }

    private static IEnumerable<(bool, string)> RunMappingTests()
    {
        var commonTime = GridWarpMapping.GridLines(0, 3.1, 4, 4);
        yield return (commonTime.Select(line => line.Tick).SequenceEqual(new long[] { 0, 960, 1920, 2880 })
            && commonTime[0].IsBar && commonTime[^1].BeatInBar == 4,
            "grid warp mapping: 4/4 bar and beat grid uses quarter-note ticks");
        var compoundTime = GridWarpMapping.GridLines(0, 3, 6, 8);
        yield return (compoundTime.Select(line => line.Tick).SequenceEqual(
                new long[] { 0, 480, 960, 1440, 1920, 2400, 2880 })
            && compoundTime[0].IsBar && compoundTime[^1].IsBar && compoundTime[5].BeatInBar == 6,
            "grid warp mapping: 6/8 uses eighth-note pulses and a three-quarter-note bar");

        using var engine = new NotaEngine();
        var map = new TempoMapState(
            [new TempoPoint(0, 120), new TempoPoint(1920, 60)],
            Array.Empty<TempoAnchor>());
        engine.TrySetTempoMap(map, out _);
        var reference = new TempoAnchor(1920, 1, 1, 0, 0, 0, 44100);
        double sourceAtBeatFour = GridWarpMapping.SourceFrameAtBeat(engine, 4, reference, 44100);
        double beatAtSource = GridWarpMapping.BeatAtSourceFrame(engine, sourceAtBeatFour, reference, 44100);
        yield return (Math.Abs(sourceAtBeatFour - 132300) < 1e-8 && Math.Abs(beatAtSource - 4) < 1e-10,
            "grid warp mapping: non-constant tempo uses the authoritative beat/seconds clock");
        var reverseReference = reference with { SourceFrame = 176400 };
        double reversedSource = GridWarpMapping.SourceFrameAtBeat(engine, 4, reverseReference, 44100, reversed: true);
        yield return (Math.Abs(reversedSource - 88200) < 1e-8
            && Math.Abs(GridWarpMapping.BeatAtSourceFrame(engine, reversedSource, reverseReference, 44100, reversed: true) - 4) < 1e-10,
            "grid warp mapping: reversed source coordinates round-trip without changing musical time");

        var start = new TempoAnchor(0, 0, 1, 0, 0, 0, 0);
        var middle = new TempoAnchor(1920, 1, 1, 0, 0, 0, 44100);
        yield return (GridWarpMapping.TryCalculateBpm(start, middle, false, 20, 300, out double bpm)
            && Math.Abs(bpm - 120) < 1e-10,
            "grid warp mapping: tempo derives from absolute interval seconds");
        yield return (!GridWarpMapping.TryCalculateBpm(middle,
                middle with { Tick = 2880, Seconds = 1 }, false, 20, 300, out _)
            && !GridWarpMapping.TryCalculateBpm(start,
                middle with { Seconds = 0.25, SourceFrame = 11025 }, false, 20, 300, out _)
            && !GridWarpMapping.TryCalculateBpm(middle,
                middle with { Tick = 2880, Seconds = 1.5, SourceFrame = 40000 }, false, 20, 300, out _),
            "grid warp mapping: zero-time, out-of-range BPM, and crossing anchors are rejected");
    }

    private static TempoMapState CreateMap(int track, int clip) => new(
        [
            new TempoPoint(0, 120),
            new TempoPoint(1920, 40),
            new TempoPoint(2880, 120),
            new TempoPoint(5760, 120)
        ],
        [
            new TempoAnchor(0, 0, 1, track, clip, 0, 0),
            new TempoAnchor(1920, 1, 1, track, clip, 0, 44100),
            new TempoAnchor(2880, 2.5, 1, track, clip, 0, 110250),
            new TempoAnchor(5760, 4, 1, track, clip, 0, 176400)
        ]);

    private static TempoAnchor[] Reindex(TempoAnchor[] anchors, int track, int clip) =>
        anchors.Select(a => a with { TrackId = track, ClipIndex = clip }).ToArray();

    private static float[] RenderReferenceAudio(NotaEngine engine)
    {
        engine.StopTransport();
        engine.Seek(0);
        engine.Play();
        var result = new float[132300 * 2];
        engine.RenderOffline(result, 132300, 44100);
        engine.StopTransport();
        return result;
    }
}
