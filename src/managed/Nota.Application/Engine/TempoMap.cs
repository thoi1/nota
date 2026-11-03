// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Runtime.InteropServices;

namespace Nota.Application;

public enum TempoCurve { Step, Linear, Smooth }

[StructLayout(LayoutKind.Sequential)]
public readonly record struct TempoPoint(long Tick, double Bpm, TempoCurve Curve = TempoCurve.Step, int LinkedAnchorId = 0)
{
    public const int TicksPerBeat = 960;
    public double Beat => (double)Tick / TicksPerBeat;
    public static long ToTick(double beat) => checked((long)Math.Round(beat * TicksPerBeat));
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct TempoAnchor(long Tick, double Seconds, int Locked = 0,
    int TrackId = -1, int ClipIndex = -1, int AnchorId = 0, double SourceFrame = 0, long ReferenceId = 0);

/// <summary>Authoring snapshot only. Native Transport owns all beat/time conversion.</summary>
public sealed record TempoMapState(TempoPoint[] Points, TempoAnchor[] Anchors, double MinBpm = 20, double MaxBpm = 300);

/// <summary>Original source window and absolute placement, independent of audio warp markers.</summary>
public sealed record GridReferenceGeometry(double SourceStart, double SourceEnd, double OriginSeconds, double SourceRate);
