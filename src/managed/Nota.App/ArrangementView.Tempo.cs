// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Nota.Application.Localization;

namespace Nota.App;
public sealed partial class ArrangementView
{
    private TempoTrackControl _tempoLane = null!;
    private Grid _tempoRow = null!;
    private StackPanel _tempoInspector = null!;
    private Glyph _tempoChevron = null!;
    private TextBlock _tempoReadout = null!;
    private DragNumber _tempoBeat = null!, _tempoValue = null!, _tempoMin = null!, _tempoMax = null!;
    private bool _tempoExpanded;
    private double _tempoHeight = 140;
    public event Action<int, int>? OriginalTempoExtractionRequested;
    private bool _tempoClipDrop;
    internal bool OverTempoRow(Point point)
    {
        if (!ShowTempo || _tempoRow.TranslatePoint(new Point(), this) is not { } origin) return false;
        return new Rect(origin, _tempoRow.Bounds.Size).Contains(point);
    }
    private void SetTempoClipDrop(bool active)
    {
        if (_tempoClipDrop == active) return;
        _tempoClipDrop = active; _tempoLane.ClipDropTarget = active; _tempoLane.InvalidateVisual();
        if (active) StatusMessage?.Invoke(Loc.T("Drop to extract original tempo into the Tempo Track"));
    }
    public event Action? TempoMapChanged;
    public bool TempoHasFocus => _tempoLane.IsKeyboardFocusWithin;
    public void CopyTempo() => _ = _tempoLane.CopyAsync();
    public System.Threading.Tasks.Task CopyTempoAsync() => _tempoLane.CopyAsync();
    public void PasteTempo() => _ = _tempoLane.PasteAsync();
    public void DeleteTempo() => _tempoLane.Delete();
    public void SelectAllTempo() => _tempoLane.SelectAll();
    public void UpdateTempoReadout(double bpm) => _tempoReadout.Text = NotaNum.Bpm(bpm);
    public bool ShowTempo { get => _tempoRow.IsVisible; set => _tempoRow.IsVisible = value; }
    public bool TempoExpanded
    {
        get => _tempoExpanded;
        set { _tempoExpanded = value; _tempoRow.Height = value ? _tempoHeight : 30; _tempoInspector.IsVisible = value; _tempoChevron.Kind = value ? GlyphKind.ChevronDown : GlyphKind.ChevronRight; _tempoLane.InvalidateVisual(); }
    }
    public double TempoLaneHeight { get => _tempoHeight; set { _tempoHeight = Math.Clamp(value, 80, 320); if (_tempoExpanded) _tempoRow.Height = _tempoHeight; } }
    public void ReloadTempo()
    {
        _tempoLane.Reload();
        if (_engine is { } e) _tempoReadout.Text = NotaNum.Bpm(e.TempoAtBeat(e.PositionBeats));
    }
    internal void TempoEdited() { ReloadTempo(); Refresh(rebuildHeaders: false); TempoMapChanged?.Invoke(); }
    internal void TempoRejected(string error) => StatusMessage?.Invoke(error.Contains("reference clip", StringComparison.OrdinalIgnoreCase)
        ? Loc.T("Choose Audio follows grid before moving a fixed reference clip.")
        : error.Contains("protected", StringComparison.OrdinalIgnoreCase)
        ? Loc.T("This edit would move a protected timing anchor. Unlock it before changing this tempo segment.")
        : Loc.T("Tempo edit rejected: check point order and BPM limits."));
    private Grid BuildTempoRow()
    {
        _tempoLane = new TempoTrackControl(this);
        _tempoChevron = new Glyph(GlyphKind.ChevronRight, 12);
        var expand = new Button { Content = _tempoChevron, Width = 22, Height = 24, Classes = { "ghost" } };
        ToolTip.SetTip(expand, Loc.T("Expand or collapse the tempo track"));
        expand.Click += (_, _) => TempoExpanded = !TempoExpanded;
        _tempoReadout = new TextBlock { Classes = { "Readout" }, VerticalAlignment = VerticalAlignment.Center };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Height = 29,
            Children = { expand, new TextBlock { Text = Loc.T("Tempo"), Classes = { "SectionLabel" }, VerticalAlignment = VerticalAlignment.Center }, _tempoReadout } };
        _tempoBeat = new DragNumber(0, 0, 100000000, 0.25, "0.000");
        _tempoValue = new DragNumber(120, 1, 999, 0.1, "0.00");
        _tempoMin = new DragNumber(20, 1, 999, 1, "0.00"); _tempoMax = new DragNumber(300, 1, 999, 1, "0.00");
        _tempoBeat.ValueChanged += v => _tempoLane.SetSelected(beat: v);
        _tempoValue.ValueChanged += v => _tempoLane.SetSelected(bpm: v);
        _tempoMin.ValueChanged += v => { if (_engine is { } e) _tempoLane.SetBounds(v, e.GetTempoMap().MaxBpm); };
        _tempoMax.ValueChanged += v => { if (_engine is { } e) _tempoLane.SetBounds(e.GetTempoMap().MinBpm, v); };
        foreach (var number in new[] { _tempoBeat, _tempoValue, _tempoMin, _tempoMax })
        {
            number.GestureBegin += () => _engine?.BeginUndoGroup();
            number.GestureEnd += () => _engine?.EndUndoGroup();
        }
        _tempoInspector = new StackPanel { Spacing = 5, Margin = new Thickness(9, 2), IsVisible = false };
        static Grid Row(string name, Control field)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,80"), Height = 20 };
            row.Children.Add(new TextBlock { Text = name, Classes = { "Caption" }, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(field, 1); row.Children.Add(field); return row;
        }
        _tempoInspector.Children.Add(Row(Loc.T("Beat"), _tempoBeat));
        _tempoInspector.Children.Add(Row(Loc.T("BPM"), _tempoValue));
        var limits = new StackPanel { Spacing = 8, Margin = new Thickness(10), Width = 190,
            Children = { Row(Loc.T("Minimum BPM"), _tempoMin), Row(Loc.T("Maximum BPM"), _tempoMax) } };
        var options = new Button { Content = new Glyph(GlyphKind.Options, 12), Width = 22, Height = 24, Classes = { "ghost" } };
        ToolTip.SetTip(options, Loc.T("BPM limits"));
        var limitsFlyout = new Flyout { Content = limits };
        options.Click += (_, _) => limitsFlyout.ShowAt(options);
        title.Children.Add(options);
        _tempoLane.SelectionChanged += () =>
        {
            var selected = _tempoLane.SelectedPoint;
            _tempoBeat.IsEnabled = selected.HasValue && selected.Value.Tick != 0;
            _tempoValue.IsEnabled = selected.HasValue;
            if (selected is { } p) { _tempoBeat.Value = p.Beat; _tempoValue.Value = p.Bpm; }
            _tempoMin.Value = _tempoLane.MinimumBpm; _tempoMax.Value = _tempoLane.MaximumBpm;
        };
        var content = new StackPanel { Children = { title, _tempoInspector } };
        _headerColumnParts.Add(content);
        var header = HeaderPanel(content);
        _tempoRow = new Grid { Height = 30, ClipToBounds = true };
        _tempoRow.Children.Add(header); _tempoRow.Children.Add(_tempoLane); AddSideRow(_tempoRow, header, _tempoLane);
        var grip = new Border { Height = 5, VerticalAlignment = VerticalAlignment.Bottom, Background = Avalonia.Media.Brushes.Transparent, Cursor = new Cursor(StandardCursorType.SizeNorthSouth) };
        double originY = 0, originH = 0; bool resizing = false;
        grip.PointerPressed += (_, e) => { if (!TempoExpanded || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return; resizing = true; originY = e.GetPosition(this).Y; originH = TempoLaneHeight; e.Pointer.Capture(grip); e.Handled = true; };
        grip.PointerMoved += (_, e) => { if (resizing) { TempoLaneHeight = originH + e.GetPosition(this).Y - originY; e.Handled = true; } };
        grip.PointerReleased += (_, e) => { resizing = false; e.Pointer.Capture(null); };
        grip.PointerCaptureLost += (_, _) => resizing = false;
        _tempoRow.Children.Add(grip);
        return _tempoRow;
    }
}
