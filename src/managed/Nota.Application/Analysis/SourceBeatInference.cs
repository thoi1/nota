// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

namespace Nota.Application.Analysis;

public sealed record SourceBeatSettings(double StartingBpm = 120, double MinimumBpm = 40,
    double MaximumBpm = 240, double Sensitivity = 0.5)
{
    public const int Version = 1;
    public void Validate()
    {
        if (!double.IsFinite(StartingBpm) || !double.IsFinite(MinimumBpm) || !double.IsFinite(MaximumBpm)
            || MinimumBpm < 1 || MaximumBpm > 999 || MaximumBpm < MinimumBpm
            || StartingBpm < MinimumBpm || StartingBpm > MaximumBpm
            || !double.IsFinite(Sensitivity) || Sensitivity is < 0 or > 1)
            throw new ArgumentException("Invalid source beat settings");
    }
}
public readonly record struct BeatEvidence(double SourceFrame, double Strength, double Confidence);
public readonly record struct SourceBeatConstraint(double SourceFrame, double Beat);
public readonly record struct SourceBeat(double SourceFrame, double Beat, double Confidence,
    bool Observed, bool Fixed = false);
public sealed record SourceBeatResult(double SampleRate, double SourceStart, double SourceEnd,
    SourceBeatSettings Settings, SourceBeat[] Beats, int Version = SourceBeatSettings.Version)
{
    public TransientAnalysisSettings? DetectionSettings { get; init; }
}

/// <summary>Original-source pulse proposals. This is source analysis, never a project clock.
/// A bounded beam distinguishes pulse continuity from attack density; missing attacks are
/// explicit proposals. Starting BPM supplies the otherwise ambiguous half/double prior.</summary>
public static class SourceBeatInference
{
    private const int BeamSize = 12, CandidateLimit = 6, MaxBeats = 20000, MaxNodes = 500000;
    private sealed record Node(SourceBeat Beat, double Period, double Score, int Missing, int Evidence,
        int Depth, Node? Previous);

    public static SourceBeatResult Analyze(IReadOnlyList<BeatEvidence> evidence, double sampleRate,
        double sourceStart, double sourceEnd, SourceBeatSettings settings,
        IReadOnlyList<SourceBeatConstraint>? fixedBeats = null, CancellationToken cancellationToken = default)
    {
        settings.Validate(); cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(sampleRate) || sampleRate <= 0 || !double.IsFinite(sourceStart)
            || !double.IsFinite(sourceEnd) || sourceStart < 0 || sourceEnd <= sourceStart || evidence.Count > 100000)
            throw new ArgumentException("Invalid source beat region or resource limit");
        var attacks = evidence.Where(a => double.IsFinite(a.SourceFrame) && a.SourceFrame >= sourceStart
            && a.SourceFrame <= sourceEnd && double.IsFinite(a.Strength) && a.Strength > 0
            && double.IsFinite(a.Confidence)).OrderBy(a => a.SourceFrame).ToArray();
        if (attacks.Length < 3) throw new InvalidOperationException("Insufficient pulse evidence");
        double scale = attacks.Select(a => a.Strength).Order().ElementAt((attacks.Length - 1) * 3 / 4);
        var constraints = (fixedBeats ?? Array.Empty<SourceBeatConstraint>()).OrderBy(a => a.SourceFrame).ToList();
        foreach (var c in constraints)
            if (!double.IsFinite(c.SourceFrame) || !double.IsFinite(c.Beat) || c.SourceFrame < sourceStart
                || c.SourceFrame > sourceEnd || c.Beat < 0) throw new ArgumentException("Invalid fixed beat");
        if (constraints.Count == 0 || constraints[0].SourceFrame > sourceStart)
            constraints.Insert(0, new(sourceStart, 0));
        for (int i = 1; i < constraints.Count; i++)
            if (constraints[i].SourceFrame <= constraints[i - 1].SourceFrame || constraints[i].Beat <= constraints[i - 1].Beat)
                throw new ArgumentException("Fixed beats must increase in source time and musical beat");
        var output = new List<SourceBeat>(); int nodes = 0;
        for (int region = 0; region < constraints.Count; region++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = constraints[region];
            SourceBeatConstraint? end = region + 1 < constraints.Count ? constraints[region + 1] : null;
            double limit = end?.SourceFrame ?? sourceEnd;
            if (limit == start.SourceFrame) continue;
            double period = sampleRate * 60 / settings.StartingBpm;
            if (end is { } endpoint)
            {
                period = (endpoint.SourceFrame - start.SourceFrame) / (endpoint.Beat - start.Beat);
                if (sampleRate * 60 / period < settings.MinimumBpm || sampleRate * 60 / period > settings.MaximumBpm)
                    throw new InvalidOperationException("Fixed beats exceed tempo limits");
            }
            int firstAttack = Lower(attacks, start.SourceFrame);
            bool observedStart = firstAttack < attacks.Length && Math.Abs(attacks[firstAttack].SourceFrame - start.SourceFrame) <= sampleRate * 0.030;
            var root = new Node(new(start.SourceFrame, start.Beat, 1, observedStart, fixedBeats?.Any(c => c == start) == true),
                period, 0, 0, observedStart ? 1 : 0, 0, null);
            var beam = new List<Node> { root }; Node best = root;
            for (int step = 1; ; step++)
            {
                if ((step & 15) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (step > MaxBeats || nodes > MaxNodes) throw new InvalidOperationException("Source beat resource limit");
                double beat = start.Beat + step;
                if (end is { } e && beat >= e.Beat - 1e-9) break;
                var next = new List<Node>(BeamSize * (CandidateLimit + 1));
                foreach (var state in beam)
                {
                    double predicted = state.Beat.SourceFrame + state.Period;
                    if (predicted >= limit - sampleRate * 0.010) continue;
                    double window = state.Period * (0.24 + settings.Sensitivity * 0.20);
                    int lo = Lower(attacks, predicted - window), hi = Lower(attacks, predicted + window);
                    var candidates = new List<int>();
                    for (int i = lo; i < attacks.Length && i <= hi; i++)
                        if (attacks[i].SourceFrame > state.Beat.SourceFrame + sampleRate * 60 / settings.MaximumBpm * 0.65
                            && attacks[i].SourceFrame < limit && Math.Abs(attacks[i].SourceFrame - predicted) <= window) candidates.Add(i);
                    foreach (int i in candidates.OrderBy(i => Math.Abs(attacks[i].SourceFrame - predicted)).Take(CandidateLimit))
                    {
                        var a = attacks[i];
                        double measured = (a.SourceFrame - state.Beat.SourceFrame) / (beat - state.Beat.Beat);
                        double bpm = sampleRate * 60 / measured;
                        if (bpm < settings.MinimumBpm || bpm > settings.MaximumBpm) continue;
                        if (end is { } tail)
                        {
                            double remaining = (tail.SourceFrame - a.SourceFrame) / (tail.Beat - beat);
                            double tailBpm = sampleRate * 60 / remaining;
                            if (tailBpm < settings.MinimumBpm || tailBpm > settings.MaximumBpm) continue;
                        }
                        double strength = Math.Clamp(a.Strength / Math.Max(scale, 1e-12), 0, 1.5);
                        double confidence = Math.Clamp(a.Confidence, 0, 1);
                        double phase = (a.SourceFrame - predicted) / state.Period;
                        double change = Math.Log(measured / state.Period);
                        double score = 2.5 * strength * (0.6 + confidence * 0.4) - 7 * phase * phase - 8 * change * change;
                        double newPeriod = Math.Clamp(0.75 * measured + 0.25 * state.Period,
                            sampleRate * 60 / settings.MaximumBpm, sampleRate * 60 / settings.MinimumBpm);
                        next.Add(new(new(a.SourceFrame, beat, Math.Clamp(strength * confidence, 0, 1), true),
                            newPeriod, state.Score + score, 0, state.Evidence + 1, step, state)); nodes++;
                    }
                    if (state.Missing < 2)
                    {
                        next.Add(new(new(predicted, beat, 0, false), state.Period, state.Score - 1.8,
                            state.Missing + 1, state.Evidence, step, state)); nodes++;
                    }
                }
                if (next.Count == 0) break;
                beam = next.OrderByDescending(s => s.Score / Math.Max(1, s.Depth)).Take(BeamSize).ToList();
                best = beam[0];
            }
            if (best.Evidence < 2 && output.Count == 0) throw new InvalidOperationException("Insufficient pulse evidence");
            if (end is null && sourceEnd-best.Beat.SourceFrame > best.Period*3)
                throw new InvalidOperationException("Insufficient pulse coverage; trim the unsupported source region");
            var segment = new List<SourceBeat>();
            for (Node? node = best; node is not null; node = node.Previous) segment.Add(node.Beat);
            segment.Reverse();
            if (end is { } fixedEnd) segment.Add(new(fixedEnd.SourceFrame, fixedEnd.Beat, 1, false, true));
            else if (best.Beat.SourceFrame < sourceEnd)
                segment.Add(new(sourceEnd, best.Beat.Beat + (sourceEnd - best.Beat.SourceFrame) / best.Period, 0, false));
            foreach (var b in segment)
                if (output.Count == 0 || b.SourceFrame > output[^1].SourceFrame + 1e-6) output.Add(b);
        }
        if (output.Count < 3 || output.Count > MaxBeats || output[^1].Beat-output[0].Beat>MaxBeats || output.Count(b => b.Observed) < 3) throw new InvalidOperationException("Insufficient pulse evidence");
        return new(sampleRate, sourceStart, sourceEnd, settings, output.ToArray());
    }
    private static int Lower(BeatEvidence[] evidence, double frame)
    {
        int lo = 0, hi = evidence.Length;
        while (lo < hi) { int mid = (lo + hi) / 2; if (evidence[mid].SourceFrame < frame) lo = mid + 1; else hi = mid; }
        return lo;
    }
}
