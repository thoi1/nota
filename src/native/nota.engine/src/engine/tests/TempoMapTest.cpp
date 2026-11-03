// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

#include "../TempoMap.h"
#include "../Transport.h"
#include <cmath>
#include <iostream>
#include <stdexcept>

int main() {
    int failures = 0;
    auto check = [&](bool ok, const char* label) {
        std::cout << (ok ? "PASS " : "FAIL ") << label << '\n';
        if (!ok) ++failures;
    };
    using namespace nota;
    TempoMap constant({{0, 120, 0}});
    check(constant.secondsAtBeat(14400) == 7200 && constant.beatAtSeconds(7200) == 14400, "constant two-hour map");
    TempoMap steps({{0, 120, 0}, {3840, 60, 0}, {7680, 180, 0}});
    check(std::abs(steps.secondsAtBeat(8) - 6) < 1e-12, "multiple step changes");
    TempoMap linear({{0, 60, 1}, {3840, 180, 0}});
    check(std::abs(linear.secondsAtBeat(4) - 2 * std::log(3.0)) < 1e-12, "linear analytical integral");
    TempoMap smooth({{0, 180, 2}, {7680, 60, 0}});
    double error = 0, previous = -1;
    for (int i = 0; i <= 10000; ++i) {
        double beat = i * 1.5;
        double time = smooth.secondsAtBeat(beat);
        error = std::max(error, std::abs(smooth.beatAtSeconds(time) - beat));
        if (time <= previous) { ++failures; break; }
        previous = time;
    }
    check(error < 1e-8, "smooth monotonic forward/inverse across two hours");
    double analyticError = 0;
    for (int i = 0; i <= 10000; ++i) {
        const double b = i * 0.0004;
        analyticError = std::max(analyticError, std::abs(linear.secondsAtBeat(b) - 2 * std::log1p(b * 0.5)));
    }
    check(analyticError * 192000 < 0.125, "linear prepared error below one eighth sample at 192 kHz");
    std::vector<TempoPoint> dense;
    for (int i = 0; i < 10000; ++i) dense.push_back({i * 960LL, 120.0 + i % 3, i % 3});
    TempoMap many(std::move(dense));
    check(std::abs(many.beatAtSeconds(many.secondsAtBeat(9999.5)) - 9999.5) < 1e-8, "10000 points");
    int rejected = 0;
    for (auto points : {std::vector<TempoPoint>{}, {{1,120,0}}, {{0,0,0}}, {{0,120,0},{0,130,0}}, {{0,120,8}}}) {
        try { TempoMap invalid(std::move(points)); } catch (const std::invalid_argument&) { ++rejected; }
    }
    check(rejected == 5, "invalid maps rejected");
    Transport transport;
    transport.setSampleRate(48000); transport.setTempoMap(&linear); transport.seekBeats(3);
    auto info=transport.transportInfo(transport.playheadSamples());
    check(std::abs(info.ppqPosition-3)<1e-10 && std::abs(info.timeInSeconds-linear.secondsAtBeat(3))<1e-10
        && std::abs(info.bpm-48000*60/transport.samplesPerBeat())<1e-10,
        "plugin PPQ/seconds/BPM snapshot uses prepared clock slope");
    transport.play(); transport.advanceBy(512);
    double before=transport.positionBeats(); transport.setTempoMap(&smooth);
    check(std::abs(transport.positionBeats()-before)<1e-10,"map adoption preserves musical position");
    transport.setLoop(true,2,6);
    check(std::abs(transport.loopEndSamples()-smooth.secondsAtBeat(6)*48000)<1e-8,"mapped loop sample boundary");
    transport.setTempoMap(&linear); transport.setLoop(true,1,5); transport.seekBeats(4.9);
    double presentationError=0;
    for (int block=0;block<160;++block) {
        if (transport.playheadSamples()>=transport.loopEndSamples()-0.5) transport.wrapToLoopStart();
        const double start=transport.playheadSamples();
        for (int left=2048;left>0;) {
            const double toEnd=transport.loopEndSamples()-transport.playheadSamples();
            if (toEnd<=0.5) { transport.wrapToLoopStart(); continue; }
            const int n=std::min(left,std::max(1,static_cast<int>(std::lround(toEnd))));
            transport.advanceBy(n); left-=n;
        }
        transport.publishPresentation(start,2048,0,8192);
        NotaTransportSnapshot snapshot{}; transport.presentationSnapshot(snapshot);
        double predicted=linear.secondsAtBeat(snapshot.start_beat)+snapshot.duration_seconds;
        const double a=linear.secondsAtBeat(1), b=linear.secondsAtBeat(5);
        if (predicted>=b) predicted=a+std::fmod(predicted-a,b-a);
        double difference=predicted-linear.secondsAtBeat(snapshot.position_beats);
        difference=std::remainder(difference,b-a);
        presentationError=std::max(presentationError,std::abs(difference)*48000);
    }
    check(presentationError<1,"loop presentation uses elapsed-time wrapping across a ramp");
    return failures ? 1 : 0;
}
