// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Nota.Application.Editing;

public enum ArrangementTool { Select, Pencil, Eraser, WaveformDraw, NotaWarp }
public enum AutomationWaveform { Sine, Triangle, Sawtooth, Square, Freehand }

/// <summary>Bounded drawing math shared by Arrangement lanes; never processes sample data.</summary>
public static class ArrangementDrawing
{
    public const int MaxStrokePoints = 4096;

    public static double Wave(AutomationWaveform shape, double cycles)
    {
        double t = cycles - Math.Floor(cycles);
        return shape switch
        {
            AutomationWaveform.Sine => Math.Sin(t * Math.Tau),
            AutomationWaveform.Triangle => 1 - 4 * Math.Abs(t - 0.5),
            AutomationWaveform.Sawtooth => 2 * t - 1,
            AutomationWaveform.Square => t < 0.5 ? 1 : -1,
            _ => 0,
        };
    }

    /// <summary>Samples the actual musical grid, including its partial bar at odd meters.
    /// Long sweeps are decimated on grid lines to bound UI work; snap-off uses a visual step.</summary>
    public static double[] SampleBeats(double from, double to, double step, int beatsPerBar)
    {
        if (!double.IsFinite(from) || !double.IsFinite(to) || !double.IsFinite(step) || step <= 0) return [];
        double lo = Math.Max(0, Math.Min(from, to)), hi = Math.Max(0, Math.Max(from, to));
        var result = new List<double> { lo };
        double estimated = (hi - lo) / step + (hi - lo) / Math.Max(1, beatsPerBar) + 3;
        if (estimated > MaxStrokePoints)
        {
            // Snap each decimated sample back to the original grid: multiplying the grid
            // step would lose its per-bar restart in odd meters.
            for (int i = 1; i < MaxStrokePoints - 1; i++)
            {
                double b = SnapGrid.SnapBeat(lo + (hi - lo) * i / (MaxStrokePoints - 1), step, beatsPerBar);
                if (b > result[^1] + 1e-8 && b < hi - 1e-8) result.Add(b);
            }
        }
        else
            foreach (double b in SnapGrid.GridLines(lo, hi, step, beatsPerBar))
                if (b > lo + 1e-8 && b < hi - 1e-8) result.Add(b);
        if (hi > lo + 1e-8) result.Add(hi);
        return result.ToArray();
    }

    public static AutomationPoint[] ReplaceRange(IEnumerable<AutomationPoint> original,
        IEnumerable<AutomationPoint> drawn, double from, double to)
    {
        double lo = Math.Min(from, to), hi = Math.Max(from, to);
        return original.Where(p => p.Beat < lo - 1e-8 || p.Beat > hi + 1e-8)
            .Concat(drawn).OrderBy(p => p.Beat).ToArray();
    }

    public static AutomationPoint[] WavePoints(AutomationWaveform shape, double from, double to,
        double anchor, double step, int beatsPerBar, float min, float max, double center,
        double amplitude, double period, double phase)
        => SampleBeats(from, to, step, beatsPerBar).Select(b => new AutomationPoint(b,
            (float)Math.Clamp(center + (max - min) * 0.5 * Math.Clamp(amplitude, 0, 1) *
                Wave(shape, (b - anchor) / Math.Max(1e-6, period) + phase), min, max), 0)).ToArray();

    /// <summary>Constrain a MIDI drawing gesture to the empty gap where it started.</summary>
    public static (double Start, double Length)? ClipInterval(double anchor, double end,
        double minimumLength, IEnumerable<(double Start, double Length)> occupied)
    {
        if (!double.IsFinite(anchor) || !double.IsFinite(end) || minimumLength <= 0) return null;
        double lo = 0, hi = double.MaxValue;
        foreach (var clip in occupied)
        {
            double right = clip.Start + clip.Length;
            if (anchor >= clip.Start && anchor < right) return null;
            if (right <= anchor) lo = Math.Max(lo, right);
            if (clip.Start > anchor) hi = Math.Min(hi, clip.Start);
        }
        double a = Math.Max(lo, Math.Min(anchor, end));
        double b = Math.Min(hi, Math.Max(anchor, end));
        if (b - a < 1e-8) b = Math.Min(hi, a + minimumLength);
        return b - a > 1e-8 ? (a, b - a) : null;
    }
}
