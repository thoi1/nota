# PR #549 warped split/undo benchmark

Before: 8abb3ad1. After: bc131d63fe9bc52fa45fb9a717b0bd28c4ca07ec (Track construction reuses its existing Session player).

AMD Ryzen 9 7900X 12-Core Processor, 24 logical CPUs; Windows_NT 10.0.26300

Release MSVC build; 9 alternating-order rounds. Identical harness executable for both DLLs, copied to separate folders. Scenario from tests/perf/nota_perf.cpp, amplified to 1000 cycles of 8 splits and 8 undos; split fades disabled as in the Linux instruction gate. Only split/undo time is measured.

| Revision | Median us/edit | Min | Max |
|---|---:|---:|---:|
| Before | 3.818 | 3.731 | 4.525 |
| After | 3.903 | 3.740 | 4.345 |

Median change: 2.2%. These are Windows wall times, not Valgrind instruction counts; Linux CI must verify the instruction limit.
