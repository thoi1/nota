// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
//
// Where a point of the audio clip editor plays in the arrangement (specs/036-clip-editor-batch).
// The editor draws the clip on one content axis — the full warp in beats, or the whole sample in
// frames when unwarped — of which only the played window [PlayStart, PlayEnd] sounds, mapped
// linearly onto [StartBeat, StartBeat + LengthBeats] (back to front when reversed). The playhead,
// the ruler labels and ruler seeks all go through this one map so they can't disagree. Free of
// Avalonia and the engine so the smoke test can link it.

namespace Nota.Application;

public enum ClipContentDomain { WarpBeats, SourceFrames }

public readonly record struct ClipTimeMap(
    double StartBeat,
    double LengthBeats,
    ClipContentDomain Domain,
    double ContentTotal,
    double PlayStart,
    double PlayEnd,
    bool Reversed,
    bool IsSlot,
    Func<double, double>? SecondsAtBeat = null,
    Func<double, double>? BeatAtSeconds = null)
{
    private double Window => PlayEnd - PlayStart;
    public bool Valid => ContentTotal > 0 && Window > 0;

    /// <summary>Content frac (0..1 over the whole axis) → played frac (0 = the first audible
    /// moment). Not clamped: points outside the played window give fracs outside 0..1.</summary>
    public double PlayFracAtContent(double contentFrac)
    {
        if (!Valid) return 0;
        double f = (contentFrac * ContentTotal - PlayStart) / Window;
        return Reversed ? 1 - f : f;
    }

    /// <summary>Played frac → content frac (the inverse of <see cref="PlayFracAtContent"/>).</summary>
    public double ContentAtPlayFrac(double playFrac)
    {
        if (!Valid) return 0;
        double f = Reversed ? 1 - playFrac : playFrac;
        return (PlayStart + f * Window) / ContentTotal;
    }

    public double ArrangementBeatAtContent(double contentFrac)
    {
        if (!IsSlot && Domain == ClipContentDomain.SourceFrames && SecondsAtBeat is { } time && BeatAtSeconds is { } beat)
        {
            double start = time(StartBeat), duration = time(StartBeat + LengthBeats) - start;
            return beat(start + PlayFracAtContent(contentFrac) * duration);
        }
        return StartBeat + PlayFracAtContent(contentFrac) * LengthBeats;
    }

    public double ContentAtArrangementBeat(double beat)
    {
        if (!IsSlot && Domain == ClipContentDomain.SourceFrames && SecondsAtBeat is { } time)
        {
            double start = time(StartBeat), duration = time(StartBeat + LengthBeats) - start;
            return duration > 0 ? ContentAtPlayFrac((time(beat) - start) / duration) : 0;
        }
        return LengthBeats > 0 ? ContentAtPlayFrac((beat - StartBeat) / LengthBeats) : 0;
    }
}
