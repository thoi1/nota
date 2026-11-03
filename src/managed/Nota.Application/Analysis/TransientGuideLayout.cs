// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Collections.ObjectModel;

namespace Nota.Application;

/// <summary>Played fractions cached per source snapshot/clip map, independent of zoom,
/// scroll, playback ticks and clip movement previews. Reverse maps sort into playing order.</summary>
public sealed class TransientGuideLayout
{
    public ReadOnlyCollection<double> Fractions { get; }
    public TransientGuideLayout(TransientSnapshot snapshot, TransientClipMap map)
    {
        var fractions = new List<double>();
        if (map.Valid)
        {
            double a = map.SourceAtContent(map.Placement.PlayStart / map.Placement.ContentTotal);
            double b = map.SourceAtContent(map.Placement.PlayEnd / map.Placement.ContentTotal);
            int first = snapshot.LowerBound((long)Math.Max(0, Math.Floor(Math.Min(a, b))));
            for (int i = first; i < snapshot.Transients.Count && snapshot.Transients[i].SamplePosition <= Math.Max(a, b); i++)
            {
                var hit = snapshot.Transients[i];
                if (map.TryArrangementBeat(hit.SamplePosition, out double beat))
                    fractions.Add((beat - map.Placement.StartBeat) / map.Placement.LengthBeats);
            }
        }
        if (map.Placement.Reversed) fractions.Reverse();
        Fractions = Array.AsReadOnly(fractions.ToArray());
    }
    public int LowerBound(double fraction)
    {
        int lo = 0, hi = Fractions.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (Fractions[mid] < fraction) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
