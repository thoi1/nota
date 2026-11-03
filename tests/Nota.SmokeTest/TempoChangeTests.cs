// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Diagnostics;
using Nota.Application;
using Nota.Infrastructure;
using Nota.Presentation;

namespace Nota.SmokeTest;

internal static class TempoChangeTests
{
    public static IEnumerable<(bool, string)> Run()
    {
        string file = Path.Combine(Path.GetTempPath(), $"nota-tempo-{Guid.NewGuid():N}.wav");
        try
        {
            WavWriter.WriteSine(file, 20, 440, 44100);
            using var e = new NotaEngine();
            e.RenderOffline(new float[128], 64, 44100);
            var clips = new List<(int Track, int Clip)>();
            for (int i = 0; i < 2; i++)
            {
                int t = e.AddAudioTrack(), c = e.AddAudioClip(t, file, 0);
                e.SetClipWarp(t, c, true, 3);
                clips.Add((t, c));
            }
            int session = e.AddAudioTrack(), slot = SessionClip.Index(0);
            e.AddSessionAudioFile(session, 0, file);
            e.SetClipWarp(session, slot, true, 3);
            clips.Add((session, slot));
            e.TryGetAudioClipInfo(clips[0].Track, clips[0].Clip, out var original);
            var source = new double[16]; var beats = new double[16];
            int markers = e.GetClipWarpMarkers(clips[0].Track, clips[0].Clip, source, beats);
            double length = original.WarpBeats;
            e.ClearUndoHistory();
            var vm = new TransportViewModel(e);
            var timer = Stopwatch.StartNew();
            vm.Bpm = 145;
            timer.Stop();
            long expected = 3 * (long)Math.Round(length * 44100 * 60 / 145);
            yield return (e.WarpBuildRemaining == expected,
                "tempo: transport VM defers all arrangement and Session caches before clock command drains");
            yield return (timer.ElapsedMilliseconds < 100,
                $"tempo: interactive setter avoids full-source DSP ({timer.Elapsed.TotalMilliseconds:F3} ms)");

            e.WarpBuildPoll(); // start an obsolete job, then replace its tempo repeatedly
            Thread.Sleep(10); // let DSP start, so cancellation covers a running job
            double slowestPoll = 0;
            foreach (int bpm in new[] { 90, 132, 110, 160 })
            {
                vm.Bpm = bpm;
                timer.Restart(); e.WarpBuildPoll(); timer.Stop();
                slowestPoll = Math.Max(slowestPoll, timer.Elapsed.TotalMilliseconds);
            }
            yield return (slowestPoll < 100, $"tempo: cancelling obsolete jobs returns promptly ({slowestPoll:F3} ms)");
            yield return (Finish(e) && e.WarpBuildRemaining == 0,
                "tempo: newest request converges without needing audio command drain");

            // Compare rendered samples, not display peaks (which come from the source).
            // Relaunch at zero so Arrangement + Session use the same phase each time.
            float[] Capture()
            {
                e.Stop(); e.Seek(0); e.SetLaunchQuant(0); e.LaunchSlot(session, 0); e.Play();
                var samples = new float[8192 * 2];
                e.RenderOffline(samples, 8192);
                return samples;
            }
            var actual = Capture();
            e.SetBpm(120); // force fresh synchronous builds, rather than reusing worker caches
            e.SetBpm(160);
            var reference = Capture();
            double difference = actual.Zip(reference, (a, b) => (double)Math.Abs(a - b)).Max();
            yield return (actual.Any(x => Math.Abs(x) > 0.01) && difference < 1e-5,
                $"tempo: Arrangement + Session audio equals fresh synchronous rebuild (max diff {difference:E1})");
            var sourceAfter = new double[16]; var beatsAfter = new double[16];
            e.TryGetAudioClipInfo(clips[0].Track, clips[0].Clip, out var after);
            int markerCount = e.GetClipWarpMarkers(clips[0].Track, clips[0].Clip, sourceAfter, beatsAfter);
            yield return (after.SampleId == original.SampleId && markerCount == markers
                && source.SequenceEqual(sourceAfter) && beats.SequenceEqual(beatsAfter),
                "tempo: rebuilding leaves source identity and warp anchors unchanged");
            yield return (!e.CanUndo, "tempo: background preparation adds no undo entries");

            e.SetBpmDeferred(90);
            e.WarpBuildPoll();
            Thread.Sleep(10);
            e.SetBpmDeferred(160); // previous cache already matches: only cancellation remains
            yield return (Finish(e) && e.WarpBuildRemaining == 0,
                "tempo: returning to a cached BPM reaps obsolete work without publishing it");

            e.SetBpmDeferred(95);
            e.SetDeferWarpBuild(true);
            e.Seek(0); e.Play();
            var audio = new float[8192 * 2];
            e.RenderOffline(audio, 8192);
            yield return (e.WarpBuildRemaining > 0 && audio.Any(x => Math.Abs(x) > 0.001),
                "tempo: explicit load deferral survives tempo edits and old audio stays audible");
            e.SetDeferWarpBuild(false);
            e.RenderOffline(audio, 8192);
            yield return (e.WarpBuildRemaining == 0 && Math.Abs(e.Bpm - 95) < 1e-8,
                "tempo: offline rendering completes a pending tempo edit before export");

            e.SetBpmDeferred(140);
            e.WarpBuildPoll();
            e.RenderOffline(audio, 8192, 48000);
            yield return (e.WarpBuildRemaining == 0 && audio.Any(x => Math.Abs(x) > 0.001),
                "tempo: export joins an active worker and prepares the export sample rate");

            bool rejected = false;
            try { e.SetBpmDeferred(double.NaN); }
            catch (NotaEngineException) { rejected = true; }
            yield return (rejected && e.WarpBuildRemaining == 0, "tempo: invalid deferred BPM is rejected without changing caches");
        }
        finally { File.Delete(file); }
    }

    private static bool Finish(NotaEngine e)
    {
        var timer = Stopwatch.StartNew();
        while (e.WarpBuildPoll() > 0)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(15)) return false;
            Thread.Sleep(1);
        }
        return true;
    }
}

