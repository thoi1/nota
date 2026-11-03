// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Runtime.InteropServices;
using Nota.Application;

namespace Nota.Infrastructure;
internal static partial class NativeMethods
{
    [LibraryImport(Lib, EntryPoint = "nota_grid_baseline_get")]
    internal static partial int GridBaselineGet(IntPtr engine, int track, int clip, [Out] byte[]? state, int capacity);
    [LibraryImport(Lib, EntryPoint = "nota_grid_baseline_restore")]
    internal static partial NotaResult GridBaselineRestore(IntPtr engine, int track, int clip, [In] byte[] state, int size);
    [LibraryImport(Lib, EntryPoint = "nota_grid_warp_reset")]
    internal static partial NotaResult GridReset(IntPtr engine, int track, int clip);
    [LibraryImport(Lib, EntryPoint = "nota_grid_reference_info")]
    internal static partial int GridReferenceInfo(IntPtr engine, int track, int clip, [Out] double[] values);
    [LibraryImport(Lib, EntryPoint = "nota_grid_reference_restore")]
    internal static partial NotaResult GridReferenceRestore(IntPtr engine, int track, int clip, [In] double[] values);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_points")]
    internal static partial int TempoPoints(IntPtr engine, [Out] TempoPoint[]? output, int capacity);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_anchors")]
    internal static partial int TempoAnchors(IntPtr engine, [Out] TempoAnchor[]? output, int capacity);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_set")]
    internal static partial NotaResult TempoSet(IntPtr engine, [In] TempoPoint[] points, int count,
        double minBpm, double maxBpm, [In] TempoAnchor[] anchors, int anchorCount, int checkpoint);
    [LibraryImport(Lib, EntryPoint = "nota_grid_warp_commit")]
    internal static partial NotaResult GridWarpCommit(IntPtr engine, int track, int clip,
        [In] TempoPoint[] points, int count, double minBpm, double maxBpm,
        [In] TempoAnchor[] anchors, int anchorCount);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_min_bpm")]
    internal static partial double TempoMin(IntPtr engine);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_max_bpm")]
    internal static partial double TempoMax(IntPtr engine);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_seconds_at_beat")]
    internal static partial double SecondsAtBeat(IntPtr engine, double beat);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_beat_at_seconds")]
    internal static partial double BeatAtSeconds(IntPtr engine, double seconds);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_bpm_at_beat")]
    internal static partial double TempoAtBeat(IntPtr engine, double beat);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_error")]
    internal static partial IntPtr TempoError(IntPtr engine);
    [LibraryImport(Lib, EntryPoint = "nota_tempo_revision")]
    internal static partial long TempoRevision(IntPtr engine);
    [LibraryImport(Lib, EntryPoint = "nota_clip_grid_follows_audio")]
    internal static partial int ClipGridFollowsAudio(IntPtr engine, int track, int clip);
    [LibraryImport(Lib, EntryPoint = "nota_clip_tempo_mode")]
    internal static partial NotaResult ClipTempoMode(IntPtr engine, int track, int clip, int gridFollows, int lockAnchors);
    [LibraryImport(Lib, EntryPoint = "nota_clip_tempo_reference_restore")]
    internal static partial NotaResult RestoreClipTempoReference(IntPtr engine, int track, int clip, int gridFollows, long referenceId);
    [LibraryImport(Lib, EntryPoint = "nota_clip_tempo_reference_id")]
    internal static partial long ClipTempoReferenceId(IntPtr engine, int track, int clip);
}
