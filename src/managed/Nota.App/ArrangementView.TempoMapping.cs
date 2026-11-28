// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Nota.Application;
using Nota.Application.Editing;
using Nota.Application.Localization;
using Nota.Presentation;

namespace Nota.App;

public sealed partial class ArrangementView
{
    private ulong _mappingHandle;
    private IAudioEngine? _mappingEngine;
    private double _mappingOrigin, _mappingScale, _mappingPressX, _mappingStartSeconds;
    private double _mappingScrollBeat, _mappingBar, _mappingSnapStep;
    private bool _mappingSnap;
    private TempoMappingPreview _mappingLimits;
    private TempoMappingPreview? _mappingPreview;
    private AutoPt[]? _mappingPoints;

    private bool IsTempoMappingTarget(TrackVM track) => IsMaster(track) && track.AutoTarget == AutomationTarget.Tempo;

    private double MappingBoundary(double x)
    {
        double under = XToBeat(x);
        double beat = Math.Round(under / TempoBarLength) * TempoBarLength;
        double whole = Math.Round(under);
        if (_pixelsPerBeat >= 30 && Math.Abs(BeatToX(whole) - x) < Math.Abs(BeatToX(beat) - x))
            beat = whole;
        return beat > 0 && Math.Abs(BeatToX(beat) - x) <= 8 ? beat : 0;
    }

    private bool BeginTimeMapping(double beat, double x)
    {
        if (_engine is not { } engine || beat <= 0 || _mappingHandle != 0) return false;
        ulong handle = engine.BeginTempoMapping(beat);
        if (handle == 0) return false;
        _mappingOrigin = engine.BeatToSeconds(_scrollBeats);
        _mappingScale = _pixelsPerBeat * (TempoAxis ? 2 : engine.Bpm / 60.0);
        _mappingPressX = x;
        _mappingStartSeconds = engine.BeatToSeconds(beat);
        if (!engine.TryPreviewTempoMapping(handle, _mappingStartSeconds, out _mappingLimits))
        {
            engine.FinishTempoMapping(handle, false);
            return false;
        }
        _mappingSnap = SnapGridState.SnapOn;
        _mappingSnapStep = SnapStepBeats;
        _mappingBar = GridBarLength;
        _mappingScrollBeat = _scrollBeats;
        _mappingEngine = engine;
        _mappingHandle = handle;
        return true;
    }

    private bool UpdateTimeMapping(double x, KeyModifiers mods)
    {
        if (_mappingEngine is not { } engine || !ReferenceEquals(_engine, engine)) return false;
        double desired = _mappingStartSeconds + (x - _mappingPressX) / _mappingScale;
        if (Math.Abs(x - _mappingPressX) < 0.5) desired = _mappingStartSeconds;
        if (_mappingSnap && !mods.HasFlag(KeyModifiers.Alt) && desired != _mappingStartSeconds)
        {
            double edge = engine.SecondsToBeat(Math.Clamp(desired, _mappingLimits.MinimumSeconds, _mappingLimits.MaximumSeconds));
            double best = _mappingStartSeconds, error = Math.Abs(best - desired);
            foreach (double beat in SnapGrid.GridLines(Math.Max(0, edge - _mappingSnapStep),
                         edge + _mappingSnapStep, _mappingSnapStep, _mappingBar))
            {
                double seconds = engine.BeatToSeconds(beat);
                double distance = Math.Abs(seconds - desired);
                if (seconds < _mappingLimits.MinimumSeconds - 1e-9 || seconds > _mappingLimits.MaximumSeconds + 1e-9 ||
                    distance >= error) continue;
                best = seconds;
                error = distance;
            }
            desired = best;
        }
        if (!engine.TryPreviewTempoMapping(_mappingHandle, desired, out var preview))
            return double.IsFinite(engine.ConvertTempoMapping(_mappingHandle, 0, toBeat: false));
        _mappingPreview = preview;
        _mappingPoints = engine.GetTempoMappingPoints(_mappingHandle)
            .Select(point => new AutoPt { Beat = point.Beat, Value = point.Value, Curve = point.Curve }).ToArray();
        Redraw();
        return true;
    }

    private void FinishTimeMapping(bool commit)
    {
        if (_mappingEngine is not { } engine) return;
        ulong handle = _mappingHandle;
        _mappingHandle = 0;
        _mappingEngine = null;
        _mappingPreview = null;
        _mappingPoints = null;
        bool finished = engine.FinishTempoMapping(handle, commit && ReferenceEquals(_engine, engine));
        if (commit && finished && ReferenceEquals(_engine, engine) && engine.IsTempoAutomated)
        {
            _scrollBeats = Math.Max(0, engine.SecondsToBeat(_mappingOrigin));
            _pixelsPerBeat = _mappingScale / 2;
        }
        else _scrollBeats = _mappingScrollBeat;
        ReloadTempo();
        if (commit && finished) BuildTempoWarpCaches();
        Refresh();
    }

    private double MappingConvert(double value, bool toBeat)
        => _mappingEngine?.ConvertTempoMapping(_mappingHandle, value, toBeat) ?? double.NaN;

    private static readonly IPen TimeMappingGuide = new Pen(NotaPalette.Accent, 1);

    private void DrawTimeMappingGuide(DrawingContext ctx, double width, double height, bool label = false)
    {
        if (_mappingPreview is not { } preview) return;
        double x = SecondsToX(preview.Seconds);
        if (x < 0 || x > width) return;
        ctx.DrawLine(TimeMappingGuide, new Point(x + .5, 0), new Point(x + .5, height));
        if (!label) return;
        string position = RulerLabels.Time(preview.Seconds, .001, NotaNum.Culture);
        var text = new NotaFormattedText(Loc.F("tempo.time_mapping_readout", position, preview.BpmBefore, preview.BpmAfter),
            NotaNum.Culture, FlowDirection.LeftToRight, NotaFonts.Measure, NotaType.Axis, NotaPalette.TextPrimary);
        double left = Math.Max(0, Math.Min(x + 6, width - text.Width - 8));
        ctx.FillRectangle(NotaPalette.SurfaceRaised, new Rect(left, 3, text.Width + 8, text.Height + 4));
        ctx.DrawText(text, new Point(left + 4, 5));
    }
}
