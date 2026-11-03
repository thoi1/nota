// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

namespace Nota.Application;

/// <summary>Read the complete marker snapshot on the authoring thread. Editors, guide
/// mapping and project persistence must not silently truncate long performances.</summary>
public static class ClipWarpMarkers
{
    public static (double[] Source, double[] Beats) Read(IAudioEngine engine, int trackId, int clipIndex)
    {
        int count = engine.GetClipWarpMarkers(trackId, clipIndex, Array.Empty<double>(), Array.Empty<double>());
        if (count <= 0) return (Array.Empty<double>(), Array.Empty<double>());
        var source = new double[count];
        var beats = new double[count];
        int actual = engine.GetClipWarpMarkers(trackId, clipIndex, source, beats);
        if (actual != count) throw new InvalidOperationException("Warp markers changed during authoring-thread capture.");
        return (source, beats);
    }
}
