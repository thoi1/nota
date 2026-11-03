// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Application.Editing;
using Nota.Application.Palette;
using Nota.Infrastructure;

namespace Nota.SmokeTest;

internal static class SharedEditorToolTests
{
    public static IEnumerable<(bool ok, string label)> Run()
    {
        var wave = new SharedWaveformState();
        var arrangement = new EditorToolState(wave); var roll = new EditorToolState(wave);
        yield return (arrangement.Tool == ArrangementTool.Select && roll.Tool == ArrangementTool.Select,
            "shared tools: Select defaults in each editor");
        arrangement.Choose(ArrangementTool.Pencil); roll.Choose(ArrangementTool.Eraser);
        yield return (arrangement.Tool == ArrangementTool.Pencil && roll.Tool == ArrangementTool.Eraser,
            "shared tools: active tools remain independent");
        wave.Shape = AutomationWaveform.Triangle;
        roll.Choose(ArrangementTool.WaveformDraw, true);
        yield return (wave.Shape == AutomationWaveform.Triangle, "shared tools: first 4 restores the last shape without cycling");
        var expected = new[] { AutomationWaveform.Sawtooth, AutomationWaveform.Square, AutomationWaveform.Freehand, AutomationWaveform.Sine, AutomationWaveform.Triangle };
        bool cycle = true;
        foreach (var shape in expected) { roll.Choose(ArrangementTool.WaveformDraw, true); cycle &= wave.Shape == shape; }
        yield return (cycle, "shared tools: repeated 4 cycles exact order and wraps");
        roll.Choose(ArrangementTool.Select); roll.Choose(ArrangementTool.WaveformDraw, true);
        yield return (wave.Shape == AutomationWaveform.Triangle, "shared tools: another tool does not reset shape");
        int otherChanges = 0; arrangement.Changing += () => otherChanges++;
        wave.Shape = AutomationWaveform.Freehand;
        roll.Choose(ArrangementTool.WaveformDraw, true);
        yield return (wave.Shape == AutomationWaveform.Sine && arrangement.Waveform.Shape == AutomationWaveform.Sine && otherChanges == 0,
            "shared tools: dropdown/cycling share shape without interrupting the other editor");
        wave.Amplitude = 0.5; wave.CycleBars = 2; wave.Phase = 0.75;
        yield return (arrangement.Waveform.Amplitude == 0.5 && roll.Waveform.CycleBars == 2 && roll.Waveform.Phase == 0.75,
            "shared tools: drawing settings use the same authoritative state");
        var registry = new CommandRegistry();
        yield return (Enumerable.Range(1, 5).All(n => registry.Get(EditorToolState.CommandForNumber(n)!)?.Gesture == n.ToString()),
            "shared tools: 1–5 have registered commands and matching shortcut metadata");
        yield return (EditorToolState.CommandForNumber(0) is null && EditorToolState.CommandForNumber(6) is null,
            "shared tools: unrelated numbers have no tool mapping");

        using var engine = new NotaEngine();
        int track = engine.AddInstrumentTrack(), clip = engine.AddMidiClip(track, 8, 8);
        var note = new NotaNote(60, 1, 1, 0.8f); engine.SetClipNotes(track, clip, new[] { note });
        var untouched = new[] { new AutomationPoint(0, 0.2f), new AutomationPoint(7, 0.9f) };
        engine.SetMidiClipController(track, clip, 11, untouched);
        var set = new ClipSet(new[] { new OpenClip(track, clip, 8, 8) }, track, clip, 4, trackMode: true);
        var generated = ArrangementDrawing.WavePoints(AutomationWaveform.Sine, 8, 12, 8, 0.25, 4, 0, 1, 0.5, 1, 4, 0)
            .Select(p => new AutomationPoint(set.ToLocal(set.Active, p.Beat), p.Value)).ToArray();
        yield return (generated.First().Beat == 0 && generated.Last().Beat == 4 && generated.All(p => p.Value >= 0 && p.Value <= 1),
            "shared tools: editor-grid waveform maps correctly into a clip-local CC lane");
        engine.SetMidiClipController(track, clip, 1, generated);
        yield return (engine.GetMidiClipController(track, clip, 1).Length > 1 && engine.Undo()
            && engine.GetMidiClipController(track, clip, 1).Length == 0 && engine.Redo(),
            "shared tools: a CC stroke is one native undo/redo operation");
        yield return (engine.GetMidiClipController(track, clip, 11).SequenceEqual(untouched)
            && engine.GetClipNotes(track, clip).Length == 1 && engine.GetClipNotes(track, clip)[0].Pitch == 60,
            "shared tools: CC drawing retains other controllers and note pitches/durations");
    }
}
