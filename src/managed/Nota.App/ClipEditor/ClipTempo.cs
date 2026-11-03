// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
//
// An audio clip's warp tempo (#248, specs/036-clip-editor-batch): the tempo its played region is
// treated as recorded at — the region's warp-beat span against the source frames it covers. For
// one warp segment that is the segment's BPM chip. Setting a tempo scales every marker's beat
// (and the trim window) by one factor, so hand-placed markers keep their relative timing.
// Free of Avalonia and the engine so the smoke test can link it.

using System;
using Nota.Application;

namespace Nota.App;

internal static class ClipTempo
{
    public const double MinBpm = 20, MaxBpm = 999;

    /// <summary>Source frame at a warp beat, piecewise linear through markers sorted by source;
    /// past either end it extends the end segment's slope.</summary>
    public static double SrcAt(ReadOnlySpan<double> src, ReadOnlySpan<double> beat, double b)
        => WarpPositionMap.SrcAt(src, beat, b);

    /// <summary>Warp beat at a source frame (the inverse of <see cref="SrcAt"/>).</summary>
    public static double BeatAt(ReadOnlySpan<double> src, ReadOnlySpan<double> beat, double s)
        => WarpPositionMap.BeatAt(src, beat, s);

    /// <summary>Tempo of the played window [playStart, playEnd] (playEnd 0 = to the last marker),
    /// or 0 when it can't be told.</summary>
    public static double Bpm(ReadOnlySpan<double> src, ReadOnlySpan<double> beat, double playStart, double playEnd, double sampleRate)
    {
        int n = Math.Min(src.Length, beat.Length);
        if (n < 2 || sampleRate <= 0) return 0;
        double pe = playEnd > 0 ? playEnd : beat[n - 1];
        double frames = SrcAt(src, beat, pe) - SrcAt(src, beat, playStart);
        return frames > 0 && pe > playStart ? (pe - playStart) * sampleRate * 60.0 / frames : 0;
    }

    /// <summary>Scale every marker beat and the trim window so the clip reads at
    /// <paramref name="newBpm"/>. playEnd 0 stays 0 ("to the end").</summary>
    public static (double[] beats, double playStart, double playEnd) Scale(
        ReadOnlySpan<double> beat, double playStart, double playEnd, double oldBpm, double newBpm)
    {
        double k = oldBpm > 0 && newBpm > 0 ? newBpm / oldBpm : 1;
        var nb = new double[beat.Length];
        for (int i = 0; i < beat.Length; i++) nb[i] = beat[i] * k;
        return (nb, playStart * k, playEnd > 0 ? playEnd * k : 0);
    }

    public static bool InRange(double bpm) => bpm >= MinBpm - 1e-9 && bpm <= MaxBpm + 1e-9;
    public static double? Half(double bpm) => bpm > 0 && InRange(bpm / 2) ? bpm / 2 : null;
    public static double? Double(double bpm) => bpm > 0 && InRange(bpm * 2) ? bpm * 2 : null;
    public static double Clamp(double bpm) => Math.Clamp(bpm, MinBpm, MaxBpm);
}
