// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
// Dedicated musical-time editor. Pointer previews never build maps or stretch audio.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Nota.Application;
using Nota.Application.Editing;
using Nota.Application.Localization;

namespace Nota.App;

public sealed class TempoTrackControl : Control
{
    private readonly ArrangementView _owner;
    private TempoMapState _map = new(new[] { new TempoPoint(0, 120) }, Array.Empty<TempoAnchor>());
    private TempoPoint[]? _original;
    private readonly HashSet<int> _selection = new();
    private int _hover = -1;
    private Point _down, _boxEnd;
    private bool _drag, _box;
    private bool _moved;
    private int _shiftToggle = -1;
    private long _revision = long.MinValue;
    public double MinimumBpm => _map.MinBpm;
    public double MaximumBpm => _map.MaxBpm;
    public bool ClipDropTarget { get; set; }
    public bool IsDragging => _drag || _box;
    public TempoPoint? SelectedPoint => _selection.Count == 1 ? _map.Points[_selection.First()] : null;
    public event Action? SelectionChanged;

    public TempoTrackControl(ArrangementView owner)
    {
        _owner = owner; Focusable = true; ClipToBounds = true;
        ToolTip.SetTip(this, Loc.T("Double-click to add a tempo point. Drag to edit; Shift for fine adjustment; Alt-click to delete."));
    }
    public void Reload(bool force = false)
    {
        if (IsDragging || _owner.Engine is not { } engine) return;
        long revision = engine.TempoRevision;
        if (!force && revision == _revision) return;
        _revision = revision;
        _map = engine.GetTempoMap();
        _selection.RemoveWhere(i => i >= _map.Points.Length);
        SelectionChanged?.Invoke(); InvalidateVisual();
    }
    private double X(TempoPoint p) => _owner.BeatToX(p.Beat);
    private double Y(double bpm) => 9 + (Bounds.Height - 18) * (1 - (bpm - _map.MinBpm) / Math.Max(1, _map.MaxBpm - _map.MinBpm));
    private double Bpm(double y) => Math.Clamp(_map.MaxBpm - (y - 9) / Math.Max(1, Bounds.Height - 18) * (_map.MaxBpm - _map.MinBpm), _map.MinBpm, _map.MaxBpm);
    private long Tick(double x, bool fine = false) => Math.Max(0, TempoPoint.ToTick(fine ? _owner.XToBeat(x) : _owner.Snap(_owner.XToBeat(x))));
    private int Lower(long tick)
    {
        int lo = 0, hi = _map.Points.Length;
        while (lo < hi) { int m = (lo + hi) / 2; if (_map.Points[m].Tick < tick) lo = m + 1; else hi = m; }
        return lo;
    }
    private double ValueAt(double beat)
    {
        int i = Math.Max(0, Lower(TempoPoint.ToTick(beat) + 1) - 1);
        var a = _map.Points[i];
        if (i + 1 == _map.Points.Length || a.Curve == TempoCurve.Step) return a.Bpm;
        var b = _map.Points[i + 1]; double u = Math.Clamp((beat - a.Beat) / (b.Beat - a.Beat), 0, 1);
        if (a.Curve == TempoCurve.Smooth) u = u * u * (3 - 2 * u);
        return a.Bpm + (b.Bpm - a.Bpm) * u;
    }
    private int Hit(Point p)
    {
        long lo = TempoPoint.ToTick(_owner.XToBeat(p.X - 8));
        long hi = TempoPoint.ToTick(_owner.XToBeat(p.X + 8));
        for (int i = Lower(lo); i < _map.Points.Length && _map.Points[i].Tick <= hi; i++)
            if (Math.Abs(Y(_map.Points[i].Bpm) - p.Y) <= 8) return i;
        return -1;
    }
    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        ctx.FillRectangle(NotaPalette.BgSunken, Bounds.WithX(0).WithY(0));
        double h = Bounds.Height, w = Bounds.Width;
        if (ClipDropTarget) ctx.DrawRectangle(NotaPalette.Wash(NotaPalette.Accent, 0x18),
            new Pen(NotaPalette.Accent, 1), new Rect(0.5, 0.5, Math.Max(0, w - 1), Math.Max(0, h - 1)));
        if (_owner.TempoExpanded)
            for (int i = 0; i <= 4; i++)
            {
                double bpm = _map.MinBpm + (_map.MaxBpm - _map.MinBpm) * i / 4;
                double y = Y(bpm);
                ctx.DrawLine(new Pen(NotaPalette.VizGrid, 1), new Point(0, y), new Point(w, y));
                Text(ctx, NotaNum.Bpm(bpm), new Point(Math.Max(0, w - 54), y - 9), NotaPalette.TextMuted);
            }
        double step = Math.Max(1, _owner.BeatsPerBar);
        double first = Math.Floor(_owner.ScrollBeats / step) * step;
        for (double b = first; _owner.BeatToX(b) < w; b += step)
            ctx.DrawLine(new Pen(NotaPalette.VizGrid, 1), new Point(_owner.BeatToX(b), 0), new Point(_owner.BeatToX(b), h));
        // Screen-bounded curve sampling, with explicit step edges and visible points.
        var pen = new Pen(NotaPalette.VizInk, 1.3);
        Point previous = new(0, Y(ValueAt(_owner.XToBeat(0))));
        for (double x = 3; x <= w + 3; x += 3)
        {
            Point next = new(Math.Min(x, w), Y(ValueAt(_owner.XToBeat(Math.Min(x, w)))));
            int edge = Lower(TempoPoint.ToTick(_owner.XToBeat(previous.X)) + 1);
            while (edge < _map.Points.Length && X(_map.Points[edge]) <= next.X)
            {
                if (edge > 0 && _map.Points[edge - 1].Curve == TempoCurve.Step)
                {
                    double at = X(_map.Points[edge]);
                    var left = new Point(at, Y(_map.Points[edge - 1].Bpm));
                    var right = new Point(at, Y(_map.Points[edge].Bpm));
                    ctx.DrawLine(pen, previous, left); ctx.DrawLine(pen, left, right); previous = right;
                }
                edge++;
            }
            ctx.DrawLine(pen, previous, next); previous = next;
        }
        long end = TempoPoint.ToTick(_owner.XToBeat(w + 8));
        for (int i = Lower(TempoPoint.ToTick(_owner.XToBeat(-8))); i < _map.Points.Length && _map.Points[i].Tick <= end; i++)
        {
            var p = _map.Points[i];
            if (i > 0 && _map.Points[i - 1].Curve == TempoCurve.Step)
                ctx.DrawLine(pen, new Point(X(p), Y(_map.Points[i - 1].Bpm)), new Point(X(p), Y(p.Bpm)));
            if (_owner.TempoExpanded)
            {
                ctx.DrawEllipse(_selection.Contains(i) || i == _hover ? NotaPalette.Accent : NotaPalette.VizInk,
                    null, new Point(X(p), Y(p.Bpm)), _selection.Contains(i) ? 4 : 3, _selection.Contains(i) ? 4 : 3);
                var label = new FormattedText(Loc.F("{0} BPM", NotaNum.Bpm(p.Bpm)), NotaNum.Culture,
                    FlowDirection.LeftToRight, NotaFonts.Mono, NotaType.RowLabel,
                    _selection.Contains(i) || i == _hover ? NotaPalette.AccentBright : NotaPalette.TextMuted);
                double labelX = Math.Clamp(X(p) + 6, 2, Math.Max(2, w - label.Width - 2));
                double labelY = Math.Clamp(Y(p.Bpm) - label.Height - 3, 1, Math.Max(1, h - label.Height - 1));
                ctx.DrawText(label, new Point(labelX, labelY));
            }
        }
        if (_box) ctx.DrawRectangle(NotaPalette.Wash(NotaPalette.Accent, 0x18), new Pen(NotaPalette.Accent, 1), new Rect(_down, _boxEnd).Normalize());
    }
    private static void Text(DrawingContext ctx, string text, Point at, IBrush ink)
        => ctx.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, NotaFonts.Mono, NotaType.RowLabel, ink), at);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var p = e.GetPosition(this); Focus();
        if (!_owner.TempoExpanded) { _owner.TempoExpanded = true; e.Handled = true; return; }
        int hit = Hit(p);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) { Menu(hit); e.Handled = true; return; }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        if (hit >= 0 && e.KeyModifiers.HasFlag(KeyModifiers.Alt)) { _selection.Clear(); _selection.Add(hit); Delete(); return; }
        if (hit < 0 && e.ClickCount == 2)
        {
            long tick = Tick(p.X); int index = Lower(tick);
            if (index < _map.Points.Length && _map.Points[index].Tick == tick) return;
            var points = _map.Points.ToList(); points.Insert(index, new(tick, Math.Round(Bpm(p.Y), 2)));
            _selection.Clear(); _selection.Add(index); Commit(_map with { Points = points.ToArray() }); return;
        }
        _down = _boxEnd = p;
        _moved = false; _shiftToggle = -1;
        if (hit >= 0)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { if (!_selection.Add(hit)) _shiftToggle = hit; }
            else if (!_selection.Contains(hit)) { _selection.Clear(); _selection.Add(hit); }
            _original = _map.Points.ToArray(); _drag = _selection.Count > 0;
        }
        else { if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _selection.Clear(); _box = true; }
        SelectionChanged?.Invoke(); e.Pointer.Capture(this); InvalidateVisual();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        Point p = e.GetPosition(this);
        if (_drag && _original is { } original)
        {
            if (!_moved && Math.Abs(p.X - _down.X) + Math.Abs(p.Y - _down.Y) < 3) return;
            _moved = true;
            double fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.1 : 1;
            long dt = TempoPoint.ToTick((p.X - _down.X) / _owner.PixelsPerBeat * fine);
            if (fine == 1) dt = Tick(_owner.BeatToX(original[_selection.Min()].Beat) + p.X - _down.X) - original[_selection.Min()].Tick;
            long low = long.MinValue / 4, high = long.MaxValue / 4;
            foreach (int i in _selection)
            {
                if (i == 0) { low = high = 0; break; }
                if (!_selection.Contains(i - 1)) low = Math.Max(low, original[i - 1].Tick + 1 - original[i].Tick);
                if (i + 1 < original.Length && !_selection.Contains(i + 1)) high = Math.Min(high, original[i + 1].Tick - 1 - original[i].Tick);
            }
            dt = Math.Clamp(dt, low, high);
            double db = -(p.Y - _down.Y) / Math.Max(1, Bounds.Height - 18) * (_map.MaxBpm - _map.MinBpm) * fine;
            double minDelta = _selection.Max(i => _map.MinBpm - original[i].Bpm);
            double maxDelta = _selection.Min(i => _map.MaxBpm - original[i].Bpm);
            db = Math.Clamp(db, minDelta, maxDelta);
            var points = original.ToArray();
            foreach (int i in _selection) points[i] = original[i] with { Tick = original[i].Tick + dt, Bpm = Math.Round(original[i].Bpm + db, 2) };
            _map = _map with { Points = points }; SelectionChanged?.Invoke(); InvalidateVisual(); e.Handled = true;
        }
        else if (_box) { _boxEnd = p; InvalidateVisual(); e.Handled = true; }
        else { int hover = Hit(p); if (hover != _hover) { _hover = hover; InvalidateVisual(); } }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_box)
        {
            Rect rect = new Rect(_down, _boxEnd).Normalize();
            for (int i = Lower(TempoPoint.ToTick(_owner.XToBeat(rect.Left))); i < _map.Points.Length && X(_map.Points[i]) <= rect.Right; i++)
                if (rect.Contains(new Point(X(_map.Points[i]), Y(_map.Points[i].Bpm)))) _selection.Add(i);
            _box = false; SelectionChanged?.Invoke();
        }
        if (_drag) { _drag = false; _original = null; Commit(_map); }
        if (!_moved && _shiftToggle >= 0) { _selection.Remove(_shiftToggle); SelectionChanged?.Invoke(); }
        _shiftToggle = -1;
        e.Pointer.Capture(null); InvalidateVisual();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { Cancel(); base.OnPointerCaptureLost(e); }
    private void Cancel()
    {
        if (_original is { } original) _map = _map with { Points = original };
        _original = null; _drag = _box = false; _shiftToggle = -1; SelectionChanged?.Invoke(); InvalidateVisual();
    }
    private void Commit(TempoMapState candidate)
    {
        if (_owner.Engine is not { } engine) return;
        if (!TrySynchronizeGridWarpPoint(engine, candidate, out candidate, out string syncError))
        {
            _owner.TempoRejected(syncError);
            Reload(force: true);
            return;
        }
        if (engine.TrySetTempoMap(candidate, out string error)) _owner.TempoEdited();
        else _owner.TempoRejected(error);
        Reload(force: true);
    }

    private bool TrySynchronizeGridWarpPoint(IAudioEngine engine, TempoMapState proposed,
        out TempoMapState candidate, out string error)
    {
        candidate = proposed;
        error = "";
        var linked = Enumerable.Range(0, _map.Points.Length)
            .Where(i => GridWarpMapping.IsLinkedTempoPoint(_map, i)).ToArray();
        if (linked.Length == 0) return true;

        if (proposed.Points.Length == _map.Points.Length)
        {
            var changedLinked = linked.Where(i =>
                proposed.Points[i].Tick != _map.Points[i].Tick
                || Math.Abs(proposed.Points[i].Bpm - _map.Points[i].Bpm) > 1e-9
                || proposed.Points[i].Curve != _map.Points[i].Curve).ToArray();
            if (changedLinked.Length == 0) return true;
            if (changedLinked.Length != 1)
            {
                error = Loc.T("Edit one Grid Warp-linked tempo point at a time.");
                return false;
            }

            int index = changedLinked[0];
            var before = _map.Points[index];
            var after = proposed.Points[index];
            if (Math.Abs(after.Bpm - before.Bpm) > 1e-9 || after.Curve != before.Curve)
            {
                error = Loc.T("A Grid Warp-linked tempo value is derived from its absolute timing anchors.");
                return false;
            }
            return GridWarpMapping.TryMoveTempoPoint(engine, _map, index, after.Tick, out candidate, out error);
        }

        if (proposed.Points.Length == _map.Points.Length - 1)
        {
            var removed = Enumerable.Range(0, _map.Points.Length)
                .Where(i => !proposed.Points.Any(p => p.Tick == _map.Points[i].Tick)).ToArray();
            var linkedRemoved = removed.Where(linked.Contains).ToArray();
            if (linkedRemoved.Length == 0) return true;
            if (linkedRemoved.Length != 1 || removed.Length != 1)
            {
                error = Loc.T("Delete one Grid Warp-linked tempo point at a time.");
                return false;
            }
            return GridWarpMapping.TryDeleteTempoPoint(engine, _map, linkedRemoved[0], out candidate, out error);
        }

        if (proposed.Points.Length < _map.Points.Length
            && linked.Any(i => !proposed.Points.Any(p => p.Tick == _map.Points[i].Tick)))
        {
            error = Loc.T("Delete one Grid Warp-linked tempo point at a time.");
            return false;
        }
        return true;
    }
    public void SetSelected(double? beat = null, double? bpm = null, TempoCurve? curve = null)
    {
        var points = _map.Points.ToArray();
        foreach (int i in _selection)
            points[i] = points[i] with { Tick = beat.HasValue && i != 0 ? Math.Max(1, TempoPoint.ToTick(beat.Value)) : points[i].Tick,
                Bpm = Math.Clamp(bpm ?? points[i].Bpm, _map.MinBpm, _map.MaxBpm), Curve = curve ?? points[i].Curve };
        Commit(_map with { Points = points });
    }
    public void SetBounds(double min, double max) => Commit(_map with { MinBpm = min, MaxBpm = max });
    public void Delete()
    {
        var points = _map.Points.Where((_, i) => i == 0 || !_selection.Contains(i)).ToArray();
        if (points.Length == _map.Points.Length) return;
        _selection.Clear(); Commit(_map with { Points = points });
    }
    public void SelectAll()
    {
        _selection.Clear(); for (int i = 0; i < _map.Points.Length; i++) _selection.Add(i);
        SelectionChanged?.Invoke(); InvalidateVisual();
    }
    private void Menu(int hit)
    {
        if (hit >= 0 && !_selection.Contains(hit)) { _selection.Clear(); _selection.Add(hit); SelectionChanged?.Invoke(); }
        var items = new List<MenuItem>();
        foreach (TempoCurve curve in Enum.GetValues<TempoCurve>())
        {
            var item = new MenuItem { Header = curve switch { TempoCurve.Step => Loc.T("Step"), TempoCurve.Linear => Loc.T("Linear"), _ => Loc.T("Smooth") }, IsEnabled = _selection.Count > 0 };
            item.Click += (_, _) => SetSelected(curve: curve); items.Add(item);
        }
        var remove = new MenuItem { Header = Loc.T("Delete tempo points"), IsEnabled = _selection.Any(i => i != 0) };
        remove.Click += (_, _) => Delete(); items.Add(remove);
        var unlock = new MenuItem { Header = Loc.T("Unlock absolute timing anchors"), IsEnabled = _map.Anchors.Any(a => a.Locked != 0) };
        unlock.Click += (_, _) => Commit(_map with { Anchors = _map.Anchors.Select(a => a with { Locked = 0 }).ToArray() }); items.Add(unlock);
        ContextMenu = new ContextMenu { ItemsSource = items }; ContextMenu.Open(this);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (e.Key == Key.Escape) Cancel();
        else if (e.Key is Key.Delete or Key.Back) Delete();
        else if (command && e.Key == Key.A) SelectAll();
        else if (command && e.Key == Key.C) _ = CopyAsync();
        else if (command && e.Key == Key.V) _ = PasteAsync();
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            double fine = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.01 : 1;
            var original = _map.Points.ToArray();
            foreach (int i in _selection)
                original[i] = original[i] with { Tick = original[i].Tick + (i == 0 ? 0 : e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0),
                    Bpm = Math.Clamp(original[i].Bpm + (e.Key == Key.Up ? fine : e.Key == Key.Down ? -fine : 0), _map.MinBpm, _map.MaxBpm) };
            Commit(_map with { Points = original });
        }
        else { base.OnKeyDown(e); return; }
        e.Handled = true; InvalidateVisual();
    }
    public async Task CopyAsync()
    {
        var points = _selection.OrderBy(i => i).Select(i => _map.Points[i]).ToArray();
        if (points.Length == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        long origin = points[0].Tick;
        await clipboard.SetTextAsync("nota-tempo:" + JsonSerializer.Serialize(points.Select(p => p with { Tick = p.Tick - origin, LinkedAnchorId = 0 })));
    }
    public async Task PasteAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        string? text = await clipboard.TryGetTextAsync();
        if (!IsEffectivelyVisible || text?.StartsWith("nota-tempo:", StringComparison.Ordinal) != true) return;
        try
        {
            var points = JsonSerializer.Deserialize<TempoPoint[]>(text[11..]);
            if (points is null || points.Length > 100000 || _owner.Engine is not { } engine) return;
            long at = TempoPoint.ToTick(_owner.Snap(engine.PositionBeats));
            var all = _map.Points.ToDictionary(p => p.Tick);
            foreach (var p in points) { if (p.Tick < 0 || p.Tick > long.MaxValue - at) return; all[p.Tick + at] = p with { Tick = p.Tick + at, LinkedAnchorId = 0 }; }
            Commit(_map with { Points = all.Values.OrderBy(p => p.Tick).ToArray() });
        }
        catch (JsonException) { }
    }
}
