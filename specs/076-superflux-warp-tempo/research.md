# Repository inspection and decisions

## Existing and reusable

| Area | Findings and paths |
|---|---|
| Engine/FFT | `src/native/nota.engine/src/engine`; `media/SampleAnalysis.cpp` uses `signalsmith::linear::RealFFT<float>`; dependency fetched by `cmake/dependencies.cmake`. |
| Stretch | `media/Warp.h`, `WarpStream.cpp`, `Engine_Tracks.cpp`: Signalsmith Stretch offline immutable device-rate caches; never stretch in callback. |
| Source/waveform | `media/SampleBuffer.h`: interleaved audio, process-unique ID, 512-frame peak/RMS tables; no explicit revision. Destructive edits create new buffers/IDs. Decoding currently holds full audio, a separate limitation from bounded analysis scratch. |
| Existing detectors | `media/TempoDetect.cpp` and managed `Analysis/AudioOnsets.cs`: energy-rise heuristics, not SuperFlux. Preserve legacy consumers in first increment; share new service for new tools. |
| Warp editor | `AudioClipEditorView.cs` edits real markers. Current uncommitted Arrangement/Piano Roll Warp tools and `Controls/WarpRuler.cs` give ruler feedback only; existing muted red feedback reused later. |
| Coordinates | `Nota.App/ClipEditor/ClipTimeMap.cs` and `ClipTempo.cs` handle content domains, trim, reversal and piecewise warp mapping. Generalise carefully for shared consumers. |
| Clock | `engine/Transport.h/.cpp`: constant BPM/meter; scalar samplesPerBeat used throughout `Engine_Render.cpp`, MIDI/controllers, Session, automation, loops, seeks, recording and TransportInfo to plugin NotaPlayHead. No variable map. |
| Scheduling | Existing .NET Task.Run appropriate; SampleLibraryIndex has bounded file workers; native warp has per-job worker. AudioThreadPool is RT-only. Use .NET pool with concurrency bound. |
| Boundary/lifetime | Engine::findSampleById scans mutable authoring state. Capture shared const buffer on message thread; worker never references engine again. Existing sample_read would copy full audio. Completed imports alone enter authoring graph. |
| Persistence/undo | `Persistence/AudioClipState.cs`, `ProjectModel.cs`, `ProjectMigrations.cs` persist markers/trim. Immutable graph history owns undo/redo. Only scalar BPM persisted. |
| Cache | `Persistence/AudioAnalysisCache.cs` stores peaks/BPM by fingerprint, not attacks/settings. Use source ID (replacement ID = revision), settings/version and bounded in-memory retention initially. |

## Reference and licensing

- Paper: Böck & Widmer, *Maximum Filter Vibrato Suppression for Onset Detection*, DAFx 2013: https://www.dafx.de/paper-archive/2013/papers/09.dafx2013_submission_12.pdf (sections 2.1-2.3).
- Reference: https://github.com/CPJKU/SuperFlux at commit `0a85f898af69188324b649a4c640052a5443078e`; SuperFlux.py embeds copyright 2012-2014 Sebastian Böck and BSD-2-Clause conditions. Preserve notice in LICENSES/SuperFlux.txt and register adaptation in third-party.md. No Python/NumPy/SciPy runtime dependency. NumPy/SciPy used only to regenerate committed test vectors.
- Decision: max-filtered delayed log-filterbank flux before separate refinement. Alternatives rejected: energy novelty called SuperFlux; whole Essentia; full spectrogram retention.
- Paper and script differ in peak windows; use paper section 2.3. Stereo magnitude pooling and rate-scaled FFT are explicit extensions.

## Clarification review

No blocking ambiguities: replacement ID represents source revision, first increment explicitly allowed, no supplied labelled recordings. Confidence is heuristic evidence; uncertain frame times retained. Existing project meter remains manual.

## US2 reuse update

The original clip-editor ClipTimeMap now lives in Application/Editing; ClipTempo delegates
its source/beat interpolation to the shared WarpPositionMap. TransientClipMap composes these
existing domains, and TransientGuideLayout caches played fractions for Arrangement. The
per-engine App TransientWorkspace owns UI notifications/settings only; native analysis and
worker scheduling remain in the original shared service. No second musical clock was added.
The missing nota-docs repository was cloned locally for EN/RU UI workflow documentation.
