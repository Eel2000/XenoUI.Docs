# Runtime benchmark — 2026-10-05

[Documentation](index.md)

**PC comparison only.** The mobile reference is now the [Samsung A15 benchmark](runtime-benchmark-a15.md), measured on the physical device. Do not use the desktop numbers below as the phone's expected performance.

## Scope

Local Release benchmark of the current, uncommitted `codex/runtime-critical-stabilization` checkpoint. This is **not** a mobile FPS or GPU benchmark and is not a before/after comparison.


The benchmark is a mode of the framework's regression suite, run with tiered compilation disabled (`DOTNET_TieredCompilation=0`). It prints JSON with timings, allocations, GC counts and build counts.

- Host: Windows x64, build 26200; Intel family 6/model 170/stepping 4; 14 logical processors visible to .NET.
- Runtime: .NET 10.0.12, tiered compilation disabled, Release.
- Renderer: Skia CPU bitmap, 360 × 800, density 1.
- Per scenario: three sequential trials, each with 200 warm-up operations and 500 measured operations (1,500 measured total).
- Stopwatch elapsed time surrounds each operation. Buffers, delegates, sorting, result objects and JSON serialization are outside allocation measurement.
- Managed allocation is measured on the executing thread; native Skia memory and other-thread allocations are not measured. Collection counts are process-wide.
- Full collections occur **before**, never within, each measured trial.

## Results

Timings are milliseconds per operation; allocations are mean bytes per operation after warm-up.

| Scenario | Mean ms | p50 ms | p95 ms | p99 ms | Max ms | Managed B/op | Builds during 1,500 ops |
|---|---:|---:|---:|---:|---:|---:|---:|
| Static frame, 100 text nodes | 0.0960 | 0.0782 | 0.2136 | 0.2539 | 0.3176 | 0 | 0 |
| Navigation frame, two 100-text scenes | 0.9667 | 0.6866 | 2.0507 | 2.4609 | 3.4352 | 0 | 0 |
| Lazy 10,000 items, stationary window | 0.0571 | 0.0687 | 0.0864 | 0.1015 | 0.4439 | 0 | 0 |
| Lazy progressive scroll, 16 dp/op | 0.0474 | 0.0332 | 0.1188 | 0.1283 | 0.5548 | 144 | 500 items |
| Lazy disjoint-window jump | 0.1693 | 0.1407 | 0.3252 | 0.3896 | 0.6276 | 11,325.93 | 76,466 items |
| 1,000 compiled transition evaluations | 0.0712 | 0.0723 | 0.0888 | 0.1127 | 0.4719 | 0 | 0 |

No Gen0/Gen1/Gen2 collection occurred inside these measured windows. This does **not** imply an allocating scroll workload can run indefinitely without GC.

The two-scene navigation uses a 600-second slide, with its clock shifted on the cold path to approximately 50% before warm-up so both scenes are visibly present. Reflection is used only for this test setup, not during sampling. The transition did not finish during measurement. The compiled evaluator scenario has four tracks; each reported operation contains 1,000 evaluations, not one frame.

Trial mean times (ms) were:

| Scenario | Trial 1 | Trial 2 | Trial 3 |
|---|---:|---:|---:|
| Static | 0.1057 | 0.1025 | 0.0798 |
| Two-scene navigation | 0.7841 | 0.7536 | 1.3626 |
| Lazy stationary | 0.0693 | 0.0481 | 0.0538 |
| Lazy progressive | 0.0523 | 0.0475 | 0.0424 |
| Lazy jump | 0.1980 | 0.1308 | 0.1792 |
| 1,000 evaluations | 0.0684 | 0.0718 | 0.0733 |

These differences indicate host scheduling/load variance. Do not treat the decimal places as portable performance guarantees or convert these CPU timings into expected phone FPS.

## Scroll integration check

One second of simulated motion from offset −5,000 dp, velocity −1,800 dp/s:

- 60 Hz steps: −6,183.4204 dp.
- 120 Hz steps: −6,183.4224 dp.
- Difference: **0.001953125 dp**.

This validates numerical consistency for that trajectory, not display frame pacing or an actual 120 Hz hardware run.

## Conclusions and remaining gates

- Warmed static, navigation and stationary Lazy frames produced **zero measured managed allocations** and no scene/item builds.
- Scrolling that materializes new items is **not allocation-free**: 144 B/op averaged over progressive scrolling here (one new item every three operations), approximately 11.1 KiB per disjoint-window jump. These are structural cold-path operations, but they still occur during interaction and can cause GC pressure over a longer run.
- The next allocation investigation should profile materialization (item strings/nodes, rollback journal and resource-cache misses) rather than weakening rollback safety or hiding those operations from the report.
- Final approval still requires GPU/presentation frame-time measurements, a sustained scroll workload, native memory high-water marks, input/IME pressure and physical 60/120 Hz devices. No such claim is made by this report.

The normal Release regression suite is run separately to ensure benchmark mode does not replace correctness tests.
