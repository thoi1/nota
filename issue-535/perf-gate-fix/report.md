<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

Generated on 2026-11-12 by the unchanged `scripts/perf-gate.sh` in isolated Ubuntu
26.04.1 / WSL2, GCC 15.2, 24 logical CPUs. The head is PR #568 at `29749ced` plus
this uncommitted fix, compared with CI base `c613167d`. Patch SHA-256:
`6d07d235f4709b99abcbd4305645cd75287a0f844304f6496d4cea84ef87d466`.

The machine differs from GitHub's four-core runner; these are local reproduction
results. Limits remain 1% work and 15% wall time, with five interleaved timing rounds.
The command exited 0. Raw TSVs, logs, and the seven original scenarios' Callgrind
profiles are retained locally in `build/tempo-linux/results/` (ignored).

## Engine performance: c613167d → 29749ced

PASS: no scenario does more than 1% more work or takes more than 15% longer than the base.

Work = CPU instructions per render, all engine threads (Valgrind, deterministic; effects: the effect's own share). Wall = fastest of the interleaved rounds, ms to render 1 s of audio on this machine; a scenario over the time limit is re-timed with more rounds before it fails.

| Scenario | | Work base | Work PR | Change | Wall base | Wall PR | Verdict |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| `instrument-03` | Instrument kind 3, one track | 6.41 M | 6.42 M | +0.19% | 0.23 ms | 0.23 ms | ok |
| `instrument-04` | Instrument kind 4, one track | 6.37 M | 6.38 M | +0.16% | 0.22 ms | 0.23 ms | ok |
| `instrument-01` | Sampler (no sample), one track | 6.37 M | 6.38 M | +0.16% | 0.22 ms | 0.22 ms | ok |
| `audio-warp-split` | Warped audio clip split in 8, playing | 6.72 M | 6.73 M | +0.16% | 0.23 ms | 0.23 ms | ok |
| `instrument-17` | Mosaic (no program), one track | 7.39 M | 7.40 M | +0.14% | 0.28 ms | 0.28 ms | ok |
| `audio-tails-8` | 8 audio tracks with Reverb + Delay, stopped, tails over | 4.14 M | 4.14 M | +0.13% | 0.14 ms | 0.14 ms | ok |
| `drum-rack` | Drum Rack, 4 Synth pads with decay | 21.65 M | 21.66 M | +0.05% | 0.78 ms | 0.78 ms | ok |
| `mix-4` | 4 tracks, all playing | 539.47 M | 539.72 M | +0.05% | 15.96 ms | 16.20 ms | ok |
| `silent-32` | 32 tracks, no clips | 827.60 M | 827.89 M | +0.03% | 9.41 ms | 9.83 ms | ok |
| `instrument-07` | Bass, one track | 34.69 M | 34.70 M | +0.03% | 2.45 ms | 2.46 ms | ok |
| `instrument-00` | Synth, one track | 45.60 M | 45.61 M | +0.02% | 1.77 ms | 1.78 ms | ok |
| `effect-base` | Synth alone (effect baseline) | 45.60 M | 45.61 M | +0.02% | 1.79 ms | 1.78 ms | ok |
| `instrument-12` | Rhythm Machine, one track | 64.01 M | 64.02 M | +0.02% | 5.24 ms | 5.24 ms | ok |
| `instrument-16` | Keys, one track | 73.23 M | 73.24 M | +0.01% | 3.69 ms | 3.76 ms | ok |
| `instrument-02` | Physical, one track | 80.33 M | 80.34 M | +0.01% | 4.08 ms | 4.15 ms | ok |
| `instrument-rack` | Instrument Rack, 2 Synth chains | 87.39 M | 87.40 M | +0.01% | 3.45 ms | 3.49 ms | ok |
| `instrument-13` | Monolith, one track | 88.28 M | 88.29 M | +0.01% | 9.78 ms | 9.76 ms | ok |
| `instrument-06` | Volt, one track | 100.53 M | 100.54 M | +0.01% | 4.44 ms | 4.48 ms | ok |
| `instrument-08` | Pendulum, one track | 128.54 M | 128.55 M | +0.01% | 8.20 ms | 8.28 ms | ok |
| `mix-1` | 1 tracks, all playing | 137.75 M | 137.76 M | +0.01% | 6.69 ms | 6.72 ms | ok |
| `instrument-05` | Aurora, one track | 156.23 M | 156.24 M | +0.01% | 6.92 ms | 6.97 ms | ok |
| `instrument-09` | Operator, one track | 179.54 M | 179.55 M | +0.01% | 13.20 ms | 13.21 ms | ok |
| `instrument-15` | Consort, one track | 189.03 M | 189.05 M | +0.01% | 14.50 ms | 14.45 ms | ok |
| `mix-32` | 32 tracks, all playing | 4,290.43 M | 4,290.66 M | +0.01% | 30.59 ms | 30.67 ms | ok |
| `mix-8` | 8 tracks, all playing | 1,075.09 M | 1,075.15 M | +0.00% | 16.77 ms | 16.48 ms | ok |
| `instrument-10` | Grain, one track | 231.08 M | 231.09 M | +0.00% | 13.20 ms | 13.09 ms | ok |
| `instrument-11` | Flux, one track | 133.65 M | 133.66 M | +0.00% | 9.34 ms | 9.28 ms | ok |
| `instrument-18` | Twin, one track | 377.21 M | 377.23 M | +0.00% | 31.47 ms | 31.67 ms | ok |
| `instrument-14` | Pentad, one track | 358.57 M | 358.58 M | +0.00% | 24.60 ms | 24.37 ms | ok |
| `mix-16` | 16 tracks, all playing | 2,146.83 M | 2,146.86 M | +0.00% | 22.27 ms | 22.91 ms | ok |
| `pentad-full-16` | Pentad, 16-voice chord | 1,198.56 M | 1,198.57 M | +0.00% | 83.08 ms | 83.09 ms | ok |
| `twin-layer-6x6` | Twin Layer, 6 + 6 voices, both parts' FX | 1,602.80 M | 1,602.81 M | +0.00% | 128.74 ms | 127.35 ms | ok |
| `twin-full-12` | Twin, 12-voice chord, both filters, 16 routes, FX | 1,664.69 M | 1,664.70 M | +0.00% | 141.24 ms | 141.51 ms | ok |
| `effect-03` | Nota Delay x8 on Synth | 17.10 M | 17.10 M | +0.00% | 10.80 ms | 10.84 ms | ok |
| `effect-19` | Nota Shutter x8 on Synth | 10.37 M | 10.37 M | +0.00% | 5.61 ms | 5.61 ms | ok |
| `effect-25` | Nota Chorus x8 on Synth | 27.27 M | 27.27 M | +0.00% | 17.04 ms | 16.26 ms | ok |
| `effect-23` | Nota Flanger x8 on Synth | 18.61 M | 18.61 M | +0.00% | 12.34 ms | 12.29 ms | ok |
| `effect-12` | Nota Crush x8 on Synth | 11.06 M | 11.06 M | +0.00% | 6.37 ms | 6.33 ms | ok |
| `effect-06` | Nota Valve x8 on Synth | 32.93 M | 32.93 M | +0.00% | 18.14 ms | 18.10 ms | ok |
| `effect-08` | Nota Vintage x8 on Synth | 44.78 M | 44.78 M | +0.00% | 29.97 ms | 29.83 ms | ok |
| `effect-13` | Nota Dynamic EQ-8 x8 on Synth | 43.98 M | 43.98 M | +0.00% | 17.78 ms | 17.90 ms | ok |
| `effect-10` | Nota Auto Shift x8 on Synth | 361.82 M | 361.82 M | +0.00% | 111.94 ms | 111.07 ms | ok |
| `effect-00` | Nota EQ-8 x8 on Synth | 10.49 M | 10.49 M | +0.00% | 5.45 ms | 5.43 ms | ok |
| `effect-16` | Nota EQ-3 x8 on Synth | 34.42 M | 34.42 M | +0.00% | 14.54 ms | 14.50 ms | ok |
| `effect-17` | Nota Forge x8 on Synth | 26.01 M | 26.01 M | -0.00% | 14.20 ms | 14.07 ms | ok |
| `effect-18` | Nota Level x8 on Synth | 42.09 M | 42.09 M | -0.00% | 28.45 ms | 28.53 ms | ok |
| `effect-26` | Nota Vocoder x8 on Synth | 181.69 M | 181.69 M | -0.00% | 79.56 ms | 79.50 ms | ok |
| `effect-01` | Nota Compressor x8 on Synth | 16.24 M | 16.24 M | -0.00% | 11.05 ms | 10.96 ms | ok |
| `effect-11` | Nota Beat Repeat x8 on Synth | 15.90 M | 15.90 M | -0.00% | 10.47 ms | 10.22 ms | ok |
| `effect-07` | Nota Auto Filter x8 on Synth | 10.76 M | 10.76 M | -0.00% | 6.71 ms | 6.72 ms | ok |
| `effect-04` | Nota Utility x8 on Synth | 20.35 M | 20.35 M | -0.00% | 9.87 ms | 9.94 ms | ok |
| `effect-09` | Nota Orbit x8 on Synth | 8.45 M | 8.45 M | -0.00% | 6.01 ms | 6.21 ms | ok |
| `effect-21` | Nota Prism x8 on Synth | 60.44 M | 60.44 M | -0.00% | 33.08 ms | 33.05 ms | ok |
| `effect-14` | Nota Ceiling x8 on Synth | 26.13 M | 26.13 M | -0.00% | 13.43 ms | 13.10 ms | ok |
| `effect-24` | Nota Phaser x8 on Synth | 24.53 M | 24.53 M | -0.00% | 13.83 ms | 13.70 ms | ok |
| `effect-05` | Nota Audio Effect Rack x8 on Synth | 1.64 M | 1.64 M | -0.00% | 2.13 ms | 2.15 ms | ok |
| `effect-22` | Nota Lens x8 on Synth | 4.81 M | 4.81 M | -0.00% | 3.44 ms | 3.48 ms | ok |
| `effect-02` | Nota Reverb x8 on Synth | 121.12 M | 121.12 M | -0.00% | 59.84 ms | 59.96 ms | ok |
| `effect-15` | Nota Strata x8 on Synth | 1.22 M | 1.22 M | -0.00% | 2.07 ms | 2.06 ms | ok |
| `effect-20` | Nota Chamber x8 on Synth | 310.31 M | 310.31 M | -0.00% | 154.02 ms | 151.19 ms | ok |
| `aurora-supersaw-2x` | Aurora Super Saw x2 | 939.90 M | 939.74 M | -0.02% | 25.27 ms | 25.64 ms | ok |
| `aurora-imported-2x` | Aurora Super Saw x2, imported 256-frame tables | 1,019.97 M | 1,019.35 M | -0.06% | 26.20 ms | 26.02 ms | ok |
| `sparse-32` | 32 tracks, 1 in 4 playing | 1,755.51 M | 1,753.09 M | -0.14% | 22.75 ms | 22.31 ms | ok |
| `edit-warp-split` | Split a warped 8 s clip 8x + undo (ms per edit) | 0.14 M | 0.14 M | -0.95% | 0.00 ms | 0.00 ms | ok |

