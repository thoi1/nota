// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

#include "TempoMap.h"
#include <algorithm>
#include <cmath>
#include <limits>
#include <stdexcept>
#include <unordered_set>
#include <unordered_map>

namespace nota {
namespace {
constexpr size_t maxKnots = 2000000;
double shape(double u, int curve) { return curve == 2 ? u * u * (3 - 2 * u) : u; }
// Smooth segments have no useful inexpensive elementary integral. Adaptive Simpson
// runs only while preparing the map; a hard depth limit rejects excessive resources.
template<class F> double integrate(const F& f, double a, double b, double fa, double fm, double fb,
    double whole, double eps, int depth) {
    double m = (a+b)*0.5, l = f((a+m)*0.5), r = f((m+b)*0.5);
    double left = (m-a)*(fa+4*l+fm)/6, right = (b-m)*(fm+4*r+fb)/6;
    double delta = left+right-whole;
    if (std::abs(delta) <= 15*eps) return left+right+delta/15;
    if (depth <= 0) throw std::invalid_argument("Tempo integration resource limit");
    return integrate(f,a,m,fa,l,fm,left,eps*0.5,depth-1)
         + integrate(f,m,b,fm,r,fb,right,eps*0.5,depth-1);
}
}
TempoMap::TempoMap(std::vector<TempoPoint> points, double minBpm, double maxBpm, std::vector<TempoAnchor> anchors)
    : points_(std::move(points)), anchors_(std::move(anchors)), min_(minBpm), max_(maxBpm) {
    if (!std::isfinite(min_) || !std::isfinite(max_) || min_ < 1 || max_ > 999 || min_ > max_
        || points_.empty() || points_.size() > 100000 || points_[0].tick != 0)
        throw std::invalid_argument("Invalid tempo map bounds or initial point");
    int64_t last = -1;
    for (const auto& p : points_) {
        if (p.tick <= last || p.tick > 960LL*100000000 || !std::isfinite(p.bpm)
            || p.bpm < min_ || p.bpm > max_ || p.curve < 0 || p.curve > 2 || p.anchorId < 0)
            throw std::invalid_argument("Invalid tempo point");
        last = p.tick;
    }
    if (anchors_.size() > 100000) throw std::invalid_argument("Too many tempo anchors");
    knots_.push_back({0,0});
    for (size_t i=0; i+1<points_.size(); ++i) {
        const auto& p = points_[i]; const auto& q = points_[i+1];
        const double origin = static_cast<double>(p.tick)/ticksPerBeat;
        const double len = static_cast<double>(q.tick-p.tick)/ticksPerBeat;
        const double delta = q.bpm-p.bpm;
        auto bpm = [&](double x) { return p.bpm + delta*shape(x/len,p.curve); };
        auto integral = [&](double a, double b) {
            if (p.curve == 0 || delta == 0) return (b-a)*60/p.bpm;
            if (p.curve == 1) {
                const double slope = delta/len;
                return 60/slope*std::log1p(slope*(b-a)/bpm(a));
            }
            auto f = [&](double x) { return 60/bpm(x); };
            double fa=f(a), fm=f((a+b)*0.5), fb=f(b);
            return integrate(f,a,b,fa,fm,fb,(b-a)*(fa+4*fm+fb)/6,1e-12/points_.size(),24);
        };
        // The interpolation error is bounded by max|T''| h²/8. Monotone BPM
        // makes the endpoint minimum safe; smoothstep's derivative has its max at 1/2.
        auto subdivide = [&](auto&& self, double a, double b, int depth) -> void {
            double deriv = p.curve == 0 ? 0 : std::abs(delta)/len;
            if (p.curve == 2) {
                double u = std::clamp(0.5,a/len,b/len);
                deriv *= 6*u*(1-u);
            }
            const double low = std::min(bpm(a),bpm(b));
            const double bound = 60*deriv/(low*low)*(b-a)*(b-a)/8;
            if (bound > timeError) {
                if (depth <= 0) throw std::invalid_argument("Tempo table resource limit");
                double m=(a+b)*0.5; self(self,a,m,depth-1); self(self,m,b,depth-1); return;
            }
            if (knots_.size() >= maxKnots) throw std::invalid_argument("Tempo table resource limit");
            knots_.push_back({origin+b,knots_.back().seconds+integral(a,b)});
        };
        subdivide(subdivide,0,len,32);
    }
    std::unordered_set<int32_t> anchorIds;
    for (const auto& a : anchors_) {
        if (a.anchorId < 0 || (a.anchorId > 0 && !anchorIds.insert(a.anchorId).second)
            || a.tick < 0 || !std::isfinite(a.seconds) || a.seconds < 0 || !std::isfinite(a.sourceFrame)
            || a.sourceFrame < 0 || a.referenceId<0 || a.referenceId>1000000000000LL || (a.locked != 0 && a.locked != 1))
            throw std::invalid_argument("Invalid tempo anchor");
        if (a.locked && std::abs(secondsAtBeat(static_cast<double>(a.tick)/ticksPerBeat)-a.seconds) > 1/maxRate)
            throw std::invalid_argument("Tempo edit conflicts with a protected absolute-time anchor");
    }
    std::unordered_map<int32_t,const TempoAnchor*> links;
    for (const auto& a : anchors_) if (a.anchorId>0 && a.locked) links.emplace(a.anchorId,&a);
    for (auto& p : points_) if (p.anchorId>0) {
        auto it=links.find(p.anchorId);
        if (it==links.end()) p.anchorId=0;
        else if (it->second->tick!=p.tick) throw std::invalid_argument("Linked tempo point conflicts with its fixed anchor");
    }
}
double TempoMap::secondsAtBeat(double beat) const noexcept {
    if (beat <= 0) return beat*60/points_.front().bpm;
    auto it=std::upper_bound(knots_.begin(),knots_.end(),beat,[](double b,const Knot& k){return b<k.beat;});
    if (it == knots_.end()) return knots_.back().seconds+(beat-knots_.back().beat)*60/points_.back().bpm;
    const auto& a=*(it-1); return a.seconds+(beat-a.beat)*(it->seconds-a.seconds)/(it->beat-a.beat);
}
double TempoMap::beatAtSeconds(double seconds) const noexcept {
    if (seconds <= 0) return seconds*points_.front().bpm/60;
    auto it=std::upper_bound(knots_.begin(),knots_.end(),seconds,[](double s,const Knot& k){return s<k.seconds;});
    if (it == knots_.end()) return knots_.back().beat+(seconds-knots_.back().seconds)*points_.back().bpm/60;
    const auto& a=*(it-1); return a.beat+(seconds-a.seconds)*(it->beat-a.beat)/(it->seconds-a.seconds);
}
double TempoMap::bpmAtBeat(double beat) const noexcept {
    auto it=std::upper_bound(points_.begin(),points_.end(),beat,[](double b,const TempoPoint& p){return b<static_cast<double>(p.tick)/ticksPerBeat;});
    if (it == points_.begin()) return it->bpm;
    const auto& a=*(it-1);
    if (it == points_.end() || a.curve == 0) return a.bpm;
    double u=(beat*ticksPerBeat-a.tick)/static_cast<double>(it->tick-a.tick);
    return a.bpm+(it->bpm-a.bpm)*shape(u,a.curve);
}
double TempoMap::secondsPerBeat(double beat) const noexcept {
    if (beat < 0) return 60/points_.front().bpm;
    auto it=std::upper_bound(knots_.begin(),knots_.end(),beat,[](double b,const Knot& k){return b<k.beat;});
    if (it == knots_.end()) return 60/points_.back().bpm;
    const auto& a=*(it-1); return (it->seconds-a.seconds)/(it->beat-a.beat);
}
double TempoMap::nextKnotSeconds(double seconds) const noexcept {
    auto it=std::upper_bound(knots_.begin(),knots_.end(),seconds+1e-12,[](double s,const Knot& k){return s<k.seconds;});
    return it == knots_.end() ? std::numeric_limits<double>::infinity() : it->seconds;
}
} // namespace nota
