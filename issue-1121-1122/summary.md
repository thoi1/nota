**CPU while idle and while playing** — % of one core, median. Playing = blocks paced like a 512-sample buffer at 48 kHz. Lower is better.

| | Clean develop | PR #1125 |
|---|---:|---:|
| Idle | 0 | 0 |
| 8 synth tracks | 1.9 | 1.7 |
| 32 synth tracks | 6.2 | 5.9 |

**Offline render (bounce) time** — ms per second of audio, median. Lower is faster.

| | Clean develop | PR #1125 |
|---|---:|---:|
| 8 synth tracks | 5.0 | 4.9 |
| 32 synth tracks | 14.7 | 14.5 |

**CPU work during an offline render** — CPU ms per second of audio, median. Lower uses less energy.

| | Clean develop | PR #1125 |
|---|---:|---:|
| 8 synth tracks | 76.6 | 70.3 |
| 32 synth tracks | 144 | 175 |

<details><summary>Machine and builds</summary>

```
Windows x64 Release, 3 interleaved runs per build and scenario
CPU: AMD Ryzen 9 7900X, 24 logical processors
Before: clean develop 43df994
After: draft PR #1125 42207ae
CPU time: Windows GetProcessTimes. Engine harness: scripts/bench/engine-bench.cpp, adapted locally for Windows.
The screenshot build ran during part of the benchmark. Treat these measurements as indicative.
```
</details>
