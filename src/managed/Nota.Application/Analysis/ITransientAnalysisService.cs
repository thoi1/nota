// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Nota.Application;

/// <summary>Versioned source analysis, independent of clip placement and musical beats.</summary>
public sealed record TransientAnalysisSettings(double Sensitivity = 0.5,
    double MinimumSpacingSeconds = 0.030, bool RefineAttacks = true)
{
    public const int AnalysisVersion = 1;
    public void Validate()
    {
        if (!double.IsFinite(Sensitivity) || Sensitivity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(Sensitivity));
        if (!double.IsFinite(MinimumSpacingSeconds) || MinimumSpacingSeconds is < 0.005 or > 0.250)
            throw new ArgumentOutOfRangeException(nameof(MinimumSpacingSeconds));
    }
}

/// <summary>Source-relative attack. Confidence is local evidence, not a calibrated probability.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct AudioTransient
{
    public readonly long SamplePosition;
    public readonly double Seconds;
    public readonly float Strength;
    public readonly float Confidence;
    public readonly long UncertaintySamples;
    private readonly int _refined;
    public readonly int AnalysisVersion;
    public bool IsRefined => _refined != 0;
}

public sealed class TransientSnapshot
{
    public long SourceId { get; }
    // Nota edits replace the immutable sample and mint a new SourceId; no in-place revision.
    public long Revision => 0;
    public double SampleRate { get; }
    public TransientAnalysisSettings Settings { get; }
    public ReadOnlyCollection<AudioTransient> Transients { get; }

    public TransientSnapshot(long sourceId, double sampleRate, TransientAnalysisSettings settings,
        IEnumerable<AudioTransient> transients)
    {
        SourceId = sourceId;
        SampleRate = sampleRate;
        Settings = settings;
        Transients = Array.AsReadOnly(transients.ToArray());
    }

    public int LowerBound(long sample)
    {
        int lo = 0, hi = Transients.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (Transients[mid].SamplePosition < sample) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}

/// <summary>Capture on the authoring thread, then drive on one background worker. Cancel may
/// run concurrently. Never use on the audio callback; dispose after the worker exits.</summary>
public interface ITransientAnalysisJob : IDisposable
{
    double SampleRate { get; }
    double Progress { get; }
    /// <returns>1 running, 0 complete, -1 failed, -2 cancelled.</returns>
    int Step(int maxAnalysisFrames);
    void Cancel();
    AudioTransient[] ReadResults();
}

/// <summary>Per-engine shared derived data. Call AnalyzeAudioAsync on the authoring thread;
/// work then runs off-thread. Any caller cancellation cancels its shared in-flight job.
/// Invalidation cancels every setting for that source; completed snapshots stay immutable.</summary>
public interface ITransientAnalysisService : IDisposable
{
    Task<Analysis.SourceBeatResult> AnalyzeBeatsAsync(long sourceId, double sourceStart, double sourceEnd,
        Analysis.SourceBeatSettings settings, IReadOnlyList<Analysis.SourceBeatConstraint>? fixedBeats = null,
        CancellationToken cancellationToken = default, TransientAnalysisSettings? detectionSettings = null);
    double OriginalBpm(long sourceId);
    void RememberOriginalBpm(long sourceId, double bpm);
    Analysis.SourceBeatResult? OriginalTiming(long sourceId);
    void RestoreOriginalTiming(long sourceId, Analysis.SourceBeatResult timing);
    Task<TransientSnapshot> AnalyzeAudioAsync(long sourceId, TransientAnalysisSettings? settings = null,
        CancellationToken cancellationToken = default, IProgress<double>? progress = null);
    TransientSnapshot? GetTransients(long sourceId, TransientAnalysisSettings? settings = null);
    AudioTransient? GetNearestTransient(long sourceId, long samplePosition, TransientAnalysisSettings? settings = null);
    IReadOnlyList<AudioTransient> GetTransientsInRange(long sourceId, long startSample, long endSample,
        TransientAnalysisSettings? settings = null);
    void InvalidateAnalysis(long sourceId);
}
