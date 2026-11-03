# Analysis model

- TransientAnalysisSettings: sensitivity finite [0,1], spacing finite [0.005,1] seconds, refine flag. Version 1.
- AudioTransient: int64 source sample, seconds, positive strength, confidence [0,1], uncertainty samples >= 1, refinement flag, version. Optional band contributions deferred.
- TransientSnapshot: source ID, revision (0 for immutable current ID), settings, sample rate and read-only sorted collection. Nearest ties earlier, range [start,end).
- ITransientAnalysisJob: engine-independent captured source; step/cancel/progress/read completed results/dispose. Capture authoring thread, one worker steps/reads, cancel concurrent.
- ITransientAnalysisService: AnalyzeAudioAsync/GetTransients/GetNearestTransient/GetTransientsInRange/InvalidateAnalysis. Per-engine instance; no worker graph access. Pending/running → completed/cancelled/failed. Only completed snapshots cached. Any caller cancellation cancels shared job; subsequent requests retry. Dispose cancels work, prohibits new requests, releases sources after workers exit.
- Cache: 16 completed entries, 200,000 retained transient records. Larger results returned uncached. At most two active workers on .NET pool.
- Later: existing warp source-frame/beat pairs; separate tempo beat/time candidates, confidence/ambiguity and immutable strictly increasing map.
