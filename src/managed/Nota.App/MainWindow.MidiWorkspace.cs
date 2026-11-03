// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using Avalonia.Controls;

namespace Nota.App;

public partial class MainWindow
{
    // A workspace is a temporary hosting/layout change. In particular, a detached
    // editor keeps its window and devices alive while its roll uses the main window.
    private sealed record MidiWorkspace(Control? Content, bool Visible, bool Splitter,
        double Wanted, double Default, bool Devices, bool Pattern, bool Clip, DetailWindow? Window);
    private MidiWorkspace? _midiWorkspace;

    private void BeginMidiWorkspace()
    {
        if (_midiWorkspace is not null || _detailMaximized) return;
        _midiWorkspace = new MidiWorkspace(DetailBody.Content as Control, DetailPanel.IsVisible,
            DetailSplitter.IsVisible, _detailWanted, _detailDefault,
            DetailDevicesBtn.IsChecked == true, DetailPatternBtn.IsChecked == true,
            DetailClipBtn.IsChecked == true, _detailWindow);
        if (_detailWindow is { } window)
        {
            window.Hide();
            _detailWindow = null;
        }
    }

    private void RestoreMidiWorkspace()
    {
        if (_midiWorkspace is not { } previous) return;
        _midiWorkspace = null; // visibility callbacks must not restore a second time
        _detailWanted = previous.Wanted;
        _detailDefault = previous.Default;
        if (previous.Window is { } window)
        {
            SetHost(window.ClipHost, DetailBody.Content as Control);
            _detailWindow = window;
            SetHost(DetailBody, previous.Content);
            window.Show(this);
        }
        else SetHost(DetailBody, previous.Content is ClipEditorView ? _clipEditor : previous.Content);
        DetailDevicesBtn.IsChecked = previous.Devices;
        DetailPatternBtn.IsChecked = previous.Pattern;
        DetailClipBtn.IsChecked = previous.Clip;
        DetailPanel.IsVisible = previous.Visible;
        _detailRow.MinHeight = previous.Visible ? 120 : 0;
        _detailRow.Height = new GridLength(previous.Visible ? previous.Wanted : 0);
        DetailSplitter.IsVisible = previous.Visible && previous.Splitter;
        _detailSplitterWasVisible = previous.Splitter;
        DetailMaximizeBtn.IsVisible = previous.Visible && previous.Content is ClipEditorView or AudioClipEditorView or DrumPatternView;
        FitBottomPanels();
    }
}
