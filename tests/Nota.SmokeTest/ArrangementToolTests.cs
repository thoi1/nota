// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Application.Editing;
using Nota.Infrastructure;

namespace Nota.SmokeTest;

internal static class ArrangementToolTests
{
    private static int ClipCount(NotaEngine engine, int track)
    {
        int count = 0;
        while (engine.TryGetClipInfo(track, count, out _)) count++;
        return count;
    }

    public static IEnumerable<(bool ok, string label)> Run()
    {
        var occupied = new[] { (Start: 0.0, Length: 2.0), (Start: 8.0, Length: 4.0) };
        yield return (ArrangementDrawing.ClipInterval(4, 20, 1, occupied) == (4, 4), "Pencil stops at the next clip");
        yield return (ArrangementDrawing.ClipInterval(4, -5, 1, occupied) == (2, 2), "reverse Pencil stops at the preceding clip");
        yield return (ArrangementDrawing.ClipInterval(1, 5, 1, occupied) is null, "Pencil cannot overwrite an occupied clip");
        yield return (ArrangementDrawing.ClipInterval(4, 4, 0.25, occupied) == (4, 0.25), "Pencil click creates one grid cell");
        var odd = ArrangementDrawing.SampleBeats(0, 6, 2.0 / 3, 3);
        yield return (odd.Contains(3) && odd.Contains(6) && odd.All(b => Math.Abs(SnapGrid.SnapBeat(b, 2.0 / 3, 3) - b) < 1e-8),
            "drawing shares triplet grid restarts in odd meters");
        var huge = ArrangementDrawing.SampleBeats(0, 100000, 2.0 / 3, 3);
        yield return (huge.Length <= ArrangementDrawing.MaxStrokePoints && huge.SkipLast(1).All(b => Math.Abs(SnapGrid.SnapBeat(b, 2.0 / 3, 3) - b) < 1e-8),
            "long wave sweeps are bounded and retain the original grid");
        yield return (ArrangementDrawing.SampleBeats(6, 0, 0.25, 4).SequenceEqual(ArrangementDrawing.SampleBeats(0, 6, 0.25, 4)),
            "reverse drawing covers the same beat interval");
        yield return (ArrangementDrawing.SampleBeats(double.NaN, 1, 1, 4).Length == 0, "invalid pointer positions cannot create points");
        yield return (Math.Abs(ArrangementDrawing.Wave(AutomationWaveform.Sine, 0.25) - 1) < 1e-8
            && ArrangementDrawing.Wave(AutomationWaveform.Triangle, 0.5) == 1
            && ArrangementDrawing.Wave(AutomationWaveform.Sawtooth, 0) == -1
            && ArrangementDrawing.Wave(AutomationWaveform.Square, 0.75) == -1,
            "wave shapes have the expected peaks, slope and polarity");
        foreach (var shape in Enum.GetValues<AutomationWaveform>().Where(s => s != AutomationWaveform.Freehand))
        {
            var wave = ArrangementDrawing.WavePoints(shape, 0, 8, 0, 0.25, 4, -1, 1, 0.9, 1, 4, 0.25);
            yield return (wave.Length == 33 && wave.All(p => p.Value >= -1 && p.Value <= 1), $"{shape}: points clamp to lane range");
        }
        var half = ArrangementDrawing.WavePoints(AutomationWaveform.Sine, 0, 4, 0, 0.25, 4, 0, 2, 1, 0.5, 4, 0.25);
        yield return (Math.Abs(half[0].Value - 1.5f) < 1e-6 && Math.Abs(half[8].Value - 0.5f) < 1e-6,
            "amplitude, cycle length and phase affect generated points");
        var original = new[] { new AutomationPoint(0, 0, 0.4f), new AutomationPoint(2, 1), new AutomationPoint(10, 0.5f, -0.7f) };
        var replacement = ArrangementDrawing.ReplaceRange(original, half, 4, 0);
        yield return (replacement[^1].Equals(original[^1]) && replacement.Count(p => p.Beat == 2) == 1,
            "drawing preserves outside points/curves and replaces duplicate beats");

        using var engine = new NotaEngine();
        int track = engine.AddInstrumentTrack();
        int index = engine.AddMidiClip(track, 4, 2);
        yield return (index >= 0 && ClipCount(engine, track) == 1 && engine.Undo() && ClipCount(engine, track) == 0
            && engine.Redo() && ClipCount(engine, track) == 1, "Pencil clip creation uses native undo/redo");
        yield return (engine.TryDeleteClip(track, index) && engine.Undo() && ClipCount(engine, track) == 1
            && engine.Redo() && ClipCount(engine, track) == 0, "Eraser clip deletion uses native undo/redo");
        string audioPath = Path.Combine(Path.GetTempPath(), "nota-tool-erase-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            WavWriter.WriteSine(audioPath, 0.2, 440, 48000);
            int audio = engine.AddAudioTrack();
            int audioClip = engine.AddAudioClip(audio, audioPath, 0);
            yield return (audioClip >= 0 && engine.TryDeleteClip(audio, audioClip) && engine.Undo()
                && engine.TryGetAudioClipInfo(audio, audioClip, out _) && engine.Redo()
                && !engine.TryGetAudioClipInfo(audio, audioClip, out _), "audio Eraser deletion restores the original clip with undo/redo");
        }
        finally { File.Delete(audioPath); }
        foreach (int target in new[] { track, engine.AddReturnTrack() })
        {
            engine.BeginUndoGroup();
            int lane;
            try
            {
                lane = engine.AddAutomationLane(target, AutomationTarget.Volume, -1, -1);
                engine.SetAutomationPoints(target, lane, half);
            }
            finally { engine.EndUndoGroup(); }
            yield return (engine.GetAutomationPoints(target, lane).Length == half.Length && engine.Undo()
                && engine.AutomationLaneCount(target) == 0 && engine.Redo()
                && engine.GetAutomationPoints(target, lane).Length == half.Length,
                "first automation stroke and lane creation are one undo step on tracks/returns");
        }
        engine.SetMasterVolumeAutomation(half);
        yield return (engine.Undo() && engine.GetMasterVolumeAutomation().Length == 0 && engine.Redo()
            && engine.GetMasterVolumeAutomation().Length == half.Length, "master drawing uses native undo/redo");
    }
}
