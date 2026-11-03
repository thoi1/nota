// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nota.Application;
using Nota.Application.Analysis;
using Nota.Application.Editing;
using Nota.Application.Localization;
using static Nota.App.ClipEditorKit;

namespace Nota.App;
public sealed partial class AudioClipEditorView
{
    private Control? _gridExtractionControls, _gridQuickBar;
    private DragNumber _beatStart = null!, _beatMin = null!, _beatMax = null!, _beatSensitivity = null!;
    private DragNumber _gridAnchorBeat = null!, _gridAnchorTime = null!;
    private Button _snapAllBeats = null!, _applyBeats = null!, _cancelBeats = null!, _resetGrid = null!;
    private TextBlock _beatStatus = null!;
    private CancellationTokenSource? _beatCancellation;
    private TempoAnchor[]? _beatPreview;
    private long _beatPreviewRevision, _beatSource, _beatSeedSource;
    private GridReferenceGeometry? _beatGeometry;
    private bool _syncGridFields, _beatAttached;
    private int _beatDirection;
    private TransientAnalysisSettings? _beatDetectionSettings;

    private Control BuildGridExtractionSection()
    {
        _beatStart = new(120, 1, 999, 1, "0.00"); _beatMin = new(40, 1, 999, 1, "0.00");
        _beatMax = new(240, 1, 999, 1, "0.00"); _beatSensitivity = new(50, 0, 100, 1, "0");
        _gridAnchorBeat = new(1, 0, 100000000, 0.25, "0.000");
        _gridAnchorTime = new(0.5, 0, 100000000, 0.01, "0.000000");
        foreach (var setting in new[] { _beatStart, _beatMin, _beatMax, _beatSensitivity })
            setting.ValueChanged += _ => { if (_beatCancellation is not null || _beatPreview is not null) CancelBeatPreview(); };
        _gridAnchorBeat.ValueChanged += _ => EditGridNumeric(); _gridAnchorTime.ValueChanged += _ => EditGridNumeric();
        foreach (var field in new[] { _gridAnchorBeat, _gridAnchorTime })
        {
            field.GestureBegin += () => { _engine.BeginUndoGroup(); _wave.BeginGridNumericPreview(); };
            field.GestureEnd += () => { _wave.EndGridNumericPreview(); _engine.EndUndoGroup(); };
        }
        static Grid Field(string name, Control control)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,84"), Height = 24 };
            row.Children.Add(new TextBlock { Text = name, Classes = { "Caption" }, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(control, 1); row.Children.Add(control); return row;
        }
        Button ActionButton(string label, Action click)
        { var b = Button(label, click); AutomationProperties.SetName(b, label); return b; }
        _snapAllBeats = ActionButton(Loc.T("Snap all beats"), () => _ = ExtractOriginalTempoAsync());
        _applyBeats = ActionButton(Loc.T("Apply"), ApplyBeatPreview);
        _cancelBeats = ActionButton(Loc.T("Cancel"), CancelBeatPreview);
        _resetGrid = ActionButton(Loc.T("Reset grid"), ResetGridWarp);
        var add = ActionButton(Loc.T("Add anchor"), () => {
            if (!_wave.AddGridAnchor(_gridAnchorBeat.Value, _gridAnchorTime.Value))
                _beatStatus.Text = Loc.T("Choose a beat and time inside the source window before adding an anchor.");
        });
        var remove = ActionButton(Loc.T("Delete anchor"), () => _wave.DeleteSelectedGridAnchor());
        var undo = ActionButton(Loc.T("Undo"), () => { if (_engine.Undo()) { Reload(); _onChanged(); } });
        _beatStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Classes = { "Caption" } };
        _wave.GridSelectionChanged = anchor =>
        {
            _snapSelectedTransient.IsEnabled = _wave.CanSnapSelectedAnchor;
            if (anchor is not { } a) return;
            _syncGridFields = true; _gridAnchorBeat.Value = (double)a.Tick / TempoPoint.TicksPerBeat;
            _gridAnchorTime.Value = a.Seconds; _syncGridFields = false;
        };
        _gridExtractionControls = Section(Loc.T("Grid Warp"),
            Field(Loc.T("Beat"), _gridAnchorBeat), Field(Loc.T("Time (s)"), _gridAnchorTime),
            new WrapPanel { Orientation = Orientation.Horizontal, Children = { add, remove, _resetGrid, undo } });
        _beatSettings = new StackPanel { Spacing = 6, Width = 220, Margin = new Thickness(10), Children = {
            Field(Loc.T("Starting BPM"), _beatStart), Field(Loc.T("Minimum BPM"), _beatMin),
            Field(Loc.T("Maximum BPM"), _beatMax), Field(Loc.T("Beat sensitivity (%)"), _beatSensitivity) } };
        SyncGridExtraction(); return _gridExtractionControls;
    }
    private Control _beatSettings = null!;
    private Control BuildGridQuickBar()
    {
        var options = Button(Loc.T("Beat settings"), () => { });
        var flyout = new Flyout { Content = _beatSettings }; options.Click += (_, _) => flyout.ShowAt(options);
        _beatStatus.TextWrapping = TextWrapping.NoWrap; _beatStatus.TextTrimming = TextTrimming.CharacterEllipsis;
        _beatStatus.VerticalAlignment = VerticalAlignment.Center;
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5,
            Children = { _snapAllBeats, options } };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5,
            Children = { _applyBeats, _cancelBeats } };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8,
            Margin = new Thickness(6, 3), Height = 28, IsVisible = _warpToolMode == 2 };
        bar.Children.Add(controls); Grid.SetColumn(_beatStatus, 1); bar.Children.Add(_beatStatus);
        Grid.SetColumn(actions, 2); bar.Children.Add(actions); _gridQuickBar = bar; return bar;
    }
    private void SyncGridExtraction()
    {
        if (_gridExtractionControls is null) return;
        _gridExtractionControls.IsVisible = _warpToolMode == 2;
        if (_gridQuickBar is not null) _gridQuickBar.IsVisible = _warpToolMode == 2;
        if (_engine.TryGetAudioClipInfo(TrackId, ClipIndex, out var info) && _beatSeedSource != info.SampleId)
        {
            _beatSeedSource = info.SampleId;
            double seed = _engine.TransientAnalysis.OriginalBpm(info.SampleId);
            if (seed <= 0 && info.WarpBeats > 0 && _engine.GetGridReferenceGeometry(TrackId, ClipIndex) is { } geometry)
                seed = info.WarpBeats * 60 / ((geometry.SourceEnd - geometry.SourceStart) / geometry.SourceRate);
            if (seed > 0) _beatStart.Value = Math.Clamp(seed, _beatMin.Value, _beatMax.Value);
            if (_engine.GetGridReferenceGeometry(TrackId, ClipIndex) is { } origin)
            {
                _syncGridFields = true;
                // This section is constructed before Reload fills editor placement fields.
                // Derive both defaults from the same authoritative source/map geometry.
                double firstBeat = _engine.BeatAtSeconds(origin.OriginSeconds);
                double lastBeat = _engine.BeatAtSeconds(origin.OriginSeconds + (origin.SourceEnd - origin.SourceStart) / origin.SourceRate);
                _gridAnchorBeat.Value = firstBeat + Math.Min(1, (lastBeat - firstBeat) / 2);
                _gridAnchorTime.Value = _engine.SecondsAtBeat(_gridAnchorBeat.Value);
                _syncGridFields = false;
            }
        }
        _applyBeats.IsEnabled = _beatPreview is not null && _beatCancellation is null;
        _cancelBeats.IsEnabled = _beatPreview is not null || _beatCancellation is not null;
        _resetGrid.IsEnabled = _engine.ClipGridFollowsAudio(TrackId, ClipIndex) && _engine.GetGridWarpBaseline(TrackId, ClipIndex).Length > 0;
    }
    private void HandleGridWarpEdit(TempoAnchor[] anchors)
    {
        if (_beatPreview is not null)
        {
            if (_beatPreview.Where(a => a.Locked != 0).Any(a => !anchors.Contains(a)))
            { _beatStatus.Text = Loc.T("Existing fixed anchors must stay unchanged during preview."); return; }
            _beatPreview = anchors; _wave.SetGridProposals(anchors); return;
        }
        CommitGridWarpAnchors(anchors); SyncGridExtraction();
    }
    private void EditGridNumeric()
    {
        if (_syncGridFields || _warpToolMode != 2 || !_wave.HasSelectedGridAnchor) return;
        if (!_wave.EditSelectedGridAnchor(_gridAnchorBeat.Value, _gridAnchorTime.Value))
            _beatStatus.Text = Loc.T("Anchor edit rejected: keep beat and time order within the source window.");
    }
    private void CancelBeatPreview()
    {
        var old = _beatCancellation; _beatCancellation = null; old?.Cancel();
        _beatPreview = null; _wave.SetGridProposals(null);
        if (_beatStatus is not null) _beatStatus.Text = Loc.T("Preview cancelled. Project timing is unchanged.");
        if (_beatAttached) SyncGridExtraction();
    }
    public async Task ExtractOriginalTempoAsync()
    {
        CancelBeatPreview();
        if (_warpToolMode != 2) SelectWarpTool(2);
        if (!_engine.TryGetAudioClipInfo(TrackId, ClipIndex, out var info)
            || _engine.GetGridReferenceGeometry(TrackId, ClipIndex) is not { } geometry || ClipIndex < 0) return;
        if (Math.Abs(info.PitchSemitones) > 1e-6)
        { _beatStatus.Text = Loc.T("Set clip pitch to zero before extracting original tempo."); return; }
        _beatDirection = info.Reversed;
        _beatDetectionSettings = Transients.Settings;
        _beatSource = info.SampleId; _beatGeometry = geometry; _beatPreviewRevision = _engine.TempoRevision;
        var cancellation = new CancellationTokenSource(); _beatCancellation = cancellation;
        _beatStatus.Text = Loc.T("Estimating beats from original audio…"); SyncGridExtraction();
        try
        {
            var settings = new SourceBeatSettings(_beatStart.Value, _beatMin.Value, _beatMax.Value, _beatSensitivity.Value / 100);
            long referenceId = _engine.ClipTempoReferenceId(TrackId, ClipIndex);
            var fixedAnchors = _engine.GetTempoMap().Anchors.Where(a => referenceId > 0 && a.ReferenceId == referenceId && a.Locked != 0).OrderBy(a => a.Tick).ToArray();
            double originBeat = _engine.BeatAtSeconds(geometry.OriginSeconds);
            double fixedSpan = fixedAnchors.Length > 0 ? (double)fixedAnchors[^1].Tick / TempoPoint.TicksPerBeat - originBeat : 0;
            var constraints = fixedAnchors.Select(a => new SourceBeatConstraint(a.SourceFrame,
                info.Reversed != 0 ? fixedSpan - ((double)a.Tick / TempoPoint.TicksPerBeat - originBeat)
                    : (double)a.Tick / TempoPoint.TicksPerBeat - originBeat)).ToArray();
            var result = await _engine.TransientAnalysis.AnalyzeBeatsAsync(info.SampleId,
                geometry.SourceStart, geometry.SourceEnd, settings, constraints, cancellation.Token, _beatDetectionSettings);
            if (cancellation.IsCancellationRequested || !ReferenceEquals(_beatCancellation, cancellation) || !_beatAttached) return;
            if (!BeatSourceMatches()) { _beatStatus.Text = Loc.T("Source or project timing changed. Analyse again."); return; }
            double span = result.Beats[^1].Beat;
            var anchors = result.Beats.Select(b => new TempoAnchor(
                TempoPoint.ToTick(originBeat + (info.Reversed != 0 ? span - b.Beat : b.Beat)),
                geometry.OriginSeconds + (info.Reversed != 0 ? geometry.SourceEnd - b.SourceFrame : b.SourceFrame - geometry.SourceStart) / geometry.SourceRate,
                0, TrackId, ClipIndex, 0, b.SourceFrame, referenceId)).OrderBy(a => a.Tick).ToList();
            foreach (var fixedAnchor in fixedAnchors)
            {
                anchors.RemoveAll(a => a.Tick == fixedAnchor.Tick || Math.Abs(a.SourceFrame - fixedAnchor.SourceFrame) < 0.5);
                anchors.Add(fixedAnchor);
            }
            SyncTransients();
            _beatPreview = anchors.OrderBy(a => a.Tick).ToArray(); _wave.SetGridProposals(_beatPreview);
            _beatStatus.Text = Loc.F("{0} proposed beats. Drag or type corrections, then Apply.", result.Beats.Length);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        { _beatStatus.Text = Loc.T("Could not establish a reliable pulse. Adjust starting BPM or add fixed anchors, then try again."); }
        finally
        {
            if (ReferenceEquals(_beatCancellation, cancellation)) _beatCancellation = null;
            cancellation.Dispose(); if (_beatAttached) SyncGridExtraction();
        }
    }
    private bool BeatSourceMatches()
        => Transients.Settings == _beatDetectionSettings && _engine.TempoRevision == _beatPreviewRevision
            && _engine.TryGetAudioClipInfo(TrackId, ClipIndex, out var info) && info.SampleId == _beatSource && info.Reversed == _beatDirection && Math.Abs(info.PitchSemitones) < 1e-6
            && _engine.GetGridReferenceGeometry(TrackId, ClipIndex) == _beatGeometry;
    private void ApplyBeatPreview()
    {
        if (_beatPreview is not { } anchors) return;
        if (!BeatSourceMatches()) { _beatStatus.Text = Loc.T("Source or project timing changed. Analyse again."); return; }
        var current = _engine.GetTempoMap();
        if (!GridWarpMapping.TryBuildMap(_engine, current, anchors, TrackId, ClipIndex,
            _engine.ClipTempoReferenceId(TrackId, ClipIndex), _reversed, out var candidate, out _)
            || !_engine.TryCommitGridWarp(TrackId, ClipIndex, candidate, out _))
        { _beatStatus.Text = Loc.T("Cannot apply beats: check fixed anchors, tempo limits and Step region boundaries."); return; }
        _beatPreview = null; _wave.SetGridProposals(null); _beatStatus.Text = Loc.T("Original tempo applied to the Tempo Track.");
        Reload(); _onChanged(); SyncGridExtraction();
    }
    private void ResetGridWarp()
    {
        CancelBeatPreview();
        if (!_engine.TryResetGridWarp(TrackId, ClipIndex, out _))
        { _beatStatus.Text = Loc.T("Cannot reset this region without moving another fixed timing anchor."); return; }
        Reload(); _onChanged(); WarpBuildRequested?.Invoke(); SyncGridExtraction();
        _beatStatus.Text = Loc.T("Grid region restored.");
    }

    private sealed partial class WaveformView
    {
        private TempoAnchor[]? _gridProposals, _gridNumericPreview, _gridNumericOriginal;
        public void BeginGridNumericPreview() { _gridNumericOriginal = GridAnchors().ToArray(); _gridNumericPreview = _gridNumericOriginal.ToArray(); }
        public void EndGridNumericPreview()
        {
            var candidate = _gridNumericPreview; var original = _gridNumericOriginal;
            _gridNumericPreview = _gridNumericOriginal = null;
            if (candidate is not null && (original is null || !candidate.SequenceEqual(original))) GridWarpCommitted?.Invoke(candidate);
        }
        private long _gridSelectedTick = -1;
        public Action<TempoAnchor?>? GridSelectionChanged;
        public void SetGridProposals(TempoAnchor[]? proposals) { _gridNumericPreview = _gridNumericOriginal = null; CancelGridWarpDrag(); _gridProposals = proposals; Redraw(); }
        private void SelectGridAnchor(TempoAnchor anchor) { _gridSelectedTick = anchor.Tick; GridSelectionChanged?.Invoke(anchor); Redraw(); }
        public bool HasSelectedGridAnchor => GridAnchors().Any(a => a.Tick == _gridSelectedTick);
        public bool EditSelectedGridAnchor(double beat, double seconds)
        {
            var anchors = GridAnchors(); int index = Array.FindIndex(anchors, a => a.Tick == _gridSelectedTick);
            if (index < 0 || !double.IsFinite(beat) || !double.IsFinite(seconds)) return false;
            var old = anchors[index];
            if (_gridProposals is not null && old.Locked != 0) return false;
            var reference = GridReference();
            double source = reference.SourceFrame + (seconds - reference.Seconds) * _gridRate * (_reversed ? -1 : 1);
            if (source < _gridSourceStart || source > _gridSourceEnd) return false;
            anchors[index] = old with { Tick = TempoPoint.ToTick(beat), Seconds = seconds, SourceFrame = source };
            if (!ValidGridGroup(anchors)) return false;
            _gridSelectedTick = anchors[index].Tick;
            if (_gridNumericPreview is not null) { _gridNumericPreview = anchors.OrderBy(a => a.Tick).ToArray(); Redraw(); }
            else GridWarpCommitted?.Invoke(anchors.OrderBy(a => a.Tick).ToArray()); return true;
        }
        public bool AddGridAnchor(double beat, double seconds)
        {
            var reference = GridReference(); var current = GridAnchors().OrderBy(a => a.Tick).ToArray();
            if (current.Length < 2 || !double.IsFinite(beat) || !double.IsFinite(seconds)) return false;
            double source = reference.SourceFrame + (seconds - reference.Seconds) * _gridRate * (_reversed ? -1 : 1);
            long tick = TempoPoint.ToTick(beat);
            if (current.Any(a => a.Tick == tick))
            {
                int at = Array.FindIndex(current, a => a.Tick == tick);
                int gap = at + 1 < current.Length ? at : at - 1;
                if (gap < 0 || current[gap + 1].Tick - current[gap].Tick <= 1) return false;
                var left = current[gap]; var right = current[gap + 1]; tick = left.Tick + (right.Tick - left.Tick) / 2;
                double weight = (double)(tick - left.Tick) / (right.Tick - left.Tick);
                source = left.SourceFrame + (right.SourceFrame - left.SourceFrame) * weight;
            }
            if (source < _gridSourceStart || source > _gridSourceEnd) return false;
            var anchors = AnchorsWithGridEdit(tick, source);
            if (!ValidGridGroup(anchors)) return false;
            GridWarpCommitted?.Invoke(anchors); _gridSelectedTick = tick;
            GridSelectionChanged?.Invoke(anchors.First(a => a.Tick == tick)); return true;
        }
        public void DeleteSelectedGridAnchor()
        {
            var anchors = GridAnchors(); var selected = anchors.FirstOrDefault(a => a.Tick == _gridSelectedTick);
            if (anchors.Length <= 2 || selected.Tick != _gridSelectedTick || IsGridBoundary(selected)
                || _gridProposals is not null && selected.Locked != 0) return;
            GridWarpCommitted?.Invoke(anchors.Where(a => a.Tick != _gridSelectedTick).ToArray()); _gridSelectedTick = -1;
            GridSelectionChanged?.Invoke(null);
        }
        private bool ValidGridGroup(TempoAnchor[] anchors)
        {
            var sorted = anchors.OrderBy(a => a.Tick).ToArray();
            for (int i = 1; i < sorted.Length; i++)
                if (!GridWarpMapping.TryCalculateBpm(sorted[i - 1], sorted[i], _reversed, _gridMap.MinBpm, _gridMap.MaxBpm, out _)) return false;
            return sorted.Length >= 2;
        }
    }
}
