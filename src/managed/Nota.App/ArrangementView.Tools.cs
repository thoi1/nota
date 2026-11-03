// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Nota.Application;
using Nota.Application.Editing;
using Nota.Application.Localization;

namespace Nota.App;

public sealed partial class ArrangementView
{
    public EditorToolState ToolState { get; } = new();
    public ArrangementTool EditingTool { get => ToolState.Tool; set => ToolState.Choose(value); }
    public AutomationWaveform DrawWaveform { get => ToolState.Waveform.Shape; set => ToolState.Waveform.Shape = value; }
    public double DrawAmplitude { get => ToolState.Waveform.Amplitude; set => ToolState.Waveform.Amplitude = value; }
    public double DrawCycleBars { get => ToolState.Waveform.CycleBars; set => ToolState.Waveform.CycleBars = value; }
    public double DrawPhase { get => ToolState.Waveform.Phase; set => ToolState.Waveform.Phase = value; }
    private Cursor ToolCursor => EditorToolCursors.For(EditingTool);

    private void InitArrangementTools()
    {
        ToolState.Changing += () => { CancelToolStroke(); CancelActiveGesture(); };
        ToolState.Changed += () =>
        {
            _lanes.ResetToolCursor(); _footerLanes.ResetToolCursor(); _ruler.InvalidateVisual();
            if (EditingTool == ArrangementTool.NotaWarp)
                StatusMessage?.Invoke(Loc.T("Nota Warp: click an audio clip to analyse attacks and edit warp anchors."));
        };
    }

    // All new drawing stays in the view until release: cancellation never touches the engine.
    private sealed class ToolStroke
    {
        public required Control Owner;
        public required List<AutoPt> Points;
        public required AutomationPoint[] Original;
        public required Action Commit;
        public required IPointer Pointer;
        public readonly SortedDictionary<double, AutomationPoint> Freehand = new();
        public double Anchor, LastBeat, LastValue, Top, Bottom, Step, Amplitude, Period, Phase;
        public float Min, Max;
        public int BeatsPerBar;
        public bool Snap;
        public AutomationWaveform Shape;
    }
    private ToolStroke? _toolStroke;
    private bool IsToolDrawing(Control owner) => _toolStroke?.Owner == owner;

    private bool ToolAutoPressed(Control owner, List<AutoPt> points, (float min, float max) range,
        double top, double bottom, PointerPressedEventArgs e, Action commit)
    {
        var pt = e.GetCurrentPoint(owner);
        if (!pt.Properties.IsLeftButtonPressed) return false;
        if (EditingTool == ArrangementTool.Select) return false;
        if (EditingTool == ArrangementTool.NotaWarp) return true;
        if (EditingTool == ArrangementTool.Eraser)
        {
            var hit = points.FirstOrDefault(p =>
            {
                double y = bottom - Math.Clamp((p.Value - range.min) / Math.Max(1e-9, range.max - range.min), 0, 1) * (bottom - top);
                return Math.Pow(BeatToX(p.Beat) - pt.Position.X, 2) + Math.Pow(y - pt.Position.Y, 2) <= 49;
            });
            if (hit is not null) { points.Remove(hit); CommitToolEdit(commit); owner.InvalidateVisual(); }
            e.Handled = true;
            return true;
        }
        var grid = SnapGridState;
        bool snap = grid.SnapOn && (e.KeyModifiers & KeyModifiers.Alt) == 0;
        double step = snap ? grid.Snap.Beats(_beatsPerBar) : Math.Max(1.0 / 960, 4 / _pixelsPerBeat);
        double beat = Math.Max(0, _scrollBeats + pt.Position.X / _pixelsPerBeat);
        if (snap) beat = SnapGrid.SnapBeat(beat, step, _beatsPerBar);
        _toolStroke = new ToolStroke
        {
            Owner = owner, Points = points, Original = points.Select(p => new AutomationPoint(p.Beat, p.Value, p.Curve)).ToArray(),
            Commit = commit, Pointer = e.Pointer, Top = top, Bottom = bottom, Min = range.min, Max = range.max,
            Anchor = beat, LastBeat = beat, Step = step, Snap = snap, BeatsPerBar = _beatsPerBar,
            Shape = EditingTool == ArrangementTool.Pencil ? AutomationWaveform.Freehand : DrawWaveform,
            Amplitude = Math.Clamp(DrawAmplitude, 0, 1), Period = Math.Max(1.0 / 64, DrawCycleBars * _beatsPerBar), Phase = DrawPhase,
        };
        _toolStroke.LastValue = StrokeValue(_toolStroke, pt.Position.Y);
        ClearAutoSelection();
        e.Pointer.Capture(owner);
        ToolAutoMoved(owner, e);
        e.Handled = true;
        return true;
    }

    private static float StrokeValue(ToolStroke s, double y) => (float)(s.Min +
        Math.Clamp((s.Bottom - y) / Math.Max(1, s.Bottom - s.Top), 0, 1) * (s.Max - s.Min));

    private bool ToolAutoMoved(Control owner, PointerEventArgs e)
    {
        if (_toolStroke is not { } s || s.Owner != owner) return false;
        var pos = e.GetPosition(owner);
        double beat = Math.Max(0, _scrollBeats + Math.Clamp(pos.X, 0, owner.Bounds.Width) / _pixelsPerBeat);
        if (s.Snap) beat = SnapGrid.SnapBeat(beat, s.Step, s.BeatsPerBar);
        float value = StrokeValue(s, pos.Y);
        AutomationPoint[] drawn;
        double lo, hi;
        if (s.Shape == AutomationWaveform.Freehand)
        {
            foreach (double b in ArrangementDrawing.SampleBeats(s.LastBeat, beat, s.Step, s.BeatsPerBar))
            {
                double f = Math.Abs(beat - s.LastBeat) < 1e-8 ? 1 : (b - s.LastBeat) / (beat - s.LastBeat);
                s.Freehand[b] = new AutomationPoint(b, (float)(s.LastValue + f * (value - s.LastValue)), 0);
            }
            // Keep long freehand gestures bounded without replacing points outside their range.
            while (s.Freehand.Count > ArrangementDrawing.MaxStrokePoints)
            {
                var keys = s.Freehand.Keys.ToArray();
                for (int i = 1; i < keys.Length - 1; i += 2) s.Freehand.Remove(keys[i]);
            }
            drawn = s.Freehand.Values.ToArray();
            lo = drawn[0].Beat; hi = drawn[^1].Beat;
            s.LastBeat = beat; s.LastValue = value;
        }
        else
        {
            lo = Math.Min(s.Anchor, beat); hi = Math.Max(s.Anchor, beat);
            drawn = ArrangementDrawing.WavePoints(s.Shape, lo, hi, s.Anchor, s.Step, s.BeatsPerBar,
                s.Min, s.Max, s.LastValue, s.Amplitude, s.Period, s.Phase);
        }
        ReplacePreview(s, ArrangementDrawing.ReplaceRange(s.Original, drawn, lo, hi));
        owner.InvalidateVisual();
        e.Handled = true;
        return true;
    }

    private static void ReplacePreview(ToolStroke s, IEnumerable<AutomationPoint> points)
    {
        s.Points.Clear();
        s.Points.AddRange(points.Select(p => new AutoPt { Beat = p.Beat, Value = p.Value, Curve = p.Curve }));
    }

    private bool ToolAutoReleased(Control owner, PointerReleasedEventArgs e)
    {
        if (_toolStroke is not { } s || s.Owner != owner) return false;
        if (e.InitialPressMouseButton != MouseButton.Left) return true;
        ToolAutoMoved(owner, e);
        _toolStroke = null;
        e.Pointer.Capture(null);
        CommitToolEdit(s.Commit);
        e.Handled = true;
        return true;
    }

    private void CommitToolEdit(Action edit)
    {
        if (_engine is not { } engine) return;
        engine.BeginUndoGroup();
        try { edit(); }
        finally { engine.EndUndoGroup(); }
    }

    private bool CancelToolStroke()
    {
        if (_toolStroke is not { } s) return false;
        _toolStroke = null;
        ReplacePreview(s, s.Original);
        s.Pointer.Capture(null);
        s.Owner.InvalidateVisual();
        return true;
    }
}
