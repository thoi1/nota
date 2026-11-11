## Engine performance: ad5b1c6f → 6495e5cd

PASS: no scenario does more than 1% more work or takes more than 15% longer than the base.

Work = CPU instructions per render, all engine threads (Valgrind, deterministic; effects: the effect's own share). Wall = fastest of the interleaved rounds, ms to render 1 s of audio on this machine; a scenario over the time limit is re-timed with more rounds before it fails.

| Scenario | | Work base | Work PR | Change | Wall base | Wall PR | Verdict |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| `mix-8` | 8 tracks, all playing | 878.32 M | 879.56 M | +0.14% | 25.38 ms | 25.40 ms | ok |
| `aurora-supersaw-2x` | Aurora Super Saw x2 | 696.62 M | 697.10 M | +0.07% | 24.75 ms | 26.09 ms | ok |
| `silent-32` | 32 tracks, no clips | 621.93 M | 622.14 M | +0.03% | 21.15 ms | 21.29 ms | ok |
| `audio-warp-split` | Warped audio clip split in 8, playing | 5.20 M | 5.20 M | +0.02% | 0.34 ms | 0.34 ms | ok |
| `audio-tails-8` | 8 audio tracks with Reverb + Delay, stopped, tails over | 3.20 M | 3.20 M | +0.01% | 0.23 ms | 0.23 ms | ok |
| `mix-32` | 32 tracks, all playing | 3,500.25 M | 3,500.43 M | +0.01% | 99.42 ms | 99.30 ms | ok |
| `instrument-14` | Pentad, one track | 256.75 M | 256.76 M | +0.00% | 22.75 ms | 22.82 ms | ok |
| `pentad-full-16` | Pentad, 16-voice chord | 856.27 M | 856.27 M | +0.00% | 76.00 ms | 76.22 ms | ok |
| `effect-05` | Nota Audio Effect Rack x8 on Synth | 1.25 M | 1.25 M | +0.00% | 2.52 ms | 2.55 ms | ok |
| `effect-15` | Nota Strata x8 on Synth | 1.65 M | 1.65 M | +0.00% | 2.43 ms | 2.41 ms | ok |
| `effect-22` | Nota Lens x8 on Synth | 3.48 M | 3.48 M | +0.00% | 5.06 ms | 5.08 ms | ok |
| `effect-09` | Nota Orbit x8 on Synth | 5.40 M | 5.40 M | +0.00% | 5.26 ms | 5.27 ms | ok |
| `effect-00` | Nota EQ-8 x8 on Synth | 7.59 M | 7.59 M | +0.00% | 6.37 ms | 6.36 ms | ok |
| `effect-07` | Nota Auto Filter x8 on Synth | 7.92 M | 7.92 M | +0.00% | 7.20 ms | 7.20 ms | ok |
| `effect-19` | Nota Shutter x8 on Synth | 8.66 M | 8.66 M | +0.00% | 6.25 ms | 6.23 ms | ok |
| `effect-12` | Nota Crush x8 on Synth | 9.32 M | 9.32 M | +0.00% | 6.32 ms | 6.32 ms | ok |
| `effect-23` | Nota Flanger x8 on Synth | 11.57 M | 11.57 M | +0.00% | 11.53 ms | 11.53 ms | ok |
| `effect-03` | Nota Delay x8 on Synth | 11.64 M | 11.64 M | +0.00% | 10.33 ms | 10.32 ms | ok |
| `effect-01` | Nota Compressor x8 on Synth | 12.97 M | 12.97 M | +0.00% | 10.62 ms | 10.61 ms | ok |
| `effect-11` | Nota Beat Repeat x8 on Synth | 13.31 M | 13.31 M | +0.00% | 15.37 ms | 15.46 ms | ok |
| `effect-04` | Nota Utility x8 on Synth | 14.62 M | 14.62 M | +0.00% | 11.23 ms | 11.24 ms | ok |
| `effect-24` | Nota Phaser x8 on Synth | 20.42 M | 20.42 M | +0.00% | 15.01 ms | 14.99 ms | ok |
| `effect-17` | Nota Forge x8 on Synth | 21.41 M | 21.41 M | +0.00% | 14.06 ms | 14.08 ms | ok |
| `effect-06` | Nota Valve x8 on Synth | 22.13 M | 22.13 M | +0.00% | 16.86 ms | 16.85 ms | ok |
| `effect-25` | Nota Chorus x8 on Synth | 22.21 M | 22.21 M | +0.00% | 17.88 ms | 17.91 ms | ok |
| `effect-16` | Nota EQ-3 x8 on Synth | 22.87 M | 22.87 M | +0.00% | 14.54 ms | 14.47 ms | ok |
| `effect-18` | Nota Level x8 on Synth | 31.24 M | 31.24 M | +0.00% | 30.52 ms | 30.50 ms | ok |
| `effect-13` | Nota Dynamic EQ-8 x8 on Synth | 37.43 M | 37.43 M | +0.00% | 18.22 ms | 18.10 ms | ok |
| `effect-08` | Nota Vintage x8 on Synth | 37.65 M | 37.65 M | +0.00% | 77.05 ms | 77.27 ms | ok |
| `effect-21` | Nota Prism x8 on Synth | 42.38 M | 42.38 M | +0.00% | 29.79 ms | 29.55 ms | ok |
| `effect-14` | Nota Ceiling x8 on Synth | 20.34 M | 20.34 M | +0.00% | 16.84 ms | 16.84 ms | ok |
| `effect-02` | Nota Reverb x8 on Synth | 98.63 M | 98.63 M | +0.00% | 70.28 ms | 70.21 ms | ok |
| `effect-26` | Nota Vocoder x8 on Synth | 135.56 M | 135.56 M | +0.00% | 80.06 ms | 80.98 ms | ok |
| `effect-10` | Nota Auto Shift x8 on Synth | 310.17 M | 310.17 M | +0.00% | 135.22 ms | 135.25 ms | ok |
| `twin-layer-6x6` | Twin Layer, 6 + 6 voices, both parts' FX | 1,220.96 M | 1,220.96 M | -0.00% | 135.74 ms | 135.75 ms | ok |
| `twin-full-12` | Twin, 12-voice chord, both filters, 16 routes, FX | 1,278.98 M | 1,278.97 M | -0.00% | 147.50 ms | 147.25 ms | ok |
| `instrument-18` | Twin, one track | 288.47 M | 288.46 M | -0.00% | 34.09 ms | 33.74 ms | ok |
| `instrument-10` | Grain, one track | 183.16 M | 183.15 M | -0.00% | 12.40 ms | 12.41 ms | ok |
| `instrument-09` | Operator, one track | 157.71 M | 157.70 M | -0.01% | 14.21 ms | 14.24 ms | ok |
| `mix-1` | 1 tracks, all playing | 112.14 M | 112.13 M | -0.01% | 7.21 ms | 7.21 ms | ok |
| `instrument-15` | Consort, one track | 135.62 M | 135.61 M | -0.01% | 12.91 ms | 13.00 ms | ok |
| `instrument-13` | Monolith, one track | 80.33 M | 80.32 M | -0.01% | 13.49 ms | 13.50 ms | ok |
| `instrument-11` | Flux, one track | 118.39 M | 118.38 M | -0.01% | 10.15 ms | 10.17 ms | ok |
| `instrument-06` | Volt, one track | 83.82 M | 83.81 M | -0.01% | 4.89 ms | 4.91 ms | ok |
| `instrument-05` | Aurora, one track | 126.85 M | 126.84 M | -0.01% | 6.90 ms | 6.90 ms | ok |
| `instrument-08` | Pendulum, one track | 72.27 M | 72.27 M | -0.01% | 5.89 ms | 5.89 ms | ok |
| `instrument-rack` | Instrument Rack, 2 Synth chains | 67.11 M | 67.11 M | -0.01% | 3.64 ms | 3.65 ms | ok |
| `instrument-12` | Rhythm Machine, one track | 58.03 M | 58.03 M | -0.01% | 10.34 ms | 10.33 ms | ok |
| `instrument-16` | Keys, one track | 55.75 M | 55.74 M | -0.02% | 3.84 ms | 3.83 ms | ok |
| `instrument-02` | Physical, one track | 50.61 M | 50.60 M | -0.02% | 3.63 ms | 3.62 ms | ok |
| `effect-20` | Nota Chamber x8 on Synth | 237.41 M | 237.36 M | -0.02% | 178.56 ms | 178.95 ms | ok |
| `instrument-07` | Bass, one track | 28.13 M | 28.12 M | -0.02% | 2.21 ms | 2.23 ms | ok |
| `instrument-00` | Synth, one track | 34.95 M | 34.94 M | -0.03% | 1.94 ms | 1.96 ms | ok |
| `effect-base` | Synth alone (effect baseline) | 34.95 M | 34.94 M | -0.03% | 1.95 ms | 1.94 ms | ok |
| `mix-16` | 16 tracks, all playing | 1,752.40 M | 1,751.66 M | -0.04% | 50.03 ms | 50.20 ms | ok |
| `drum-rack` | Drum Rack, 4 Synth pads with decay | 16.88 M | 16.87 M | -0.08% | 1.04 ms | 1.05 ms | ok |
| `edit-warp-split` | Split a warped 8 s clip 8x + undo (ms per edit) | 0.13 M | 0.13 M | -0.10% | 0.00 ms | 0.00 ms | ok |
| `instrument-17` | Mosaic (no program), one track | 5.78 M | 5.77 M | -0.16% | 0.47 ms | 0.48 ms | ok |
| `instrument-03` | Instrument kind 3, one track | 4.91 M | 4.91 M | -0.16% | 0.34 ms | 0.35 ms | ok |
| `instrument-01` | Sampler (no sample), one track | 4.87 M | 4.86 M | -0.19% | 0.34 ms | 0.34 ms | ok |
| `instrument-04` | Instrument kind 4, one track | 4.87 M | 4.86 M | -0.19% | 0.34 ms | 0.34 ms | ok |
| `sparse-32` | 32 tracks, 1 in 4 playing | 1,374.56 M | 1,369.46 M | -0.37% | 44.72 ms | 44.68 ms | ok |
| `mix-4` | 4 tracks, all playing | 444.76 M | 441.92 M | -0.64% | 12.90 ms | 12.97 ms | ok |
