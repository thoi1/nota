// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using Avalonia;
using Avalonia.Media;
using Nota.Application.Editing;
using Nota.Application.Localization;

namespace Nota.App;

/// <summary>Shared Warp feedback only; no tempo-map engine or fake warp anchors.</summary>
internal static class WarpRuler
{
    internal static void Background(DrawingContext ctx, double width, double height)
        => ctx.FillRectangle(NotaPalette.Wash(NotaPalette.Record, 24), new Rect(0, 0, width, height));

    internal static void Overlay(DrawingContext ctx, double width, double height, double hoverX,
        double pixelsPerBeat, double scrollBeats, double gridStep, int beatsPerBar)
    {
        if (double.IsFinite(hoverX) && pixelsPerBeat > 0)
        {
            double beat = SnapGrid.SnapBeat(scrollBeats + hoverX / pixelsPerBeat, Math.Max(1, gridStep), beatsPerBar);
            double x = (beat - scrollBeats) * pixelsPerBeat + 0.5;
            if (x >= 0 && x <= width) ctx.DrawLine(new Pen(NotaPalette.Record, 1), new Point(x, 0), new Point(x, height));
        }
        var label = new FormattedText(Loc.T("WARP"), NotaNum.Culture, FlowDirection.LeftToRight,
            NotaFonts.Mono, NotaType.Axis, NotaPalette.TextPrimary);
        double left = Math.Max(0, width - label.Width - 8);
        ctx.FillRectangle(NotaPalette.SurfaceRaised, new Rect(left, 1, label.Width + 8, Math.Max(1, height - 2)));
        ctx.DrawText(label, new Point(left + 4, (height - label.Height) / 2));
    }
}
