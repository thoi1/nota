**CPU while idle and while playing** — % of one core, median. Playing = blocks paced like a 512-sample buffer at 48 kHz. Lower is better.

| | Develop f239a748 | Tempo track fe07fa4a |
|---|---:|---:|
| Idle | 0 | 0 |
| 32 audio tracks | 39.0 | 38.9 |
| 32 synth tracks | 5.2 | 5.3 |

**Offline render (bounce) time** — ms per second of audio, median. Lower is faster.

| | Develop f239a748 | Tempo track fe07fa4a |
|---|---:|---:|
| 32 audio tracks | 43.1 | 43.0 |
| 32 synth tracks | 13.3 | 13.3 |

**CPU work during an offline render** — CPU ms per second of audio, median. Lower uses less energy.

| | Develop f239a748 | Tempo track fe07fa4a |
|---|---:|---:|
| 32 audio tracks | 628 | 658 |
| 32 synth tracks | 209 | 208 |

**Export and freeze render time** — ms per second of audio, median. Lower is faster.

| | Develop f239a748 | Tempo track fe07fa4a |
|---|---:|---:|
| 16 audio tracks · 8192-frame chunks | 19.2 | 19.3 |
| 16 synth tracks · 8192-frame chunks | 5.9 | 6.0 |

**CPU work during an export** — CPU ms per second of audio, median. Lower uses less energy.

| | Develop f239a748 | Tempo track fe07fa4a |
|---|---:|---:|
| 16 audio tracks · 8192-frame chunks | 253 | 209 |
| 16 synth tracks · 8192-frame chunks | 43.8 | 40.6 |

<details><summary>Machine and builds</summary>

```
date: 2026-11-11T15:14:53Z
os: Microsoft Windows 11 Pro 10.0.26300
cpu: AMD Ryzen 9 7900X 12-Core Processor            , 24 threads
runs: 3
before: f239a74841c6d506c74bd9a68d1019ff4344a0fe
after: fe07fa4a
```
</details>
