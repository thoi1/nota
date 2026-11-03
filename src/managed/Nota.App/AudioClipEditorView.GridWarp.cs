// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Nota.Application;
using Nota.Application.Editing;
using Nota.Application.Localization;

namespace Nota.App;

public sealed partial class AudioClipEditorView
{
    private int _timeSigNumerator = 4, _timeSigDenominator = 4;

    public void SetTimeSignature(int numerator, int denominator)
    {
        _timeSigNumerator = Math.Clamp(numerator, 1, 16);
        _timeSigDenominator = denominator is 1 or 2 or 4 or 8 or 16 ? denominator : 4;
        _wave.SetGridWarpMeter(_timeSigNumerator, _timeSigDenominator);
    }

    private void CommitGridWarpAnchors(TempoAnchor[] anchors)
    {
        var current = _engine.GetTempoMap();
        long referenceId = _engine.ClipTempoReferenceId(TrackId, ClipIndex);
        if (!GridWarpMapping.TryBuildMap(_engine, current, anchors, TrackId, ClipIndex,
                referenceId, _reversed, out var candidate, out string error)
            || !_engine.TryCommitGridWarp(TrackId, ClipIndex, candidate, out error))
        {
            _warpEditError = error;
            _transientStatus.Text = error;
            _beatStatus.Text = error;
            return;
        }
        _warpEditError = null;
        Reload();
        _onChanged();
    }

    private sealed partial class WaveformView
    {
        private readonly List<(GridWarpLine line, double x)> _gridLineHits = new();
        private readonly List<(TempoAnchor anchor, double x)> _gridAnchorHits = new();
        private IAudioEngine? _gridEngine;
        private TempoAnchor[] _committedGridAnchors = Array.Empty<TempoAnchor>();
        private TempoMapState _gridMap = new(new[] { new TempoPoint(0, 120) }, Array.Empty<TempoAnchor>());
        private int _gridTrackId, _gridClipIndex, _gridNumerator = 4, _gridDenominator = 4;
        private long _gridReferenceId;
        private double _gridRate, _gridSourceStart, _gridSourceEnd, _gridFallbackEndSeconds, _gridFallbackEndSource;
        private TempoAnchor _gridFallback;
        private long _gridFallbackEndTick;
        private long _gridHoverTick = -1;
        private GridWarpDrag? _gridDrag;
        private double _gridPreviewBpm;
        private bool _gridPreviewValid;
        private bool _gridWarpMode;
        private static readonly IPen GridWarpHoverPen = new Pen(NotaPalette.TextPrimary, 1.5);
        private static readonly IPen GridWarpAnchorPen = new Pen(NotaPalette.Accent, 1.5);
        private static readonly IPen GridWarpHandleOutlinePen = new Pen(NotaPalette.TextPrimary, 1);
        private static readonly IPen GridWarpPreviewPen = new Pen(NotaPalette.AccentBright, 2);

        private sealed class GridWarpDrag(long tick, double sourceFrame, double downX)
        {
            public long Tick { get; } = tick;
            public double SourceFrame { get; } = sourceFrame;
            public double DownX { get; } = downX;
            public double PreviewSource { get; set; } = sourceFrame;
            public bool Moved { get; set; }
            public TempoAnchor[]? PreviewAnchors { get; set; }
        }

        public Action<TempoAnchor[]>? GridWarpCommitted;

        public void SetGridWarp(IAudioEngine engine, TempoMapState map, long referenceId,
            int trackId, int clipIndex, double startBeat, double sourceOffset, double sourceLength,
            double sourceRate, bool reversed)
        {
            _gridEngine = engine;
            _gridMap = map;
            _gridReferenceId = referenceId;
            _gridTrackId = trackId;
            _gridClipIndex = clipIndex;
            _committedGridAnchors = map.Anchors.Where(a => referenceId > 0 ? a.ReferenceId == referenceId
                : a.TrackId == trackId && a.ClipIndex == clipIndex).OrderBy(a => a.Tick).ToArray();
            _gridRate = sourceRate;
            _gridSourceStart = sourceOffset;
            _gridSourceEnd = sourceOffset + sourceLength;
            if (sourceRate > 0 && sourceLength > 0)
            {
                double head = reversed ? _gridSourceEnd : _gridSourceStart;
                double tail = reversed ? _gridSourceStart : _gridSourceEnd;
                double origin = engine.SecondsAtBeat(startBeat);
                double tailSeconds = origin + Math.Abs(tail - head) / sourceRate;
                double tailBeat = Math.Max(startBeat, engine.BeatAtSeconds(tailSeconds));
                long firstTick = Math.Max(0, TempoPoint.ToTick(startBeat));
                _gridFallbackEndTick = Math.Max(firstTick + 1, TempoPoint.ToTick(tailBeat));
                _gridFallbackEndSeconds = tailSeconds;
                _gridFallbackEndSource = tail;
                _gridFallback = new(firstTick, origin, 1, trackId, clipIndex, 0, head, referenceId);
            }
            _gridEngine = engine;
            Redraw();
        }

        public void SetGridWarpMeter(int numerator, int denominator)
        {
            _gridNumerator = numerator;
            _gridDenominator = denominator;
            Redraw();
        }

        public void SetGridWarpMode(bool enabled)
        {
            if (_gridWarpMode == enabled) return;
            if (!enabled) CancelGridWarpDrag();
            _gridWarpMode = enabled;
            _gridHoverTick = -1;
            Redraw();
        }

        private TempoAnchor[] GridAnchors()
        {
            if (_gridNumericPreview is not null) return _gridNumericPreview;
            if (_gridProposals is not null) return _gridProposals;
            if (_gridRate <= 0 || _gridSourceEnd <= _gridSourceStart) return Array.Empty<TempoAnchor>();
            var anchors = _committedGridAnchors;
            return anchors.Length >= 2
                ? anchors
                : new[]
                {
                    _gridFallback,
                    new TempoAnchor(_gridFallbackEndTick, _gridFallbackEndSeconds,
                        1, _gridTrackId, _gridClipIndex, 0,
                        _gridFallbackEndSource, _gridReferenceId)
                };
        }

        private TempoAnchor GridReference()
        {
            var anchors = GridAnchors();
            if (anchors.Length == 0) return default;
            return anchors.OrderBy(a => a.Tick).First();
        }

        private double GridSourceAtBeat(double beat)
            => _gridEngine is null || _gridRate <= 0 ? double.NaN
                : GridWarpMapping.SourceFrameAtBeat(_gridEngine, beat, GridReference(), _gridRate, _reversed);

        private double GridBeatAtSource(double sourceFrame)
            => _gridEngine is null || _gridRate <= 0 ? double.NaN
                : GridWarpMapping.BeatAtSourceFrame(_gridEngine, sourceFrame, GridReference(), _gridRate, _reversed);

        private bool IsGridBoundary(TempoAnchor anchor)
        {
            double head = _reversed ? _gridSourceEnd : _gridSourceStart;
            double tail = _reversed ? _gridSourceStart : _gridSourceEnd;
            return Math.Abs(anchor.SourceFrame - head) <= 1
                || Math.Abs(anchor.SourceFrame - tail) <= 1
                || anchor.Tick == _gridFallback.Tick || anchor.Tick == _gridFallbackEndTick;
        }

        private int HitGridAnchor(double x)
        {
            int best = -1;
            double distance = HandlePx + 2;
            for (int i = 0; i < _gridAnchorHits.Count; i++)
            {
                double d = Math.Abs(_gridAnchorHits[i].x - x);
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        private long HitGridLine(double x)
        {
            long tick = -1;
            double distance = HandlePx + 2;
            foreach (var hit in _gridLineHits)
            {
                double d = Math.Abs(hit.x - x);
                if (d < distance) { distance = d; tick = hit.line.Tick; }
            }
            return tick;
        }

        private bool OverGridWarpHandle(Point p)
            => _gridWarpMode && !_envMode && !_adsrMode && p.Y >= 0 && (HitGridAnchor(p.X) >= 0 || HitGridLine(p.X) >= 0);

        private Point? GridWarpHoverHandleCenter(long tick, double x)
        {
            if (!_gridWarpMode || (_gridHoverTick != tick && _gridDrag?.Tick != tick)) return null;
            return new Point(x, RulerH + StripH / 2);
        }

        private TempoAnchor[] AnchorsWithGridEdit(long tick, double sourceFrame)
        {
            var anchors = GridAnchors().ToList();
            var index = anchors.FindIndex(a => a.Tick == tick);
            TempoAnchor reference = anchors[0];
            var updated = new TempoAnchor(tick,
                GridWarpMapping.SecondsAtSourceFrame(sourceFrame, reference, _gridRate, _reversed),
                _gridProposals is not null && (index < 0 || anchors[index].Locked == 0) ? 0 : 1,
                _gridTrackId, _gridClipIndex, index >= 0 ? anchors[index].AnchorId : 0, sourceFrame, _gridReferenceId);
            if (index >= 0) anchors[index] = updated;
            else
            {
                index = anchors.FindIndex(a => a.Tick > tick);
                if (index < 0) index = anchors.Count;
                anchors.Insert(index, updated);
            }
            return anchors.OrderBy(a => a.Tick).ToArray();
        }

        private bool ValidateGridAnchors(TempoAnchor[] anchors, long tick, out double previewBpm)
        {
            previewBpm = 0;
            int index = Array.FindIndex(anchors, a => a.Tick == tick);
            if (index < 0 || index == 0 || index + 1 >= anchors.Length) return false;
            var left = anchors[index - 1];
            var current = anchors[index];
            var right = anchors[index + 1];
            double minBpm = _gridMap.MinBpm, maxBpm = _gridMap.MaxBpm;
            bool leftOk = GridWarpMapping.TryCalculateBpm(left, current, _reversed, minBpm, maxBpm, out double leftBpm);
            bool rightOk = GridWarpMapping.TryCalculateBpm(current, right, _reversed, minBpm, maxBpm, out double rightBpm);
            previewBpm = leftOk ? leftBpm : rightBpm;
            return leftOk && rightOk;
        }

        private int NearestGridTransient(double x, double width, bool snapDisabled)
        {
            if (!_snapTransients || snapDisabled || _transients is not { Transients.Count: > 0 } data) return -1;
            double sample = XToSrc(x, width);
            int at = data.LowerBound((long)Math.Clamp(sample, 0, long.MaxValue));
            int best = -1;
            double distance = HandlePx + 1;
            for (int i = Math.Max(0, at - 1); i <= Math.Min(data.Transients.Count - 1, at); i++)
            {
                long candidate = data.Transients[i].SamplePosition;
                if (candidate < _gridSourceStart || candidate > _gridSourceEnd) continue;
                double d = Math.Abs(SrcToX(candidate, width) - x);
                if (d < distance) { best = i; distance = d; }
            }
            return best;
        }

        private bool BeginGridWarpGesture(PointerPressedEventArgs e)
        {
            if (!_gridWarpMode || _envMode || _adsrMode || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return false;
            Point p = e.GetPosition(this);
            if (p.Y < 0) return false;
            long tick = HitGridLine(p.X);
            int anchorHit = HitGridAnchor(p.X);
            if (anchorHit >= 0)
            {
                var anchor = _gridAnchorHits[anchorHit].anchor;
                SelectGridAnchor(anchor);
                if (_gridProposals is not null && anchor.Locked != 0) { e.Handled = true; return true; }
                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    if (!IsGridBoundary(anchor))
                    {
                        var anchors = GridAnchors().Where(a => a.Tick != anchor.Tick).ToArray();
                        if (anchors.Length >= 2) GridWarpCommitted?.Invoke(anchors);
                    }
                    e.Handled = true;
                    return true;
                }
                tick = anchor.Tick;
            }
            if (tick < 0) { _gridSelectedTick = -1; GridSelectionChanged?.Invoke(null); Redraw(); return false; }

            var current = GridAnchors();
            if (e.ClickCount == 2)
            {
                if (!current.Any(a => a.Tick == tick))
                {
                    double source = XToSrc(p.X, Bounds.Width);
                    int transient = NearestGridTransient(p.X, Bounds.Width,
                        e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    if (transient >= 0 && _transients is { } data)
                        source = data.Transients[transient].SamplePosition;
                    var anchors = AnchorsWithGridEdit(tick, source);
                    if (ValidateGridAnchors(anchors, tick, out _)) GridWarpCommitted?.Invoke(anchors);
                }
                e.Handled = true;
                return true;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                var anchor = current.FirstOrDefault(a => a.Tick == tick);
                if (anchor.Tick == tick && !IsGridBoundary(anchor))
                {
                    var anchors = current.Where(a => a.Tick != tick).ToArray();
                    if (anchors.Length >= 2) GridWarpCommitted?.Invoke(anchors);
                }
                e.Handled = true;
                return true;
            }

            var fixedAnchor = current.FirstOrDefault(a => a.Tick == tick);
            if (fixedAnchor.Tick == tick && IsGridBoundary(fixedAnchor))
            {
                e.Handled = true;
                return true;
            }

            _gridDrag = new GridWarpDrag(tick, XToSrc(p.X, Bounds.Width), p.X);
            _gridDrag.PreviewSource = _gridDrag.SourceFrame;
            _gridDrag.PreviewAnchors = current;
            _gridPreviewValid = true;
            _gridPreviewBpm = 0;
            Focus();
            e.Pointer.Capture(this);
            _gesturePointer = e.Pointer;
            SetCursorKind(StandardCursorType.SizeWestEast);
            Redraw();
            e.Handled = true;
            return true;
        }

        private bool MoveGridWarpGesture(PointerEventArgs e)
        {
            if (_gridDrag is null) return false;
            Point p = e.GetPosition(this);
            double source = XToSrc(p.X, Bounds.Width);
            int transient = NearestGridTransient(p.X, Bounds.Width, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            _nearestTransient = transient;
            if (transient >= 0 && _transients is { } data)
                source = data.Transients[transient].SamplePosition;
            _gridDrag.PreviewSource = source;
            _gridDrag.Moved |= Math.Abs(p.X - _gridDrag.DownX) > 2;
            _gridDrag.PreviewAnchors = AnchorsWithGridEdit(_gridDrag.Tick, source);
            _gridPreviewValid = ValidateGridAnchors(_gridDrag.PreviewAnchors, _gridDrag.Tick, out _gridPreviewBpm);
            Redraw();
            e.Handled = true;
            return true;
        }

        private bool EndGridWarpGesture(PointerReleasedEventArgs e)
        {
            if (_gridDrag is null) return false;
            var drag = _gridDrag;
            _gridDrag = null;
            _gesturePointer = null;
            e.Pointer.Capture(null);
            if (drag.Moved && _gridPreviewValid && drag.PreviewAnchors is { } anchors)
            {
                GridWarpCommitted?.Invoke(anchors);
                SelectGridAnchor(anchors.First(a => a.Tick == drag.Tick));
            }
            _nearestTransient = -1;
            Redraw();
            e.Handled = true;
            return true;
        }

        private void CancelGridWarpDrag()
        {
            _gridDrag = null;
            _gridPreviewValid = false;
            _nearestTransient = -1;
            var pointer = _gesturePointer;
            _gesturePointer = null;
            pointer?.Capture(null);
            Redraw();
        }

        private (double first, double last) VisibleGridBeats(double width)
        {
            var reference = GridReference();
            double a = GridBeatAtSource(XToSrc(0, width));
            double b = GridBeatAtSource(XToSrc(width, width));
            return (Math.Max(0, Math.Min(a, b)), Math.Max(a, b));
        }

        private void DrawGridWarpRuler(DrawingContext ctx, double width)
        {
            if (_gridEngine is null || _gridRate <= 0 || _srcTotal <= 0 || _gridSourceEnd <= _gridSourceStart) return;
            _gridLineHits.Clear();
            _gridAnchorHits.Clear();
            var (first, last) = VisibleGridBeats(width);
            foreach (var line in GridWarpMapping.GridLines(first, last, _gridNumerator, _gridDenominator))
            {
                double source = GridSourceAtBeat(line.Beat);
                if (!double.IsFinite(source) || source < _gridSourceStart - 1 || source > _gridSourceEnd + 1) continue;
                double x = SrcToX(source, width);
                _gridLineHits.Add((line, x));
                if (x < -40 || x > width + 40) continue;
                bool bar = line.IsBar;
                ctx.DrawLine(bar ? GridBarPen : GridBeatPen, new Point(x + 0.5, 0), new Point(x + 0.5, RulerH));
                if (x > 0 && x < width)
                {
                    string label = bar ? line.Bar.ToString(NotaNum.Culture)
                        : $"{line.Bar}.{line.BeatInBar}";
                    var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        GridFace, 9, bar ? RulerBar : RulerBeat);
                    if (bar || Math.Abs(x - SrcToX(GridSourceAtBeat(line.Beat - 4.0 / _gridDenominator), width)) > text.Width + 10)
                        ctx.DrawText(text, new Point(Math.Clamp(x + 3, 2, Math.Max(2, width - text.Width - 2)), RulerH - text.Height - 3));
                }
            }
        }

        private void DrawGridWarpOverlay(DrawingContext ctx, double width, double height)
        {
            if (!_gridWarpMode || _gridEngine is null || _gridRate <= 0 || _gridSourceEnd <= _gridSourceStart) return;
            double top = WaveTop;
            foreach (var (line, originalX) in _gridLineHits)
            {
                double x = _gridDrag?.Tick == line.Tick
                    ? SrcToX(_gridDrag.PreviewSource, width) : originalX;
                bool hot = line.Tick == _gridHoverTick || _gridDrag?.Tick == line.Tick;
                var pen = hot ? GridWarpHoverPen : line.IsBar ? GridBarPen : GridBeatPen;
                ctx.DrawLine(pen, new Point(x + 0.5, top), new Point(x + 0.5, height));
                if (GridWarpHoverHandleCenter(line.Tick, x) is { } handleCenter)
                    ctx.DrawEllipse(NotaPalette.AccentBright, GridWarpHandleOutlinePen,
                        handleCenter, 5.5, 5.5);
            }
            double lastAnchorX = double.NaN;
            foreach (var anchor in GridAnchors())
            {
                // Source boundaries stay fixed in time but remain visible/selectable.
                double frame = _gridDrag?.Tick == anchor.Tick ? _gridDrag.PreviewSource : anchor.SourceFrame;
                double x = SrcToX(frame, width);
                if (x < -8 || x > width + 8) continue;
                if (!double.IsNaN(lastAnchorX) && Math.Abs(x-lastAnchorX)<5
                    && anchor.Tick != _gridSelectedTick && anchor.Tick != _gridHoverTick && !IsGridBoundary(anchor)) continue;
                lastAnchorX=x;
                _gridAnchorHits.Add((anchor, x));
                bool hot = _gridDrag?.Tick == anchor.Tick || anchor.Tick == _gridHoverTick;
                bool proposed = _gridProposals is not null && anchor.Locked == 0;
                var anchorPen = proposed ? new Pen(NotaPalette.TextMuted, 1, DashStyle.Dash) : GridWarpAnchorPen;
                ctx.DrawLine(hot ? GridWarpHoverPen : anchorPen,
                    new Point(x + 0.5, top), new Point(x + 0.5, height));
                ctx.DrawEllipse(proposed ? null : NotaPalette.Accent, proposed ? anchorPen : null, new Point(x, RulerH + StripH / 2),
                    anchor.Tick == _gridSelectedTick ? 5.5 : 4.5, anchor.Tick == _gridSelectedTick ? 5.5 : 4.5);
            }
            if (_gridDrag is { Moved: true } drag)
            {
                double x = Math.Clamp(SrcToX(drag.PreviewSource, width), 6, Math.Max(6, width - 6));
                string position = GridAnchorLabel(drag.Tick);
                string bpm = _gridPreviewValid ? Loc.F("{0:0.##} BPM", _gridPreviewBpm) : Loc.T("Invalid");
                var text = new FormattedText($"{position} · {bpm}", NotaNum.Culture,
                    FlowDirection.LeftToRight, GridFace, 10,
                    _gridPreviewValid ? NotaPalette.AccentBright : NotaPalette.Danger);
                double cx = Math.Clamp(x + 8, 2, Math.Max(2, width - text.Width - 8));
                ctx.DrawText(text, new Point(cx, top + 8));
            }
        }

        private string GridAnchorLabel(long tick)
        {
            double pulseBeats = 4.0 / _gridDenominator;
            long pulse = (long)Math.Round(tick / (TempoPoint.TicksPerBeat * pulseBeats));
            return $"{pulse / _gridNumerator + 1}.{pulse % _gridNumerator + 1}";
        }
    }
}
