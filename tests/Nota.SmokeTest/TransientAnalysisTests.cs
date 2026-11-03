// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Diagnostics;
using Nota.Application;
using Nota.Infrastructure;

namespace Nota.SmokeTest;

internal static class TransientAnalysisTests
{
    public static IEnumerable<(bool, string)> Run()
    {
        var results = new List<(bool, string)>();
        void Check(bool ok, string label) => results.Add((ok, label));
        string dir = Path.Combine(Path.GetTempPath(), "nota-transients-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var engine = new NotaEngine();
            var clock = Stopwatch.StartNew();
            foreach (int sr in new[] { 22050, 44100, 48000, 96000 })
            {
                long id = Load(engine, dir, sr, Attacks(sr, new[] { .2, .7, 1.2 }, sharp: true));
                var snapshot = engine.TransientAnalysis.AnalyzeAudioAsync(id).GetAwaiter().GetResult();
                var hits = snapshot.Transients;
                Check(hits.Count == 3 && hits.Zip(new[] { .2, .7, 1.2 }).All(p => Math.Abs(p.First.Seconds - p.Second) <= .002),
                    $"SuperFlux {sr} Hz: three sharp attacks within 2 ms ({string.Join(",", hits.Select(h => h.Seconds.ToString("F4")))})");
                Check(hits.All(t => t.SamplePosition >= 0 && t.Confidence is >= 0 and <= 1 && t.Strength > 0
                    && t.UncertaintySamples >= 1 && t.AnalysisVersion == TransientAnalysisSettings.AnalysisVersion)
                    && hits.Select(t => t.SamplePosition).SequenceEqual(hits.Select(t => t.SamplePosition).Order()),
                    $"SuperFlux {sr} Hz: sorted versioned source results with confidence/uncertainty");
            }

            const int rate = 44100;
            var attacks = new[] { .2, .6, 1.0, 1.4 };
            foreach (var (name, signal) in new[]
            {
                ("soft piano", Attacks(rate, attacks, sharp: false)),
                ("piano chords", Attacks(rate, attacks, sharp: false, chord: true)),
                ("arpeggios", Attacks(rate, new[] { .2, .35, .50, .65, .8, .95, 1.1, 1.25 }, sharp: false))
            })
            {
                var h = engine.TransientAnalysis.AnalyzeAudioAsync(Load(engine, dir, rate, signal)).GetAwaiter().GetResult().Transients;
                var expected = name == "arpeggios" ? new[] { .2, .35, .50, .65, .8, .95, 1.1, 1.25 } : attacks;
                Check(h.Count == expected.Length && h.Zip(expected).All(p => Math.Abs(p.First.Seconds - p.Second) <= .020),
                    $"SuperFlux {name}: one candidate per attack within 20 ms ({string.Join(",", h.Select(t => t.Seconds.ToString("F3")))})");
            }
            var silent = engine.TransientAnalysis.AnalyzeAudioAsync(Load(engine, dir, rate, new float[rate])).GetAwaiter().GetResult();
            Check(silent.Transients.Count == 0, "SuperFlux silence: no arbitrary attack");
            Check(engine.TransientAnalysis.AnalyzeAudioAsync(Load(engine, dir, rate, new float[32])).GetAwaiter().GetResult().Transients.Count == 0,
                "SuperFlux short recording safely zero-pads");
            var noise = new float[rate];
            var random = new Random(21);
            for (int i = 0; i < noise.Length; i++) noise[i] = (float)((random.NextDouble() * 2 - 1) * 1e-7);
            Check(engine.TransientAnalysis.AnalyzeAudioAsync(Load(engine, dir, rate, noise)).GetAwaiter().GetResult().Transients.Count == 0,
                "SuperFlux low-level noise: no false attacks");
            var offGrid = new[] { .20337, .61159, 1.10731, 1.71777 };
            var oh = engine.TransientAnalysis.AnalyzeAudioAsync(Load(engine, dir, rate, Attacks(rate, offGrid, sharp: true))).GetAwaiter().GetResult().Transients;
            Check(oh.Count == offGrid.Length && oh.Zip(offGrid).All(p => p.First.IsRefined && Math.Abs(p.First.Seconds - p.Second) < .002),
                "SuperFlux refinement improves attacks between analysis hops");
            var clipped = Attacks(rate, offGrid, sharp: true).Select(x => Math.Clamp(x * 20, -1, 1)).ToArray();
            Check(engine.TransientAnalysis.AnalyzeAudioAsync(Load(engine, dir, rate, clipped)).GetAwaiter().GetResult().Transients.Count == 4,
                "SuperFlux finite clipped attacks remain analysable");
            var vibrato = new float[rate * 2];
            double phase = 0;
            for (int i = 0; i < vibrato.Length; i++)
            {
                double t = (double)i / rate;
                phase += 2 * Math.PI * 440 * Math.Pow(2, .7 / 12 * Math.Sin(2 * Math.PI * 6 * t)) / rate;
                vibrato[i] = (float)(.4 * Math.Min(1, t / .010) * Math.Min(1, (2 - t) / .100) * Math.Sin(phase));
            }
            var vh = engine.TransientAnalysis.AnalyzeAudioAsync(Load(engine, dir, rate, vibrato)).GetAwaiter().GetResult().Transients;
            Check(vh.All(t => t.Seconds < .08), $"SuperFlux sustained vibrato: no repeated attacks ({string.Join(",", vh.Select(t => t.Seconds.ToString("F4")))})");

            var mono = Attacks(rate, attacks, sharp: true);
            long source = Load(engine, dir, rate, mono);
            var settings = new TransientAnalysisSettings();
            var shared = engine.TransientAnalysis;
            var a = shared.AnalyzeAudioAsync(source, settings).GetAwaiter().GetResult();
            var progressValues = new List<double>();
            var unrefined = shared.AnalyzeAudioAsync(source, settings with { RefineAttacks = false },
                progress: new InlineProgress(progressValues.Add)).GetAwaiter().GetResult();
            Check(unrefined.Transients.All(t => !t.IsRefined && t.UncertaintySamples >= rate / 200)
                && progressValues.Count > 0 && progressValues[^1] == 1
                && progressValues.All(p => p is >= 0 and <= 1) && progressValues.SequenceEqual(progressValues.Order()),
                "unrefined spectral results preserve uncertainty; progress is monotonic through completion");
            Check(ReferenceEquals(a, shared.AnalyzeAudioAsync(source, settings).GetAwaiter().GetResult())
                && ReferenceEquals(a, shared.GetTransients(source)), "transient service reuses completed immutable snapshot");
            var stereo = new float[mono.Length * 2];
            for (int i = 0; i < mono.Length; i++) { stereo[i * 2] = mono[i]; stereo[i * 2 + 1] = -mono[i]; }
            var sh = shared.AnalyzeAudioAsync(Load(engine, dir, rate, stereo, 2)).GetAwaiter().GetResult();
            Check(sh.Transients.Select(t => t.SamplePosition).SequenceEqual(a.Transients.Select(t => t.SamplePosition)),
                "SuperFlux antiphase stereo retains identical attack positions");
            Check(shared.GetTransientsInRange(source, a.Transients[1].SamplePosition, a.Transients[3].SamplePosition).Count == 2,
                "transient range is start-inclusive/end-exclusive");
            Check(shared.GetNearestTransient(source, a.Transients[1].SamplePosition + 1)?.SamplePosition == a.Transients[1].SamplePosition
                && shared.GetNearestTransient(source, long.MaxValue)?.SamplePosition == a.Transients[^1].SamplePosition
                && shared.GetNearestTransient(source, long.MinValue)?.SamplePosition == a.Transients[0].SamplePosition,
                "transient nearest binary search handles exact/before/after and extreme positions");
            var different = settings with { Sensitivity = .7 };
            var b = shared.AnalyzeAudioAsync(source, different).GetAwaiter().GetResult();
            Check(!ReferenceEquals(a, b) && ReferenceEquals(a, shared.GetTransients(source)), "transient cache separates settings");
            var thresholdAudio = Attacks(rate, new[] { .2, .6, 1.0 }, sharp: true);
            Array.Clear(thresholdAudio, (int)(.6 * rate), rate / 4);
            var weakHit = Attacks(rate, new[] { .6 }, sharp: true);
            for (int i = 0; i < rate / 4; i++) thresholdAudio[(int)(.6 * rate) + i] = weakHit[(int)(.6 * rate) + i] * .03f;
            long thresholdSource = Load(engine, dir, rate, thresholdAudio);
            var lowerThreshold = shared.AnalyzeAudioAsync(thresholdSource, settings with { Sensitivity = .8 }).GetAwaiter().GetResult();
            var higherThreshold = shared.AnalyzeAudioAsync(thresholdSource, settings with { Sensitivity = .2 }).GetAwaiter().GetResult();
            Check(lowerThreshold.Transients.Count > higherThreshold.Transients.Count,
                $"lower detection threshold accepts more weak transients than a higher threshold ({lowerThreshold.Transients.Count} vs {higherThreshold.Transients.Count})");
            var spacingSource = Load(engine, dir, rate, Attacks(rate, new[] { .2, .3, .6, .7 }, sharp: true));
            var spaced = shared.AnalyzeAudioAsync(spacingSource, settings with { MinimumSpacingSeconds = .250 }).GetAwaiter().GetResult();
            Check(spaced.Transients.Count == 2 && spaced.Transients.Zip(spaced.Transients.Skip(1)).All(p => p.Second.Seconds - p.First.Seconds >= .250),
                "minimum transient spacing suppresses close candidates without quantising source positions");
            shared.InvalidateAnalysis(source);
            Check(shared.GetTransients(source) is null && shared.GetTransients(source, different) is null && a.Transients.Count == 4,
                "invalidation removes every setting, retained snapshot remains valid");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                bool cancelled = false;
                try { shared.AnalyzeAudioAsync(source, cancellationToken: cancel.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && shared.GetTransients(source) is null, "pre-cancelled analysis does not publish");
            }
            var retry = shared.AnalyzeAudioAsync(source);
            Check(ReferenceEquals(retry, shared.AnalyzeAudioAsync(source)), "simultaneous same-key consumers share job/task");
            retry.GetAwaiter().GetResult();
            using (var cancelJob = engine.CreateTransientAnalysis(source, settings))
            {
                Check(cancelJob.Step(1) == 1 && cancelJob.Progress is > 0 and < 1, "native step exposes bounded monotonic progress");
                bool partialBlocked = false;
                try { cancelJob.ReadResults(); } catch (InvalidOperationException) { partialBlocked = true; }
                cancelJob.Cancel();
                Check(cancelJob.Step(32) == -2 && partialBlocked, "native cancellation suppresses partial result publication");
            }
            var maximumSpacing = settings with { MinimumSpacingSeconds = .250 };
            maximumSpacing.Validate();
            foreach (var bad in new[] { settings with { Sensitivity = double.NaN },
                settings with { MinimumSpacingSeconds = .004 }, settings with { MinimumSpacingSeconds = .251 } })
            {
                bool rejected = false;
                try { shared.AnalyzeAudioAsync(source, bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "transient invalid settings rejected before native capture");
            }
            ServiceLifecycleTests(a.Transients.ToArray(), Check);
            using (var captured = engine.CreateTransientAnalysis(source, settings))
            {
                // A captured job never reads engine state on its worker, even after engine disposal.
                engine.Dispose();
                Task.Run(() => { while (captured.Step(32) > 0) { } }).GetAwaiter().GetResult();
                Check(captured.ReadResults().Length == 4, "native captured source survives engine disposal");
            }
            Check(clock.Elapsed.TotalSeconds < 60, $"transient deterministic suite completed in {clock.Elapsed.TotalSeconds:F2}s (measured)");
        }
        finally { Directory.Delete(dir, recursive: true); }
        return results;
    }

    private static void ServiceLifecycleTests(AudioTransient[] hits, Action<bool, string> check)
    {
        var jobs = new List<ControlledJob>();
        int authoringThread = Environment.CurrentManagedThreadId;
        bool captureOnAuthoring = true;
        using var service = new TransientAnalysisService((id, _) =>
        {
            captureOnAuthoring &= Environment.CurrentManagedThreadId == authoringThread;
            var job = new ControlledJob(id == 99 ? Enumerable.Repeat(hits[0], 200_001).ToArray() : hits);
            jobs.Add(job);
            return job;
        });
        var tasks = Enumerable.Range(1, 6).Select(i => service.AnalyzeAudioAsync(i)).ToArray();
        try
        {
            check(jobs[0].Started.Wait(5000) && jobs[1].Started.Wait(5000)
                && jobs.Skip(2).All(j => !j.Started.IsSet), "transient service bounds active workers to two");
            service.InvalidateAnalysis(1);
            bool cancelled = false;
            try { tasks[0].GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            check(cancelled && service.GetTransients(1) is null, "running invalidation cancels and prevents stale publication");
            service.InvalidateAnalysis(6);
            cancelled = false;
            try { tasks[5].GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            check(cancelled && !jobs[5].Started.IsSet, "queued invalidation cancels before processing");
            foreach (var job in jobs) job.Release.Set();
            foreach (var task in tasks.Skip(1).Take(4)) task.GetAwaiter().GetResult();
            var restarted = service.AnalyzeAudioAsync(1);
            jobs[^1].Release.Set();
            check(restarted.GetAwaiter().GetResult().Transients.Count == hits.Length, "invalidated source can be analysed again");
            using var cancel = new CancellationTokenSource();
            var sharedJob = service.AnalyzeAudioAsync(7, cancellationToken: cancel.Token);
            var secondConsumer = service.AnalyzeAudioAsync(7);
            check(ReferenceEquals(sharedJob, secondConsumer), "in-flight consumers share one analysis");
            cancel.Cancel();
            cancelled = false;
            try { sharedJob.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            check(cancelled, "consumer cancellation propagates through shared job");
            // Fill the cache past its limit, then touch a recent item and ensure eviction.
            for (int id = 10; id < 30; id++)
            {
                var t = service.AnalyzeAudioAsync(id);
                jobs[^1].Release.Set();
                t.GetAwaiter().GetResult();
            }
            check(service.GetTransients(10) is null && service.GetTransients(29) is not null,
                "completed transient cache evicts old sources at capacity");
            var oversized = service.AnalyzeAudioAsync(99);
            jobs[^1].Release.Set();
            check(oversized.GetAwaiter().GetResult().Transients.Count == 200_001
                && service.GetTransients(99) is null && service.GetTransients(29) is not null,
                "oversized results return uncached without evicting useful sources");
            check(captureOnAuthoring, "all engine-source captures stay on requesting authoring thread");
            var disposing = service.AnalyzeAudioAsync(100);
            service.Dispose();
            cancelled = false;
            try { disposing.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            bool rejected = false;
            try { service.AnalyzeAudioAsync(101); } catch (ObjectDisposedException) { rejected = true; }
            check(cancelled && rejected, "service disposal cancels outstanding work and rejects requests");
        }
        finally
        {
            foreach (var job in jobs) job.Release.Set();
            service.Dispose();
        }
    }

    private sealed class ControlledJob(AudioTransient[] results) : ITransientAnalysisJob
    {
        internal readonly ManualResetEventSlim Started = new(), Release = new();
        private volatile bool _cancelled;
        public double SampleRate => 44100;
        public double Progress => 0;
        public int Step(int maxFrames) { Started.Set(); if (!Release.Wait(5000)) throw new TimeoutException(); return _cancelled ? -2 : 0; }
        public void Cancel() { _cancelled = true; Release.Set(); }
        public AudioTransient[] ReadResults() => results;
        public void Dispose() { }
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    internal static float[] Attacks(int sr, double[] times, bool sharp, bool chord = false)
    {
        var audio = new float[sr * 2];
        for (int k = 0; k < times.Length; k++)
        {
            int start = (int)Math.Round(times[k] * sr);
            for (int j = 0; j < sr * .25 && start + j < audio.Length; j++)
            {
                double t = (double)j / sr;
                double env = sharp ? Math.Exp(-t * 160) : (1 - Math.Exp(-t * 180)) * Math.Exp(-t * 24);
                double f = 220 * Math.Pow(2, k % 5 / 12.0);
                double wave = sharp ? (j % 2 == 0 ? 1 : -.7) : Math.Sin(2 * Math.PI * f * t) + .4 * Math.Sin(2 * Math.PI * f * 2.003 * t);
                if (chord) wave += .6 * Math.Sin(2 * Math.PI * f * 1.2599 * t) + .5 * Math.Sin(2 * Math.PI * f * 1.4983 * t);
                audio[start + j] += (float)(.3 * env * wave);
            }
        }
        return audio;
    }

    private static long Load(NotaEngine engine, string dir, int sr, float[] data, int channels = 1)
        => LoadFixture(engine, dir, sr, data, channels).SourceId;

    internal static (long SourceId, int Track, int Clip) LoadFixture(NotaEngine engine, string dir, int sr, float[] data, int channels = 1)
    {
        string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".wav");
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8); writer.Write(36 + data.Length * 4); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)3); writer.Write((short)channels); writer.Write(sr);
            writer.Write(sr * channels * 4); writer.Write((short)(channels * 4)); writer.Write((short)32);
            writer.Write("data"u8); writer.Write(data.Length * 4);
            foreach (float value in data) writer.Write(value);
        }
        int track = engine.AddAudioTrack();
        int clip = engine.AddAudioClip(track, path, 0);
        if (clip < 0 || !engine.TryGetAudioClipInfo(track, clip, out var info)) throw new InvalidOperationException("Fixture import failed");
        return (info.SampleId, track, clip);
    }
}
