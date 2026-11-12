<!-- SPDX-License-Identifier: AGPL-3.0-only -->
<!-- Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms. -->

# Pre-push validation of 44bd1eb8

- The unchanged Linux performance gate passed all 64 scenarios (exit 0).
- `scripts/build-win.ps1` on commit 44bd1eb8 passed (exit 0), including native and
  managed builds, scanner self-test, complete smoke suite, design checks, and
  scan-scale verification.
- An earlier pre-push Windows run had one borderline render-pool CPU-work failure:
  parallel 1260 ms vs serial 399 ms. The actual guard is `parallel <= 3 * serial +
  50 ms`, making that run's limit 1247 ms. All other checks in that run passed.
- The isolated `--perf` recheck passed unchanged: parallel 1169 ms vs serial 409 ms.
- The subsequent complete Windows build/smoke rerun passed unchanged: parallel
  1089 ms vs serial 415 ms. No timing limit, CPU guard, or acceptance label was
  changed to obtain the passing result.
- Windows logs remain local in `build/tempo-perf-publish-build-first.log`,
  `build/tempo-perf-publish-perf-recheck.log`, and `build/tempo-perf-publish-build.log`.
