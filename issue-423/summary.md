**CPU while idle and while playing** — % of one core, median. Playing = blocks paced like a 512-sample buffer at 48 kHz. Lower is better.

| | Official develop | Playhead fix |
|---|---:|---:|
| Idle | 0 | 0 |
| 8 audio tracks | 6.7 | 6.6 |
| 8 synth tracks | 1.2 | 1.4 |

**Offline render (bounce) time** — ms per second of audio, median. Lower is faster.

| | Official develop | Playhead fix |
|---|---:|---:|
| 1 audio track | 7.2 | 7.2 |
| 8 audio tracks | 12.7 | 12.8 |
| 8 synth tracks | 4.4 | 4.4 |

**CPU work during an offline render** — CPU ms per second of audio, median. Lower uses less energy.

| | Official develop | Playhead fix |
|---|---:|---:|
| 1 audio track | 7.8 | 7.8 |
| 8 audio tracks | 125 | 116 |
| 8 synth tracks | 51.6 | 50.0 |

**Export and freeze render time** — ms per second of audio, median. Lower is faster.

| | Official develop | Playhead fix |
|---|---:|---:|
| 16 audio tracks · 8192-frame chunks | 15.3 | 15.3 |

**CPU work during an export** — CPU ms per second of audio, median. Lower uses less energy.

| | Official develop | Playhead fix |
|---|---:|---:|
| 16 audio tracks · 8192-frame chunks | 197 | 136 |

**Late blocks at small buffers (heavy sessions)** — % of blocks late, median. Lower is better.

| | Official develop | Playhead fix |
|---|---:|---:|
| 32 audio tracks · 64-sample buffer | 0 | 0 |

**CPU at small buffers (heavy sessions)** — % of one core, median. Lower is better.

| | Official develop | Playhead fix |
|---|---:|---:|
| 32 audio tracks · 64-sample buffer | 29.2 | 28.8 |

<details><summary>Machine and builds</summary>

```
date: 2026-11-02T07:53:30Z
os: Microsoft Windows NT 10.0.26300.0
cpu: AMD Ryzen 9 7900X 12-Core Processor, 24 logical processors
runs: 3, interleaved before/after per scenario
before: 4da86c88f6f792a603d5199f6c63fa85deead08c
after: 6ba07c46da54beedcdbdce5d7e0b3316eefc4c3d
Windows adaptation of scripts/bench/engine-bench.cpp: GetProcessTimes and GetProcAddress; scenario definitions/audio duration unchanged.
Representative scenarios: idle; 1/8 audio and 8 synth offline; 8 audio/synth realtime; 32 audio realtime at 64 frames; 16 audio export.
```
</details>
