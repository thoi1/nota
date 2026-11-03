// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

namespace Nota.Application;

/// <summary>Immutable source-to-content-to-arrangement composition. Length comes from the
/// engine, so unwarped varispeed and sample-rate conversion follow the actual playback.
/// Reuses the editor's ClipTimeMap; no separate musical clock or warp representation.</summary>
public sealed class TransientClipMap
{
    private readonly double[] _src, _beat;
    private readonly long _frames;
    public ClipTimeMap Placement { get; }
    public bool Valid { get; }

    public TransientClipMap(NotaAudioClipInfo clip, NotaSampleInfo source, double lengthBeats,
        double[]? markerSource = null, double[]? markerBeats = null,
        Func<double, double>? secondsAtBeat = null, Func<double, double>? beatAtSeconds = null)
    {
        _frames = source.Frames;
        _src = markerSource?.ToArray() ?? Array.Empty<double>();
        _beat = markerBeats?.ToArray() ?? Array.Empty<double>();
        bool warp = clip.WarpEnabled != 0;
        double end = clip.WarpPlayEnd > 0 ? clip.WarpPlayEnd : clip.WarpBeats;
        double len = clip.LengthFrames > 0 ? clip.LengthFrames : source.Frames - clip.SourceOffsetFrames;
        Placement = warp
            ? new(clip.StartBeat, lengthBeats, ClipContentDomain.WarpBeats, clip.WarpBeats,
                clip.WarpPlayStart, end, clip.Reversed != 0, false)
            : new(clip.StartBeat, lengthBeats, ClipContentDomain.SourceFrames, source.Frames,
                clip.SourceOffsetFrames, clip.SourceOffsetFrames + len, clip.Reversed != 0, false, secondsAtBeat, beatAtSeconds);
        Valid = source.Frames > 0 && source.SampleRate > 0 && double.IsFinite(lengthBeats) && lengthBeats > 0
            && double.IsFinite(clip.StartBeat) && double.IsFinite(Placement.PlayStart)
            && double.IsFinite(Placement.PlayEnd) && Placement.Valid
            && (!warp || WarpPositionMap.Valid(_src, _beat));
    }

    public double ContentAtSource(double sample)
        => Placement.Domain == ClipContentDomain.SourceFrames ? sample / Placement.ContentTotal
            : WarpPositionMap.BeatAt(_src, _beat, sample) / Placement.ContentTotal;
    public double SourceAtContent(double fraction)
        => Placement.Domain == ClipContentDomain.SourceFrames ? fraction * Placement.ContentTotal
            : WarpPositionMap.SrcAt(_src, _beat, fraction * Placement.ContentTotal);
    public double SourceAtArrangementBeat(double beat)
        => SourceAtContent(Placement.ContentAtArrangementBeat(beat));
    public bool TryArrangementBeat(double sourceSample, out double beat)
    {
        beat = 0;
        if (!Valid || !double.IsFinite(sourceSample) || sourceSample < 0 || sourceSample >= _frames) return false;
        double content = ContentAtSource(sourceSample);
        double fraction = Placement.PlayFracAtContent(content);
        if (fraction < -1e-10 || fraction > 1 + 1e-10) return false;
        beat = Placement.ArrangementBeatAtContent(content);
        return true;
    }
}
