// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Infrastructure;
using Nota.Presentation;

namespace Nota.SmokeTest;
internal static class TempoMapTests
{
    public static IEnumerable<(bool, string)> Run()
    {
        using var e = new NotaEngine();
        e.SetBpm(135);
        yield return (e.GetTempoMap().Points is [{ Tick: 0, Bpm: 135 }], "tempo map: legacy constant BPM before command drain");
        var step = new TempoMapState(new[] { new TempoPoint(0, 120), new TempoPoint(3840, 60), new TempoPoint(7680, 180) }, Array.Empty<TempoAnchor>());
        yield return (e.TrySetTempoMap(step, out _), "tempo map: native map transaction accepted");
        yield return (Math.Abs(e.SecondsAtBeat(8) - 6) < 1e-10 && Math.Abs(e.BeatAtSeconds(6) - 8) < 1e-10, "tempo map: multiple steps use accumulated time");
        yield return (e.Undo() && e.GetTempoMap().Points.Length == 1 && e.GetTempoMap().Points[0].Bpm == 135
            && e.Redo() && e.GetTempoMap().Points.SequenceEqual(step.Points), "tempo map: graph undo/redo restores the complete map");
        foreach (int rate in new[] { 44100, 48000, 96000 })
        {
            e.SetLoop(false, 0, 0); e.Stop(); e.Seek(3); e.Play();
            var output = new float[rate * 2];
            e.RenderOffline(output, rate, rate);
            double expected = e.BeatAtSeconds(e.SecondsAtBeat(3) + 1);
            yield return (Math.Abs(e.PositionBeats - expected) < 1e-8, $"tempo map: transport crosses a step at {rate} Hz");
            e.Stop(); e.SetLoop(true, 3, 5); e.Seek(3); e.Play();
            int frames = rate * 2;
            e.RenderOffline(new float[frames * 2], frames, rate);
            double duration = e.SecondsAtBeat(5) - e.SecondsAtBeat(3);
            expected = e.BeatAtSeconds(e.SecondsAtBeat(3) + 2 % duration);
            yield return (Math.Abs(e.PositionBeats - expected) * rate < 1, $"tempo map: loop across step has no accumulated drift at {rate} Hz");
        }
        e.Stop(); e.SetLoop(false, 0, 0);
        var ramp = step with { Points = new[] { new TempoPoint(0, 60, TempoCurve.Linear), new TempoPoint(3840, 180) } };
        yield return (e.TrySetTempoMap(ramp, out _) && Math.Abs(e.SecondsAtBeat(4) - 2 * Math.Log(3)) < 1e-10,
            "tempo map: linear accelerando analytical duration");
        double error = 0;
        for (int i = 0; i < 10000; i++) { double beat = i * 1.44; error = Math.Max(error, Math.Abs(e.BeatAtSeconds(e.SecondsAtBeat(beat)) - beat)); }
        yield return (error < 1e-8, "tempo map: two-hour forward/inverse round trips");
        var smooth = ramp with { Points = new[] { new TempoPoint(0, 180, TempoCurve.Smooth), new TempoPoint(7680, 60) } };
        yield return (e.TrySetTempoMap(smooth, out _) && e.TempoAtBeat(4) == 120 && e.SecondsAtBeat(8) > e.SecondsAtBeat(4), "tempo map: smooth ritardando stays positive and monotone");
        var locked = step with { Anchors = new[] { new TempoAnchor(3840, 2, 1) } };
        e.TrySetTempoMap(locked, out _); e.ClearUndoHistory();
        var invalid = locked with { Points = new[] { new TempoPoint(0, 140), new TempoPoint(3840, 60), new TempoPoint(7680, 180) } };
        yield return (!e.TrySetTempoMap(invalid, out var reason) && reason.Contains("protected")
            && !e.CanUndo && e.GetTempoMap().Points.SequenceEqual(locked.Points), "tempo map: protected absolute anchor rejects atomically without undo");
        yield return (!e.TrySetTempoMap(step with { Points = new[] { new TempoPoint(1, 120) } }, out _)
            && !e.TrySetTempoMap(step with { Points = new[] { new TempoPoint(0, double.NaN) } }, out _), "tempo map: invalid initial/nonfinite points rejected");
        var vm = new TransportViewModel(e); vm.RestoreBpm(60);
        yield return (e.GetTempoMap().Points.SequenceEqual(locked.Points), "tempo map: restoring UI BPM never overwrites automation");
        using (var gesture = new NotaEngine())
        {
            gesture.TrySetTempoMap(ramp, out _); gesture.Seek(1); gesture.RenderOffline(new float[128], 64, 44100);
            gesture.ClearUndoHistory();
            var transportVm = new TransportViewModel(gesture);
            transportVm.BeginBpmGesture(); transportVm.Bpm = 130;
            gesture.Seek(2); gesture.RenderOffline(new float[128], 64, 44100); transportVm.Bpm = 140; transportVm.EndBpmGesture();
            yield return (gesture.GetTempoMap().Points is [_, { Tick: 960, Bpm: 140 }, { Tick: 3840 }]
                && gesture.Undo() && gesture.GetTempoMap().Points.SequenceEqual(ramp.Points) && !gesture.CanUndo,
                "tempo map: top transport BPM gesture latches insertion beat and preserves other points in one undo");
        }
        var warnings = new List<string>();
        var doc = ProjectService.Capture(e, new TransportState(120, 1, false, false, 4, 4), warnings, contentNames: false);
        using var restored = new NotaEngine();
        ProjectService.Apply(doc, restored, Path.GetTempPath());
        yield return (restored.GetTempoMap().Points.SequenceEqual(locked.Points) && restored.GetTempoMap().Anchors.SequenceEqual(locked.Anchors), "tempo map: project capture/apply retains points and anchors");
        using (var compatibility = new NotaEngine())
            yield return (compatibility.MidiCtlSelfTest() == 0, "tempo map: existing controller scheduling self-test stays compatible");
        string wave = Path.Combine(Path.GetTempPath(), $"nota-grid-reference-{Guid.NewGuid():N}.wav");
        try
        {
            WavWriter.WriteSine(wave, 2, 440, 44100);
            foreach (var timing in new[] { step, ramp, smooth })
            {
                using var midi = new NotaEngine(); midi.RenderOffline(new float[128], 64, 44100);
                midi.TrySetTempoMap(timing, out _);
                int sampler = midi.AddSamplerTrack(wave, 60);
                double origin = timing == step ? 5 : 1;
                double local = timing == step ? 1 : 2;
                int notes = midi.AddMidiClip(sampler, origin, 8);
                midi.SetClipNotes(sampler, notes, new[] { new NotaNote(60, local, 0.5, 0.8f) });
                midi.Stop(); midi.Seek(0); midi.Play();
                int expectedFrame = (int)Math.Floor(midi.SecondsAtBeat(origin + local) * 44100);
                var signal = new float[(expectedFrame + 256) * 2];
                midi.RenderOffline(signal, signal.Length / 2, 44100);
                int first = Array.FindIndex(signal, x => Math.Abs(x) > 1e-8) / 2;
                yield return (Math.Abs(first - expectedFrame) <= 1,
                    $"tempo map: actual MIDI onset at nonzero clip origin through {timing.Points[0].Curve} (sample {first}, expected {expectedFrame})");
                midi.SetMidiClipController(sampler, notes, MidiController.Sustain, new[] { new AutomationPoint(local, 1f) });
                midi.SendTrackController(sampler, MidiController.Sustain, 0);
                midi.StopTransport(); midi.Seek(0); midi.Play();
                midi.RenderOffline(new float[Math.Max(1, expectedFrame - 1) * 2], expectedFrame - 1, 44100);
                float pedalBefore = midi.DeliveredController(sampler, MidiController.Sustain);
                midi.RenderOffline(new float[6], 3);
                yield return (pedalBefore == 0 && midi.DeliveredController(sampler, MidiController.Sustain) == 1,
                    $"tempo map: recorded controller event crosses its mapped sample through {timing.Points[0].Curve}");
                long bounce = midi.BeginBounceRange(sampler, 2, 5);
                long expectedBounce = (long)Math.Ceiling((midi.SecondsAtBeat(5) - midi.SecondsAtBeat(2)) * 44100);
                yield return (bounce == expectedBounce, "tempo map: nonzero-origin bounce uses mapped interval duration");
                midi.CancelFreeze();
            }
            using var grid = new NotaEngine(); grid.RenderOffline(new float[128], 64, 44100);
            int track = grid.AddAudioTrack(), clip = grid.AddAudioClip(track, wave, 0);
            grid.TryGetAudioClipInfo(track, clip, out var sourceInfo);
            var sourceBefore = grid.ReadSample(sourceInfo.SampleId);
            grid.SetClipWarpMarkers(track, clip, new double[] { 0, 22050, 88200 }, new double[] { 0, 2, 4 });
            yield return (grid.TrySetClipTempoMode(track, clip, true, false, out _)
                && grid.ClipGridFollowsAudio(track, clip) && grid.WarpBuildRemaining == 0
                && Math.Abs(grid.SecondsAtBeat(2) - 0.5) < 1e-8 && Math.Abs(grid.SecondsAtBeat(4) - 2) < 1e-8,
                "tempo map: Grid follows audio fits explicit source anchors into the shared map");
            grid.ClearUndoHistory();
            var beforeAudioWarpEdit = grid.GetTempoMap();
            grid.SetClipWarpMarkers(track, clip, new double[] { 0, 22050, 88200 }, new double[] { 0, 1, 4 });
            var afterAudioWarpEdit = grid.GetTempoMap();
            bool audioWarpEditUnlinks = !grid.ClipGridFollowsAudio(track, clip)
                && afterAudioWarpEdit.Points.SequenceEqual(beforeAudioWarpEdit.Points)
                && afterAudioWarpEdit.Anchors.All(a => a.TrackId != track || a.ClipIndex != clip);
            yield return (audioWarpEditUnlinks && grid.Undo()
                && grid.ClipGridFollowsAudio(track, clip)
                && grid.GetTempoMap().Points.SequenceEqual(beforeAudioWarpEdit.Points)
                && !grid.CanUndo && grid.Redo()
                && !grid.ClipGridFollowsAudio(track, clip)
                && grid.GetTempoMap().Points.SequenceEqual(afterAudioWarpEdit.Points),
                "tempo map: Audio follows grid marker edits unlink Grid Warp without changing tempo");
            yield return (grid.TrySetClipTempoMode(track, clip, true, true, out _), "tempo map: explicit timing-anchor protection");
            var lockedMap = grid.GetTempoMap();
            long revision = grid.TempoRevision;
            var conflictingMap = lockedMap with { Points = lockedMap.Points.ToArray() };
            conflictingMap.Points[0] = conflictingMap.Points[0] with { Bpm = conflictingMap.Points[0].Bpm + 1 };
            bool rejected = !grid.TrySetTempoMap(conflictingMap, out _);
            yield return (rejected && grid.TempoRevision == revision
                && grid.GetTempoMap().Points.SequenceEqual(lockedMap.Points),
                "tempo map: locked warp anchor rejects tempo edits without partial map publication");
            grid.TrySetClipTempoMode(track, clip, true, false, out _);
            var editable = grid.GetTempoMap();
            editable.Points[0] = editable.Points[0] with { Bpm = 140 };
            yield return (grid.TrySetTempoMap(editable, out _) && grid.ReadSample(sourceInfo.SampleId).SequenceEqual(sourceBefore),
                "tempo map: unlocked grid edits preserve the source samples");
            var saved = ProjectService.Capture(grid, new TransportState(120, 1, false, false), new List<string>(), contentNames: false);
            yield return (saved.Tracks[0].AudioClips[0].GridFollowsAudio && saved.TempoMap!.Anchors.All(a => a.TrackId == 0),
                "tempo map: grid relationship and anchor track indices are captured");
            yield return (grid.TrySetClipTempoMode(track, clip, false, false, out _) && !grid.ClipGridFollowsAudio(track, clip)
                && grid.GetTempoMap().Anchors.Length == 0 && grid.WarpBuildRemaining > 0,
                "tempo map: Audio follows grid retains the same clock and defers stretching");
            using var indexed = new NotaEngine();
            indexed.RenderOffline(new float[128], 64, 44100);
            int indexedTrack = indexed.AddAudioTrack();
            indexed.AddAudioClip(indexedTrack, wave, 16);
            int referenceClip = indexed.AddAudioClip(indexedTrack, wave, 0);
            indexed.SetClipWarpMarkers(indexedTrack, referenceClip, new double[] { 0, 44100, 88200 }, new double[] { 0, 2, 4 });
            indexed.TrySetClipTempoMode(indexedTrack, referenceClip, true, false, out _);
            long identity = indexed.ClipTempoReferenceId(indexedTrack, referenceClip);
            indexed.DeleteClip(indexedTrack, 0);
            var reindexed = indexed.GetTempoMap();
            yield return (identity > 0 && indexed.ClipTempoReferenceId(indexedTrack, 0) == identity
                && reindexed.Anchors.All(a => a.ReferenceId == identity && a.ClipIndex == 0),
                "tempo map: source relationships survive earlier clip deletion and reindexing");
            reindexed.Points[0] = reindexed.Points[0] with { Bpm = 140 };
            yield return (indexed.TrySetTempoMap(reindexed, out _) && indexed.ClipGridFollowsAudio(indexedTrack, 0),
                "tempo map: reindexed reference refreshes from its durable identity");
            string bundle = Path.Combine(Path.GetTempPath(), $"nota-tempo-roundtrip-{Guid.NewGuid():N}.nota");
            try
            {
                var snapshot = ProjectService.Capture(indexed, new TransportState(120, 1, false, false), new List<string>());
                ProjectService.Save(snapshot, bundle, indexed);
                using var reload = new NotaEngine();
                reload.RenderOffline(new float[128], 64, 44100);
                ProjectService.Apply(ProjectService.Load(bundle), reload, bundle);
                reload.TryGetTrackInfo(0, out var loadedTrack);
                var loadedMap = reload.GetTempoMap();
                yield return (loadedMap.Points.SequenceEqual(indexed.GetTempoMap().Points)
                    && loadedMap.Anchors.All(a => a.TrackId == loadedTrack.Id && a.ReferenceId == identity)
                    && reload.ClipTempoReferenceId(loadedTrack.Id, 0) == identity
                    && reload.ClipGridFollowsAudio(loadedTrack.Id, 0),
                    "tempo map: actual bundle save/reload restores reference IDs and anchors");
                int right = reload.SplitClip(loadedTrack.Id, 0, 2);
                yield return (right == 1 && reload.ClipTempoReferenceId(loadedTrack.Id, right) == identity
                    && reload.ClipGridFollowsAudio(loadedTrack.Id, 0), "tempo map: reference split preserves shared source identity");
                yield return (reload.TrySetClipTempoMode(loadedTrack.Id, right, false, false, out _)
                    && reload.GetTempoMap().Anchors.Length > 0, "tempo map: detaching a reference fragment retains its sibling anchors");
            }
            finally { if (Directory.Exists(bundle)) Directory.Delete(bundle, true); }

            using var frozenEngine = new NotaEngine();
            frozenEngine.RenderOffline(new float[128], 64, 44100);
            frozenEngine.TrySetTempoMap(ramp, out _);
            int frozenTrack = frozenEngine.AddAudioTrack();
            frozenEngine.AddAudioClip(frozenTrack, wave, 0);
            float[] Capture(NotaEngine engine, int count)
            {
                engine.StopTransport(); engine.Seek(0); engine.Play();
                var data = new float[count * 2]; engine.RenderOffline(data, count); engine.StopTransport();
                return data;
            }
            var live = Capture(frozenEngine, 44100);
            bool frozenOk = new TrackFreezer().Freeze(frozenEngine, frozenTrack, 4, false, false);
            var frozenAudio = Capture(frozenEngine, 44100);
            double maxDifference = live.Zip(frozenAudio, (a, b) => (double)Math.Abs(a - b)).Max();
            yield return (frozenOk && live.Any(x => Math.Abs(x) > 0.01) && maxDifference < 1e-5,
                $"tempo map: variable-tempo freeze equals live samples (max difference {maxDifference:G3})");
            var blob = frozenEngine.GetFreezeState(frozenTrack);
            frozenEngine.UnfreezeTrack(frozenTrack); frozenEngine.SetFreezeState(frozenTrack, blob);
            var restoredFreeze = Capture(frozenEngine, 44100);
            yield return (restoredFreeze.SequenceEqual(frozenAudio), "tempo map: serialized freeze retains its captured tempo clock");

            string exported = Path.Combine(Path.GetTempPath(), $"nota-tempo-export-{Guid.NewGuid():N}.wav");
            try
            {
                new AudioExporter().ExportMaster(frozenEngine, new ExportRequest(exported, 3, 999, 48000,
                    WavBitDepth.Float32, false, false, StartBeats: 1));
                long expected = (long)Math.Ceiling((frozenEngine.SecondsAtBeat(4) - frozenEngine.SecondsAtBeat(1)) * 48000);
                using var exportedEngine = new NotaEngine();
                int exportedTrack = exportedEngine.AddAudioTrack(), exportedClip = exportedEngine.AddAudioClip(exportedTrack, exported, 0);
                exportedEngine.TryGetAudioClipInfo(exportedTrack, exportedClip, out var exportInfo);
                long exportedFrames = exportedEngine.ReadSample(exportInfo.SampleId).Length / 2;
                yield return (Math.Abs(exportedFrames - expected) <= 1,
                    "tempo map: actual nonzero-origin 48 kHz export ignores stale scalar request BPM");
            }
            finally { File.Delete(exported); }

            using var record = new NotaEngine(); record.TrySetTempoMap(smooth, out _);
            bool arrangementRecord = record.AudioRecordSelfTest();
            record.TryGetTrackInfo(0, out var recordedTrack); record.TryGetClipInfo(recordedTrack.Id, 0, out var recordedClip);
            yield return (arrangementRecord && Math.Abs(record.SecondsAtBeat(recordedClip.StartBeat + recordedClip.LengthBeats)
                    - record.SecondsAtBeat(recordedClip.StartBeat) - 0.1) < 1.0 / 44100 && record.SessionAudioRecordSelfTest(),
                "tempo map: simulated Arrangement and Session input record/replay with a smooth map");

            float[] NaturalSession(TempoMapState? map)
            {
                using var session = new NotaEngine(); session.RenderOffline(new float[128], 64, 44100);
                if (map is not null) session.TrySetTempoMap(map, out _);
                int sessionTrack = session.AddAudioTrack();
                session.AddSessionAudioClip(sessionTrack, 0, wave, 16, 0, 0, 1);
                session.SetLaunchQuant(0); session.Seek(0); session.LaunchSlot(sessionTrack, 0); session.Play();
                var data = new float[88200 * 2]; session.RenderOffline(data, 88200); return data;
            }
            var naturalReference = NaturalSession(null);
            var naturalMapped = NaturalSession(ramp);
            double naturalError = naturalMapped.Zip(naturalReference, (a, b) => (double)Math.Abs(a - b)).Max();
            yield return (naturalError < 1e-5,
                $"tempo map: natural Session audio preserves source seconds through a ramp (max difference {naturalError:G3})");

            using var warped = new NotaEngine(); warped.RenderOffline(new float[128], 64, 44100);
            warped.TrySetTempoMap(ramp, out _);
            int warpTrack = warped.AddAudioTrack(), warpClip = warped.AddAudioClip(warpTrack, wave, 1);
            warped.SetClipWarp(warpTrack, warpClip, true, 3); warped.SetClipWarpLength(warpTrack, warpClip, 4);
            int finish = (int)Math.Ceiling(warped.SecondsAtBeat(5) * 44100);
            foreach (bool reverse in new[] { false, true })
            {
                warped.SetClipReverse(warpTrack, warpClip, reverse);
                var signal = Capture(warped, finish + 2048);
                int begin = (int)Math.Ceiling(warped.SecondsAtBeat(1) * 44100);
                double Frequency(int start)
                {
                    int crossings = 0, size = 11025;
                    for (int i = start + 1; i < start + size; i++)
                        if (signal[(i - 1) * 2] <= 0 && signal[i * 2] > 0) crossings++;
                    return crossings * 44100.0 / size;
                }
                double early = Frequency(begin + 11025), late = Frequency(finish - 22050);
                int firstSound = Array.FindIndex(signal, x => Math.Abs(x) > 1e-8) / 2;
                int lastSound = Array.FindLastIndex(signal, x => Math.Abs(x) > 1e-8) / 2;
                yield return (Math.Abs(early - 440) <= 12 && Math.Abs(late - 440) <= 12
                    && signal.Take(Math.Max(0, begin - 1) * 2).All(x => Math.Abs(x) < 1e-8)
                    && signal.Skip((finish + 128) * 2).All(x => Math.Abs(x) < 1e-8),
                    $"tempo map: {(reverse ? "reverse" : "forward")} warped sine retains pitch and mapped bounds ({early:F0}/{late:F0} Hz, frames {firstSound}..{lastSound}, expected {begin}..{finish})");
            }

            var dense = new TempoMapState(Enumerable.Range(0, 10000).Select(i => new TempoPoint(i * 960L, 100 + i % 80)).ToArray(), Array.Empty<TempoAnchor>());
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool denseOk = record.TrySetTempoMap(dense, out _); watch.Stop();
            yield return (denseOk && watch.ElapsedMilliseconds < 50,
                $"tempo map: 10,000-point native transaction takes {watch.Elapsed.TotalMilliseconds:F2} ms");
        }
        finally { File.Delete(wave); }
    }
}
