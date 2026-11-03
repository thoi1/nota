// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;

namespace Nota.Application.Editing;

/// <summary>One waveform choice/settings for all editors in the running application.</summary>
public sealed class SharedWaveformState
{
    public static SharedWaveformState Shared { get; } = new();
    private AutomationWaveform _shape;
    private double _amplitude = 1, _cycleBars = 1, _phase;
    public event Action? Changed;
    public AutomationWaveform Shape
    {
        get => _shape;
        set { if (_shape == value) return; _shape = value; Changed?.Invoke(); }
    }
    public double Amplitude { get => _amplitude; set => Set(ref _amplitude, Math.Clamp(value, 0, 1)); }
    public double CycleBars { get => _cycleBars; set => Set(ref _cycleBars, Math.Clamp(value, 0.25, 4)); }
    public double Phase { get => _phase; set => Set(ref _phase, value - Math.Floor(value)); }
    private void Set(ref double field, double value)
    {
        if (!double.IsFinite(value) || field == value) return;
        field = value; Changed?.Invoke();
    }
    public void Cycle() => Shape = (AutomationWaveform)(((int)Shape + 1) % 5);
}

/// <summary>Each editor retains its own tool; changing shape never interrupts another editor.</summary>
public sealed class EditorToolState
{
    public EditorToolState(SharedWaveformState? waveform = null) => Waveform = waveform ?? SharedWaveformState.Shared;
    public SharedWaveformState Waveform { get; }
    public ArrangementTool Tool { get; private set; }
    public event Action? Changing;
    public event Action? Changed;
    public void Choose(ArrangementTool tool, bool cycleActiveWaveform = false)
    {
        if (Tool == tool)
        {
            if (cycleActiveWaveform && tool == ArrangementTool.WaveformDraw) Waveform.Cycle();
            return;
        }
        Changing?.Invoke();
        Tool = tool;
        Changed?.Invoke();
    }
    public static string? CommandForNumber(int number) => number switch
    {
        1 => "tool.select", 2 => "tool.pencil", 3 => "tool.eraser", 4 => "tool.waveform", 5 => "tool.warp", _ => null,
    };
}
