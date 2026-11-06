# Benchmark — four heavy hosted plug-in instances (issue #532)

Deterministic `nota-render-probe` VST3 (built by `NOTA_BUILD_RENDER_PROBE`), per-instance
render load ≈ 1.47 ms, one instance per instrument track, real-time device callback.
Machine: Windows 11 Pro, AMD Ryzen 9 7900X (12 cores / 24 threads) — same processor
model as the issue reporter's. The develop and fix builds run alternately on the same
machine, 256-block runs of 200 blocks each (the chart shows the medians; both
`--perf`-independent harness `--profile-hosted` and the `--hosted-render-pool` guard
numbers are included).

## Alternating benchmark (`--profile-hosted`, 3 rounds)

| round | build | 4 instances median | p95 | late (of 200) | worker CPU |
|---|---|---|---|---|---|
| 1 | develop | 5.823 ms | 5.909 | 200 | 0 |
| 1 | fix | 1.658 ms | 1.850 | 0 | 890.6 ms |
| 2 | develop | 5.815 ms | 5.844 | 200 | 0 |
| 2 | fix | 1.686 ms | 1.888 | 0 | 1015.6 ms |
| 3 | develop | 5.823 ms | 5.855 | 200 | 0 |
| 3 | fix | 1.576 ms | 1.882 | 0 | 906.3 ms |

One instance ≈ 1.47 ms on both builds (unchanged). Helpers forced off reads 5.82 ms
on both builds — the serial path is untouched; worker CPU on the fix build ≈ 0.9–1.0 s
is the same work spread over the pool instead of piling on the audio thread.

## Hosted render-pool guards (`--hosted-render-pool` from `--perf`, same 7900X)

| scenario | serial median | pool median | late serial → pool |
|---|---|---|---|
| 4 heavy VST3 instrument tracks, device callback | 5.834 ms | 1.662 ms | 100/100 → 0/100 |
| 4 heavy VST3 instrument tracks, offline bounce | 2.928 ms | 0.768 ms | 100/100 → 0/100 |
| 512-frame large jobs, headroom sizing | one 3.006 ms | four 3.196 ms (≈1.07× serial) | 0/100 |

Raw alternating-round output: `results.csv`; guard JSON: `guards.csv`.
