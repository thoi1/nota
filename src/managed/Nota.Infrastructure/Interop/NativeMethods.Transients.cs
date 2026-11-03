// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Nota.Infrastructure;

internal sealed class TransientAnalysisHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal TransientAnalysisHandle(IntPtr value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle() { NativeMethods.TransientDestroy(handle); return true; }
}

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TransientSettings
    {
        internal double Sensitivity, MinimumSpacingSeconds;
        internal int RefineAttacks, Reserved;
    }
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_create")]
    internal static partial IntPtr TransientCreate(IntPtr engine, long sourceId, in TransientSettings settings);
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_step")]
    internal static partial int TransientStep(TransientAnalysisHandle job, int maxFrames);
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_cancel")]
    internal static partial void TransientCancel(TransientAnalysisHandle job);
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_progress")]
    internal static partial double TransientProgress(TransientAnalysisHandle job);
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_sample_rate")]
    internal static partial double TransientSampleRate(TransientAnalysisHandle job);
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_count")]
    internal static partial long TransientCount(TransientAnalysisHandle job);
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_read")]
    internal static partial int TransientRead(TransientAnalysisHandle job, long offset, [Out] AudioTransient[] output, int capacity);
    [LibraryImport(Lib, EntryPoint = "nota_transient_analysis_destroy")]
    internal static partial void TransientDestroy(IntPtr job);
}
