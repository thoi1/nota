// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using Avalonia.Controls;
using Nota.Presentation;

namespace Nota.App;

public partial class MainWindow
{
    private Window? _playheadFrameOwner;
    private Action<TimeSpan>? _playheadFrameCallback;
    private bool _playheadFramePending;
    private bool _playheadFramesClosed;
    private int _playheadFrameGeneration;
    private readonly MeterRefreshCadence _meterCadence = new();

    private Window? VisiblePlayheadWindow()
        => IsVisible && WindowState != WindowState.Minimized ? this
         : _detailWindow is { IsVisible: true, WindowState: not WindowState.Minimized } detail ? detail
         : _mixerWindow is { IsVisible: true, WindowState: not WindowState.Minimized } mixer ? mixer : null;

    private bool VisibleLevelMeters()
        => _masterMeter is { CanUpdate: true }
        || (_mixer is { IsEffectivelyVisible: true } && TopLevel.GetTopLevel(_mixer)
            is Window { IsVisible: true, WindowState: not WindowState.Minimized })
        || (IsVisible && WindowState != WindowState.Minimized
            && (Timeline.IsEffectivelyVisible || _session is { IsEffectivelyVisible: true }));

    // One shared presentation loop: cursors every frame, primary meters at their chosen rate.
    // Recording materialization, CPU/DSP sampling and device polling keep their service clock.
    private void EnsurePlayheadFrames()
    {
        var vm = _vm;
        Window? owner = !_playheadFramesClosed && vm is { SuspendEnginePolling: false }
            && vm.Engine.IsRunning && (vm.Engine.IsPlaying || VisibleLevelMeters()) ? VisiblePlayheadWindow() : null;
        if (!ReferenceEquals(owner, _playheadFrameOwner))
        {
            _playheadFrameOwner = owner;
            _playheadFramePending = false;
            _meterCadence.Reset();
            int generation = ++_playheadFrameGeneration;
            _playheadFrameCallback = owner is null ? null : _ => OnPlayheadFrame(generation);
        }
        if (owner is null || _playheadFramePending) return;
        _playheadFramePending = true;
        owner.RequestAnimationFrame(_playheadFrameCallback!);
    }

    private void OnPlayheadFrame(int generation)
    {
        if (_playheadFramesClosed || generation != _playheadFrameGeneration) return;
        _playheadFramePending = false;
        var vm = _vm;
        if (vm is null || vm.SuspendEnginePolling) { StopPlayheadFrames(); return; }
        // A minimize, docking or export may have happened since this frame was queued.
        if (!ReferenceEquals(VisiblePlayheadWindow(), _playheadFrameOwner) || !vm.Engine.IsRunning)
        {
            EnsurePlayheadFrames();
            return;
        }
        if (_meterCadence.IsDue((double)System.Diagnostics.Stopwatch.GetTimestamp()
            / System.Diagnostics.Stopwatch.Frequency, vm.Settings.Current.MeterRefreshHz)) UpdateLevelMetersFrame();
        if (vm.Engine.IsPlaying && vm.Engine.TryGetTransportSnapshot(out var snapshot))
        {
            double displayBeats = TransportDisplayPosition.At(snapshot, vm.Engine);
            if (IsVisible && WindowState != WindowState.Minimized)
                Timeline.SetPlayheadFrame(snapshot.PositionBeats, displayBeats, snapshot.IsPlaying);
            // Editor cursors use cheap overlays too, including in the floating detail window.
            if (_audioEditor is { IsEffectivelyVisible: true })
                _audioEditor.OnPlayhead(displayBeats, snapshot.IsPlaying);
            if (_clipEditor is { IsEffectivelyVisible: true } && _editorRoll is not null && _editorTrackId > 0)
                _editorRoll.SetPlayhead(displayBeats - _editorRoll.Origin, true);
        }
        EnsurePlayheadFrames(); // meters can monitor live input while transport is stopped
    }

    private void UpdateLevelMetersFrame()
    {
        if (_vm is null) return;
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            if (_masterMeter is { CanUpdate: true }) _masterMeter.Push(_vm.Engine.MasterMeter());
            if (Timeline.IsEffectivelyVisible) Timeline.UpdateLevelMeters();
            if (_session is { IsEffectivelyVisible: true }) _session.UpdateLevelMeters();
            if (DetailBody.Content is MixerView mx && mx.IsEffectivelyVisible) mx.UpdateLevelMeters();
        }
        if (_mixer is { IsEffectivelyVisible: true }
            && (TopLevel.GetTopLevel(_mixer) is Window { IsVisible: true, WindowState: not WindowState.Minimized }))
            _mixer.UpdateLevelMeters();
    }

    private void StopPlayheadFrames()
    {
        ++_playheadFrameGeneration; // invalidate queued callbacks before disposing the engine
        _playheadFrameOwner = null;
        _playheadFrameCallback = null;
        _playheadFramePending = false;
    }
}
