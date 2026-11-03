// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Threading.Tasks;

namespace Nota.App;

public partial class MainWindow
{
    private int _tempoWarpGeneration;

    // Dragging sends the clock changes immediately; start expensive cache preparation
    // only after the last value settles. All engine authoring calls stay on the UI thread.
    private async void RequestTempoWarpBuild()
    {
        int generation = ++_tempoWarpGeneration;
        int project = _warpBuildGen;
        var vm = _vm;
        bool Alive() => !_playheadFramesClosed && vm is { SuspendEnginePolling: false } && ReferenceEquals(_vm, vm)
            && generation == _tempoWarpGeneration && project == _warpBuildGen;
        await Task.Delay(120);
        if (!Alive()) return;
        await PollWarpBuildAsync(Alive);
        if (!Alive()) return;
        Timeline.Refresh(rebuildHeaders: false);
        _audioEditor?.Reload();
        _session?.Refresh();
    }
}
