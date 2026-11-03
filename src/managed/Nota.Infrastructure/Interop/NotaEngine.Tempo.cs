// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Runtime.InteropServices;
using Nota.Application;

namespace Nota.Infrastructure;
public sealed partial class NotaEngine
{
    public byte[] GetGridWarpBaseline(int trackId, int clipIndex)
    {
        ThrowIfDisposed(); int size = NativeMethods.GridBaselineGet(_handle, trackId, clipIndex, null, 0);
        if (size <= 0 || size > 8 * 1024 * 1024) return Array.Empty<byte>();
        var bytes = new byte[size];
        if (NativeMethods.GridBaselineGet(_handle, trackId, clipIndex, bytes, size) != size)
            throw new InvalidOperationException("Grid Warp baseline changed during capture");
        return bytes;
    }
    public void RestoreGridWarpBaseline(int trackId, int clipIndex, byte[] state)
    { ThrowIfDisposed(); Check(NativeMethods.GridBaselineRestore(_handle, trackId, clipIndex, state, state.Length)); }
    public bool TryResetGridWarp(int trackId, int clipIndex, out string error)
    {
        ThrowIfDisposed(); bool ok = NativeMethods.GridReset(_handle, trackId, clipIndex) == NativeMethods.NotaResult.Ok;
        error = ok ? "" : TempoEditError; return ok;
    }
    public GridReferenceGeometry? GetGridReferenceGeometry(int trackId, int clipIndex)
    {
        ThrowIfDisposed(); var values = new double[4];
        return NativeMethods.GridReferenceInfo(_handle, trackId, clipIndex, values) == 0 ? null
            : new(values[0], values[1], values[2], values[3]);
    }
    public void RestoreGridReferenceGeometry(int trackId, int clipIndex, GridReferenceGeometry geometry)
    {
        ThrowIfDisposed(); Check(NativeMethods.GridReferenceRestore(_handle, trackId, clipIndex,
            new[] { geometry.SourceStart, geometry.SourceEnd, geometry.OriginSeconds, geometry.SourceRate }));
    }
    public bool ClipGridFollowsAudio(int trackId, int clipIndex)
    { ThrowIfDisposed(); return NativeMethods.ClipGridFollowsAudio(_handle, trackId, clipIndex) != 0; }
    public void RestoreClipTempoReference(int trackId, int clipIndex, bool gridFollows, long referenceId = 0)
    { ThrowIfDisposed(); Check(NativeMethods.RestoreClipTempoReference(_handle, trackId, clipIndex, gridFollows ? 1 : 0, referenceId)); }
    public long ClipTempoReferenceId(int trackId, int clipIndex)
    { ThrowIfDisposed(); return NativeMethods.ClipTempoReferenceId(_handle, trackId, clipIndex); }
    public bool TrySetClipTempoMode(int trackId, int clipIndex, bool gridFollows, bool lockAnchors, out string error)
    {
        ThrowIfDisposed();
        var result = NativeMethods.ClipTempoMode(_handle, trackId, clipIndex, gridFollows ? 1 : 0, lockAnchors ? 1 : 0);
        error = result == NativeMethods.NotaResult.Ok ? "" : Marshal.PtrToStringUTF8(NativeMethods.TempoError(_handle)) ?? "Invalid tempo edit";
        return result == NativeMethods.NotaResult.Ok;
    }
    public long TempoRevision { get { ThrowIfDisposed(); return NativeMethods.TempoRevision(_handle); } }
    public string TempoEditError { get { ThrowIfDisposed(); return Marshal.PtrToStringUTF8(NativeMethods.TempoError(_handle)) ?? ""; } }
    public TempoMapState GetTempoMap()
    {
        ThrowIfDisposed();
        var points = new TempoPoint[NativeMethods.TempoPoints(_handle, null, 0)];
        var anchors = new TempoAnchor[NativeMethods.TempoAnchors(_handle, null, 0)];
        NativeMethods.TempoPoints(_handle, points, points.Length);
        NativeMethods.TempoAnchors(_handle, anchors, anchors.Length);
        return new(points, anchors, NativeMethods.TempoMin(_handle), NativeMethods.TempoMax(_handle));
    }
    public bool TrySetTempoMap(TempoMapState map, out string error, bool checkpoint = true)
    {
        ThrowIfDisposed();
        var result = NativeMethods.TempoSet(_handle, map.Points, map.Points.Length,
            map.MinBpm, map.MaxBpm, map.Anchors, map.Anchors.Length, checkpoint ? 1 : 0);
        error = result == NativeMethods.NotaResult.Ok ? "" : Marshal.PtrToStringUTF8(NativeMethods.TempoError(_handle)) ?? "Invalid tempo map";
        return result == NativeMethods.NotaResult.Ok;
    }
    public bool TryCommitGridWarp(int trackId, int clipIndex, TempoMapState map, out string error)
    {
        ThrowIfDisposed();
        var result = NativeMethods.GridWarpCommit(_handle, trackId, clipIndex, map.Points,
            map.Points.Length, map.MinBpm, map.MaxBpm, map.Anchors, map.Anchors.Length);
        error = result == NativeMethods.NotaResult.Ok ? "" : Marshal.PtrToStringUTF8(NativeMethods.TempoError(_handle)) ?? "Invalid Grid Warp edit";
        return result == NativeMethods.NotaResult.Ok;
    }
    public double SecondsAtBeat(double beat) { ThrowIfDisposed(); return NativeMethods.SecondsAtBeat(_handle, beat); }
    public double BeatAtSeconds(double seconds) { ThrowIfDisposed(); return NativeMethods.BeatAtSeconds(_handle, seconds); }
    public double TempoAtBeat(double beat) { ThrowIfDisposed(); return NativeMethods.TempoAtBeat(_handle, beat); }
}
