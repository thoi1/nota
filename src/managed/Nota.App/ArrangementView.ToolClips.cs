// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Nota.Application.Editing;

namespace Nota.App;

public sealed partial class ArrangementView
{
    private sealed partial class LaneControl
    {
        private TrackVM? _toolClipTrack;
        private IPointer? _toolClipPointer;
        private double _toolClipAnchor, _toolClipStep;
        private bool _toolClipSnap;
        private (double Start, double Length)? _toolClipPreview;

        internal void ResetToolCursor()
        {
            _pending = false; _marqueeArmed = false;
            _rowSeamCursorOn = _resizeCursorOn = false;
            ClearEdgeHover(); ClearAdsrHover();
            Cursor = _o.ToolCursor;
        }

        private bool ToolClipPressed(PointerPressedEventArgs e)
        {
            var pt = e.GetCurrentPoint(this);
            if (_o.EditingTool == ArrangementTool.Select || !pt.Properties.IsLeftButtonPressed) return false;
            var hit = HitTest(pt.Position, out double beat);
            if (_o.EditingTool == ArrangementTool.NotaWarp && hit is { } audio && !audio.clip.IsMidi)
            {
                _o.SetSelection(new[] { (audio.track.Id, audio.clip.ClipIndex) });
                _o.OnClipDoubleClicked(audio.track.Id, audio.clip.ClipIndex, false);
                if (_o._engine is { } engine && engine.TryGetAudioClipInfo(audio.track.Id, audio.clip.ClipIndex, out var info))
                    _ = TransientWorkspace.For(engine).AnalyzeAsync(info.SampleId);
            }
            else if (_o.EditingTool == ArrangementTool.Eraser)
            {
                if (hit is { } h && _o._engine is { } engine && engine.TryDeleteClip(h.track.Id, h.clip.ClipIndex))
                {
                    _o._sel.Clear(); _o.SelClipIndex = -1;
                    _o.Refresh();
                }
            }
            else if (_o.EditingTool == ArrangementTool.Pencil && hit is null)
            {
                int row = _o.RowAtY(pt.Position.Y);
                if (row >= 0 && _o._tracks[row] is { IsInstrument: true, IsGroup: false } t)
                {
                    var grid = _o.SnapGridState;
                    _toolClipSnap = grid.SnapOn && (e.KeyModifiers & KeyModifiers.Alt) == 0;
                    _toolClipStep = grid.Snap.Beats(_o._beatsPerBar);
                    _toolClipAnchor = Math.Max(0, _toolClipSnap ? SnapGrid.SnapBeat(beat, _toolClipStep, _o._beatsPerBar) : beat);
                    _toolClipTrack = t;
                    _toolClipPointer = e.Pointer;
                    UpdateToolClip(_toolClipAnchor);
                    e.Pointer.Capture(this);
                    InvalidateVisual();
                }
            }
            e.Handled = true;
            return true;
        }

        private void UpdateToolClip(double end)
        {
            if (_toolClipTrack is not { } t) return;
            _toolClipPreview = ArrangementDrawing.ClipInterval(_toolClipAnchor, end, _toolClipStep,
                t.Clips.Select(c => (c.StartBeat, c.LengthBeats)));
        }

        private bool ToolClipMoved(PointerEventArgs e)
        {
            if (_toolClipTrack is null) return false;
            double end = Math.Max(0, _o._scrollBeats + Math.Clamp(e.GetPosition(this).X, 0, Bounds.Width) / _o._pixelsPerBeat);
            if (_toolClipSnap) end = SnapGrid.SnapBeat(end, _toolClipStep, _o._beatsPerBar);
            UpdateToolClip(end);
            InvalidateVisual();
            return true;
        }

        private bool ToolClipReleased(PointerReleasedEventArgs e)
        {
            if (_toolClipTrack is not { } t) return false;
            if (e.InitialPressMouseButton != MouseButton.Left) return true;
            ToolClipMoved(e);
            var preview = _toolClipPreview;
            _toolClipTrack = null; _toolClipPreview = null;
            _toolClipPointer = null;
            e.Pointer.Capture(null);
            if (preview is { } p && _o._engine is { } engine)
            {
                int index = engine.AddMidiClip(t.Id, p.Start, p.Length);
                _o.Refresh();
                if (index >= 0) _o.Select(t.Id, index);
            }
            InvalidateVisual();
            e.Handled = true;
            return true;
        }

        private void DrawToolClipPreview(DrawingContext ctx)
        {
            if (_toolClipTrack is not { } t || _toolClipPreview is not { } p) return;
            int row = _o._tracks.IndexOf(t);
            if (row < 0) return;
            var r = new Rect(_o.BeatToX(p.Start), RowTop(row) + 2,
                Math.Max(2, p.Length * _o._pixelsPerBeat), RowH(row) - 4);
            ctx.DrawRectangle(NotaPalette.Wash(NotaPalette.Accent, 46), new Pen(NotaPalette.AccentBright, 1),
                r, NotaRadius.ClipValue, NotaRadius.ClipValue);
        }
    }
}
