**CPU while idle and while playing** — % of one core, median. Playing = blocks paced like a 512-sample buffer at 48 kHz. Lower is better.

| | Develop f239a748 | Tempo track f76d4480 |
|---|---:|---:|
| Idle | 0 | 0 |
| 32 audio tracks | 25.9 | 27.5 |
| 32 synth tracks | 5.5 | 5.0 |

**Offline render (bounce) time** — ms per second of audio, median. Lower is faster.

| | Develop f239a748 | Tempo track f76d4480 |
|---|---:|---:|
| 32 audio tracks | 39.1 | 38.4 |
| 32 synth tracks | 12.8 | 12.9 |

**CPU work during an offline render** — CPU ms per second of audio, median. Lower uses less energy.

| | Develop f239a748 | Tempo track f76d4480 |
|---|---:|---:|
| 32 audio tracks | 575 | 589 |
| 32 synth tracks | 212 | 212 |

**Export and freeze render time** — ms per second of audio, median. Lower is faster.

| | Develop f239a748 | Tempo track f76d4480 |
|---|---:|---:|
| 16 audio tracks · 8192-frame chunks | 18.7 | 18.4 |
| 16 synth tracks · 8192-frame chunks | 5.9 | 6.0 |

**CPU work during an export** — CPU ms per second of audio, median. Lower uses less energy.

| | Develop f239a748 | Tempo track f76d4480 |
|---|---:|---:|
| 16 audio tracks · 8192-frame chunks | 206 | 247 |
| 16 synth tracks · 8192-frame chunks | 28.1 | 29.7 |

<details><summary>Machine and builds</summary>

```
date: 2026-11-11T12:06:26Z
os: Microsoft Windows 11 Pro 10.0.26300
cpu: AMD Ryzen 9 7900X 12-Core Processor            , 24 threads
runs: 3
before: f239a74841c6d506c74bd9a68d1019ff4344a0fe
after: f76d4480
```
</details>
