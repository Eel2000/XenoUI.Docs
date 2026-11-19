# Performance

[Documentation](index.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [State](state.md) · [Layouts](layouts.md) · [A15 benchmark](runtime-benchmark-a15.md)

This guide gathers the practices that keep a XenoUI application smooth: **where the engine is already
economical**, what really costs, how to structure state and lists, and how to measure without fooling yourself. It
reads like the "Performance" page of a declarative UI guide: simple rules first, details after. For the engine's
dirty-flag mechanics see [Runtime §4](architecture/runtime.md#4-dirty-flags-and-fine-grained-updates); for the
recomposition roadmap, [Decisions §5](architecture/decisions-v1.md#5-future-recomposition-design-constraints).

## Contents

1. [The frame budget](#1-the-frame-budget)
2. [What the engine does for you](#2-what-the-engine-does-for-you)
3. [The ten rules](#3-the-ten-rules)
4. [Fine-grained binding or `Rebuild()`](#4-fine-grained-binding-or-rebuild)
5. [Lists](#5-lists)
6. [Text, images and shadows](#6-text-images-and-shadows)
7. [Animations](#7-animations)
8. [Work off the UI thread](#8-work-off-the-ui-thread)
9. [Measuring](#9-measuring)
10. [Runtime: CoreCLR and Mono](#10-runtime-coreclr-and-mono)
11. [Checklist](#11-checklist)

---

## 1. The frame budget

At 60 Hz a frame lasts ~16.7 ms; at 120 Hz, ~8.3 ms. Within that time the UI thread must: handle input, run
callbacks, re-measure/re-arrange what is "dirty", then draw. Going over budget produces jank. The general rule:
**do little per frame, and never heavy work on the UI thread**.

## 2. What the engine does for you

- **No allocation per frame** on stable frames: rendering, layout and text reuse their buffers (a contract tested by
  the framework's suite).
- **Dirty flags**: a colour or shadow change does not trigger layout; a size change only re-measures what depends
  on it.
- **Virtualisation**: `LazyColumn`/`LazyRow` only compose the visible window plus a margin.
- **Cached text**: formatted paragraphs, fonts and decoded images are cached.
- **Precompiled transitions**: `NavTransition`, `NavigationSelectionTransition` and `OverlayAnimation` are track
  arrays evaluated with no call into your code.
- **No tree diff**: there is no comparison cost; there is, however, the cost of **recomposing the whole page** on each
  `Rebuild()` (see §4).

Your role: not to undo these guarantees with work in `Body()`, non-virtualised lists or bursts of rebuilds.

## 3. The ten rules

1. **A `State` first, `Rebuild()` only if the structure changes.**
2. **Do not rebuild during a gesture** (scrolling, dragging, typing).
3. **Use `LazyColumn`/`LazyRow`** as soon as a list exceeds a few dozen items.
4. **Give every list item a stable key.**
5. **Keep `Body()` and item functions fast**: no network, disk, heavy LINQ or expensive formatting.
6. **Do not create persistent objects in `Body()`** (`UseState`, `UseScroll`…): lost state and allocations.
7. **Build transitions and animations once** (`static readonly`).
8. **Move heavy work off the UI thread** and come back through the dispatcher.
9. **Avoid animating sizes** (width, `FlexGrow`) on large containers; prefer colours and alpha, or the scenes the
   framework animates.
10. **Measure in Release, without a debugger, on a real device.**

## 4. Fine-grained binding or `Rebuild()`

| | Fine-grained binding (`State<T>`) | `Rebuild()` |
|---|---|---|
| What is redone | One entity (text, colour) | The whole page's `Body()` and its scene |
| Cost | Very low | Proportional to the page size |
| State loss | No | Objects created in `Body()` |
| Use for | Values that change often | Rare structural changes |

A concrete example, `PeoplePage`: tapping "Follow" updates **two row `State`s** and **one `State` of the counter at
the top**; there is no `Rebuild()` even though the list has 10,000 items.

Conversely, a `Rebuild()` for each typed character recomposes the whole page: use `Text(_preview)` bound to a
`State<string>` updated by `controller.Changed`.

**Split heavy pages**: a very large page rebuilt entirely for one detail costs more than several pages or a
virtualised list. Isolate the frequently changing parts in `State`s.

## 5. Lists

```csharp
LazyColumn(items, ItemRow, key: item => item.Id)    // visible window only, measured sizes
```

- **Stable key**: without a key, identity is the index; an insertion at the head re-gives every item.
- **Light item function**: it is called again when the window moves. Precompute (date formatting, labels) in the model,
  not in the function.
- **No `Rebuild()` on each scroll**: the list manages its window by itself.
- **Bounded list height** (`FlexGrow(1)` or `Height`) to avoid materialising too many items.
- **Per-row states**: create them in the item function (alive while the row is materialised) and keep the truth in a
  page field ([State §8](state.md#8-per-list-item-state)).
- **Images in lists**: explicit sizes (`Size`) to avoid re-measures; decoding once per asset.
- For a full-screen feed (one page = one screen), `VerticalPager` rather than a `LazyColumn`: a single active page
  carries the expensive resource (video), the others show a poster.

## 6. Text, images and shadows

| Element | Advice |
|---|---|
| Long text | `MaxLines(n, ellipsis: true)`; avoid `AutoSize` on large blocks |
| Text that changes often | `Text(State<T>)`: only that text is re-measured |
| Images | `Image("name")` decodes once; `ImageFile(State<string?>)` caches by path; size explicitly |
| Shadows | `Elevation(0)` removes the shadow; a zero blur is skipped; avoid very large shadows on dozens of elements |
| Scene opacity | A scene with `Opacity < 1` goes through a composited layer: limit it to short transitions |
| Clip / `CornerRadius` | Cheap; avoid stacking them with big shadows |
| Icon fonts | One font for all icons; glyphs rather than images for simple icons |

## 7. Animations

- **Scene** animations (pages, navigation selection, overlays) are the cheapest: no allocation, no re-layout.
- An application animation (timer + `State`) costs **one `Post` and one invalidation per step**: bound its duration
  and frequency; animate colours rather than sizes ([Animations §5](animations.md#5-animating-yourself-step-by-step)).
- **Reduced motion**: honour it (duration 0); it also reduces work.
- Cancel a timer before starting a new one on the same property.

## 8. Work off the UI thread

Anything beyond a few milliseconds leaves the UI thread: network, JSON, disk, sorting, computation. Come back with
`IUiDispatcherCapability.Post`, publish an **immutable snapshot**, and only touch `State`s on the UI thread
([Side effects](side-effects.md)). The sample even parses JSON off the UI thread (`Task.Run` in `RemoteFeed<T>`).

Beware of `Clickable` callbacks: a `Task.Run(...).Wait()` or synchronous disk access freezes the interface.

## 9. Measuring

1. **Release**, no debugger attached, on a **real device** (an emulator does not reflect the GPU).
2. **Same conditions** for each measurement: brightness, battery-saver mode, thermal state (let it cool between
   series).
3. **Separate** the render thread's CPU measure (Refresh) from GPU times; the repository measures `Refresh` CPU time.
4. **Repeatable scenarios**: continuous scrolling, repeated navigation, typing, tab switching.
5. **Allocation**: on stable frames the framework's goal is zero bytes; watch Gen0 collections during a scenario.
6. **Reliability**: several series, median and p95, not a single run.

The repository provides tools: `tests/XenoUI.Core.RegressionTests --benchmark` (PC CPU), the
`tests/XenoUI.Android.Benchmarks` APK (device) and the method detailed in [A15 benchmark](runtime-benchmark-a15.md)
and [Runtime benchmark](runtime-benchmark.md). These figures are **dated** and measured on a specific runtime: they
are a reference point, not a promise.

## 10. Runtime: CoreCLR and Mono

Android applications on this branch run on **CoreCLR** (`UseMonoRuntime=false`) with .NET 11; going back to Mono is
done with `UseMonoRuntime=true` ([Developer guide §2](developer-guide.md#2-prerequisites)). The figures published in
the benchmarks date from .NET 10 on Mono: **re-measure on your target** before concluding, notably cold start and
application size. Publish options (AOT, trimming) change the profile a great deal: state them with your numbers.

## 11. Checklist

- [ ] No `UseState`/`UseScroll`/`UseTextEdit` in `Body()`.
- [ ] Every list of more than ~30 items is a `LazyColumn`/`LazyRow` with a stable key.
- [ ] Frequently changing values are bound `State`s, not `Rebuild()`s.
- [ ] No `Rebuild()` in a gesture, a keystroke or a scroll callback.
- [ ] No blocking work in `Body()`, `Clickable` or item functions.
- [ ] Transitions and animations created once.
- [ ] Images sized; long texts bounded (`MaxLines`).
- [ ] Reduced motion honoured.
- [ ] Measured in Release on a device, several series.
