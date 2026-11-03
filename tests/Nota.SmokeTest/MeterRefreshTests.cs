// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Text.Json;
using Nota.Presentation;

namespace Nota.SmokeTest;

internal static class MeterRefreshTests
{
    public static IEnumerable<(bool Ok, string Label)> Run()
    {
        yield return (new Settings().MeterRefreshHz == 30 && MeterRefreshRate.Presets.SequenceEqual(new[] {30,60}),
            "meter refresh: exactly 30/60 Hz; fresh preferences default to 30");
        foreach (int value in new[] {-1,0,30,60,120})
        {
            var saved = JsonSerializer.Deserialize<Settings>($"{{\"MeterRefreshHz\":{value}}}")!;
            yield return (saved.MeterRefreshHz == (value==60 ? 60 : 30), $"meter refresh: saved {value} normalizes safely");
            yield return (JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(saved))!.MeterRefreshHz == saved.MeterRefreshHz,
                $"meter refresh: normalized {value} persists through JSON");
        }
        yield return (JsonSerializer.Deserialize<Settings>("{}")!.MeterRefreshHz == 30,
            "meter refresh: older settings without the field default to 30");
        foreach(int hz in new[]{30,60})
        {
            var gate = new MeterRefreshCadence(); int due=0;
            for(int i=0;i<2400;i++) if(gate.IsDue(i/240.0,hz))due++;
            yield return (due==hz*10, $"meter refresh: {hz} Hz retains phase across display callbacks ({due} in 10 s)");
        }
        var cadence = new MeterRefreshCadence();
        bool first=cadence.IsDue(0,30), early=cadence.IsDue(.01,30), changed=cadence.IsDue(.01,60);
        yield return (first && !early && changed && !cadence.IsDue(.011,60),
            "meter refresh: switching rates takes effect immediately without duplicate work");
        yield return (cadence.IsDue(5,60) && !cadence.IsDue(5,60),
            "meter refresh: a stalled callback skips missed periods without catch-up bursts");
        cadence.Reset();
        yield return (cadence.IsDue(5,30), "meter refresh: resuming a visible owner refreshes immediately");
    }
}
