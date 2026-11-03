// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Nota.Application.Analysis;

namespace Nota.Infrastructure;
public sealed partial class TransientAnalysisService
{
    private readonly Dictionary<long, double> _originalBpm = new();
    private readonly Dictionary<long, SourceBeatResult> _originalTiming = new();
    public double OriginalBpm(long sourceId) { lock (_gate) return _originalBpm.GetValueOrDefault(sourceId); }
    public void RememberOriginalBpm(long sourceId, double bpm)
    {
        if (sourceId <= 0 || !double.IsFinite(bpm) || bpm is < 1 or > 999) return;
        lock (_gate) _originalBpm.TryAdd(sourceId, bpm);
    }
    public SourceBeatResult? OriginalTiming(long sourceId)
    { lock (_gate) return _originalTiming.TryGetValue(sourceId, out var value) ? value with { Beats = value.Beats.ToArray() } : null; }
    public void RestoreOriginalTiming(long sourceId, SourceBeatResult timing)
    {
        if (timing.Settings is null || timing.Beats is null) throw new ArgumentException("Invalid source timing cache");
        timing.Settings.Validate(); timing.DetectionSettings?.Validate();
        if (sourceId <= 0 || timing.Version != SourceBeatSettings.Version || timing.Beats.Length is < 2 or > 20000
            || !double.IsFinite(timing.SampleRate) || timing.SampleRate <= 0
            || !double.IsFinite(timing.SourceStart) || !double.IsFinite(timing.SourceEnd)
            || timing.SourceStart < 0 || timing.SourceEnd <= timing.SourceStart) throw new ArgumentException("Invalid source timing cache");
        double frame = -1, beat = -1;
        foreach (var point in timing.Beats)
        {
            if (!double.IsFinite(point.SourceFrame) || !double.IsFinite(point.Beat) || !double.IsFinite(point.Confidence)
                || point.SourceFrame <= frame || point.Beat <= beat || point.SourceFrame < timing.SourceStart
                || point.SourceFrame > timing.SourceEnd) throw new ArgumentException("Invalid source beat cache");
            frame = point.SourceFrame; beat = point.Beat;
        }
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _originalTiming[sourceId] = timing with { Beats = timing.Beats.ToArray() };
        }
    }
    public async Task<SourceBeatResult> AnalyzeBeatsAsync(long sourceId, double sourceStart, double sourceEnd,
        SourceBeatSettings settings, IReadOnlyList<SourceBeatConstraint>? fixedBeats = null,
        CancellationToken cancellationToken = default, TransientAnalysisSettings? detectionSettings = null)
    {
        settings.Validate(); cancellationToken.ThrowIfCancellationRequested();
        // Capture on the calling authoring thread before the first await. This always reads
        // immutable source PCM, never a stretch cache or a temporary exported clip.
        var snapshotTask = AnalyzeAudioAsync(sourceId, detectionSettings ?? new(settings.Sensitivity, 0.030), cancellationToken);
        var snapshot = await snapshotTask.ConfigureAwait(false);
        var evidence = snapshot.Transients.Select(t => new BeatEvidence(t.SamplePosition, t.Strength, t.Confidence)).ToArray();
        await _workers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await Task.Run(() => SourceBeatInference.Analyze(evidence, snapshot.SampleRate,
                sourceStart, sourceEnd, settings, fixedBeats, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            result = result with { DetectionSettings = snapshot.Settings };
            RestoreOriginalTiming(sourceId, result);
            return result;
        }
        finally { _workers.Release(); }
    }
}
