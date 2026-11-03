// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

namespace Nota.Application;

/// <summary>Shared piecewise source/beat mapping, extracted from the clip editor's
/// ClipTempo. End segments extrapolate; callers decide which played window is visible.</summary>
public static class WarpPositionMap
{
    public static double SrcAt(ReadOnlySpan<double> src, ReadOnlySpan<double> beat, double b)
        => Interpolate(beat, src, b);
    public static double BeatAt(ReadOnlySpan<double> src, ReadOnlySpan<double> beat, double s)
        => Interpolate(src, beat, s);

    private static double Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y, double at)
    {
        int n = Math.Min(x.Length, y.Length);
        if (n < 2) return n == 1 ? y[0] : 0;
        int lo = 0, hi = n - 2;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (x[mid] <= at) lo = mid; else hi = mid - 1;
        }
        double span = x[lo + 1] - x[lo];
        return span <= 0 ? y[lo] : y[lo] + (y[lo + 1] - y[lo]) * (at - x[lo]) / span;
    }

    public static bool Valid(ReadOnlySpan<double> src, ReadOnlySpan<double> beat)
    {
        if (src.Length < 2 || src.Length != beat.Length) return false;
        for (int i = 0; i < src.Length; i++)
            if (!double.IsFinite(src[i]) || !double.IsFinite(beat[i])
                || (i > 0 && (src[i] <= src[i - 1] || beat[i] <= beat[i - 1]))) return false;
        return true;
    }
}
