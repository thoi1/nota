// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

#include "Engine.h"
#include <cstring>
#include <stdexcept>
#include <cmath>

namespace nota {
namespace {
template<class T> void put(std::vector<uint8_t>& bytes,T value) {
    auto at=bytes.size(); bytes.resize(at+sizeof(T)); std::memcpy(bytes.data()+at,&value,sizeof(T));
}
struct Reader {
    const uint8_t* data; size_t size,offset=0;
    template<class T> T get() {
        if (offset>size || sizeof(T)>size-offset) throw std::invalid_argument("Truncated Grid Warp baseline");
        T value; std::memcpy(&value,data+offset,sizeof(T)); offset+=sizeof(T); return value;
    }
};
}
std::vector<uint8_t> Engine::gridWarpBaseline(int32_t track,int32_t clip) const {
    auto t=findTrackAuthoring(track); const auto* c=t ? audioClipRef(*t,clip) : nullptr;
    if (!c || !c->gridBaseline || !c->gridBaseline->map) return {};
    const auto& base=*c->gridBaseline; const auto& map=*base.map;
    std::vector<uint8_t> bytes; put<uint32_t>(bytes,0x31425747); // GWB1; fields, never padded structs
    put(bytes,base.firstTick); put(bytes,base.lastTick); put(bytes,base.referenceId);
    put(bytes,base.sourceStart); put(bytes,base.sourceEnd); put(bytes,base.originSeconds);
    put<int32_t>(bytes,base.gridFollowsAudio?1:0); put<int32_t>(bytes,base.warpEnabled?1:0);
    put(bytes,map.minBpm()); put(bytes,map.maxBpm());
    put<int32_t>(bytes,static_cast<int32_t>(map.points().size()));
    put<int32_t>(bytes,static_cast<int32_t>(map.anchors().size()));
    for (const auto& p : map.points()) { put(bytes,p.tick); put(bytes,p.bpm); put(bytes,p.curve); put(bytes,p.anchorId); }
    for (const auto& a : map.anchors()) {
        put(bytes,a.tick); put(bytes,a.seconds); put(bytes,a.locked); put(bytes,a.trackId); put(bytes,a.clipIndex);
        put(bytes,a.sourceFrame); put(bytes,a.referenceId); put(bytes,a.anchorId);
    }
    return bytes;
}
bool Engine::restoreGridWarpBaseline(int32_t track,int32_t clip,const uint8_t* data,int32_t size) {
    auto t=findTrackAuthoring(track); auto* c=t ? audioClipRef(*t,clip) : nullptr;
    if (!c || !c->sample || size<0 || size>8*1024*1024 || (size && !data)) return false;
    try {
        std::shared_ptr<GridWarpBaseline> base;
        if (size) {
            Reader read{data,static_cast<size_t>(size)};
            if (read.get<uint32_t>()!=0x31425747) return false;
            base=std::make_shared<GridWarpBaseline>();
            base->firstTick=read.get<int64_t>(); base->lastTick=read.get<int64_t>(); base->referenceId=read.get<int64_t>();
            base->sourceStart=read.get<double>(); base->sourceEnd=read.get<double>(); base->originSeconds=read.get<double>();
            base->gridFollowsAudio=read.get<int32_t>()!=0; base->warpEnabled=read.get<int32_t>()!=0;
            double min=read.get<double>(), max=read.get<double>();
            int32_t points=read.get<int32_t>(), anchors=read.get<int32_t>();
            if (points<1 || points>100000 || anchors<0 || anchors>100000 || base->firstTick<0
                || base->lastTick<=base->firstTick || !std::isfinite(base->sourceStart) || !std::isfinite(base->sourceEnd)
                || !std::isfinite(base->originSeconds) || base->sourceStart<0 || base->sourceEnd<=base->sourceStart
                || base->sourceEnd>c->sample->frames || base->originSeconds<0) return false;
            std::vector<TempoPoint> p; std::vector<TempoAnchor> a;
            p.reserve(points); a.reserve(anchors);
            for (int32_t i=0;i<points;++i) {
                TempoPoint v; v.tick=read.get<int64_t>(); v.bpm=read.get<double>(); v.curve=read.get<int32_t>(); v.anchorId=read.get<int32_t>(); p.push_back(v);
            }
            for (int32_t i=0;i<anchors;++i) {
                TempoAnchor v; v.tick=read.get<int64_t>(); v.seconds=read.get<double>(); v.locked=read.get<int32_t>();
                v.trackId=read.get<int32_t>(); v.clipIndex=read.get<int32_t>(); v.sourceFrame=read.get<double>();
                v.referenceId=read.get<int64_t>(); v.anchorId=read.get<int32_t>(); a.push_back(v);
            }
            if (read.offset!=read.size) return false;
            base->map=std::make_shared<const TempoMap>(std::move(p),min,max,std::move(a));
        }
        if (base && base->map) for (const auto& anchor : base->map->anchors())
            nextGridAnchorId_=std::max(nextGridAnchorId_,anchor.anchorId);
        auto nt=cloneTrack(*t); audioClipRef(*nt,clip)->gridBaseline=std::move(base);
        republishWithTrackRaw(track,std::move(nt)); return true;
    } catch (...) { return false; }
}
bool Engine::resetGridWarp(int32_t track,int32_t clip) {
    tempoError_.clear(); auto t=findTrackAuthoring(track); const auto* old=t ? audioClipRef(*t,clip) : nullptr;
    if (!old || !old->gridFollowsAudio || !old->gridBaseline) { tempoError_="No active Grid Warp baseline"; return false; }
    for (const auto& sibling : authoring_->tracks) for (const auto& c : sibling->clips)
        if (&c!=old && c.gridFollowsAudio && c.tempoReferenceId==old->tempoReferenceId) {
            tempoError_="Reset would change another fragment of this shared Grid Warp reference"; return false;
        }
    const auto& base=*old->gridBaseline;
    try {
        auto points=tempoPoints();
        points.erase(std::remove_if(points.begin(),points.end(),[&](const auto& p){return p.tick>=base.firstTick && p.tick<=base.lastTick;}),points.end());
        for (const auto& p : base.map->points()) if (p.tick>=base.firstTick && p.tick<base.lastTick) points.push_back(p);
        auto boundary=[&](int64_t tick) {
            if (std::none_of(points.begin(),points.end(),[&](const auto& p){return p.tick==tick;}))
                points.push_back({tick,base.map->bpmAtBeat(static_cast<double>(tick)/TempoMap::ticksPerBeat),0});
        };
        auto firstPrior=std::lower_bound(points.begin(),points.end(),base.firstTick,[](const auto& p,int64_t tick){return p.tick<tick;});
        const double firstBpm=base.map->bpmAtBeat(static_cast<double>(base.firstTick)/TempoMap::ticksPerBeat);
        if (firstPrior==points.begin() || (firstPrior-1)->curve!=0 || std::abs((firstPrior-1)->bpm-firstBpm)>1e-12)
            boundary(base.firstTick);
        const double tailBpm=tempoAtBeat(static_cast<double>(base.lastTick)/TempoMap::ticksPerBeat);
        std::sort(points.begin(),points.end(),[](const auto& a,const auto& b){return a.tick<b.tick;});
        auto prior=std::lower_bound(points.begin(),points.end(),base.lastTick,[](const auto& p,int64_t tick){return p.tick<tick;});
        if (prior==points.begin() || std::abs((prior-1)->bpm-tailBpm)>1e-12)
            points.push_back({base.lastTick,tailBpm,0});
        std::sort(points.begin(),points.end(),[](const auto& a,const auto& b){return a.tick<b.tick;});
        auto anchors=tempoAnchors();
        anchors.erase(std::remove_if(anchors.begin(),anchors.end(),[&](const auto& a){return a.referenceId==old->tempoReferenceId;}),anchors.end());
        if (base.gridFollowsAudio) for (const auto& a : base.map->anchors())
            if (a.referenceId==base.referenceId) anchors.push_back(a);
        auto map=std::make_shared<const TempoMap>(std::move(points),tempoMinBpm(),tempoMaxBpm(),std::move(anchors));
        auto nt=cloneTrack(*t); auto& c=*audioClipRef(*nt,clip);
        c.gridFollowsAudio=base.gridFollowsAudio; c.tempoReferenceId=base.referenceId; c.warpEnabled=base.warpEnabled;
        c.gridSourceStart=base.sourceStart; c.gridSourceEnd=base.sourceEnd;
        c.gridOriginSeconds=base.gridFollowsAudio ? base.originSeconds : -1;
        c.startBeat=map->beatAtSeconds(base.originSeconds);
        auto graph=std::make_shared<Graph>(); inheritGraphLevel(*graph,*authoring_); graph->tracks=authoring_->tracks;
        for (auto& target : graph->tracks) if (target->id()==track) target=nt;
        graph->tempoMap=std::move(map); refreshTempoReferences(*graph,*graph->tempoMap);
        publish(std::move(graph)); placeTakesForTempo(); return true;
    } catch (const std::exception& ex) { tempoError_=ex.what(); return false; }
}
} // namespace nota
