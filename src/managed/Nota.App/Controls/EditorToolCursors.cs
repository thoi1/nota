// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Nota.Application.Editing;

namespace Nota.App;

internal static class EditorToolCursors
{
    private static readonly Cursor Draw = new(StandardCursorType.Cross);
    private static Cursor? _eraser;
    private static object? _theme;
    internal static Cursor For(ArrangementTool tool)
    {
        if (tool is ArrangementTool.Pencil or ArrangementTool.WaveformDraw) return Draw;
        if (tool != ArrangementTool.Eraser) return Cursor.Default;
        if (_eraser is null || !ReferenceEquals(_theme, NotaPalette.Theme))
        {
            var glyph = new Glyph(GlyphKind.Eraser, 20) { Foreground = NotaPalette.TextPrimary };
            glyph.Measure(new Size(24, 24)); glyph.Arrange(new Rect(2, 2, 20, 20));
            using var bitmap = new RenderTargetBitmap(new PixelSize(24, 24), new Vector(96, 96)); bitmap.Render(glyph);
            _eraser = new Cursor(bitmap, new PixelPoint(3, 20)); _theme = NotaPalette.Theme;
        }
        return _eraser;
    }
}
