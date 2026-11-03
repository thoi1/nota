# Research

Decision: share MainWindow's existing display callback and gate meter reads at 30/60.
Rationale: dispatcher timers previously measured around 30 Hz, and an additional timer
duplicates scheduling. Use phase-preserving deadlines rather than now+period (which
accumulates callback quantization). Skip missed periods without bursts.

Separate meter-only work from mixed meter/fader/scene methods. Keep the original 33ms
service clock, ProcessCpuSampler's 500ms cache and 1s CPU tooltip throttle unchanged.
Include visible floating Mixer in frame ownership, even with main minimized.
User clarified audio level meter; no new CPU/RAM Performance Monitor or analyzers.
