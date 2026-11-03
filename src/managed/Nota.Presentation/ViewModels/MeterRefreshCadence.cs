// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System;
using Nota.Application;

namespace Nota.Presentation;

// Keep the phase across slightly late display callbacks; skip missed periods, never burst.
public sealed class MeterRefreshCadence
{
    private int _hz;
    private double _next;
    public void Reset() => _hz = 0;
    public bool IsDue(double seconds, int requestedHz)
    {
        int hz = MeterRefreshRate.Normalize(requestedHz);
        double period = 1.0 / hz;
        if (_hz != hz)
        {
            _hz = hz; _next = seconds + period;
            return true;
        }
        if (seconds + 1e-9 < _next) return false;
        _next += period * Math.Max(1, Math.Floor((seconds - _next + 1e-9) / period) + 1);
        return true;
    }
}
