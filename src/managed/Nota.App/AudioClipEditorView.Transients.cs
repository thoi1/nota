// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Linq;
using Nota.Application.Editing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Nota.Application;
using Nota.Application.Localization;
using static Nota.App.ClipEditorKit;

namespace Nota.App;

public sealed partial class AudioClipEditorView
{
    private TransientWorkspace Transients => TransientWorkspace.For(_engine);
    private long _transientSource;
    private Button _analyzeTransients = null!;
    private Button _snapSelectedTransient = null!;
    private TextBlock _transientStatus = null!;
    private double _thresholdPercent = 50, _minimumSpacingMs = 30;
    private Action _syncThreshold = () => { }, _syncSpacing = () => { };
    private string? _warpEditError;
    private Action _syncShowTransients = () => { }, _syncSnapTransients = () => { };

    private Control BuildTransientSection()
    {
        _analyzeTransients = ClipEditorKit.Button(Loc.T("Analyse transients"), () =>
        {
            if (Transients.IsRunning(_transientSource)) Transients.Cancel(_transientSource);
            else _ = Transients.AnalyzeAsync(_transientSource);
        });
        ToolTip.SetTip(_analyzeTransients, Loc.T("Find source attacks in the background without changing the audio or tempo"));
        var show = SwitchRow(Loc.T("Show transients"), () => Transients.Show,
            () => Transients.SetShow(!Transients.Show), out _syncShowTransients);
        var snap = SwitchRow(Loc.T("Snap to transients"), () => Transients.Snap,
            () => Transients.SetSnap(!Transients.Snap), out _syncSnapTransients);
        var sensitivity = SliderRow(Loc.T("Detection threshold"), 84,
            () => _thresholdPercent / 100,
            value => { _thresholdPercent = Math.Round(value * 100); ConfigureTransients(); },
            () => $"{_thresholdPercent:0}\u2009%", out _syncThreshold, valueW: 40);
        ToolTip.SetTip(sensitivity, Loc.T("Lower values detect weaker transients; higher values detect only stronger transients."));
        var spacing = SliderRow(Loc.T("Minimum spacing"), 66,
            () => (_minimumSpacingMs - 5) / 245,
            value => { _minimumSpacingMs = Math.Round(5 + value * 245); ConfigureTransients(); },
            () => $"{_minimumSpacingMs:0}\u2009ms", out _syncSpacing, valueW: 48);
        _transientStatus = Hint(Loc.T("Not analysed"));
        _snapSelectedTransient = ClipEditorKit.Button(Loc.T("Snap selected anchor"), () => _wave.SnapSelectedAnchor());
        ToolTip.SetTip(_snapSelectedTransient, Loc.T("Move only the selected anchor's source to the nearest attack; keep its grid position"));
        _wave.SelectionChanged = () => _snapSelectedTransient.IsEnabled = _wave.CanSnapSelectedAnchor;
        return Section(Loc.T("Transients"), _analyzeTransients, show, snap, _snapSelectedTransient,
            sensitivity, spacing, _transientStatus);
    }
    private void SetTempoWorkflow(bool gridFollows, bool protect)
    {
        if (gridFollows && !protect && _engine.ClipGridFollowsAudio(TrackId, ClipIndex)) return;
        if (!_engine.TrySetClipTempoMode(TrackId, ClipIndex, gridFollows, protect, out string error))
        {
            _warpEditError = error.Contains("protected", StringComparison.OrdinalIgnoreCase)
                ? Loc.T("Protected timing anchors prevent this edit. Unlock them in the Tempo Track first.")
                : Loc.T("Cannot fit the grid: check source anchors, point order and BPM limits.");
            _transientStatus.Text = _warpEditError;
            return;
        }
        _warpEditError = null;
        _onChanged(); Reload(); WarpBuildRequested?.Invoke();
    }

    private void ConfigureTransients()
    {
        Transients.Configure(new(1 - _thresholdPercent / 100, _minimumSpacingMs / 1000), _transientSource);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _beatAttached = true;
        Transients.Changed += SyncTransients;
        Transients.ProgressChanged += SyncTransientProgress;
        SyncTransients();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _beatAttached = false;
        CancelBeatPreview();
        Transients.Changed -= SyncTransients;
        Transients.ProgressChanged -= SyncTransientProgress;
        base.OnDetachedFromVisualTree(e);
    }

    private void SyncTransients()
    {
        var state = Transients;
        if ((_beatCancellation is not null || _beatPreview is not null) && _beatDetectionSettings != state.Settings)
            CancelBeatPreview();
        _syncShowTransients(); _syncSnapTransients();
        _thresholdPercent = (1 - state.Settings.Sensitivity) * 100;
        _minimumSpacingMs = state.Settings.MinimumSpacingSeconds * 1000;
        _syncThreshold(); _syncSpacing();
        _wave.SetTransients(state.Snapshot(_transientSource), state.Show, state.Snap);
        _snapSelectedTransient.IsEnabled = _wave.CanSnapSelectedAnchor;
        SyncTransientProgress();
    }
    private void SyncTransientProgress()
    {
        bool running = Transients.IsRunning(_transientSource);
        _analyzeTransients.IsEnabled = _transientSource > 0;
        _analyzeTransients.Content = running ? Loc.T("Cancel analysis") : Loc.T("Analyse transients");
        _transientStatus.Text = running ? Loc.F("Analysing… {0:0}\u2009%", Transients.Progress(_transientSource) * 100)
            : Transients.Error(_transientSource) ?? (_wave.TransientCount is int n && n > 0
                ? Loc.Plural(n, "{0} transient", "{0} transients")
                : Transients.Snapshot(_transientSource) is null ? Loc.T("Not analysed") : Loc.T("No transients found"));
        if (_warpEditError is { } error) _transientStatus.Text = error;
    }

    private sealed partial class WaveformView
    {
        private TransientSnapshot? _transients;
        private bool _showTransients = true, _snapTransients = true;
        private int _nearestTransient = -1;
        private static readonly IPen TransientPen = new Pen(NotaPalette.Wash(NotaPalette.TextPrimary, 0x68), 1);
        private static readonly IPen TransientHotPen = new Pen(NotaPalette.Accent, 1);
        public int TransientCount => _transients?.Transients.Count ?? 0;
        public void SetTransients(TransientSnapshot? snapshot, bool show, bool snap)
        {
            if (ReferenceEquals(snapshot, _transients) && show == _showTransients && snap == _snapTransients) return;
            _transients = snapshot; _showTransients = show; _snapTransients = snap;
            _nearestTransient = -1;
            Redraw();
        }
        private double SourceToContent(double sample)
        {
            if (_mk.Count < 2) return _srcTotal > 0 ? sample / _srcTotal : 0;
            return WarpPositionMap.BeatAt(_cSrc, _lBeat, sample) / TotalBeats;
        }
        private double ContentToSource(double content)
        {
            if (_mk.Count < 2) return content * _srcTotal;
            return WarpPositionMap.SrcAt(_cSrc, _lBeat, content * TotalBeats);
        }
        public Action? SelectionChanged;
        public bool CanSnapSelectedAnchor => _warpToolMode == 2 ? CanSnapSelectedGridAnchor
            : _selMk > 0 && _selMk < _mk.Count - 1 && _transients is { Transients.Count: > 0 };
        public void SnapSelectedAnchor()
        {
            if (_warpToolMode == 2) { SnapSelectedGridAnchor(); return; }
            if (!CanSnapSelectedAnchor || _transients is not { } data) return;
            double sample = _mk[_selMk].src;
            int at = data.LowerBound((long)sample);
            int best = -1; double distance = double.MaxValue;
            for (int i = Math.Max(0, at - 1); i <= Math.Min(data.Transients.Count - 1, at); i++)
            {
                double candidate = data.Transients[i].SamplePosition;
                if (candidate <= _mk[_selMk - 1].src || candidate >= _mk[_selMk + 1].src) continue;
                if (Math.Abs(candidate - sample) < distance) { best = i; distance = Math.Abs(candidate - sample); }
            }
            if (best < 0 || data.Transients[best].SamplePosition == sample) return;
            _mk[_selMk] = (data.Transients[best].SamplePosition, _mk[_selMk].beat);
            Commit();
        }
        private bool CanSnapSelectedGridAnchor
        {
            get
            {
                var anchors = GridAnchors();
                int index = Array.FindIndex(anchors, a => a.Tick == _gridSelectedTick);
                return !_envMode && !_adsrMode && index > 0 && index + 1 < anchors.Length
                    && !IsGridBoundary(anchors[index]) && (_gridProposals is null || anchors[index].Locked == 0)
                    && _transients is { Transients.Count: > 0 };
            }
        }
        private void SnapSelectedGridAnchor()
        {
            if (!CanSnapSelectedGridAnchor || _transients is not { } data) return;
            var anchors = GridAnchors();
            int index = Array.FindIndex(anchors, a => a.Tick == _gridSelectedTick);
            var selected = anchors[index]; var reference = GridReference();
            double best = selected.SourceFrame, distance = double.MaxValue;
            double low = Math.Min(anchors[index - 1].SourceFrame, anchors[index + 1].SourceFrame);
            double high = Math.Max(anchors[index - 1].SourceFrame, anchors[index + 1].SourceFrame);
            for (int i = data.LowerBound((long)Math.Max(0, low)); i < data.Transients.Count; i++)
            {
                double frame = data.Transients[i].SamplePosition;
                if (frame >= high) break;
                if (frame <= low || Math.Abs(frame - selected.SourceFrame) >= distance) continue;
                var candidate = selected with { SourceFrame = frame,
                    Seconds = GridWarpMapping.SecondsAtSourceFrame(frame, reference, _gridRate, _reversed) };
                if (!GridWarpMapping.TryCalculateBpm(anchors[index - 1], candidate, _reversed, _gridMap.MinBpm, _gridMap.MaxBpm, out _)
                    || !GridWarpMapping.TryCalculateBpm(candidate, anchors[index + 1], _reversed, _gridMap.MinBpm, _gridMap.MaxBpm, out _)) continue;
                best = frame; distance = Math.Abs(frame - selected.SourceFrame);
            }
            if (best == selected.SourceFrame) return;
            var updated = AnchorsWithGridEdit(selected.Tick, best);
            GridWarpCommitted?.Invoke(updated);
            SelectGridAnchor(updated.First(a => a.Tick == selected.Tick));
        }

        private int NearestTransient(double x, double w)
        {
            if (!_snapTransients || _free || _transients is not { } data || data.Transients.Count == 0) return -1;
            double source = ContentToSource(XToFrac(x, w));
            int at = data.LowerBound((long)Math.Clamp(source, 0, long.MaxValue));
            int best = -1; double distance = 8;
            for (int i = Math.Max(0, at - 1); i <= Math.Min(data.Transients.Count - 1, at); i++)
            {
                double content = SourceToContent(data.Transients[i].SamplePosition);
                double played = Map.PlayFracAtContent(content);
                if (played < 0 || played > 1) continue;
                double d = Math.Abs(FracToX(content, w) - x);
                if (d <= distance) { best = i; distance = d; }
            }
            return best;
        }
        private void HighlightTransient(double x, double w)
        {
            int nearest = NearestTransient(x, w);
            if (_nearestTransient == nearest) return;
            _nearestTransient = nearest;
            Redraw();
        }
        private void DrawTransients(DrawingContext ctx, double w, double h)
        {
            if (!_showTransients || _transients is not { } data || _envMode || _adsrMode) return;
            // A bounded number of strokes, no waveform/transient geometry allocation on
            // pointer moves or playback. Binary search skips attacks collapsed into a pixel.
            double a = ContentToSource(XToFrac(0, w)), b = ContentToSource(XToFrac(w, w));
            int i = data.LowerBound((long)Math.Max(0, Math.Min(a, b)));
            double lastX = double.NaN;
            while (i < data.Transients.Count && data.Transients[i].SamplePosition <= Math.Max(a, b))
            {
                double content = SourceToContent(data.Transients[i].SamplePosition);
                double played = Map.PlayFracAtContent(content);
                double x = FracToX(content, w);
                if (played >= 0 && played <= 1 && (double.IsNaN(lastX) || Math.Abs(x - lastX) >= 3))
                {
                    ctx.DrawLine(TransientPen, new Point(x + .5, RulerH + 15), new Point(x + .5, h));
                    lastX = x;
                }
                double nextContent = XToFrac(x + (_reversed ? -3 : 3), w);
                long nextSource = (long)Math.Max(0, ContentToSource(nextContent));
                i = Math.Max(i + 1, data.LowerBound(nextSource));
            }
            if (_nearestTransient >= 0 && _nearestTransient < data.Transients.Count)
            {
                double x = FracToX(SourceToContent(data.Transients[_nearestTransient].SamplePosition), w);
                ctx.DrawLine(TransientHotPen, new Point(x + .5, RulerH + 15), new Point(x + .5, h));
            }
        }
    }
}
