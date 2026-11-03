// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application;
using Nota.Application.Analysis;
using Nota.Infrastructure;

namespace Nota.SmokeTest;
internal static class SourceBeatTests
{
    public static IEnumerable<(bool, string)> Run()
    {
        const double rate = 48000;
        var settings = new SourceBeatSettings(120, 50, 240, 0.5);
        foreach (var kind in new[] { "constant", "accelerando", "ritardando", "rubato", "subdivisions", "missing" })
        {
            var times = new List<double> { 0 };
            for (int i = 0; i < 24; i++) times.Add(times[^1] + (kind switch {
                "accelerando" => 0.6 - i * 0.008, "ritardando" => 0.42 + i * 0.008,
                "rubato" => 0.5 + 0.065 * Math.Sin(i * 0.45), _ => 0.5 }));
            var evidence = new List<BeatEvidence>();
            for (int i = 0; i < times.Count; i++)
            {
                if (kind != "missing" || i is not (7 or 15)) evidence.Add(new(times[i] * rate, 1, 1));
                if (kind == "subdivisions" && i + 1 < times.Count)
                    evidence.Add(new((times[i] + times[i + 1]) * 0.5 * rate, 0.65, 1));
            }
            var result = SourceBeatInference.Analyze(evidence.OrderBy(e => e.SourceFrame).ToArray(), rate,
                0, times[^1] * rate, settings with { StartingBpm = 60 / (times[1] - times[0]) });
            double max = result.Beats.Where(b => Math.Abs(b.Beat - Math.Round(b.Beat)) < 1e-6)
                .Where(b => b.Beat < times.Count).Max(b => Math.Abs(b.SourceFrame / rate - times[(int)Math.Round(b.Beat)]));
            yield return (max < 0.030 && result.Beats.Count(b => b.Observed) >= 20,
                $"source beats: {kind} follows original pulse (max error {max * 1000:F1} ms)");
            if (kind == "missing") yield return (result.Beats.Any(b => !b.Observed && Math.Abs(b.Beat - 7) < 1e-6),
                "source beats: a missing attack is an interpolated proposal, not a transient");
        }
        var attacks = Enumerable.Range(0, 17).Select(i => new BeatEvidence(i * 24000, 1, 1)).ToArray();
        var fixedResult = SourceBeatInference.Analyze(attacks, rate, 0, 384000, settings,
            new[] { new SourceBeatConstraint(192000, 8) });
        yield return (fixedResult.Beats.Any(b => b.Fixed && b.SourceFrame == 192000 && b.Beat == 8),
            "source beats: a manual source/beat constraint stays exact");
        bool noEvidence = false, invalid = false, cancelled = false;
        try { SourceBeatInference.Analyze(Array.Empty<BeatEvidence>(), rate, 0, 48000, settings); } catch (InvalidOperationException) { noEvidence = true; }
        try { SourceBeatInference.Analyze(attacks, rate, 0, 384000, settings with { MinimumBpm = 0 }); } catch (ArgumentException) { invalid = true; }
        try { SourceBeatInference.Analyze(attacks, rate, 0, 384000, settings, cancellationToken: new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
        yield return (noEvidence && invalid && cancelled, "source beats: silence, invalid limits and cancellation reject safely");
        var doubled = SourceBeatInference.Analyze(attacks, rate, 0, 384000, settings with { StartingBpm = 60 });
        yield return (doubled.Beats[^1].Beat < fixedResult.Beats[^1].Beat * 0.6,
            "source beats: starting BPM resolves half/double ambiguity instead of using project tempo");
        string file = Path.Combine(Path.GetTempPath(), $"nota-source-pulse-{Guid.NewGuid():N}.wav");
        try
        {
            const int sr = 48000;
            var beatTimes = Enumerable.Range(0, 17).Select(i => 0.5 + i * 0.5).ToArray();
            WavWriter.WriteStereo(file, 9, sr, frame =>
            {
                double value = 0;
                foreach (double time in beatTimes)
                {
                    int k = frame - (int)(time * sr);
                    if (k >= 0 && k < sr * 0.05) value += 0.7 * Math.Exp(-k / (sr * 0.009)) * (k % 2 == 0 ? 1 : -0.7);
                }
                return (value, value);
            });
            using var engine = new NotaEngine(); engine.RenderOffline(new float[128], 64, sr);
            int track = engine.AddAudioTrack(), clip = engine.AddAudioClip(track, file, 4);
            engine.TryGetAudioClipInfo(track, clip, out var info);
            engine.TransientAnalysis.RememberOriginalBpm(info.SampleId, 120);
            var before = engine.TransientAnalysis.AnalyzeBeatsAsync(info.SampleId, 24000, 408000, settings).GetAwaiter().GetResult();
            engine.AutoWarpClipAtBpm(track, clip, 120); engine.SetBpm(155);
            var after = engine.TransientAnalysis.AnalyzeBeatsAsync(info.SampleId, 24000, 408000, settings).GetAwaiter().GetResult();
            yield return (before.Beats.SequenceEqual(after.Beats) && engine.TransientAnalysis.OriginalBpm(info.SampleId) == 120
                && before.Beats.Count(b => b.Observed) >= 14,
                "source beats: real SuperFlux source extraction is identical before/after auto-warp and project BPM edits");
        }
        finally { File.Delete(file); }
    }
}
