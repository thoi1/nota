// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

#include "media/SuperFlux.h"
#include "odf-golden.h"
#include <algorithm>
#include <cmath>
#include <cstdio>

int main() {
    auto source = std::make_shared<nota::SampleBuffer>();
    source->sourceSampleRate = 44100;
    source->frames = 8192;
    source->channels = 1;
    source->samples.resize(8192);
    for (int i = 1000; i < 8192; ++i) {
        const int period = i < 4096 ? 16 : 17;
        const int phase = (i - 1000) % period;
        const double value = (phase < period / 2 ? phase : period - phase) / 16.0 - .25;
        const double envelope = std::min(1.0, (i - 1000) / 32.0) * std::min(1.0, (8192 - i) / 2048.0);
        source->samples[i] = static_cast<float>(value * envelope);
    }
    nota::SuperFlux analysis(source, {.5, .03, 0, 0});
    double maxError = 0;
    for (double expected : kSuperFluxGolden) {
        if (analysis.step(1) < 0) return 1;
        maxError = std::max(maxError, std::abs(analysis.lastOdf() - expected));
    }
    std::printf("SuperFlux vs NumPy/SciPy reference: max ODF error %.8g (%zu frames)\n", maxError, std::size(kSuperFluxGolden));
    if (maxError > 0.0001) return 1;
    while (analysis.step(7) > 0) { }
    if (!analysis.complete() || analysis.progress() != 1) return 1;
    // Long sources must not enlarge the ODF/spectral working set. Input/output
    // storage and fixed-size FFT internals are deliberately outside this diagnostic.
    const size_t shortBytes = analysis.workingStorageBytes();
    auto longSource = std::make_shared<nota::SampleBuffer>();
    longSource->sourceSampleRate = 44100;
    longSource->frames = 44100 * 60;
    longSource->channels = 1;
    longSource->samples.resize(static_cast<size_t>(longSource->frames));
    nota::SuperFlux longAnalysis(longSource, {.5, .03, 0, 0});
    while (longAnalysis.step(100) > 0) { }
    if (!longAnalysis.complete() || !longAnalysis.results().empty() || shortBytes != longAnalysis.workingStorageBytes()) return 1;
    std::printf("SuperFlux scratch: %zu bytes for both 0.186s and 60s sources (excludes audio, output, fixed FFT internals)\n", shortBytes);
    nota::SuperFlux cancelled(source, {.5, .03, 1, 0});
    cancelled.cancel();
    if (cancelled.step(1) != -2 || cancelled.complete()) return 1;
    if (nota::SuperFlux::validSettings({.5, 0, 1, 0}) || nota::SuperFlux::validSettings({.5, .03, 1, 1})) return 1;
    std::printf("SuperFlux native reference and incremental finalisation PASS\n");
    return 0;
}
