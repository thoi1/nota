// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

#pragma once

#include "SampleBuffer.h"
#include "nota/nota_engine.h"
#include <atomic>
#include <memory>
#include <vector>

namespace nota {

// Non-RT, single worker. Source captured on authoring thread; processing never accesses
// engine/graph state. DSP state is prepared on first step, not during capture.
class SuperFlux {
  public:
    SuperFlux(std::shared_ptr<const SampleBuffer> source, NotaTransientSettings settings);
    ~SuperFlux();
    static bool validSettings(const NotaTransientSettings& settings);
    int step(int maxFrames);
    void cancel() { cancelled_.store(true, std::memory_order_relaxed); }
    double progress() const;
    double sampleRate() const { return source_->sourceSampleRate; }
    bool complete() const { return status_ == 0; }
    const std::vector<NotaTransient>& results() const { return results_; }
    // Last spectral novelty, for deterministic comparison against the original algorithm.
    double lastOdf() const;
    size_t workingStorageBytes() const; // diagnostic scratch only, excludes source/results and FFT internals

  private:
    struct State;
    std::shared_ptr<const SampleBuffer> source_;
    NotaTransientSettings settings_;
    std::unique_ptr<State> state_;
    std::atomic<bool> cancelled_{false};
    std::vector<NotaTransient> results_;
    int status_ = 1;
    void pick(int64_t frame);
    void refine(NotaTransient& candidate) const;
};

} // namespace nota
