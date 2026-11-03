// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using Avalonia;
using Avalonia.Media;
using Nota.Application;

namespace Nota.App;

public sealed partial class ArrangementView
{
    private TransientWorkspace? _transientWorkspace;
    private bool _transientAttached;
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _transientAttached = true;
        if (_transientWorkspace is not null) _transientWorkspace.Changed += TransientViewsChanged;
        TransientViewsChanged();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _transientAttached = false;
        if (_transientWorkspace is not null) _transientWorkspace.Changed -= TransientViewsChanged;
        base.OnDetachedFromVisualTree(e);
    }
    private void TransientViewsChanged()
    {
        if (_engine is { } engine)
            foreach (var track in _tracks)
                foreach (var clip in track.Clips)
                    if (!clip.IsMidi && clip.TransientMap is null
                        && engine.TryGetAudioClipInfo(track.Id, clip.ClipIndex, out var info))
                        LoadTransientMap(engine, track.Id, clip, info);
        _lanes.InvalidateVisual();
    }
    private void LoadTransientMap(IAudioEngine engine, int track, ClipVM clip, NotaAudioClipInfo info)
    {
        if (_transientWorkspace?.Snapshot(info.SampleId) is null
            || !engine.TryGetSampleInfo(info.SampleId, out var source)) return;
        if (engine.ClipGridFollowsAudio(track, clip.ClipIndex)
            && engine.GetGridReferenceGeometry(track, clip.ClipIndex) is { } reference)
        {
            info.WarpEnabled = 0; info.SourceOffsetFrames = reference.SourceStart;
            info.LengthFrames = (long)Math.Round(reference.SourceEnd - reference.SourceStart);
        }
        double[]? src = null, beats = null;
        if (info.WarpEnabled != 0)
        {
            (src, beats) = ClipWarpMarkers.Read(engine, track, clip.ClipIndex);
        }
        clip.TransientMap = new(info, source, clip.LengthBeats, src, beats, engine.SecondsAtBeat, engine.BeatAtSeconds);
    }

    private sealed partial class LaneControl
    {
        private static readonly IPen TransientGuidePen = new Pen(NotaPalette.Wash(NotaPalette.TextStrong, 0x90), 1);
        private void DrawTransientGuides(DrawingContext context, ClipVM clip, Rect rect)
        {
            if (_o._transientWorkspace is not { Show: true } state || clip.TransientMap is not { Valid: true } map
                || state.Snapshot(clip.SampleId) is not { } snapshot || rect.Width <= 0 || rect.Height <= 0) return;
            if (!ReferenceEquals(snapshot, clip.TransientSnapshot))
            {
                clip.TransientSnapshot = snapshot;
                clip.TransientGuides = new(snapshot, map);
            }
            if (clip.TransientGuides is not { } layout) return;
            using var bounds = context.PushClip(rect);
            int index = layout.LowerBound(Math.Max(0, -rect.X / rect.Width));
            double end = Math.Min(1, (Bounds.Width - rect.X) / rect.Width);
            while (index < layout.Fractions.Count && layout.Fractions[index] <= end)
            {
                double fraction = layout.Fractions[index];
                double x = rect.X + fraction * rect.Width + .5;
                context.DrawLine(TransientGuidePen, new Point(x, rect.Y), new Point(x, rect.Bottom));
                index = Math.Max(index + 1, layout.LowerBound(fraction + 3 / rect.Width));
            }
        }
    }
}
