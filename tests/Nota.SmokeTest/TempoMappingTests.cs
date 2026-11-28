// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Infrastructure;

namespace Nota.SmokeTest;

internal static class TempoMappingTests
{
    private const int SampleRate = 48000;
    private static bool SameTime(double a, double b) => Math.Abs(a - b) <= 1.0 / SampleRate;
    private static float[] Render(NotaEngine engine)
    {
        const int frames = SampleRate * 7;
        var pcm = new float[frames * 2];
        engine.Seek(0);
        engine.Play();
        engine.RenderOffline(pcm, frames, SampleRate);
        engine.StopTransport();
        return pcm;
    }

    public static IEnumerable<(bool, string)> Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"nota-time-mapping-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "source.wav");
            WavWriter.WriteStereo(input, 2, SampleRate, i =>
            {
                double v = i > 24000 && i < 36000 ? .3 * Math.Sin(2 * Math.PI * 220 * i / SampleRate) : 0;
                return (v, v);
            });
            using var e = new NotaEngine();
            e.SetBpm(120);
            e.RenderOffline(new float[2], 1, SampleRate);
            int lane = e.AddAutomationLane(e.MasterTrackId, AutomationTarget.Tempo, -1, -1);
            e.SetAutomationPoints(e.MasterTrackId, lane, [new(0, 120, .2f), new(4, 100, -.3f), new(12, 90)]);
            int audio = e.AddAudioTrack();
            e.AddAudioClip(audio, input, 5);
            e.AddAudioClip(audio, input, 8);
            e.SetClipWarp(audio, 0, true, 5);
            e.SetClipWarpLength(audio, 0, 2);
            e.SetClipReverse(audio, 0, true);
            int midi = e.AddInstrumentTrack();
            int midiClip = e.AddMidiClip(midi, 4, 8);
            e.SetClipNotes(midi, midiClip, [new NotaNote(60, 4, .25, 1)]);
            e.SetClipActive(midi, midiClip, false);
            double left = e.BeatToSeconds(4), right = e.BeatToSeconds(12), before = e.BeatToSeconds(8);
            e.TryGetClipInfo(audio, 0, out var original);
            double start = e.BeatToSeconds(original.StartBeat);
            double end = e.BeatToSeconds(original.StartBeat + original.LengthBeats);
            var originalPcm = Render(e);
            e.ClearUndoHistory();

            ulong cancelled = e.BeginTempoMapping(8);
            bool cancellation = cancelled != 0 && e.TryPreviewTempoMapping(cancelled, before + .2, out _) &&
                SameTime(e.BeatToSeconds(8), before) && e.FinishTempoMapping(cancelled, false) && !e.CanUndo;
            yield return (cancellation, "time mapping: detached preview and cancel leave playback and history unchanged");

            ulong stale = e.BeginTempoMapping(8);
            e.AddAudioTrack();
            yield return (stale != 0 && !e.TryPreviewTempoMapping(stale, before + .2, out _) &&
                !e.FinishTempoMapping(stale, true), "time mapping: another edit invalidates the preview");
            e.ClearUndoHistory();

            ulong handle = e.BeginTempoMapping(8);
            bool committed = handle != 0 && e.TryPreviewTempoMapping(handle, before + .2, out var preview) &&
                SameTime(preview.Seconds, before + .2) && e.FinishTempoMapping(handle, true);
            yield return (committed && SameTime(e.BeatToSeconds(8), before + .2) &&
                SameTime(e.BeatToSeconds(4), left) && SameTime(e.BeatToSeconds(12), right),
                "time mapping: commit moves the boundary while preserving both anchors");
            yield return (e.TryGetClipInfo(midi, midiClip, out var midiInfo) && midiInfo.StartBeat == 4 &&
                e.GetClipNotes(midi, midiClip).Single().StartBeat == 4 &&
                SameTime(e.BeatToSeconds(8), before + .2), "time mapping: MIDI notes keep musical positions and follow the new time");
            yield return (e.TryGetClipInfo(audio, 0, out var after) &&
                SameTime(e.BeatToSeconds(after.StartBeat), start) &&
                SameTime(e.BeatToSeconds(after.StartBeat + after.LengthBeats), end) &&
                e.TryGetRetainedAudioTiming(audio, 0, out var retained, out var points) && points.Length > 0 &&
                SameTime(retained.StartSeconds, start) && SameTime(retained.EndSeconds, end),
                "time mapping: warped clip keeps both physical edges");
            var mappedPcm = Render(e);
            yield return (originalPcm.Any(x => Math.Abs(x) > .01f) &&
                originalPcm.Zip(mappedPcm).Max(pair => Math.Abs(pair.First - pair.Second)) < .001f,
                "time mapping: warped and unwarped audio samples retain their relative and absolute timing");
            yield return (e.Undo() && !e.HasRetainedAudioTiming(audio, 0) && !e.CanUndo &&
                SameTime(e.BeatToSeconds(8), before) && e.Redo() && e.HasRetainedAudioTiming(audio, 0),
                "time mapping: one undo and redo covers tempo and audio timing");

            string project = Path.Combine(dir, "mapping.nota");
            var doc = ProjectService.Capture(e, new TransportState(120, 1, false, false), new List<string>());
            ProjectService.Save(doc, project, e);
            using var reopened = new NotaEngine();
            var warnings = ProjectService.Apply(ProjectService.Load(project), reopened, project);
            yield return (warnings.Count == 0 && reopened.TryGetTrackInfo(0, out var reopenedTrack) &&
                reopened.TryGetRetainedAudioTiming(reopenedTrack.Id, 0, out var restored, out var restoredPoints) &&
                restoredPoints.Length > 0 && SameTime(restored.StartSeconds, start) && SameTime(restored.EndSeconds, end),
                "time mapping: original warp timing survives save and reopen");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
