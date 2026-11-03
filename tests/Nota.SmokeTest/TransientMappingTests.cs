// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Infrastructure;

namespace Nota.SmokeTest;

internal static class TransientMappingTests
{
    public static IEnumerable<(bool, string)> Run()
    {
        var source = new NotaSampleInfo { Channels = 2, Frames = 96000, SampleRate = 48000 };
        var clip = new NotaAudioClipInfo { StartBeat = 16, SourceOffsetFrames = 24000, LengthFrames = 48000 };
        var normal = new TransientClipMap(clip, source, 4);
        yield return (normal.TryArrangementBeat(48000, out double beat) && beat == 18
            && normal.SourceAtArrangementBeat(beat) == 48000, "transient mapping accounts for source offset and trim");
        yield return (!normal.TryArrangementBeat(1000, out _) && !normal.TryArrangementBeat(95000, out _),
            "transient mapping hides source attacks outside the played window");
        var faster = new TransientClipMap(clip, source, 2);
        yield return (faster.TryArrangementBeat(48000, out beat) && beat == 17,
            "transient mapping follows native varispeed length without deriving another clock");
        clip.Reversed = 1;
        var reverse = new TransientClipMap(clip, source, 4);
        yield return (reverse.TryArrangementBeat(36000, out beat) && beat == 19
            && reverse.SourceAtArrangementBeat(beat) == 36000, "reversed transient mapping is invertible in playing order");
        clip.StartBeat = 40;
        var moved = new TransientClipMap(clip, source, 4);
        yield return (moved.TryArrangementBeat(36000, out beat) && beat == 43, "moving a clip changes placement, not source identity");
        clip.WarpEnabled = 1; clip.WarpBeats = 8; clip.WarpPlayStart = 2; clip.WarpPlayEnd = 6;
        double[] src = [0, 24000, 72000, 96000], beats = [0, 2, 5, 8];
        var warped = new TransientClipMap(clip, source, 4, src, beats);
        bool roundTrip = true;
        for (double s = 24000; s < 80000; s += 113.73)
            if (warped.TryArrangementBeat(s, out beat)) roundTrip &= Math.Abs(warped.SourceAtArrangementBeat(beat) - s) < 1e-6;
        yield return (roundTrip && warped.TryArrangementBeat(72000, out beat) && beat == 41,
            "piecewise warped/reversed/trimmed positions round-trip to sub-sample precision");
        src[1] = 99000;
        yield return (warped.TryArrangementBeat(72000, out beat) && beat == 41,
            "transient mapping snapshot owns immutable warp metadata");
        yield return (!new TransientClipMap(clip, source, 4, src, beats).Valid,
            "crossing source anchors invalidate mapping rather than inventing guide positions");
        clip.WarpEnabled = 0; clip.Reversed = 0; clip.SourceOffsetFrames = -12000; clip.LengthFrames = 48000;
        var padded = new TransientClipMap(clip, source, 4);
        yield return (padded.TryArrangementBeat(0, out beat) && beat == 41 && !padded.TryArrangementBeat(-1, out _),
            "extended unwarped regions preserve leading silence and reject negative source positions");
        foreach (var result in NativeEditing()) yield return result;
    }

    private static List<(bool, string)> NativeEditing()
    {
        var checks = new List<(bool, string)>();
        string folder = Path.Combine(Path.GetTempPath(), "nota-warp-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var engine = new NotaEngine();
            var fixture = TransientAnalysisTests.LoadFixture(engine, folder, 44100,
                TransientAnalysisTests.Attacks(44100, [.2, .6, 1.0, 1.4], true));
            var snapshot = engine.TransientAnalysis.AnalyzeAudioAsync(fixture.SourceId).GetAwaiter().GetResult();
            var original = engine.ReadSample(fixture.SourceId);
            engine.SetClipWarp(fixture.Track, fixture.Clip, true, 3);
            double[] src = [0, snapshot.Transients[1].SamplePosition, 88200], beats = [0, 1, 4];
            engine.SetClipWarpMarkers(fixture.Track, fixture.Clip, src, beats);
            double[] gotSrc = new double[16], gotBeat = new double[16];
            int Count() => engine.GetClipWarpMarkers(fixture.Track, fixture.Clip, gotSrc, gotBeat);
            checks.Add((Count() == 3 && gotSrc[1] == src[1] && gotBeat[1] == 1,
                "selected attack becomes one warp anchor, remaining attacks stay unquantised"));
            checks.Add((engine.Undo() && Count() == 2 && engine.Redo() && Count() == 3,
                "transient anchor edit uses existing native undo/redo"));
            bool rejected = true;
            foreach (var bad in new[] { new[] { 0.0, 5.0, 4.0 }, new[] { 0.0, double.NaN, 4.0 }, new[] { 0.0, 0.0, 4.0 } })
            {
                try { engine.SetClipWarpMarkers(fixture.Track, fixture.Clip, src, bad); rejected = false; }
                catch (NotaEngineException) { }
            }
            try { engine.SetClipWarpMarkers(fixture.Track, fixture.Clip, [0, 0, 88200], beats); rejected = false; }
            catch (NotaEngineException) { }
            checks.Add((rejected && Count() == 3 && gotSrc[1] == src[1] && gotBeat[1] == 1,
                "crossing, duplicate and non-finite anchors are rejected without changing the graph"));
            checks.Add((engine.Undo() && Count() == 2 && engine.Redo() && Count() == 3,
                "invalid anchor requests add no undo step"));
            engine.SetClipWarpTrim(fixture.Track, fixture.Clip, .5, 3.5);
            checks.Add((engine.ReadSample(fixture.SourceId).SequenceEqual(original), "warp and trim preserve original source samples"));
            var warnings = new List<string>();
            string bundle = Path.Combine(folder, "warp.nota");
            ProjectService.Save(ProjectService.Capture(engine, new TransportState(120, 0, false, false), warnings), bundle, engine);
            using var reopened = new NotaEngine();
            warnings.AddRange(ProjectService.Apply(ProjectService.Load(bundle), reopened, bundle));
            int track = -1;
            for (int i = 0; i < reopened.TrackCount; i++)
                if (reopened.TryGetTrackInfo(i, out var info) && info.Type == 0) { track = info.Id; break; }
            int n = reopened.GetClipWarpMarkers(track, 0, gotSrc, gotBeat);
            reopened.TryGetAudioClipInfo(track, 0, out var saved);
            checks.Add((warnings.Count == 0 && n == 3 && gotSrc[1] == src[1] && gotBeat[1] == 1
                && saved.WarpPlayStart == .5 && saved.WarpPlayEnd == 3.5,
                "transient warp anchors and trim survive existing project save/load"));
            checks.Add((reopened.ReadSample(saved.SampleId).SequenceEqual(original), "saved warped clip retains sample-exact source audio"));
            var layout = new TransientGuideLayout(snapshot, new TransientClipMap(saved,
                new NotaSampleInfo { Frames = 88200, SampleRate = 44100, Channels = 1 }, 3, gotSrc[..n], gotBeat[..n]));
            checks.Add((layout.Fractions.Count > 0 && layout.Fractions.All(f => f is >= 0 and <= 1)
                && layout.Fractions.SequenceEqual(layout.Fractions.Order()),
                "cached Arrangement guide layout includes only attacks inside the trim"));
            var manySrc = Enumerable.Range(0, 141).Select(i => i * 88200.0 / 140).ToArray();
            var manyBeat = Enumerable.Range(0, 141).Select(i => i * 4.0 / 140).ToArray();
            engine.SetDeferWarpBuild(true);
            try { engine.SetClipWarpMarkers(fixture.Track, fixture.Clip, manySrc, manyBeat); }
            finally { engine.SetDeferWarpBuild(false); }
            ProjectService.Save(ProjectService.Capture(engine, new TransportState(120, 0, false, false), new List<string>()), bundle, engine);
            using var longReopen = new NotaEngine();
            var longWarnings = ProjectService.Apply(ProjectService.Load(bundle), longReopen, bundle);
            int longTrack = -1;
            for (int i = 0; i < longReopen.TrackCount; i++)
                if (longReopen.TryGetTrackInfo(i, out var info) && info.Type == 0) { longTrack = info.Id; break; }
            var completeMarkers = ClipWarpMarkers.Read(longReopen, longTrack, 0);
            checks.Add((longWarnings.Count == 0 && completeMarkers.Source.SequenceEqual(manySrc)
                && completeMarkers.Beats.SequenceEqual(manyBeat),
                "141 selected warp anchors survive save/load without the previous 128-anchor truncation"));
        }
        finally { Directory.Delete(folder, recursive: true); }
        return checks;
    }
}
