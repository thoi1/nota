// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

namespace Nota.Infrastructure;

public sealed partial class NotaEngine
{
    private TransientAnalysisService? _transientAnalysis;
    public ITransientAnalysisService TransientAnalysis
    {
        get
        {
            ThrowIfDisposed();
            return _transientAnalysis ??= new TransientAnalysisService(CreateTransientAnalysis);
        }
    }

    public ITransientAnalysisJob CreateTransientAnalysis(long sampleId, TransientAnalysisSettings settings)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var s = new NativeMethods.TransientSettings
        {
            Sensitivity = settings.Sensitivity,
            MinimumSpacingSeconds = settings.MinimumSpacingSeconds,
            RefineAttacks = settings.RefineAttacks ? 1 : 0
        };
        var handle = NativeMethods.TransientCreate(_handle, sampleId, in s);
        if (handle == IntPtr.Zero) throw new ArgumentException("Source unavailable or unsupported for transient analysis.", nameof(sampleId));
        return new TransientJob(new TransientAnalysisHandle(handle));
    }

    private sealed class TransientJob(TransientAnalysisHandle handle) : ITransientAnalysisJob
    {
        private int _status = 1;
        public double SampleRate => NativeMethods.TransientSampleRate(handle);
        public double Progress => NativeMethods.TransientProgress(handle);
        public int Step(int maxAnalysisFrames) => _status = NativeMethods.TransientStep(handle, maxAnalysisFrames);
        public void Cancel() => NativeMethods.TransientCancel(handle);
        public AudioTransient[] ReadResults()
        {
            if (_status != 0) throw new InvalidOperationException("Analysis is not complete.");
            long count = NativeMethods.TransientCount(handle);
            if (count > Array.MaxLength) throw new InvalidOperationException("Transient result exceeds managed capacity.");
            var result = new AudioTransient[(int)count];
            if (count > 0 && NativeMethods.TransientRead(handle, 0, result, result.Length) != result.Length)
                throw new InvalidOperationException("Incomplete native transient result.");
            return result;
        }
        public void Dispose() => handle.Dispose();
    }
}
