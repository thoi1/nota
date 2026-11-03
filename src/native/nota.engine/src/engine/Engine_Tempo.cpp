// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

#include "Engine.h"
#include <cmath>

namespace nota {
int64_t Engine::tempoRevision() const {
    return tempoRevision_.load(std::memory_order_relaxed);
}
bool Engine::setTempoMap(std::vector<TempoPoint> points, double minBpm, double maxBpm,
    std::vector<TempoAnchor> anchors, bool checkpoint) {
    tempoError_.clear();
    try {
        std::unordered_map<int32_t,int64_t> linkedTicks;
        for (const auto& a : anchors) if (a.anchorId>0 && a.locked) linkedTicks.emplace(a.anchorId,a.tick);
        for (auto& p : points) if (p.anchorId>0) {
            auto it=linkedTicks.find(p.anchorId);
            if (it==linkedTicks.end()) p.anchorId=0; // an explicit unlink/removal
            else if (it->second!=p.tick) throw std::invalid_argument("Linked tempo point must retain its fixed anchor mapping");
        }
        int32_t highestAnchorId=nextGridAnchorId_;
        for (const auto& a : anchors) highestAnchorId=std::max(highestAnchorId,a.anchorId);
        auto map = std::make_shared<TempoMap>(std::move(points),minBpm,maxBpm,std::move(anchors));
        for (auto& a : map->anchors_) if (!a.locked && a.trackId >= 0)
            a.tick=std::llround(map->beatAtSeconds(a.seconds)*TempoMap::ticksPerBeat);
        if (!authoring_) return false;
        if (authoring_->tempoMap && map->points() == authoring_->tempoMap->points()
            && map->anchors() == authoring_->tempoMap->anchors()
            && minBpm == authoring_->tempoMap->minBpm() && maxBpm == authoring_->tempoMap->maxBpm()) return true;
        auto g = std::make_shared<Graph>();
        inheritGraphLevel(*g,*authoring_); g->tracks=authoring_->tracks;
        refreshTempoReferences(*g,*map); g->tempoMap=std::move(map);
        if (checkpoint) publish(std::move(g)); else publishRaw(std::move(g));
        nextGridAnchorId_=highestAnchorId;
        placeTakesForTempo();
        tempoWarpBuildPending_=true;
        if (wj_) wj_->cancel.store(true,std::memory_order_relaxed);
        return true;
    } catch (const std::exception& ex) {
        tempoError_=ex.what();
        return false;
    }
}
std::vector<TempoPoint> Engine::tempoPoints() const {
    auto map=tempoQueryMap_.load(std::memory_order_acquire);
    return map ? map->points() : std::vector<TempoPoint>{{0,tempoQueryBpm_.load(std::memory_order_relaxed),0}};
}
std::vector<TempoAnchor> Engine::tempoAnchors() const {
    auto map=tempoQueryMap_.load(std::memory_order_acquire);
    auto anchors=map ? map->anchors() : std::vector<TempoAnchor>{};
    if (authoring_) {
        std::unordered_map<int64_t,std::pair<int32_t,int32_t>> references;
        for (const auto& t : authoring_->tracks) for (size_t i=0;i<t->clips.size();++i)
            if (t->clips[i].gridFollowsAudio && t->clips[i].tempoReferenceId>0)
                references.try_emplace(t->clips[i].tempoReferenceId,t->id(),static_cast<int32_t>(i));
        for (auto& a : anchors) if (a.referenceId>0) {
            auto it=references.find(a.referenceId);
            if (it!=references.end()) { a.trackId=it->second.first; a.clipIndex=it->second.second; }
            else { a.trackId=-1; a.clipIndex=-1; a.referenceId=0; a.sourceFrame=0; }
        }
    }
    return anchors;
}
double Engine::tempoMinBpm() const { auto map=tempoQueryMap_.load(); return map ? map->minBpm() : std::min(20.0,tempoQueryBpm_.load()); }
double Engine::tempoMaxBpm() const { auto map=tempoQueryMap_.load(); return map ? map->maxBpm() : std::max(300.0,tempoQueryBpm_.load()); }
double Engine::secondsAtBeat(double beat) const {
    auto map=tempoQueryMap_.load(std::memory_order_acquire);
    return map ? map->secondsAtBeat(beat) : beat*60/tempoQueryBpm_.load(std::memory_order_relaxed);
}
double Engine::beatAtSeconds(double seconds) const {
    auto map=tempoQueryMap_.load(std::memory_order_acquire);
    return map ? map->beatAtSeconds(seconds) : seconds*tempoQueryBpm_.load(std::memory_order_relaxed)/60;
}
double Engine::tempoAtBeat(double beat) const {
    auto map=tempoQueryMap_.load(std::memory_order_acquire);
    return map ? map->bpmAtBeat(beat) : tempoQueryBpm_.load(std::memory_order_relaxed);
}

namespace {
double sourceAtBeat(const AudioClip& c,double beat) {
    auto it=std::upper_bound(c.warpMarkers.begin(),c.warpMarkers.end(),beat,[](double b,const WarpMarker& m){return b<m.beat;});
    if (it == c.warpMarkers.begin()) ++it;
    if (it == c.warpMarkers.end()) --it;
    const auto& a=*(it-1); const auto& b=*it;
    return a.srcFrame+(b.srcFrame-a.srcFrame)*(beat-a.beat)/(b.beat-a.beat);
}
}
bool Engine::clipGridFollowsAudio(int32_t trackId,int32_t clipIndex) const {
    auto t=findTrackAuthoring(trackId); const auto* c=t ? audioClipRef(*t,clipIndex) : nullptr;
    return c && c->gridFollowsAudio;
}
int64_t Engine::clipTempoReferenceId(int32_t trackId,int32_t clipIndex) const {
    auto t=findTrackAuthoring(trackId); auto* c=t ? audioClipRef(*t,clipIndex) : nullptr;
    return c ? c->tempoReferenceId : 0;
}
bool Engine::restoreClipTempoReference(int32_t trackId,int32_t clipIndex,bool grid,int64_t referenceId) {
    if (referenceId<0 || referenceId>1000000000000LL) return false;
    auto t=findTrackAuthoring(trackId); if (!t || !audioClipRef(*t,clipIndex)) return false;
    auto nt=cloneTrack(*t); auto& c=*audioClipRef(*nt,clipIndex);
    if (grid && (clipIndex < 0 || !c.sample)) return false;
    c.gridFollowsAudio=grid;
    if (grid) {
        c.tempoReferenceId=referenceId>0 ? referenceId : ++nextTempoReferenceId_;
        nextTempoReferenceId_=std::max(nextTempoReferenceId_,c.tempoReferenceId);
    }
    if (grid) {
        double geometry[4]; if (!gridReferenceInfo(trackId,clipIndex,geometry)) return false;
        c.gridSourceStart=geometry[0]; c.gridSourceEnd=geometry[1]; c.gridOriginSeconds=geometry[2]; c.warpCache=nullptr;
    }
    republishWithTrackRaw(trackId,std::move(nt)); return true;
}
bool Engine::setClipTempoMode(int32_t trackId,int32_t clipIndex,bool grid,bool lockAnchors) {
    tempoError_.clear();
    auto t=findTrackAuthoring(trackId);
    if (!t || !audioClipRef(*t,clipIndex) || (grid && clipIndex<0)) { tempoError_="Choose an Arrangement audio clip"; return false; }
    auto nt=cloneTrack(*t); auto& c=*audioClipRef(*nt,clipIndex);
    if (!c.sample) return false;
    if (!grid && c.warpMarkers.size()<2) {
        const double len=beatAtSeconds(secondsAtBeat(c.startBeat)+static_cast<double>(c.effectiveLength())/c.sample->sourceSampleRate/c.pitchRatio())-c.startBeat;
        c.warpMarkers={{c.sourceOffsetFrames,0},{c.sourceOffsetFrames+static_cast<double>(c.effectiveLength()),len}};
        c.warpBeats=len; c.warpPlayStart=c.warpPlayEnd=0;
    }
    if (grid && (!c.gridFollowsAudio || lockAnchors)) return fitClipTempo(trackId,clipIndex,std::move(nt),lockAnchors);
    auto anchors=tempoAnchors();
    for (auto& a : anchors) if ((c.tempoReferenceId>0 ? a.referenceId==c.tempoReferenceId : a.trackId==trackId && a.clipIndex==clipIndex)) a.locked=lockAnchors ? 1 : 0;
    bool sharedReference=false;
    if (!grid && c.tempoReferenceId>0) for (const auto& t : authoring_->tracks)
        for (size_t i=0;i<t->clips.size();++i) if ((t->id()!=trackId || static_cast<int32_t>(i)!=clipIndex)
            && t->clips[i].gridFollowsAudio && t->clips[i].tempoReferenceId==c.tempoReferenceId) sharedReference=true;
    if (!grid && !sharedReference) anchors.erase(std::remove_if(anchors.begin(),anchors.end(),[&](const TempoAnchor& a){return (c.tempoReferenceId>0 ? a.referenceId==c.tempoReferenceId : a.trackId==trackId && a.clipIndex==clipIndex);}),anchors.end());
    try {
        auto map=std::make_shared<const TempoMap>(tempoPoints(),tempoMinBpm(),tempoMaxBpm(),std::move(anchors));
        c.gridFollowsAudio=grid; c.warpEnabled=true; if (!grid) c.tempoReferenceId=0;
        auto g=std::make_shared<Graph>(); inheritGraphLevel(*g,*authoring_); g->tracks=authoring_->tracks;
        refreshTempoReferences(*g,*map); g->tempoMap=std::move(map);
        for (auto& track : g->tracks) if (track->id()==trackId) track=nt;
        publish(std::move(g)); placeTakesForTempo(); return true;
    } catch (const std::exception& e) { tempoError_=e.what(); return false; }
}
bool Engine::commitGridWarp(int32_t trackId,int32_t clipIndex,std::vector<TempoPoint> requestedPoints,
    double minBpm,double maxBpm,std::vector<TempoAnchor> requestedAnchors) {
    tempoError_.clear();
    auto t=findTrackAuthoring(trackId);
    const auto* current=t ? audioClipRef(*t,clipIndex) : nullptr;
    if (!authoring_ || !current || clipIndex<0 || !current->sample) {
        tempoError_="Choose an Arrangement audio clip"; return false;
    }
    if (minBpm!=tempoMinBpm() || maxBpm!=tempoMaxBpm()) {
        tempoError_="Grid Warp must preserve the project tempo limits"; return false;
    }
    auto nt=cloneTrack(*t);
    auto* target=audioClipRef(*nt,clipIndex);
    if (!target) { tempoError_="Choose an Arrangement audio clip"; return false; }
    auto& c=*target;
    if (std::abs(c.pitchSemitones)>1e-6) {
        tempoError_="Set clip pitch to zero before extracting original-speed grid timing"; return false;
    }
    double geometry[4];
    if (!gridReferenceInfo(trackId,clipIndex,geometry)) return false;
    const double sourceStart=geometry[0], sourceEnd=geometry[1], origin=geometry[2], sourceRate=geometry[3];
    const int64_t referenceId=c.tempoReferenceId>0 ? c.tempoReferenceId : nextTempoReferenceId_+1;
    if (referenceId<=0 || referenceId>1000000000000LL) {
        tempoError_="Grid Warp reference limit reached"; return false;
    }

    int32_t lastAnchorId=nextGridAnchorId_;
    for (const auto& a : tempoAnchors()) lastAnchorId=std::max(lastAnchorId,a.anchorId);
    for (const auto& a : requestedAnchors) lastAnchorId=std::max(lastAnchorId,a.anchorId);
    std::vector<TempoAnchor> targetAnchors;
    for (auto a : requestedAnchors) {
        if ((c.tempoReferenceId>0 && a.referenceId==c.tempoReferenceId)
            || (a.trackId==trackId && a.clipIndex==clipIndex)) {
            a.trackId=trackId; a.clipIndex=clipIndex; a.referenceId=referenceId; a.locked=1;
            if (a.anchorId==0) {
                if (lastAnchorId==INT32_MAX) { tempoError_="Grid Warp anchor identity limit reached"; return false; }
                a.anchorId=++lastAnchorId;
            }
            targetAnchors.push_back(a);
        }
    }
    std::sort(targetAnchors.begin(),targetAnchors.end(),[](const auto& a,const auto& b){return a.tick<b.tick;});
    if (targetAnchors.size()<2) {
        tempoError_="Grid Warp requires at least two timing anchors"; return false;
    }
    for (size_t i=0;i<targetAnchors.size();++i) {
        const auto& a=targetAnchors[i];
        if (a.tick<0 || !std::isfinite(a.seconds) || !std::isfinite(a.sourceFrame)
            || a.seconds<0 || a.sourceFrame<sourceStart || a.sourceFrame>sourceEnd
            || (i && (a.tick<=targetAnchors[i-1].tick || a.seconds<=targetAnchors[i-1].seconds
                || (c.reversed ? a.sourceFrame>=targetAnchors[i-1].sourceFrame
                    : a.sourceFrame<=targetAnchors[i-1].sourceFrame)))) {
            tempoError_="Grid Warp anchors must increase in beat and time without crossing"; return false;
        }
        const double expected=origin+(c.reversed ? sourceEnd-a.sourceFrame : a.sourceFrame-sourceStart)/sourceRate;
        if (std::abs(a.seconds-expected)>1/TempoMap::maxRate) {
            tempoError_="Grid Warp anchors must preserve the audio's absolute source timing"; return false;
        }
    }

    const int64_t firstTick=targetAnchors.front().tick, lastTick=targetAnchors.back().tick;
    auto generatedPoints=tempoPoints();
    auto seamAt=[&](int64_t tick) {
        auto it=std::upper_bound(generatedPoints.begin(),generatedPoints.end(),tick,[](int64_t v,const TempoPoint& p){return v<p.tick;});
        return it==generatedPoints.begin() ? generatedPoints.front().curve : (it-1)->curve;
    };
    auto prior=std::lower_bound(generatedPoints.begin(),generatedPoints.end(),firstTick,[](const TempoPoint& p,int64_t v){return p.tick<v;});
    if ((prior!=generatedPoints.begin() && (prior-1)->curve!=0) || seamAt(lastTick)!=0) {
        tempoError_="Grid Warp region boundaries must meet Step tempo segments; split or change the adjacent ramp first"; return false;
    }
    generatedPoints.erase(std::remove_if(generatedPoints.begin(),generatedPoints.end(),
        [&](const auto& p){return p.tick>=firstTick && p.tick<=lastTick;}),generatedPoints.end());
    for (size_t i=0;i+1<targetAnchors.size();++i) {
        const auto& a=targetAnchors[i]; const auto& b=targetAnchors[i+1];
        const double bpm=60*static_cast<double>(b.tick-a.tick)/TempoMap::ticksPerBeat/(b.seconds-a.seconds);
        generatedPoints.push_back({a.tick,bpm,0,a.anchorId});
    }
    generatedPoints.push_back({lastTick,tempoAtBeat(static_cast<double>(lastTick)/TempoMap::ticksPerBeat),0,targetAnchors.back().anchorId});
    std::sort(generatedPoints.begin(),generatedPoints.end(),[](const auto& a,const auto& b){return a.tick<b.tick;});
    if (generatedPoints.size()!=requestedPoints.size()) {
        tempoError_="Grid Warp tempo points do not match the timing anchors"; return false;
    }
    for (size_t i=0;i<generatedPoints.size();++i) {
        const auto& a=generatedPoints[i]; const auto& b=requestedPoints[i];
        if (a.tick!=b.tick || a.curve!=b.curve || std::abs(a.bpm-b.bpm)>1e-9) {
            tempoError_="Grid Warp tempo points do not match the timing anchors"; return false;
        }
    }

    auto anchors=tempoAnchors();
    anchors.erase(std::remove_if(anchors.begin(),anchors.end(),[&](const auto& a){
        return c.tempoReferenceId>0 ? a.referenceId==c.tempoReferenceId
            : a.trackId==trackId && a.clipIndex==clipIndex;
    }),anchors.end());
    anchors.insert(anchors.end(),targetAnchors.begin(),targetAnchors.end());
    try {
        auto map=std::make_shared<const TempoMap>(std::move(generatedPoints),minBpm,maxBpm,std::move(anchors));
        if (c.gridFollowsAudio && authoring_->tempoMap && map->points()==authoring_->tempoMap->points()
            && map->anchors()==authoring_->tempoMap->anchors()) return true;
        if (!c.gridBaseline) {
            auto baseline=std::make_shared<GridWarpBaseline>();
            baseline->map=authoring_->tempoMap ? authoring_->tempoMap
                : std::make_shared<const TempoMap>(tempoPoints(),minBpm,maxBpm,tempoAnchors());
            baseline->firstTick=firstTick; baseline->lastTick=lastTick;
            baseline->referenceId=c.tempoReferenceId; baseline->gridFollowsAudio=c.gridFollowsAudio;
            baseline->warpEnabled=c.warpEnabled; baseline->sourceStart=sourceStart;
            baseline->sourceEnd=sourceEnd; baseline->originSeconds=origin;
            c.gridBaseline=std::move(baseline);
        }
        c.gridFollowsAudio=true; c.warpCache=nullptr;
        c.gridSourceStart=sourceStart; c.gridSourceEnd=sourceEnd; c.gridOriginSeconds=origin; c.tempoReferenceId=referenceId;
        auto g=std::make_shared<Graph>(); inheritGraphLevel(*g,*authoring_); g->tracks=authoring_->tracks;
        for (auto& track : g->tracks) if (track->id()==trackId) track=nt;
        refreshTempoReferences(*g,*map); g->tempoMap=std::move(map);
        publish(std::move(g)); nextTempoReferenceId_=std::max(nextTempoReferenceId_,referenceId);
        nextGridAnchorId_=lastAnchorId;
        placeTakesForTempo(); tempoWarpBuildPending_=true;
        if (wj_) wj_->cancel.store(true,std::memory_order_relaxed);
        return true;
    } catch (const std::exception& e) { tempoError_=e.what(); return false; }
}
bool Engine::fitClipTempo(int32_t trackId,int32_t clipIndex,std::shared_ptr<Track> nt,bool lockAnchors) {
    auto& c=*audioClipRef(*nt,clipIndex);
    if (!c.sample || clipIndex<0) return false;
    if (c.warpMarkers.size()<2) {
        double len=beatAtSeconds(secondsAtBeat(c.startBeat)+static_cast<double>(c.effectiveLength())/c.sample->sourceSampleRate/c.pitchRatio())-c.startBeat;
        c.warpMarkers={{c.sourceOffsetFrames,0},{c.sourceOffsetFrames+static_cast<double>(c.effectiveLength()),len}};
        c.warpBeats=len;
    }
    if (c.tempoReferenceId==0) c.tempoReferenceId=++nextTempoReferenceId_;
    const double start=c.warpPlayStart, end=c.warpPlayEndEff();
    if (end<=start) return false;
    const double sourceStart=sourceAtBeat(c,start), sourceEnd=sourceAtBeat(c,end);
    const double origin=secondsAtBeat(c.startBeat), rate=c.sample->sourceSampleRate*c.pitchRatio();
    std::vector<WarpMarker> material{{sourceStart,start}};
    for (const auto& m : c.warpMarkers) if (m.beat>start && m.beat<end) material.push_back(m);
    material.push_back({sourceEnd,end});
    std::vector<TempoAnchor> fitted;
    for (const auto& m : material) fitted.push_back({std::llround((c.startBeat+(c.reversed ? end-m.beat : m.beat-start))*TempoMap::ticksPerBeat),
        origin+(c.reversed ? sourceEnd-m.srcFrame : m.srcFrame-sourceStart)/rate,lockAnchors?1:0,trackId,clipIndex,m.srcFrame,c.tempoReferenceId});
    std::sort(fitted.begin(),fitted.end(),[](const auto& a,const auto& b){return a.tick<b.tick;});
    auto anchors=tempoAnchors();
    for (const auto& a : anchors) if (a.locked && (c.tempoReferenceId>0 ? a.referenceId==c.tempoReferenceId : a.trackId==trackId && a.clipIndex==clipIndex)) {
        auto match=std::find_if(fitted.begin(),fitted.end(),[&](const auto& b){return std::abs(a.sourceFrame-b.sourceFrame)<1e-6;});
        if (match==fitted.end() || match->tick!=a.tick || std::abs(match->seconds-a.seconds)>1/TempoMap::maxRate) {
            tempoError_="Warp edit conflicts with a protected absolute-time anchor"; return false;
        }
        match->locked=1;
    }
    anchors.erase(std::remove_if(anchors.begin(),anchors.end(),[&](const auto& a){return (c.tempoReferenceId>0 ? a.referenceId==c.tempoReferenceId : a.trackId==trackId && a.clipIndex==clipIndex);}),anchors.end());
    auto points=tempoPoints();
    const auto first=fitted.front().tick, last=fitted.back().tick;
    points.erase(std::remove_if(points.begin(),points.end(),[&](const auto& p){return p.tick>=first && p.tick<=last;}),points.end());
    for (size_t i=0;i+1<fitted.size();++i) {
        const auto& a=fitted[i]; const auto& b=fitted[i+1];
        if (b.tick<=a.tick || b.seconds<=a.seconds) { tempoError_="Timing anchors must remain ordered and at distinct ticks"; return false; }
        points.push_back({a.tick,60*static_cast<double>(b.tick-a.tick)/TempoMap::ticksPerBeat/(b.seconds-a.seconds),0});
    }
    points.push_back({last,tempoAtBeat(static_cast<double>(last)/TempoMap::ticksPerBeat),0});
    std::sort(points.begin(),points.end(),[](const auto& a,const auto& b){return a.tick<b.tick;});
    anchors.insert(anchors.end(),fitted.begin(),fitted.end());
    try {
        auto map=std::make_shared<const TempoMap>(std::move(points),tempoMinBpm(),tempoMaxBpm(),std::move(anchors));
        // Grid follows audio preserves the reference's absolute origin as well as its source.
        if (std::abs(map->secondsAtBeat(c.startBeat)-origin)>1/TempoMap::maxRate) {
            tempoError_="Grid edit conflicts with the protected audio origin"; return false;
        }
        c.gridFollowsAudio=true; c.warpEnabled=true; c.warpCache=nullptr;
        c.gridSourceStart=sourceStart; c.gridSourceEnd=sourceEnd;
        auto g=std::make_shared<Graph>(); inheritGraphLevel(*g,*authoring_); g->tracks=authoring_->tracks;
        refreshTempoReferences(*g,*map); g->tempoMap=std::move(map);
        for (auto& track : g->tracks) if (track->id()==trackId) track=nt;
        publish(std::move(g)); placeTakesForTempo(); return true;
    } catch (const std::exception& e) { tempoError_=e.what(); return false; }
}
void Engine::refreshTempoReferences(Graph& graph,const TempoMap& map) {
    for (auto& track : graph.tracks) {
        std::shared_ptr<Track> nt;
        for (size_t i=0;i<track->clips.size();++i) {
            const auto& before=track->clips[i];
            if (!before.gridFollowsAudio || !before.sample) continue;
            auto ref=std::find_if(map.anchors().begin(),map.anchors().end(),[&](const auto& a){return
                before.tempoReferenceId>0 ? a.referenceId==before.tempoReferenceId
                    : a.trackId==track->id() && a.clipIndex==static_cast<int32_t>(i);});
            if (ref==map.anchors().end()) continue;
            if (!nt) nt=cloneTrack(*track);
            auto& c=nt->clips[i];
            const double rate=c.sample->sourceSampleRate*c.pitchRatio();
            if (c.gridOriginSeconds<0) c.gridOriginSeconds=ref->seconds
                -(c.reversed ? c.gridSourceEnd-ref->sourceFrame : ref->sourceFrame-c.gridSourceStart)/rate;
            c.startBeat=map.beatAtSeconds(c.gridOriginSeconds);
        }
        if (nt) track=std::move(nt);
    }
}

bool Engine::gridReferenceInfo(int32_t trackId,int32_t clipIndex,double* result) const {
    auto t=findTrackAuthoring(trackId); const auto* c=t ? audioClipRef(*t,clipIndex) : nullptr;
    if (!c || !c->sample || !result || c->sample->sourceSampleRate<=0) return false;
    double start=c->sourceOffsetFrames, end=start+c->effectiveLength();
    if (c->gridFollowsAudio && c->gridSourceEnd>c->gridSourceStart) {
        start=c->gridSourceStart; end=c->gridSourceEnd;
    } else if (c->warpEnabled && c->warpMarkers.size()>=2 && c->warpPlayLen()>0) {
        start=sourceAtBeat(*c,c->warpPlayStart); end=sourceAtBeat(*c,c->warpPlayEndEff());
    }
    if (end<=start) return false;
    result[0]=start; result[1]=end;
    result[2]=c->gridFollowsAudio && c->gridOriginSeconds>=0 ? c->gridOriginSeconds : secondsAtBeat(c->startBeat);
    result[3]=c->sample->sourceSampleRate*c->pitchRatio();
    return true;
}
bool Engine::restoreGridReferenceGeometry(int32_t trackId,int32_t clipIndex,const double* values) {
    auto t=findTrackAuthoring(trackId); const auto* c=t ? audioClipRef(*t,clipIndex) : nullptr;
    if (!c || !c->sample || !values || !std::isfinite(values[0]) || !std::isfinite(values[1])
        || !std::isfinite(values[2]) || values[0]<0 || values[1]<=values[0]
        || values[1]>c->sample->frames || values[2]<0) return false;
    auto nt=cloneTrack(*t); auto& target=*audioClipRef(*nt,clipIndex);
    target.gridSourceStart=values[0]; target.gridSourceEnd=values[1]; target.gridOriginSeconds=values[2];
    republishWithTrackRaw(trackId,std::move(nt)); return true;
}
} // namespace nota
