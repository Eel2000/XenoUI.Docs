# Benchmark on a Samsung Galaxy A15 — 5 October 2026

[Index](index.md) · [Runtime](architecture/runtime.md) · [PC comparison](runtime-benchmark.md)

These measurements were run **on the phone**, not extrapolated from the PC. They are the mobile reference on `codex/runtime-critical-stabilization` (fixes and first measurements committed in `5f50e38`). Sections 1 and 2 keep the initial baseline; section 6 gives the new series after the allocation fix.

## Conditions

- Samsung SM-A155F, Android 16/API 36, ARM64, 8 logical processors visible to .NET.
- .NET 10.0.12, Release benchmark APK, without AOT or trimming, no debugger attached. This is not a measurement of an AOT/trimmed production package.
- Physical screen 1080 × 2340, density 2.8125; observed mode 90 Hz (available modes: 60/90 Hz, not 120 Hz).
- Phone connected and charging over USB. Android thermal status 1 before the measurements, 2 after; AP temperature read after the run: 44.8 °C. The results do not describe a phone cooled in a lab.
- No thermal, CPU or display frequency setting was forced.

## 1. CPU scenarios identical to the PC

CPU Skia bitmap 360 × 800, density 1, run on a worker on the phone. Three series of 500 operations after 200 warm-up operations per series.

| Scenario | Mean ms/op | p50 | p95 | p99 | Maximum | Managed bytes/op |
|---|---:|---:|---:|---:|---:|---:|
| Stable render, 100 texts | 0.400 | 0.388 | 0.462 | 0.507 | 1.008 | 0 |
| Navigation halfway, two scenes of 100 texts | 2.775 | 2.549 | 3.418 | 3.936 | 6.170 | 0 |
| Lazy 10,000 items, still window | 0.114 | 0.105 | 0.135 | 0.384 | 0.688 | 0 |
| Progressive scroll, 16 dp/op | 0.203 | 0.149 | 0.319 | 0.886 | 2.109 | 146.67 |
| Large jump to a disjoint window | 0.738 | 0.674 | 0.924 | 1.041 | 12.392 | 11,365.94 |
| 1,000 evaluations of a compiled transition | 0.283 | 0.282 | 0.292 | 0.299 | 0.330 | 0 |

Stable frames built no item/scene during the measurements. Progressive scrolling built 500 items over 1,500 operations, the jumps 76,466. The jumps triggered **3 Gen0 collections**, no Gen1/Gen2; the other scenarios recorded no collection in their measurement window. The 12.392 ms maximum is not attributed to a specific GC without a correlated trace.

The numeric simulation of one second of scrolling with 60/120 Hz steps reproduces a 0.001953125 dp gap on the A15. It is a simulation: this device's screen offers no 120 Hz mode.

## 2. Frames on the real OpenGL surface

Rendering on the phone's GL thread, 1080 × 2340 surface at density 2.8125. Per scenario: 120 warm-up frames then 300 measured frames, a single series. The figures below are the CPU time around `XenoEngine.Refresh`, not a GPU timer.

| Scenario | Mean CPU ms | p50 | p95 | p99 | Maximum | Managed bytes/Refresh |
|---|---:|---:|---:|---:|---:|---:|
| Stable render, 100 texts | 1.174 | 1.013 | 2.177 | 2.695 | 3.336 | 0 |
| Navigation halfway, two scenes of 100 texts | 6.484 | 6.411 | 8.351 | 8.760 | 9.326 | 0 |
| Lazy 10,000 items, still window | 0.447 | 0.377 | 0.829 | 0.930 | 1.038 | 0 |
| Progressive lazy, 16 dp/frame | 0.636 | 0.460 | 1.369 | 1.622 | 2.417 | 146.67 |

No construction in the stable scenarios; 100 items created over 300 frames of progressive scrolling. No GC collection in these four short windows.

| Scenario | Mean interval between GL callbacks | p95 | Maximum |
|---|---:|---:|---:|
| Stable | 11.108 ms | 13.517 ms | 14.860 ms |
| Navigation | 11.108 ms | 13.597 ms | 15.228 ms |
| Lazy still | 11.097 ms | 13.573 ms | 15.021 ms |
| Lazy progressive | 11.092 ms | 13.690 ms | 15.356 ms |

The intervals are consistent with a callback cadence close to 90 Hz, but **are not SurfaceFlinger presentation timestamps**. They prove neither 90 FPS displayed without jank nor a GPU cost below budget. At 90 Hz, the theoretical budget is 11.11 ms; a transition at 8.35 ms CPU at p95 leaves little margin for other costs.

## 3. SampleApp startup

The measurement series uses `am start -W`: the process is stopped before each cold launch, Home then resume for hot mode. `TotalTime` is Android's launch measurement; `WaitTime` is kept separately in the raw data.

This measurement does not replace `reportFullyDrawn`/TTFD and does not instrument the first presented Skia buffer. [Official Android definitions](https://developer.android.com/topic/performance/vitals/launch-time).

SampleApp installed in Release, without AOT or trimming, no debugger. Ten launches, all confirmed `COLD` by Android:

| Measurement | Mean | p50 | p95 | Minimum | Maximum |
|---|---:|---:|---:|---:|---:|
| Cold launch `TotalTime`, 10 runs | 1,268.3 ms | 1,259 ms | 1,339 ms | 1,237 ms | 1,339 ms |

Samples in order: 1339, 1260, 1241, 1254, 1276, 1259, 1274, 1237, 1258, 1285 ms. Percentiles use the upper rank (`ceil(n × p)`), without interpolation; with only ten runs, p95 is the observed maximum.

The ten resumes from Home brought the task to the foreground, with `LaunchState: UNKNOWN (0)` and **no `TotalTime` field**. Their hot start time is therefore **unavailable**, not zero. `WaitTime` is not used as a substitute. The collector keeps these missing values as `null` and excludes them from the statistics.

The previous bench was stopped before this series. The phone was still charging and warm: Android thermal status 2, AP 52.0 °C and skin 43.2 °C read during the series. These figures are a reference under these conditions, not a start on a cooled device nor a validation of a production AOT build.

## 4. Memory after the benchmark

`dumpsys meminfo` snapshot of the benchmark process after the scenarios:

- Total PSS: 199,932 KB Android (about 195.2 MiB).
- Total RSS: 281,563 KB (about 275.0 MiB).
- Native Heap PSS: 41,033 KB; heap size: 48,820 KB, allocated: 46,590 KB.
- Graphics: 57,131 KB, of which EGL mtrack 28,775 KB and GL mtrack 28,356 KB.
- Total swap PSS: 451 KB.

This snapshot includes the runtime, JIT, surfaces, caches and bench resources. It is neither SampleApp's memory, nor the high-water mark, nor proof of the absence of leaks. The bytes/op in the tables only measure the managed allocations of the thread running `Refresh`.

## 5. Reproduction

From the repository root:

```powershell
dotnet build tests/XenoUI.Android.Benchmarks -c Release -t:Rebuild -p:NuGetAudit=false
android run --device=R58X70F8NPN --apks=tests/XenoUI.Android.Benchmarks/bin/Release/net10.0-android/android-arm64/com.xenoui.benchmarks-Signed.apk --install-options=-r,-g
```

Wait for the `XenoBenchmark: GL_COMPLETE` log. The bench writes `runtime-benchmark.json` and `gl-benchmark.json` to `/sdcard/Android/data/com.xenoui.benchmarks/files/`, retrievable with `adb pull`. The run's results are also kept locally in `artifacts/a15-runtime-benchmark.json` and `artifacts/a15-gl-benchmark.json` (files ignored by Git).

For startup:

```powershell
dotnet build samples/SampleApp/SampleApp.csproj -c Release -f net10.0-android -t:Rebuild -p:RunAOTCompilation=false -p:AndroidEnableProfiledAot=false -p:PublishTrimmed=false -p:AndroidPackageFormats=apk
android run --device=R58X70F8NPN --apks=samples/SampleApp/bin/Release/net10.0-android/com.xenoui.sample-Signed.apk --install-options=-r,-g
./tools/benchmarks/Measure-AndroidStartup.ps1 -Adb '<absolute path to adb.exe>' -Device R58X70F8NPN -Samples 10
```

Raw result: `artifacts/a15-startup.json`. `-Reprocess` recomputes its statistics from the kept Android outputs without using the phone.

## 6. Allocation fix — new A15 series

Two recurring engine allocations were removed: the Lazy rollback journal is reused with its capacity kept, and the measurement font is reused per engine. The font size stays exact; the typeface is explicitly reset so as not to contaminate the measurement of the next text. Rollback of failing builders is still tested.

Same device, Release JIT packaging, dimensions and measurement protocols. Phone still charging: thermal status 2 before, 3 after the series; final readings AP 47.3 °C and SKIN 43.7 °C. The thermal variation forbids attributing any timing difference to the fix alone. Allocation counters are not replaced by an FPS comparison.

| Scenario | Before B/op | After B/op | CPU p95 after | Gen0 GC after |
|---|---:|---:|---:|---:|
| CPU, progressive scroll with new texts | 146.67 | 80 | 0.230 ms | 0 |
| CPU, random jumps with new texts | 11,365.94 | 4,880.30 | 0.578 ms | 0 |
| GL, progressive scroll with new texts | 146.67 | 80 | 0.920 ms | 0 |
| CPU, progressive scroll with a prepared constant label | not measured | 0 | 0.238 ms | 0 |
| CPU, random jumps with a prepared constant label | not measured | 0.73 | 0.474 ms | 0 |
| GL, progressive scroll with a prepared constant label | not measured | 0 | 0.924 ms | 0 |
| GL, alternating jumps with a prepared constant label | not measured | 0 | 1.836 ms | 0 |

The initial scenario is unchanged: formatting and concatenating the text in the builder, then first renders of distinct texts with a bounded Skia cache. Its allocations drop by 45.5 % in progressive scrolling and by 57.1 % for CPU jumps, but do not disappear. Builder strings and blob creation on cache misses remain allocating paths; this comparison is not a profiling that attributes each byte to a call stack.

The prepared diagnostic uses a single constant label, already rendered during warm-up, without preloading the 10,000 items or enlarging the 512-blob cache. It isolates materialisation: 100 items built over 300 progressive GL frames, 15,900 over 300 alternating jumps between indexes 1000/5000, with zero managed bytes measured. The 0.73 B/op residue of the **random CPU** jumps is not explained by an allocation trace: it stays open, and must not be rounded to zero.

On the new GL series: stable render p95 1.626 ms, navigation p95 8.602 ms, Lazy still p95 0.731 ms; zero managed bytes in these three scenarios. No Gen0/1/2 collection was observed in the measured windows of this series; this does not prove the absence of GC during a sustained session.

The Core suite passes **100/100** Release tests, with new tests for repeated text measurement and materialisation of disjoint windows with a constant label. Android builds of the benchmark and SampleApp: zero warnings, zero errors. A first incremental packaging of the benchmark failed in JNI before running the engine; a full rebuild allowed the retained run. No figure from that failed attempt is kept.

Local data kept separately: `artifacts/a15-runtime-after-allocation-fix.json` and `artifacts/a15-gl-after-allocation-fix.json`. The previous baseline is not overwritten.

Fixed SampleApp fully rebuilt and reinstalled on the A15: Home → Runtime Lab, vertical list scrolled to batches 6–9 and horizontal list to chips 6–9, display checked with screenshots. The tested PID stayed alive with no AndroidRuntime error in its filtered log. Its first incremental package had also failed in JNI before `OnCreate`; the functional run uses the rebuilt package, not the failing one. This smoke test is not an allocation measurement of SampleApp's complex items.

The next fixes require attributed profiling of Skia misses, the random-jump residue, complex item trees (child collections, callbacks, subscriptions) and a bounded recycling policy. The zero-allocation contract cannot arbitrarily include a developer builder that creates strings or closures on each item entry. Native allocations still need measuring separately.

## 7. Sustained GC collections on the A15 GL surface

The dedicated `gc-stress` mode runs the GL bench directly, without going through the CPU benchmark. It contains no call to `GC.Collect()` or waiting for finalizers. The Core/Android/iOS runtime does not contain any either; the comparative CPU benchmark uses one before each window, outside the published counters.

Per scenario: 120 warm-up frames then 2,700 measured frames, three successive scenarios in the same process. The `GC.CollectionCount(0/1/2)` counters are captured immediately after the last frame, **before** sorting, building the statistics and serialisation. Construction, warm-up, destruction and statistics are outside each window. No forced collection between scenarios; filling of the previous heap can therefore influence the next one.

`Refresh` allocations are counted on the GL thread. The `GC.GetTotalAllocatedBytes(false)` delta covers approximately **the whole process**, including native/SkiaSharp callbacks and other threads; it is not a measure of retained memory. GC counters also cover the process, not only `Refresh`. Per-generation columns must not be added up as a number of independent pauses.

This test counts collections, but does not measure their pause duration and does not precisely distinguish their origin. A frame's CPU time or its maximum is not a GC duration. ART/Java, native allocations and SurfaceFlinger presentation are not covered by the .NET counters.

A15 results, Release JIT, 1080 × 2340 surface, density 2.8125:

| Scenario | Window | Items built | B/Refresh | Total Refresh allocation | Gen0 | Gen1 |
|---|---:|---:|---:|---:|---:|---:|
| Progressive scroll, new texts | 29.963 s | 900 | 80.01 | 216,040 B | 0 | 0 |
| Random jumps, new texts | 29.970 s | 143,062 | 5,040.11 | 13,608,288 B | **3** | 0 |
| Alternating jumps, prepared constant label | 29.963 s | 143,100 | 0 | 0 B | 0 | 0 |

`GC.MaxGeneration = 1` on this Mono runtime: Gen1 is the maximum generation exposed. The Gen2 value kept in the JSON is not a supported third generation and is not used for the conclusion. The three Gen0 collections of the jumps amount to about 6 collections/minute **over this window**, not a prediction of the frequency over a whole session.

| Scenario | CPU Refresh p95 | p99 | Maximum |
|---|---:|---:|---:|
| Progressive scroll | 1.173 ms | 1.573 ms | 3.903 ms |
| Random jumps, new texts | 1.942 ms | 2.489 ms | 26.749 ms |
| Alternating jumps, prepared label | 2.025 ms | 2.344 ms | 4.968 ms |

The 26.749 ms maximum and the 31.448 ms maximum interval between callbacks in the allocating scenario are concerning, but no trace allows attributing them to a particular GC. This is not a measurement of GC pause duration nor of an actually presented frame.

The approximate global counter returns 0 for scrolling despite 216,040 B counted on the thread, and 13,255,208 B for the jumps. It is therefore not precise enough over these windows to quantify allocations outside `Refresh`; its zeros do not mean the process did not allocate. It remains a raw diagnostic, not an acceptance metric.

Android thermal status 3 before/during, 2 after; final readings AP 45.5 °C and SKIN 41.9 °C, still charging over USB. A single series, warm device. The bench was stopped after retrieving the report, then SampleApp brought back to the foreground. No AndroidRuntime error for the bench PID in the consulted log. Local data: `artifacts/a15-gc-stress.json`.

Reproduction after rebuilding/installing the benchmark:

```powershell
adb -s R58X70F8NPN shell am force-stop com.xenoui.benchmarks
adb -s R58X70F8NPN shell am start -W -n com.xenoui.benchmarks/com.xenoui.benchmarks.MainActivity --ez gc-stress true
# Wait for XenoBenchmark: GC_COMPLETE, then retrieve the report.
adb -s R58X70F8NPN pull /sdcard/Android/data/com.xenoui.benchmarks/files/gc-stress.json artifacts/a15-gc-stress.json
```

## Conclusion

Zero allocation is confirmed on the **tested** settled frames, and after the fix on GL materialisation with a prepared constant label. The scenario with new texts still allocates; the large jumps had caused three Gen0 GCs in the baseline, none in the new short window. The random-jump residue, presentation/jank traces, TTFD, sustained native memory and production packaging remain open.

The sustained test in section 7 does however show **three Gen0 GCs over 2,700 large GL jumps with new texts**: the absence of GC in a short window does not validate the zero-GC-pressure goal over the whole interaction.
