// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Collections.Generic;

namespace Nota.Application;

public static class MeterRefreshRate
{
    public const int Default = 30;
    public static IReadOnlyList<int> Presets { get; } = new[] { 30, 60 };
    public static int Normalize(int hz) => hz == 60 ? 60 : Default;
}
