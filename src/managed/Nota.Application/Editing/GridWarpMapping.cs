// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Application.Localization;

namespace Nota.Application.Editing;

public readonly record struct GridWarpLine(long Tick, int Bar, int BeatInBar, bool IsBar)
{
    public double Beat => (double)Tick / TempoPoint.TicksPerBeat;
}

public static class GridWarpMapping
{
    private const int MaxGridLines = 8192;

    public static bool IsLinkedTempoPoint(TempoMapState map, int pointIndex)
        => pointIndex >= 0 && pointIndex < map.Points.Length
            && map.Anchors.Any(a => a.AnchorId > 0 && a.AnchorId == map.Points[pointIndex].LinkedAnchorId && a.Locked != 0 && a.ReferenceId > 0);

    public static GridWarpLine[] GridLines(double firstBeat, double lastBeat, int numerator, int denominator)
    {
        if (!double.IsFinite(firstBeat) || !double.IsFinite(lastBeat) || firstBeat < 0 || lastBeat < firstBeat)
            throw new ArgumentOutOfRangeException(nameof(firstBeat));
        if (numerator is < 1 or > 16 || denominator is < 1 or > 64 || (denominator & (denominator - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(denominator));

        double pulseBeats = 4.0 / denominator;
        double barBeats = numerator * pulseBeats;
        long firstPulse = checked((long)Math.Ceiling((firstBeat - 1e-9) / pulseBeats));
        long lastPulse = checked((long)Math.Floor((lastBeat + 1e-9) / pulseBeats));
        if (lastPulse < firstPulse) return Array.Empty<GridWarpLine>();

        long pulseCount = lastPulse - firstPulse + 1;
        long stride = Math.Max(1, (pulseCount + MaxGridLines - 1) / MaxGridLines);
        var result = new List<GridWarpLine>((int)Math.Min(pulseCount, MaxGridLines));
        for (long pulse = firstPulse; pulse <= lastPulse; pulse += stride)
            AddLine(result, pulse, numerator, pulseBeats, barBeats);
        if (lastPulse % stride != firstPulse % stride)
            AddLine(result, lastPulse, numerator, pulseBeats, barBeats);
        return result.ToArray();
    }

    public static double SourceFrameAtBeat(IAudioEngine engine, double beat, TempoAnchor reference,
        double sourceRate, bool reversed = false)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ValidateSourceRate(sourceRate);
        double direction = reversed ? -1 : 1;
        return reference.SourceFrame
            + (engine.SecondsAtBeat(beat) - reference.Seconds) * sourceRate * direction;
    }

    public static double BeatAtSourceFrame(IAudioEngine engine, double sourceFrame, TempoAnchor reference,
        double sourceRate, bool reversed = false)
    {
        ArgumentNullException.ThrowIfNull(engine);
        double seconds = SecondsAtSourceFrame(sourceFrame, reference, sourceRate, reversed);
        return engine.BeatAtSeconds(seconds);
    }

    public static double SecondsAtSourceFrame(double sourceFrame, TempoAnchor reference,
        double sourceRate, bool reversed = false)
    {
        ValidateSourceRate(sourceRate);
        if (!double.IsFinite(sourceFrame)) throw new ArgumentOutOfRangeException(nameof(sourceFrame));
        double direction = reversed ? -1 : 1;
        return reference.Seconds + (sourceFrame - reference.SourceFrame) / (sourceRate * direction);
    }

    public static bool TryCalculateBpm(TempoAnchor left, TempoAnchor right, bool reversed,
        double minBpm, double maxBpm, out double bpm)
    {
        bpm = 0;
        if (left.Tick < 0 || right.Tick <= left.Tick || !double.IsFinite(left.Seconds)
            || !double.IsFinite(right.Seconds) || right.Seconds <= left.Seconds
            || !double.IsFinite(left.SourceFrame) || !double.IsFinite(right.SourceFrame)
            || left.SourceFrame < 0 || right.SourceFrame < 0
            || (reversed ? right.SourceFrame >= left.SourceFrame : right.SourceFrame <= left.SourceFrame)
            || !double.IsFinite(minBpm) || !double.IsFinite(maxBpm) || minBpm <= 0 || maxBpm < minBpm)
            return false;
        bpm = 60 * ((double)(right.Tick - left.Tick) / TempoPoint.TicksPerBeat)
            / (right.Seconds - left.Seconds);
        return double.IsFinite(bpm) && bpm >= minBpm && bpm <= maxBpm;
    }

    public static bool TryBuildMap(IAudioEngine engine, TempoMapState current,
        IReadOnlyList<TempoAnchor> anchors, int trackId, int clipIndex, long referenceId,
        bool reversed, out TempoMapState candidate, out string error)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(anchors);
        candidate = current;
        error = "";
        var ordered = anchors.OrderBy(a => a.Tick).ToArray();
        if (ordered.Length < 2)
        {
            error = Loc.T("Grid Warp requires at least two anchors.");
            return false;
        }
        for (int i = 0; i + 1 < ordered.Length; i++)
        {
            if (!TryCalculateBpm(ordered[i], ordered[i + 1], reversed,
                    current.MinBpm, current.MaxBpm, out _))
            {
                error = Loc.T("Grid Warp anchors cross or exceed the supported tempo range.");
                return false;
            }
        }

        long firstTick = ordered[0].Tick, lastTick = ordered[^1].Tick;
        var points = current.Points.Where(p => p.Tick < firstTick || p.Tick > lastTick).ToList();
        for (int i = 0; i + 1 < ordered.Length; i++)
        {
            TryCalculateBpm(ordered[i], ordered[i + 1], reversed, current.MinBpm, current.MaxBpm, out double bpm);
            points.Add(new TempoPoint(ordered[i].Tick, bpm, LinkedAnchorId: ordered[i].AnchorId));
        }
        points.Add(new TempoPoint(lastTick, engine.TempoAtBeat((double)lastTick / TempoPoint.TicksPerBeat), LinkedAnchorId: ordered[^1].AnchorId));
        points.Sort((a, b) => a.Tick.CompareTo(b.Tick));

        bool IsLinked(TempoAnchor a) => referenceId > 0
            ? a.ReferenceId == referenceId
            : a.TrackId == trackId && a.ClipIndex == clipIndex;
        var retained = current.Anchors.Where(a => !IsLinked(a));
        var committed = ordered.Select(a => a with
        {
            Locked = 1,
            TrackId = trackId,
            ClipIndex = clipIndex,
            ReferenceId = referenceId
        });
        candidate = current with { Points = points.ToArray(), Anchors = retained.Concat(committed).ToArray() };
        return true;
    }

    public static bool TryMoveTempoPoint(IAudioEngine engine, TempoMapState current, int pointIndex,
        long tick, out TempoMapState candidate, out string error)
    {
        candidate = current;
        error = "";
        if (pointIndex <= 0 || pointIndex >= current.Points.Length
            || tick <= current.Points[pointIndex - 1].Tick
            || pointIndex + 1 < current.Points.Length && tick >= current.Points[pointIndex + 1].Tick)
        {
            error = Loc.T("Tempo point must stay between its neighboring points.");
            return false;
        }

        var point = current.Points[pointIndex];
        var linked = current.Anchors.Where(a => a.AnchorId > 0 && a.AnchorId == point.LinkedAnchorId && a.Locked != 0 && a.ReferenceId > 0).ToArray();
        if (linked.Length == 0)
        {
            var points = current.Points.ToArray();
            points[pointIndex] = points[pointIndex] with { Tick = tick };
            candidate = current with { Points = points };
            return true;
        }
        if (linked.Select(a => a.ReferenceId).Distinct().Count() != 1)
        {
            error = Loc.T("This tempo point is linked to conflicting Grid Warp anchors.");
            return false;
        }

        long referenceId = linked[0].ReferenceId;
        var anchors = current.Anchors.Where(a => a.ReferenceId == referenceId).OrderBy(a => a.Tick)
            .Select(a => a.Tick == point.Tick ? a with { Tick = tick } : a).OrderBy(a => a.Tick).ToArray();
        if (anchors.Select(a => a.Tick).Distinct().Count() != anchors.Length)
        {
            error = Loc.T("The moved tempo point would cross a linked Grid Warp anchor.");
            return false;
        }
        bool reversed = anchors.Length > 1 && anchors[^1].SourceFrame < anchors[0].SourceFrame;
        return TryBuildMap(engine, current, anchors, linked[0].TrackId, linked[0].ClipIndex,
            referenceId, reversed, out candidate, out error);
    }

    public static bool TryDeleteTempoPoint(IAudioEngine engine, TempoMapState current, int pointIndex,
        out TempoMapState candidate, out string error)
    {
        candidate = current;
        error = "";
        if (pointIndex <= 0 || pointIndex >= current.Points.Length)
        {
            error = Loc.T("The first tempo point cannot be deleted.");
            return false;
        }

        long tick = current.Points[pointIndex].Tick;
        var linked = current.Anchors.Where(a => a.AnchorId > 0 && a.AnchorId == current.Points[pointIndex].LinkedAnchorId && a.Locked != 0 && a.ReferenceId > 0).ToArray();
        if (linked.Length == 0)
        {
            candidate = current with { Points = current.Points.Where((_, i) => i != pointIndex).ToArray() };
            return true;
        }
        if (linked.Select(a => a.ReferenceId).Distinct().Count() != 1)
        {
            error = Loc.T("This tempo point is linked to conflicting Grid Warp anchors.");
            return false;
        }

        long referenceId = linked[0].ReferenceId;
        var anchors = current.Anchors.Where(a => a.ReferenceId == referenceId && a.Tick != tick)
            .OrderBy(a => a.Tick).ToArray();
        if (anchors.Length < 2)
        {
            error = Loc.T("At least two Grid Warp anchors must remain linked to the Tempo Track.");
            return false;
        }
        bool reversed = anchors[^1].SourceFrame < anchors[0].SourceFrame;
        return TryBuildMap(engine, current, anchors, linked[0].TrackId, linked[0].ClipIndex,
            referenceId, reversed, out candidate, out error);
    }

    public static bool TrySetTempoPointBpm(TempoMapState current, int pointIndex, double bpm,
        out TempoMapState candidate, out string error)
    {
        candidate = current;
        error = "";
        if (pointIndex < 0 || pointIndex >= current.Points.Length || !double.IsFinite(bpm)
            || bpm < current.MinBpm || bpm > current.MaxBpm)
        {
            error = Loc.T("Tempo point BPM is outside the supported range.");
            return false;
        }
        if (IsLinkedTempoPoint(current, pointIndex)
            && Math.Abs(current.Points[pointIndex].Bpm - bpm) > 1e-9)
        {
            error = Loc.T("A Grid Warp-linked tempo value is derived from its absolute timing anchors.");
            return false;
        }
        var points = current.Points.ToArray();
        points[pointIndex] = points[pointIndex] with { Bpm = bpm };
        candidate = current with { Points = points };
        return true;
    }

    private static void AddLine(List<GridWarpLine> result, long pulse, int numerator, double pulseBeats, double barBeats)
    {
        double beat = pulse * pulseBeats;
        int bar = (int)Math.Floor(beat / barBeats + 1e-9) + 1;
        int beatInBar = (int)(pulse % numerator) + 1;
        result.Add(new GridWarpLine(TempoPoint.ToTick(beat), bar, beatInBar, pulse % numerator == 0));
    }

    private static void ValidateSourceRate(double sourceRate)
    {
        if (!double.IsFinite(sourceRate) || sourceRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceRate));
    }
}
