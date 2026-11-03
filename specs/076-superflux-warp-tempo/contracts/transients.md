# Transient ABI and service contract

All nota_transient_* exports are non-real-time functions in nota_engine.h. create(engine,sample_id,settings) captures a completed source on authoring thread, returns null on invalid input. Handle retains source independently of engine. step(handle,max_frames) processes bounded analysis frames: 1 running, 0 completed, -1 failed, -2 cancelled. cancel is atomic/concurrent; destroy only after other calls exit. progress queried on worker, [0,1], completed=1. count/read return results only after completion; read supports offset/capacity. Partial data inaccessible; null inputs safe; exceptions never escape.

Settings POD: doubles sensitivity/spacing, int32 refine/reserved. Result POD: int64 sample, double seconds, float strength/confidence, int64 uncertainty_samples, int32 refined/version. No C++ objects or callbacks cross ABI.

Service requests/capture originate on authoring thread. Cache reuses source/settings tasks; binary search for nearest/range. Cancellation cancels shared in-flight job. Invalidation cancels all source work, removes cached results and rejects stale publication. Disposed service rejects new work. IProgress decides UI dispatch. Completed snapshots survive eviction/invalidation/disposal.
