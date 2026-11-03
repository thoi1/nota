// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Nota.Application.Editing;
using Nota.Application.Localization;

namespace Nota.App;

/// <summary>The same native tool keys/menu in Arrangement and the docked or detached roll.</summary>
public sealed class EditingToolBar : UserControl
{
    private EditorToolState? _state;
    private bool _attached;
    private readonly Dictionary<ArrangementTool, ToggleButton> _buttons = new();
    private readonly Glyph _waveIcon;
    private readonly Button _menuButton;
    public EditorToolState? State
    {
        get => _state;
        set { Unsubscribe(); _state = value; if (_attached) Subscribe(); Sync(); }
    }

    public EditingToolBar()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (tool, name, kind, number) in new[]
        {
            (ArrangementTool.Select, Loc.T("Select Tool"), GlyphKind.Pointer, 1),
            (ArrangementTool.Pencil, Loc.T("Pencil Tool"), GlyphKind.Edit, 2),
            (ArrangementTool.Eraser, Loc.T("Eraser Tool"), GlyphKind.Eraser, 3),
            (ArrangementTool.WaveformDraw, Loc.T("Waveform Draw Tool"), GlyphKind.WaveDraw, 4),
            (ArrangementTool.NotaWarp, Loc.T("Nota Warp Tool"), GlyphKind.WarpGrid, 5),
        })
        {
            var icon = new Glyph(kind, 15);
            var button = new ToggleButton { Classes = { "tp-icon" }, Content = icon };
            var label = Loc.F("{0} ({1})", name, number);
            ToolTip.SetTip(button, label); AutomationProperties.SetName(button, label);
            if (tool == ArrangementTool.NotaWarp)
                AutomationProperties.SetHelpText(button, Loc.T("Ruler tempo-grid editing is not available yet"));
            button.Click += (_, _) => { State?.Choose(tool); Sync(); };
            _buttons.Add(tool, button); row.Children.Add(button);
            if (tool == ArrangementTool.WaveformDraw)
            {
                _waveIcon = icon;
                _menuButton = new Button { Classes = { "tp-icon" }, Content = new Glyph(GlyphKind.CaretDown, 15) };
                ToolTip.SetTip(_menuButton, Loc.T("Waveform drawing settings"));
                AutomationProperties.SetName(_menuButton, Loc.T("Waveform drawing settings"));
                _menuButton.Click += (_, _) => ShowWaveformMenu();
                row.Children.Add(_menuButton);
            }
        }
        Content = row;
        AttachedToVisualTree += (_, _) => { _attached = true; Subscribe(); Sync(); };
        DetachedFromVisualTree += (_, _) => { Unsubscribe(); _attached = false; };
        _waveIcon ??= new Glyph(GlyphKind.WaveDraw, 15);
        _menuButton ??= new Button();
    }

    private void Subscribe() { if (_state is null) return; _state.Changed += Sync; _state.Waveform.Changed += Sync; }
    private void Unsubscribe() { if (_state is null || !_attached) return; _state.Changed -= Sync; _state.Waveform.Changed -= Sync; }
    private void Sync()
    {
        foreach (var (tool, button) in _buttons) button.IsChecked = (_state?.Tool ?? ArrangementTool.Select) == tool;
        _waveIcon.Kind = IconFor(_state?.Waveform.Shape ?? AutomationWaveform.Sine);
    }
    internal static GlyphKind IconFor(AutomationWaveform shape) => shape switch
    {
        AutomationWaveform.Triangle => GlyphKind.WaveTriangle,
        AutomationWaveform.Sawtooth => GlyphKind.WaveSawtooth,
        AutomationWaveform.Square => GlyphKind.WaveSquare,
        AutomationWaveform.Freehand => GlyphKind.WaveFreehand,
        _ => GlyphKind.WaveDraw,
    };

    private void ShowWaveformMenu()
    {
        if (_state is not { } state) return;
        var settings = state.Waveform;
        var menu = new MenuFlyout();
        foreach (var (shape, name) in new[] { (AutomationWaveform.Sine, Loc.T("Sine")),
            (AutomationWaveform.Triangle, Loc.T("Triangle")), (AutomationWaveform.Sawtooth, Loc.T("Sawtooth")),
            (AutomationWaveform.Square, Loc.T("Square")), (AutomationWaveform.Freehand, Loc.T("Freehand")) })
        {
            var item = new MenuItem { Header = name, Icon = MenuKit.Icon(IconFor(shape)),
                ToggleType = MenuItemToggleType.Radio, IsChecked = settings.Shape == shape };
            item.Click += (_, _) => { settings.Shape = shape; state.Choose(ArrangementTool.WaveformDraw); };
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var amplitude = new MenuItem { Header = Loc.T("Amplitude") };
        foreach (int percent in new[] { 25, 50, 75, 100 })
        {
            double value = percent / 100.0;
            var item = new MenuItem { Header = NotaNum.Pct(value), ToggleType = MenuItemToggleType.Radio, IsChecked = settings.Amplitude == value };
            item.Click += (_, _) => settings.Amplitude = value; amplitude.Items.Add(item);
        }
        menu.Items.Add(amplitude);
        var cycle = new MenuItem { Header = Loc.T("Cycle length") };
        foreach (var (bars, label) in new[] { (0.25, Loc.T("1/4 bar")), (0.5, Loc.T("1/2 bar")), (1.0, Loc.T("1 bar")), (2.0, Loc.T("2 bars")), (4.0, Loc.T("4 bars")) })
        {
            var item = new MenuItem { Header = label, ToggleType = MenuItemToggleType.Radio, IsChecked = settings.CycleBars == bars };
            item.Click += (_, _) => settings.CycleBars = bars; cycle.Items.Add(item);
        }
        menu.Items.Add(cycle);
        var phase = new MenuItem { Header = Loc.T("Phase") };
        foreach (int degrees in new[] { 0, 90, 180, 270 })
        {
            double value = degrees / 360.0;
            var item = new MenuItem { Header = NotaNum.Str(degrees, "0") + "°", ToggleType = MenuItemToggleType.Radio, IsChecked = settings.Phase == value };
            item.Click += (_, _) => settings.Phase = value; phase.Items.Add(item);
        }
        menu.Items.Add(phase); menu.ShowAt(_menuButton);
    }
}
