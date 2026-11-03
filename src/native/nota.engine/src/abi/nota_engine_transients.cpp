// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

#include "nota_engine_internal.h"
#include "media/SuperFlux.h"
#include <algorithm>

namespace {
nota::SuperFlux* job(NotaTransientAnalysis* p) { return reinterpret_cast<nota::SuperFlux*>(p); }
const nota::SuperFlux* job(const NotaTransientAnalysis* p) { return reinterpret_cast<const nota::SuperFlux*>(p); }
}

extern "C" {
NotaTransientAnalysis* nota_transient_analysis_create(const NotaEngine* e, int64_t id, const NotaTransientSettings* s) {
    if (!e || !s || !nota::SuperFlux::validSettings(*s)) return nullptr;
    try {
        auto source = CENG(e)->captureAnalysisSource(id);
        return reinterpret_cast<NotaTransientAnalysis*>(new nota::SuperFlux(std::move(source), *s));
    } catch (...) { return nullptr; }
}
int32_t nota_transient_analysis_step(NotaTransientAnalysis* p, int32_t frames) {
    if (!p) return -1;
    try { return job(p)->step(frames); } catch (...) { return -1; }
}
void nota_transient_analysis_cancel(NotaTransientAnalysis* p) { if (p) job(p)->cancel(); }
double nota_transient_analysis_progress(const NotaTransientAnalysis* p) { return p ? job(p)->progress() : 0; }
double nota_transient_analysis_sample_rate(const NotaTransientAnalysis* p) { return p ? job(p)->sampleRate() : 0; }
int64_t nota_transient_analysis_count(const NotaTransientAnalysis* p) {
    return p && job(p)->complete() ? static_cast<int64_t>(job(p)->results().size()) : 0;
}
int32_t nota_transient_analysis_read(const NotaTransientAnalysis* p, int64_t offset, NotaTransient* out, int32_t cap) {
    if (!p || !out || cap <= 0 || offset < 0 || !job(p)->complete()) return 0;
    const auto& results = job(p)->results();
    if (static_cast<uint64_t>(offset) >= results.size()) return 0;
    const auto n = static_cast<int32_t>(std::min<int64_t>(cap, static_cast<int64_t>(results.size()) - offset));
    std::copy_n(results.data() + offset, n, out);
    return n;
}
void nota_transient_analysis_destroy(NotaTransientAnalysis* p) { delete job(p); }
} // extern "C"
