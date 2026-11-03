// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Nota.Application;
using Nota.Application.Localization;

namespace Nota.App;

/// <summary>Per-engine UI preferences/notifications shared by Arrangement and the audio
/// editor. DSP and caching belong to the Application service; this object only marshals
/// completed snapshots/progress to UI listeners. Capture always occurs before the await.</summary>
internal sealed class TransientWorkspace
{
    private static readonly ConditionalWeakTable<IAudioEngine, TransientWorkspace> Instances = new();
    internal static TransientWorkspace For(IAudioEngine engine) => Instances.GetValue(engine, e => new(e));
    private readonly IAudioEngine _engine;
    private readonly Dictionary<long, Job> _running = new();
    private readonly HashSet<long> _knownSources = new();
    private TransientSnapshot? _latest;
    private long _errorSource;
    private long _settingsGeneration;
    private CancellationTokenSource? _settingsDebounce;
    private string? _error;
    private sealed class Job { internal readonly CancellationTokenSource Cancel = new(); internal double Progress; }
    private TransientWorkspace(IAudioEngine engine)
    {
        _engine = engine;
    }
    internal TransientAnalysisSettings Settings { get; private set; } = new();
    internal bool Show { get; private set; } = true;
    internal bool Snap { get; private set; } = true;
    internal event Action? Changed;
    internal event Action? ProgressChanged;
    internal bool IsRunning(long source) => _running.ContainsKey(source);
    internal double Progress(long source) => _running.TryGetValue(source, out var job) ? job.Progress : 0;
    internal string? Error(long source) => source == _errorSource ? _error : null;
    internal TransientSnapshot? Snapshot(long source)
    {
        try
        {
            return _engine.TransientAnalysis.GetTransients(source, Settings)
                ?? (_latest?.SourceId == source && _latest.Settings == Settings ? _latest : null);
        }
        catch (ObjectDisposedException) { return null; }
    }

    internal void SetShow(bool show) { Show = show; Changed?.Invoke(); }
    internal void SetSnap(bool snap) { Snap = snap; Changed?.Invoke(); }
    internal void Configure(TransientAnalysisSettings settings, long source)
    {
        settings.Validate();
        if (Settings == settings) return;
        if (source > 0) _knownSources.Add(source);
        foreach (var job in _running.Values) job.Cancel.Cancel();
        _running.Clear();
        _latest = null; _error = null;
        Settings = settings;
        Changed?.Invoke();
        ProgressChanged?.Invoke();
        _settingsGeneration++;
        _settingsDebounce?.Cancel();
        _settingsDebounce = null;
        if (_knownSources.Count > 0)
        {
            var debounce = new CancellationTokenSource();
            _settingsDebounce = debounce;
            _ = DebounceAnalysisAsync(debounce, _settingsGeneration);
        }
    }
    internal void Cancel(long source) { if (_running.TryGetValue(source, out var job)) job.Cancel.Cancel(); }

    internal async Task AnalyzeAsync(long source)
    {
        if (source <= 0) return;
        _knownSources.Add(source);
        if (_running.ContainsKey(source)) return;
        var job = new Job();
        _running.Add(source, job);
        _error = null;
        ProgressChanged?.Invoke();
        TransientSnapshot? result = null;
        string? error = null;
        try
        {
            var progress = new UiProgress(value =>
            {
                if (!_running.TryGetValue(source, out var current) || current != job) return;
                job.Progress = value;
                ProgressChanged?.Invoke();
            });
            // Authoring-thread source capture happens synchronously in this call.
            var task = _engine.TransientAnalysis.AnalyzeAudioAsync(source, Settings, job.Cancel.Token, progress);
            result = await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception) { error = Loc.T("Transient analysis failed. Try analysing the audio again."); }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_running.TryGetValue(source, out var current) && current == job)
            {
                _running.Remove(source);
                if (!job.Cancel.IsCancellationRequested && result is not null) _latest = result;
                _errorSource = source; _error = error;
                Changed?.Invoke();
                ProgressChanged?.Invoke();
            }
            job.Cancel.Dispose();
        });
    }

    private async Task AnalyzeConfiguredSourcesAsync(long generation)
    {
        foreach (long source in _knownSources.ToArray())
        {
            if (generation != _settingsGeneration) return;
            await AnalyzeAsync(source);
        }
    }

    private async Task DebounceAnalysisAsync(CancellationTokenSource debounce, long generation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(180), debounce.Token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_settingsDebounce, debounce)) return;
                _settingsDebounce = null;
                _ = AnalyzeConfiguredSourcesAsync(generation);
            });
        }
        catch (OperationCanceledException) { }
        finally { debounce.Dispose(); }
    }

    private sealed class UiProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => Dispatcher.UIThread.Post(() => report(value));
    }
}
