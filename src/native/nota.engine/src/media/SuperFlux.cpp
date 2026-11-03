// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
//
// SuperFlux (Böck & Widmer, DAFx 2013, equations 2/4/5/6). Adapted from the
// BSD-2-Clause reference by Sebastian Böck, Copyright (c) 2012 - 2014.
// Full upstream copyright, conditions and disclaimer: LICENSES/SuperFlux.txt.
// Streaming rings replace the reference's duration-sized spectrogram; optional
// attack refinement is a separate Nota stage, never part of the spectral ODF.

#include "SuperFlux.h"
#include <signalsmith-linear/fft.h>
#include <algorithm>
#include <array>
#include <cmath>
#include <complex>
#include <stdexcept>

namespace nota {
namespace {
constexpr double kPi = 3.14159265358979323846;
constexpr double kFps = 200;
constexpr int kPreAvg = 20, kPostAvg = 14, kPreMax = 6, kPostMax = 6;
constexpr int kOdfRing = 64;

int fftSize(double sr) {
    int n = 256;
    while (n < 8192 && n * std::sqrt(2.0) < sr * 2048 / 44100) n *= 2;
    return n;
}

// Invalid samples contribute silence; clipped finite recordings remain analysable.
float finiteSample(float v) { return std::isfinite(v) ? std::clamp(v, -16.0f, 16.0f) : 0.0f; }

double frameEnergy(const SampleBuffer& s, int64_t start, int64_t end) {
    double sum = 0;
    for (int64_t f = start; f < end; ++f)
        for (int c = 0; c < s.channels; ++c) {
            const double x = finiteSample(s.at(f, c));
            sum += x * x;
        }
    return sum / std::max<int64_t>(1, (end - start) * s.channels);
}
} // namespace

struct SuperFlux::State {
    explicit State(const SampleBuffer& src)
        : sr(src.sourceSampleRate), n(fftSize(sr)), bins(n / 2), fft(static_cast<size_t>(n)),
          time(n), freq(bins), magnitude(bins), window(n) {
        for (int i = 0; i < n; ++i) window[i] = static_cast<float>(.5 - .5 * std::cos(2 * kPi * i / (n - 1)));
        int first = 0;
        while (window[first] <= .5f) ++first;
        lag = std::max(1, static_cast<int>(std::lround((n / 2.0 - first) / (sr / kFps))));

        // Reference frequencies are anchored at A4 and extend one corner beyond
        // each bound, then rounded to unique FFT bins before forming triangles.
        std::vector<int> corners;
        const double factor = std::pow(2.0, 1.0 / 24);
        const double high = std::min(16000.0, sr / 2);
        auto add = [&](double hz) {
            int bin = static_cast<int>(std::nearbyint(hz * n / sr));
            if (bin >= 0 && bin < bins) corners.push_back(bin);
        };
        double hz = 440;
        add(hz);
        while (hz <= high) { hz *= factor; add(hz); }
        hz = 440;
        while (hz >= 27.5) { hz /= factor; add(hz); }
        std::sort(corners.begin(), corners.end());
        corners.erase(std::unique(corners.begin(), corners.end()), corners.end());
        for (size_t b = 0; b + 2 < corners.size(); ++b)
            bands.push_back({corners[b], corners[b + 1], corners[b + 2]});
        if (bands.size() < 3) throw std::invalid_argument("sample rate cannot support filterbank");
        history.resize(static_cast<size_t>(lag + 1) * bands.size());
        current.resize(bands.size());
        frames = static_cast<int64_t>(std::ceil(src.frames * kFps / sr));
    }

    struct Band { int lo, mid, hi; };
    double sr;
    int n, bins, lag = 1;
    signalsmith::linear::RealFFT<float> fft;
    std::vector<float> time;
    std::vector<std::complex<float>> freq;
    std::vector<double> magnitude;
    std::vector<float> window;
    std::vector<Band> bands;
    std::vector<double> history, current;
    std::array<double, kOdfRing> odf{};
    int64_t frames = 0, next = 0;
    double last = 0;

    double at(int64_t f) const { return f < 0 || f >= frames ? 0 : odf[static_cast<size_t>(f % kOdfRing)]; }

    void spectralFrame(const SampleBuffer& src, int64_t f) {
        std::fill(magnitude.begin(), magnitude.end(), 0);
        // Pool magnitudes instead of waveform means: stereo polarity cannot cancel an attack.
        const int64_t start = static_cast<int64_t>(f * sr / kFps - n / 2);
        for (int ch = 0; ch < src.channels; ++ch) {
            for (int i = 0; i < n; ++i) time[i] = finiteSample(src.at(start + i, ch)) * window[i];
            fft.fft(time.data(), freq.data());
            for (int k = 0; k < bins; ++k) magnitude[k] += std::abs(freq[k]) / src.channels;
        }
        for (size_t b = 0; b < bands.size(); ++b) {
            const auto& band = bands[b];
            double v = 0;
            for (int k = band.lo; k < band.mid; ++k)
                v += magnitude[k] * (k - band.lo) / (band.mid - band.lo);
            for (int k = band.mid; k < band.hi; ++k)
                v += magnitude[k] * (band.hi - k) / (band.hi - band.mid);
            current[b] = std::log10(1 + v);
        }
        double flux = 0;
        if (f >= lag) {
            const size_t old = static_cast<size_t>((f - lag) % (lag + 1)) * bands.size();
            for (size_t b = 0; b < bands.size(); ++b) {
                const double maximum = std::max({history[old + (b > 0 ? b - 1 : 0)], history[old + b],
                    history[old + std::min(b + 1, bands.size() - 1)]});
                flux += std::max(0.0, current[b] - maximum);
            }
        }
        const size_t dest = static_cast<size_t>(f % (lag + 1)) * bands.size();
        std::copy(current.begin(), current.end(), history.begin() + dest);
        last = flux;
        odf[static_cast<size_t>(f % kOdfRing)] = flux;
    }
};

bool SuperFlux::validSettings(const NotaTransientSettings& s) {
    return std::isfinite(s.sensitivity) && s.sensitivity >= 0 && s.sensitivity <= 1
        && std::isfinite(s.minimum_spacing_seconds) && s.minimum_spacing_seconds >= .005
        && s.minimum_spacing_seconds <= 1 && (s.refine_attacks == 0 || s.refine_attacks == 1) && s.reserved == 0;
}

SuperFlux::SuperFlux(std::shared_ptr<const SampleBuffer> source, NotaTransientSettings settings)
    : source_(std::move(source)), settings_(settings) {
    if (!source_ || source_->channels < 1 || source_->channels > 2 || source_->frames < 0
        || !std::isfinite(source_->sourceSampleRate) || source_->sourceSampleRate < 8000
        || source_->sourceSampleRate > 192000 || !validSettings(settings_)
        || static_cast<uint64_t>(source_->frames) > source_->samples.size() / source_->channels)
        throw std::invalid_argument("invalid transient source or settings");
}
SuperFlux::~SuperFlux() = default;

int SuperFlux::step(int maxFrames) {
    if (maxFrames <= 0) return -1;
    if (status_ != 1) return status_;
    if (cancelled_.load(std::memory_order_relaxed)) return status_ = -2;
    if (!state_) state_ = std::make_unique<State>(*source_);
    auto& s = *state_;
    for (int i = 0; i < maxFrames && s.next < s.frames + kPostAvg; ++i, ++s.next) {
        if (cancelled_.load(std::memory_order_relaxed)) return status_ = -2;
        if (s.next < s.frames) s.spectralFrame(*source_, s.next);
        if (s.next >= kPostAvg) pick(s.next - kPostAvg);
    }
    if (s.next >= s.frames + kPostAvg) status_ = 0;
    return status_;
}

double SuperFlux::progress() const {
    if (status_ == 0) return 1;
    return state_ ? static_cast<double>(state_->next) / (state_->frames + kPostAvg) : 0;
}
double SuperFlux::lastOdf() const { return state_ ? state_->last : 0; }
size_t SuperFlux::workingStorageBytes() const {
    if (!state_) return 0;
    const auto& s = *state_;
    return sizeof(State) + (s.time.capacity() + s.window.capacity()) * sizeof(float)
        + s.freq.capacity() * sizeof(std::complex<float>) + s.magnitude.capacity() * sizeof(double)
        + s.bands.capacity() * sizeof(State::Band) + (s.history.capacity() + s.current.capacity()) * sizeof(double);
}

void SuperFlux::pick(int64_t f) {
    auto& s = *state_;
    if (f >= s.frames) return;
    const double v = s.at(f);
    if (v <= 0) return;
    for (int64_t k = f - kPreMax; k <= f + kPostMax; ++k) {
        if (s.at(k) > v || (k < f && s.at(k) == v)) return; // stable first peak on a plateau
    }
    double mean = 0;
    for (int64_t k = f - kPreAvg; k <= f + kPostAvg; ++k) mean += s.at(k);
    mean /= kPreAvg + kPostAvg + 1;
    const double margin = 2.05 - 1.9 * settings_.sensitivity; // default .5 -> reference delta 1.1
    if (v < mean + margin) return;
    NotaTransient t{};
    t.sample_position = std::min(source_->frames - 1, static_cast<int64_t>(std::llround(f * s.sr / kFps)));
    t.strength = static_cast<float>(v);
    t.confidence = static_cast<float>(std::clamp((v - mean) / (v + margin), 0.0, 1.0));
    // The hop is not an accuracy guarantee for soft/polyphonic attacks; the
    // centred spectral window can place them early. Retain its half-width.
    t.uncertainty_samples = std::max<int64_t>(s.n / 2, static_cast<int64_t>(std::ceil(s.sr / kFps)));
    t.analysis_version = 1;
    if (settings_.refine_attacks) refine(t);
    t.seconds = t.sample_position / s.sr;
    const int64_t gap = static_cast<int64_t>(std::ceil(settings_.minimum_spacing_seconds * s.sr));
    if (!results_.empty() && t.sample_position - results_.back().sample_position < gap) return;
    results_.push_back(t);
}

void SuperFlux::refine(NotaTransient& t) const {
    const auto& src = *source_;
    const double sr = src.sourceSampleRate;
    // Only move to a well-supported rise out of a quiet local floor. Oscillations in
    // sustained polyphony or soft ambiguous ramps retain spectral timing/uncertainty.
    const int64_t radius = static_cast<int64_t>(std::ceil(sr * .020));
    const int64_t block = std::max<int64_t>(1, static_cast<int64_t>(sr * .001));
    const int64_t lo = std::max<int64_t>(0, t.sample_position - radius);
    const int64_t hi = std::min(src.frames, t.sample_position + radius + 1);
    const double floor = frameEnergy(src, std::max<int64_t>(0, lo - block * 5), lo);
    double peak = 0;
    int64_t peakAt = lo;
    for (int64_t f = lo; f < hi; f += block) {
        double e = frameEnergy(src, f, std::min(hi, f + block));
        if (e > peak) { peak = e; peakAt = f; }
    }
    if (peak < 1e-10 || peak < std::max(1e-12, floor) * 16) return;
    const double threshold = std::max(floor * 4, peak * .04);
    int64_t rise = lo;
    for (; rise < peakAt; rise += block)
        if (frameEnergy(src, rise, std::min(hi, rise + block)) >= threshold) break;
    // Refine only when the envelope reaches its crest promptly; slow rises have
    // no defensible single-sample onset. Report that uncertainty rather than invent it.
    if (peakAt - rise > sr * .008) return;
    const double sampleThreshold = std::max(floor * 9, peak * .01);
    for (int64_t f = std::max(lo, rise - block); f < std::min(hi, rise + block); ++f) {
        if (frameEnergy(src, f, f + 1) < sampleThreshold) continue;
        t.sample_position = f;
        t.uncertainty_samples = block;
        t.refined = 1;
        return;
    }
}

} // namespace nota
