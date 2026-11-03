// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

namespace Nota.Infrastructure;

/// <summary>One derived-data cache for Warp and tempo inference. No audio-thread code or
/// independent worker pool. Capture is synchronous on authoring thread; only captured jobs
/// enter the .NET pool. Invalidation removes entry identity before cancelling obsolete work.</summary>
public sealed partial class TransientAnalysisService(Func<long, TransientAnalysisSettings, ITransientAnalysisJob> capture)
    : ITransientAnalysisService
{
    private const int MaxEntries = 16, MaxCachedRecords = 200_000;
    private readonly object _gate = new();
    private readonly Dictionary<Key, Entry> _entries = new();
    private readonly SemaphoreSlim _workers = new(2);
    private bool _disposed;
    private long _access;
    private readonly record struct Key(long SourceId, TransientAnalysisSettings Settings);

    private sealed class Entry(Key key, ITransientAnalysisJob job)
    {
        internal readonly Key Key = key;
        internal readonly ITransientAnalysisJob Job = job;
        internal readonly CancellationTokenSource Cancellation = new();
        internal readonly TaskCompletionSource<TransientSnapshot> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly List<CancellationTokenRegistration> Registrations = new();
        internal readonly List<IProgress<double>> Observers = new();
        internal TransientSnapshot? Snapshot;
        internal long Access;
        internal bool Finished;
    }

    public Task<TransientSnapshot> AnalyzeAudioAsync(long sourceId, TransientAnalysisSettings? settings = null,
        CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        settings ??= new();
        settings.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var key = new Key(sourceId, settings);
            if (_entries.TryGetValue(key, out var old))
            {
                old.Access = ++_access;
                if (!old.Finished) Attach(old, cancellationToken, progress);
                else progress?.Report(1);
                return old.Completion.Task;
            }
            // This is the ONLY call that accesses engine state. Do not move it to Task.Run.
            var entry = new Entry(key, capture(sourceId, settings)) { Access = ++_access };
            _entries.Add(key, entry);
            Attach(entry, cancellationToken, progress);
            _ = RunAsync(entry);
            return entry.Completion.Task;
        }
    }

    private static void Attach(Entry entry, CancellationToken token, IProgress<double>? progress)
    {
        if (token.CanBeCanceled) entry.Registrations.Add(token.Register(() => entry.Cancellation.Cancel()));
        if (progress is not null) entry.Observers.Add(progress);
    }

    private async Task RunAsync(Entry entry)
    {
        bool acquired = false;
        TransientSnapshot? result = null;
        Exception? error = null;
        var token = entry.Cancellation.Token;
        try
        {
            await _workers.WaitAsync(token).ConfigureAwait(false);
            acquired = true;
            result = await Task.Run(() =>
            {
                using var registration = token.Register(entry.Job.Cancel);
                int status;
                int batches = 0;
                do
                {
                    token.ThrowIfCancellationRequested();
                    status = entry.Job.Step(32);
                    if (++batches % 4 == 0) Report(entry, entry.Job.Progress);
                } while (status > 0);
                if (status == -2) throw new OperationCanceledException(token);
                if (status < 0) throw new InvalidOperationException("Native transient analysis failed.");
                token.ThrowIfCancellationRequested();
                return new TransientSnapshot(entry.Key.SourceId, entry.Job.SampleRate, entry.Key.Settings, entry.Job.ReadResults());
            }, token).ConfigureAwait(false);
            Report(entry, 1);
        }
        catch (Exception ex) { error = ex; }
        finally
        {
            entry.Job.Dispose();
            if (acquired) _workers.Release();
        }
        // Disposing user registrations can wait for a callback; do it outside the cache lock.
        CancellationTokenRegistration[] registrations;
        lock (_gate)
        {
            entry.Finished = true;
            registrations = entry.Registrations.ToArray();
            entry.Registrations.Clear();
            entry.Observers.Clear();
            bool current = _entries.TryGetValue(entry.Key, out var value) && ReferenceEquals(value, entry);
            if (error is null && result is not null && !token.IsCancellationRequested && current && !_disposed)
            {
                entry.Completion.TrySetResult(result);
                if (result.Transients.Count <= MaxCachedRecords)
                {
                    entry.Snapshot = result;
                    TrimCache();
                }
                else _entries.Remove(entry.Key); // one oversized file must not flush useful cached sources
            }
            else
            {
                if (current) _entries.Remove(entry.Key);
                if (error is OperationCanceledException || token.IsCancellationRequested || !current || _disposed)
                    entry.Completion.TrySetCanceled(new CancellationToken(true));
                else entry.Completion.TrySetException(error ?? new InvalidOperationException("Missing transient result."));
            }
        }
        foreach (var registration in registrations) registration.Dispose();
        entry.Cancellation.Dispose();
    }

    private void Report(Entry entry, double value)
    {
        IProgress<double>[] observers;
        lock (_gate) observers = entry.Observers.ToArray();
        foreach (var observer in observers) observer.Report(value);
    }

    private void TrimCache()
    {
        var completed = _entries.Values.Where(e => e.Snapshot is not null).OrderBy(e => e.Access).ToArray();
        int count = completed.Length;
        long records = completed.Sum(e => (long)e.Snapshot!.Transients.Count);
        foreach (var entry in completed)
        {
            if (count <= MaxEntries && records <= MaxCachedRecords) break;
            _entries.Remove(entry.Key);
            records -= entry.Snapshot!.Transients.Count;
            count--;
        }
    }

    public TransientSnapshot? GetTransients(long sourceId, TransientAnalysisSettings? settings = null)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(new Key(sourceId, settings ?? new()), out var entry))
            { entry.Access = ++_access; return entry.Snapshot; }
            return null;
        }
    }

    public AudioTransient? GetNearestTransient(long sourceId, long samplePosition, TransientAnalysisSettings? settings = null)
    {
        var snapshot = GetTransients(sourceId, settings);
        if (snapshot is null || snapshot.Transients.Count == 0) return null;
        int i = snapshot.LowerBound(samplePosition);
        var hits = snapshot.Transients;
        if (i == 0) return hits[0];
        if (i == hits.Count) return hits[^1];
        // Decimal differences avoid overflow/precision loss at extreme int64 positions.
        return (decimal)samplePosition - hits[i - 1].SamplePosition <= (decimal)hits[i].SamplePosition - samplePosition ? hits[i - 1] : hits[i];
    }

    public IReadOnlyList<AudioTransient> GetTransientsInRange(long sourceId, long startSample, long endSample,
        TransientAnalysisSettings? settings = null)
    {
        var snapshot = GetTransients(sourceId, settings);
        if (snapshot is null || endSample <= startSample) return Array.Empty<AudioTransient>();
        int lo = snapshot.LowerBound(startSample), hi = snapshot.LowerBound(endSample);
        var result = new AudioTransient[hi - lo];
        for (int i = lo; i < hi; i++) result[i - lo] = snapshot.Transients[i];
        return Array.AsReadOnly(result);
    }

    public void InvalidateAnalysis(long sourceId)
    {
        lock (_gate)
        {
            foreach (var entry in _entries.Values.Where(e => e.Key.SourceId == sourceId).ToArray())
            {
                _entries.Remove(entry.Key);
                if (!entry.Finished) entry.Cancellation.Cancel();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var entry in _entries.Values) if (!entry.Finished) entry.Cancellation.Cancel();
            _entries.Clear();
        }
        // Semaphore remains alive for outstanding workers, released once their finally exits.
    }
}
