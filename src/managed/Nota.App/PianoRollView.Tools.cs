// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Nota.Application;
using Nota.Application.Editing;
using Nota.Application.Localization;

namespace Nota.App;

public sealed partial class PianoRollView
{
    public EditorToolState ToolState { get; }
    public ArrangementTool EditingTool { get => ToolState.Tool; set => ToolState.Choose(value); }
    private Cursor ToolCursor => EditorToolCursors.For(EditingTool);
    private TextBlock _toolHint = null!;

    private Control WithEditingTools(Control body)
    {
        var toolbar = new EditingToolBar { State = ToolState };
        _toolHint = new TextBlock { FontSize = 9, Foreground = NotaPalette.TextTertiary,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 0, 4, 0) };
        var row = new DockPanel { Margin = new Thickness(4, 2) };
        DockPanel.SetDock(toolbar, Dock.Left); row.Children.Add(toolbar); row.Children.Add(_toolHint);
        var host = new DockPanel(); DockPanel.SetDock(row, Dock.Top); host.Children.Add(row); host.Children.Add(body);
        AttachedToVisualTree += (_, _) => { ToolState.Changing += OnToolsChanging; ToolState.Changed += OnToolChanged; OnToolChanged(); };
        DetachedFromVisualTree += (_, _) => { CancelToolGesture(); ToolState.Changing -= OnToolsChanging; ToolState.Changed -= OnToolChanged; };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && CancelToolGesture()) e.Handled = true;
        }, RoutingStrategies.Tunnel);
        OnToolChanged();
        return host;
    }

    private void OnToolsChanging()
    {
        CancelToolGesture();
        _gridControl.FinishForToolChange();
    }
    private void OnToolChanged()
    {
        _gridControl.Cursor = ToolCursor; _ctlLane.Cursor = ToolCursor;
        foreach (var item in _controllerLanes) item.Lane.Cursor = ToolCursor;
        _toolHint.Text = EditingTool switch
        {
            ArrangementTool.WaveformDraw => Loc.T("Choose a MIDI CC lane to draw a waveform; notes are unchanged."),
            ArrangementTool.NotaWarp => Loc.T("Tempo-grid editing is unavailable; this ruler does not change notes or tempo."),
            _ => "",
        };
        _ruler.InvalidateVisual();
    }
    public bool CancelToolGesture()
    {
        bool cancelled = _gridControl.CancelToolNote();
        if (_ctlLane.IsGesturing) { _ctlLane.ResetGesture(); cancelled = true; }
        foreach (var item in _controllerLanes)
            if (item.Lane.IsGesturing) { item.Lane.ResetGesture(); cancelled = true; }
        return cancelled;
    }
    private bool BlockIncompatibleToolLane(PointerPressedEventArgs e)
    {
        if (EditingTool is not (ArrangementTool.WaveformDraw or ArrangementTool.NotaWarp)
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return false;
        OnToolChanged(); e.Handled = true; return true;
    }

    private sealed partial class NoteGrid
    {
        private NotaNote[]? _toolOriginalNotes;
        private int[]? _toolOriginalOwners, _toolOriginalSelection;
        private IPointer? _toolNotePointer;
        private int _toolNoteIndex = -1, _toolNoteSlot;
        private double _toolNoteAnchor;
        private Point _toolNotePress;

        internal bool CancelToolNote()
        {
            if (_toolOriginalNotes is null) return false;
            _o._notes.Clear(); _o._notes.AddRange(_toolOriginalNotes);
            _o._owner.Clear(); _o._owner.AddRange(_toolOriginalOwners!);
            _o._selection.Clear(); _o._selection.UnionWith(_toolOriginalSelection!);
            ClearToolNote(); _o.StopAudition(); _o.Invalidate(); return true;
        }
        private void ClearToolNote()
        {
            _toolOriginalNotes = null; _toolOriginalOwners = _toolOriginalSelection = null; _toolNoteIndex = -1;
            var pointer = _toolNotePointer; _toolNotePointer = null; pointer?.Capture(null);
        }
        internal void FinishForToolChange() { if (_drag != Mode.None) EndDrag(); }

        private bool ToolNotePressed(PointerPressedEventArgs e)
        {
            if (_o.EditingTool == ArrangementTool.Select || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return false;
            var pos = e.GetPosition(this);
            if (_o.EditingTool is ArrangementTool.WaveformDraw or ArrangementTool.NotaWarp)
            { _o.OnToolChanged(); e.Handled = true; return true; }
            _o.EndVelocityWheel();
            _o.SettlePlacement();
            int hit = HitTest(pos, out _);
            if (_o.EditingTool == ArrangementTool.Eraser)
            {
                if (hit >= 0 && !_o.IsLocked(hit))
                { _o.RemoveNoteAt(hit); _o._selection.Clear(); _o.CommitNotes(); _o.Invalidate(); _o.Changed?.Invoke(); }
                e.Handled = true; return true;
            }
            if (hit >= 0) { e.Handled = true; return true; }
            _free = (e.KeyModifiers & KeyModifiers.Alt) != 0;
            double at = Math.Max(0, Snap(XToBeat(pos.X)));
            int slot = _o.DrawTarget(at);
            if (slot < 0 || !_o._set.IsEditable(slot)) { e.Handled = true; return true; }
            if (slot != _o._set.Active) _o.SetActiveClip(slot);
            _toolOriginalNotes = _o._notes.ToArray(); _toolOriginalOwners = _o._owner.ToArray();
            _toolOriginalSelection = _o._selection.ToArray();
            double length = Math.Min(_o.NewNoteLen(at, slot), _o._set.EditorEnd(slot) - at);
            if (length <= 1e-9) { ClearToolNote(); return true; }
            var note = new NotaNote(YToPitch(pos.Y), at, length, s_lastVel);
            _o.AddNote(note, slot); _toolNoteIndex = _o._notes.Count - 1; _toolNoteSlot = slot;
            _toolNoteAnchor = at; _toolNotePress = pos;
            _o._selection.Clear(); _o._selection.Add(_toolNoteIndex);
            _toolNotePointer = e.Pointer; e.Pointer.Capture(this);
            _o.Audition(note.Pitch, note.Velocity, slot: slot); _o.Invalidate(); e.Handled = true; return true;
        }
        private bool ToolNoteMoved(PointerEventArgs e)
        {
            if (_toolNoteIndex < 0) return false;
            var pos = e.GetPosition(this);
            var note = _o._notes[_toolNoteIndex];
            note.Pitch = YToPitch(pos.Y);
            if (Math.Abs(pos.X - _toolNotePress.X) >= 3)
            {
                double end = Math.Clamp(Snap(XToBeat(pos.X)), _o._set.EditorStart(_toolNoteSlot), _o._set.EditorEnd(_toolNoteSlot));
                note.StartBeat = Math.Min(_toolNoteAnchor, end);
                note.LengthBeats = Math.Min(Math.Max(_o.MinNoteLen, Math.Abs(end - _toolNoteAnchor)), _o._set.EditorEnd(_toolNoteSlot) - note.StartBeat);
            }
            _o._notes[_toolNoteIndex] = note; _o.Audition(note.Pitch, note.Velocity, slot: _toolNoteSlot);
            _o.Invalidate(); return true;
        }
        private bool ToolNoteReleased(PointerReleasedEventArgs e)
        {
            if (_toolNoteIndex < 0) return false;
            if (e.InitialPressMouseButton != MouseButton.Left) return true;
            ToolNoteMoved(e); _o.Remember(_toolNoteIndex);
            ClearToolNote(); _o.StopAudition(); _o._placementPending = true;
            // Settle same-pitch overlaps before the first publication, so focus loss cannot
            // turn one completed Pencil gesture into a second undo entry.
            if (!_o.SettlePlacement()) { _o.CommitNotes(); _o.Changed?.Invoke(); }
            _o.Invalidate(); e.Handled = true; return true;
        }
    }

    private sealed partial class ControllerLane
    {
        internal bool IsGesturing => _drag != Drag.None;
        private AutomationWaveform _waveShape;
        private double _waveAnchor, _waveLast, _waveStep, _waveAmplitude, _wavePeriod, _wavePhase;
        private float _waveCenter, _waveLastValue;
        private readonly SortedDictionary<double, AutomationPoint> _waveFreehand = new();

        private bool ToolControllerPressed(PointerPressedEventArgs e)
        {
            if (_o.EditingTool is ArrangementTool.Select or ArrangementTool.Pencil || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return false;
            if (!Editable || _o.EditingTool == ArrangementTool.NotaWarp) { e.Handled = true; return true; }
            var p = e.GetPosition(this);
            if (_o.EditingTool == ArrangementTool.Eraser)
            {
                if (IsSwitch) { var (block, _) = BlockAt(p); if (block >= 0) DeleteBlock(block); }
                else { int hit = PointAt(p); if (hit >= 0) { var pts = Cur.ToList(); pts.RemoveAt(hit); _o.StoreController(Ctl, Normalize(pts)); } }
                e.Handled = true; return true;
            }
            ResetGesture();
            _orig = _o.ControllerPoints(Ctl).ToList();
            var grid = _o.SnapGridState;
            _free = !grid.SnapOn || (e.KeyModifiers & KeyModifiers.Alt) != 0;
            _waveStep = _free ? Math.Max(1.0 / 960, 4 / Math.Max(1, _o._pixelsPerBeat)) : grid.Snap.Beats(_o._beatsPerBar);
            _waveAnchor = Ed(Math.Clamp(SnapLocal(LocalAt(p.X)), 0, ClipLen)); _waveLast = _waveAnchor;
            _waveCenter = _waveLastValue = YToValue(p.Y);
            var wave = _o.ToolState.Waveform; _waveShape = wave.Shape;
            _waveAmplitude = wave.Amplitude; _wavePeriod = wave.CycleBars * _o._beatsPerBar; _wavePhase = wave.Phase;
            _waveFreehand.Clear(); _drag = Drag.Waveform; _capturedPointer = e.Pointer;
            e.Pointer.Capture(this); MoveWaveform(e); e.Handled = true; return true;
        }

        private void MoveWaveform(PointerEventArgs e)
        {
            var pos = e.GetPosition(this);
            double beat = Ed(Math.Clamp(SnapLocal(LocalAt(Math.Clamp(pos.X, 0, Bounds.Width))), 0, ClipLen));
            var (min, max) = MidiController.Range(Ctl);
            List<AutomationPoint> points;
            if (_waveShape == AutomationWaveform.Freehand)
            {
                float value = YToValue(pos.Y);
                foreach (double b in ArrangementDrawing.SampleBeats(_waveLast, beat, _waveStep, _o._beatsPerBar))
                {
                    double f = Math.Abs(beat - _waveLast) < 1e-8 ? 1 : (b - _waveLast) / (beat - _waveLast);
                    _waveFreehand[b] = new AutomationPoint(b, (float)(_waveLastValue + f * (value - _waveLastValue)));
                }
                while (_waveFreehand.Count > ArrangementDrawing.MaxStrokePoints)
                { var keys = _waveFreehand.Keys.ToArray(); for (int i = 1; i < keys.Length - 1; i += 2) _waveFreehand.Remove(keys[i]); }
                points = _waveFreehand.Values.ToList(); _waveLast = beat; _waveLastValue = value;
            }
            else points = ArrangementDrawing.WavePoints(_waveShape, _waveAnchor, beat, _waveAnchor,
                _waveStep, _o._beatsPerBar, min, max, _waveCenter, _waveAmplitude, _wavePeriod, _wavePhase).ToList();
            var local = points.Select(p => new AutomationPoint(Math.Clamp(_o._set.ToLocal(_o._set.Active, p.Beat), 0, ClipLen),
                IsSwitch ? (p.Value >= 0.5f ? 1f : 0f) : p.Value, p.Curve)).ToList();
            if (!IsSwitch && local.Count > 2)
                local = Thin(local.Select(p => (p.Beat, p.Value)).ToList(), (max - min) / (Ctl == MidiController.PitchWheel ? 32768 : 512))
                    .Select(p => new AutomationPoint(p.Beat, p.Value)).ToList();
            // Preserve outside points exactly; normalize orders/deduplicates switch states.
            _work = Normalize(ArrangementDrawing.ReplaceRange(_orig, local, local[0].Beat, local[^1].Beat).ToList());
            InvalidateVisual();
        }
    }
}
