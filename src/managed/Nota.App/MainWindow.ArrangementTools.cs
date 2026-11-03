// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Nota.Application.Editing;

namespace Nota.App;

public partial class MainWindow
{
    private readonly EditorToolState _pianoToolState = new();
    private (Control View, EditorToolState State)? _toolEditorContext;
    private (Control View, EditorToolState State)? _toolKeyboardContext;
    private EditorToolState? _toolCommandTarget;

    private void InitEditorTools()
    {
        ArrangementTools.State = Timeline.ToolState;
        TrackEditorToolContext(this);
    }

    internal void TrackEditorToolContext(Window window)
    {
        window.AddHandler(PointerPressedEvent, (_, e) =>
        {
            _toolKeyboardContext = FindToolEditor(e.Source as Visual);
            if (_toolKeyboardContext is not null) _toolEditorContext = _toolKeyboardContext;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        // A focused child can consume key-up. Always release the latch in the tunnel phase.
        window.AddHandler(KeyUpEvent, (_, e) => _heldKeys.Remove(e.Key), RoutingStrategies.Tunnel, handledEventsToo: true);
        window.Deactivated += (_, _) =>
        {
            _heldKeys.RemoveWhere(key => key >= Key.D1 && key <= Key.D5);
            _editorRoll?.CancelToolGesture();
        };
    }

    private static (Control View, EditorToolState State)? FindToolEditor(Visual? source)
    {
        if (source?.FindAncestorOfType<EditingToolBar>(includeSelf: true) is { State: { } tools } bar) return (bar, tools);
        if (source?.FindAncestorOfType<PianoRollView>(includeSelf: true) is { } roll) return (roll, roll.ToolState);
        if (source?.FindAncestorOfType<ArrangementView>(includeSelf: true) is { } arrangement) return (arrangement, arrangement.ToolState);
        return null;
    }

    private static bool IsToolTextEntry(Visual? source)
        => source?.FindAncestorOfType<TextBox>(includeSelf: true) is not null
            || source?.FindAncestorOfType<NumericUpDown>(includeSelf: true) is not null;

    private bool TryEditorToolKey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key < Key.D1 || e.Key > Key.D5) return false;
        var source = e.Source as Visual;
        var top = source is null ? null : TopLevel.GetTopLevel(source);
        if (top is not Window window || (window != this && window is not DetailWindow) || !window.IsEnabled) return false;
        // Observe releases even for typing, so a held digit cannot turn into a fresh
        // tool press merely because focus moved from a field into an editor.
        bool fresh = _heldKeys.Add(e.Key);
        if (IsToolTextEntry(source) || IsToolTextEntry(window.FocusManager?.GetFocusedElement() as Visual)) return false;
        var target = FindToolEditor(source) ?? (source is Window ? _toolKeyboardContext : null);
        if (target is not { } context || !context.View.IsEffectivelyVisible) return false;
        _toolEditorContext = context;
        if (fresh)
        {
            _toolCommandTarget = context.State;
            try { _commands.Run(EditorToolState.CommandForNumber((int)e.Key - (int)Key.D1 + 1)!); }
            finally { _toolCommandTarget = null; }
        }
        e.Handled = true;
        return true;
    }

    private void ApplyEditorTool(ArrangementTool tool)
    {
        var state = _toolCommandTarget ?? (_toolEditorContext is { } context && context.View.IsEffectivelyVisible ? context.State : null);
        state?.Choose(tool, cycleActiveWaveform: true);
    }
}
