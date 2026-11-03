// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
// Prepared on the authoring thread. Playback only searches/interpolates immutable knots.

#pragma once
#include <cstdint>
#include <vector>

namespace nota {
struct TempoPoint {
    int64_t tick = 0;
    double bpm = 120.0;
    int32_t curve = 0; // outgoing: Step, Linear, Smooth (BPM smoothstep in beat space)
    int32_t anchorId = 0; // explicit link, zero is unlinked/legacy
    bool operator==(const TempoPoint&) const = default;
};
struct TempoAnchor {
    int64_t tick = 0;
    double seconds = 0;
    int32_t locked = 0;
    int32_t trackId = -1, clipIndex = -1;
    double sourceFrame = 0;
    int64_t referenceId = 0;
    int32_t anchorId = 0; // stable committed anchor identity
    bool operator==(const TempoAnchor&) const = default;
};
class TempoMap {
  public:
    static constexpr int64_t ticksPerBeat = 960;
    static constexpr double maxRate = 192000.0;
    static constexpr double timeError = 0.0625 / maxRate;
    struct Knot { double beat, seconds; };
    explicit TempoMap(std::vector<TempoPoint> points, double minBpm = 1, double maxBpm = 999,
        std::vector<TempoAnchor> anchors = {});
    double secondsAtBeat(double beat) const noexcept;
    double beatAtSeconds(double seconds) const noexcept;
    double bpmAtBeat(double beat) const noexcept;
    double secondsPerBeat(double beat) const noexcept;
    double nextKnotSeconds(double seconds) const noexcept;
    const std::vector<TempoPoint>& points() const { return points_; }
    const std::vector<TempoAnchor>& anchors() const { return anchors_; }
    const std::vector<Knot>& knots() const { return knots_; }
    double minBpm() const { return min_; }
    double maxBpm() const { return max_; }
    bool variable() const { return points_.size() > 1; }
  private:
    friend class Engine; // may normalize reference metadata only before publication
    std::vector<TempoPoint> points_;
    std::vector<TempoAnchor> anchors_;
    std::vector<Knot> knots_;
    double min_, max_;
};
} // namespace nota
