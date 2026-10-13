# XenoUI v1 — roadmap and architecture decisions

> **Historical roadmap.** Some points are implemented, changed or abandoned. The current audited trajectory is [Decisions and the V1 trajectory](architecture/decisions-v1.md).

## Executive summary

XenoUI already has a distinctive technical direction: a declarative C# API, a compact entity tree, a home-grown layout and SkiaSharp rendering shared by Android and iOS. The core is advanced enough to justify a v1, but the priority must be the reliability of internal contracts rather than a rapid increase in the number of widgets.

The v1 must make it possible to build and publish a modest-sized mobile application with navigation, state, lists, simple forms, images and themes. It must be reproducible from a clean clone, work in Release with trimming, expose usable minimal accessibility and keep memory consumption stable across repeated rebuilds and navigations.

The plan sets six milestones: reproducible build, memory safety, state and invalidation, layout/render/input consistency, scrolling and virtualisation, then controls/accessibility/publishing. Each milestone has observable exit criteria. Variable-size virtualisation now has a first implementation; its stabilisation and benchmarks stay in the V1 gate. A complete positional hooks system remains postponed.

## 1. State of the framework

The engine stores its components in parallel native slabs. An entity's identifier currently corresponds to the index shared between styles, transforms, layout caches, visuals, text, interactions and images. This representation is simple, compact and favourable to sequential iteration.

The composition model rebuilds the tree on a structural change. Pages keep their fields between navigations, while ECS data is reset then emitted again by `Body()`. Layout follows two phases, bottom-up measure then top-down arrange. Rendering walks the tree and draws with SkiaSharp. Input does its own hit-test.

This architecture remains viable for v1. It must however make explicit three contracts that are implicit today: how long an identifier is valid, who owns state and subscriptions, and when a mutation may touch the data used for a frame.

The main risks observed in the repository are:

| Area | Current risk | v1 consequence |
|---|---|---|
| Native memory | partial lifecycle, access without bounds checking, former double allocation of the image slab | native corruption or leak |
| State | `UseState()` creates an object on each `Body()` call and subscriptions are not scoped | lost state and stale callbacks |
| Layout | the cache does not clearly represent input constraints | stale positions after a sibling or the viewport changes |
| Scroll | `.Scrollable()` and the `Lazy*` layouts do not have the same contract; rewindowing exists but is not triggered | blank areas and misleading examples |
| SDK | task loaded from `bin/Debug`, manifests generated and consumed inconsistently | clean clone not reproducible |
| Release | application discovered through global reflection with full trimming | startup broken in Release only |
| iOS | `SKGLView` is flagged obsolete by the .NET analyser in favour of Metal | backend to migrate before the RC |
| Accessibility | a drawn surface does not yet provide a semantic hierarchy | application unusable with a screen reader |

## 2. The v1 contract

The v1 targets Android and iOS with a single main XenoUI surface per window. It guarantees the `Column`, `Row`, `ZStack`, `Box`, `Text`, `Image` and `Spacer` primitives, explicit constraints, margins and paddings, the documented alignments and consistent rendering at different densities.

The standard library must provide at least a button, a simple text input, a checkbox or switch, a progress indicator and list surfaces. Controls must share the `normal`, `pressed`, `focused` and `disabled` states where relevant. Their size, colour and content adapt to the theme and to the text size configured by the system.

Navigation includes `push`, `back` and replacing the active page. Page state survives a round-trip navigation. Asynchronous changes request a frame without any further interaction. The v1 defines an owner thread for the engine: mutations coming from other threads are published to it before the next build or render.

Simple scrolling applies to any flow container. Virtualised lists accept a typed source, an optional builder and an optional stable key. Their measurement index learns variable sizes without asking the application for a default height or width. Keeping the anchor exactly during every source mutation remains to be stabilised.

v1 compatibility means that a public API not marked experimental stays compatible across the whole `1.x` series. ECS types, slabs and direct references to their elements stay internal. Future extensions go through documented abstractions rather than exposing pointers or internal collections.

## 3. Principles adopted

### 3.1 Separate description, instance and rendering

Declarative rebuilding must not mean that every logical identity disappears. Frameworks with efficient rebuilding keep a layer of instances that own state and render objects. Flutter, for example, matches children by type and key and reuses the corresponding elements; this reuse precisely preserves state and layout information.^1

For XenoUI v1, general reconciliation is not mandatory. However, pages, states, subscriptions and virtualised entries must have an explicit identity. A rebuilt component must never silently reuse the identity of another component created at the same index during a later rebuild.

### 3.2 Constraints are part of the layout cache

A measurement is only valid for the constraints that produced it. A desired-size cache must therefore remember, directly or by version, the maximum constraints received. A new constraint invalidates the subtree even when style and content have not changed.

Arrangement follows the same rule: a child must receive its new position when the parent changes how space is distributed. The child's content being clean does not allow skipping the write of its new bounds. It can only avoid recomputing its descendants if their constraints and origin stay identical.

### 3.3 Deterministic resource ownership

The engine creates the slabs, Skia caches, images and fonts; it is therefore responsible for releasing them. The platform view owns the engine and must release it with its own lifecycle. Microsoft recommends that `Dispose()` be idempotent: later calls must do nothing.^2

References returned by a slab are short borrows. They must not outlive `Push`, `Clear`, `Reset` or `Dispose`, because a resize can move the native block. The v1 must document this rule and restrict these references to internals.

Eventually, an entity handle must combine index and generation. When a slot is reused, its generation changes; any operation carrying an old handle fails deterministically. This property matters more than keeping an eight-byte handle.

### 3.4 A frame is a transaction

Building, layout, hit-testing and rendering must observe a consistent version of the tree. Application mutations are collected then applied before a frame. A callback does not reset the slabs while they are being walked.

This contract simplifies future animations, asynchronous tasks and multi-touch. It also makes tests reproducible: a mutation produces at most one scheduled invalidation, then one observable frame.

### 3.5 Measuring performance

The "zero-GC" wording is too broad to describe the framework: pages, strings, callbacks, dictionaries, reactive state and asset decoding legitimately use the managed heap. The v1 uses measurable goals: no recurring managed allocation in a stable render frame, no unbounded memory growth during a defined scenario, and a frame time compatible with 60 Hz on the reference devices.

## 4. Roadmap

### Milestone 1 — Reproducible build and validation lab

The SDK must build its tasks before using them or embed them as package assets. No consuming project may depend on a `bin/Debug` path. Properties, targets, platform entry points, manifests and assets must be placed in NuGet packages that can be tested locally. SDK-style projects natively have the `pack` target, and Microsoft recommends testing package installation in a real consuming project.^3

Manifest generation is separated per platform. Android permissions are placed at the valid level of the document, without prefixing an already qualified value. The produced files are explicitly linked to the relevant `AndroidManifest` or `ApplicationManifest` properties. An integration application consumes only the generated packages, never the repository's project references.

The pipeline runs the core tests, builds Debug and Release, produces the packages, creates the integration application, publishes with trimming then runs at least smoke tests on Android. The iOS build runs on a macOS runner.

The iOS backend moves from `SKGLView` to the Metal view supported by SkiaSharp. This migration must keep the pixel/logical unit conversions, the frame scheduler and the native resource release cycle.

**Gate M1**

- clean clone, restore and build without pre-existing artefacts;
- package installable in an empty project;
- Android and iOS startup in Release;
- manifests inspected and application identities correct;
- .NET versions and dependencies pinned.

### Milestone 2 — Memory, handles and lifecycle

The first batch of this milestone makes the slab bounded and idempotent, removes a lost allocation from the image slab and ties `XenoEngine.Dispose()` to the destruction of platform views. The rest introduces a generational handle, removes `int` identifiers from the public API and adds a check that the handle belongs to the engine.

String and callback tables must reuse or replace their entries on fine-grained updates. Subscriptions are owned by a scope and removed when that scope is unmounted. Skia caches have a size or purge policy so that an application generating dynamic colours or shadows does not create permanent growth.

**Gate M2**

- bounds, growth, reset and double-dispose tests;
- old handle refused after a rebuild;
- 1,000 navigations and rebuilds without persistent native growth;
- repeated view creation/destruction without callbacks into a released engine;
- memory profile kept as a CI artefact.

### Milestone 3 — State, invalidation and the frame cycle

The v1 first adopts state explicitly owned by a page or a component. An API such as `State<T> CreateState(T initial)` registers the state in the owner scope and provides a disposable subscription. `UseState()` must only stay public once a stable slot identity exists; otherwise it is removed or marked experimental.

Each change calls a frame scheduler common to the platforms. The scheduler merges multiple requests and runs the mutation on the engine thread. Asynchronous data callbacks do not access the slabs directly.

**Gate M3**

- state kept after rebuild and round-trip navigation;
- asynchronous update visible without a touch event;
- no notification from an unmounted component;
- several synchronous changes produce a single scheduled frame;
- callback exceptions reported without corrupting the current tree.

### Milestone 4 — Layout, render and input consistency

The layout cache remembers the constraints and a content version. `Auto` is represented separately from the numeric value zero. `AlignSelf` has an `Auto` state distinct from an explicit `Start` alignment. The engine tests empty, constrained, over-constrained, nested and resized layouts.

Hit-testing reuses the paint order or an index produced by the same traversal. It applies clips and scroll offsets. A `Cancel` gesture cancels the interaction without invoking the callback. Text is measured and drawn with the same typeface, the same metrics and an explicit wrapping and ellipsis policy.

**Gate M4**

- snapshots or geometric assertions on the primitives;
- changing a sibling repositions the following elements;
- correct hit-testing with overlap, clipping and scroll;
- no click after `Cancel`;
- Latin, RTL, emoji text and system scale changes covered.

### Milestone 5 — Scrolling and virtualised lists

The `Scrollable` trait describes viewport behaviour; it does not depend on the value of `LayoutDirection`. Non-virtualised containers scroll their whole content. `LazyColumn<T>` and `LazyRow<T>` take a source, an optional typed builder, an optional stable key and an optional state. No item size is required.

The viewport computes the visible indexes from the offset and the item size. Children are positioned at their global coordinate, including when the first created child does not carry index zero. Crossing the overscan triggers rewindowing at the frame boundary. Removed elements release their scopes.

Velocity is expressed per unit of time and integrated with the real delta between frames. Behaviour thus stays close at 60, 90 and 120 Hz. Source changes clamp the offset and keep the anchor item where possible.

**Gate M5**

- list of 10,000 elements traversed in both directions without blank areas;
- number of entities bounded by the viewport and overscan;
- insertion, deletion and reordering with stable keys;
- comparable fling at several refresh rates;
- nested scrolling explicitly supported or rejected with a diagnostic.

### Milestone 6 — Controls, accessibility and publishing

A fully drawn surface must provide its own semantic tree. Android recommends a virtual hierarchy for views containing several logical elements and requires that a custom touch action also be exposed to accessibility services, notably through `performClick()` for clicks.^4 The Android bridge can rely on `ExploreByTouchHelper`; iOS must expose a container and matching accessibility elements.^5

Text input needs the system keyboard, IME composition, selection, clipboard and autofill. The v1 favours a bridge to invisible native services or controls rather than a complete text editor implemented in Skia.

A reference application checks navigation, list, search, form, loading, error and theme. It consumes the produced packages. The documentation only describes guaranteed features and gives their platform status.

**Gate M6**

- reference flow completed on Android and iOS;
- controls usable with TalkBack and VoiceOver;
- focus, keyboard, text size and disabled state checked;
- packages signed and versioned with SemVer;
- migration guide, compatibility policy and limitations published.

## 5. API decisions

| Topic | v1 decision | Rationale |
|---|---|---|
| Identity | internal index + generation handle | blocks stale references |
| State | explicit ownership and disposable subscriptions | predictable behaviour before a full hooks system |
| Rebuild | whole page for structural changes | keeps the current simplicity |
| Fine-grained update | reserved for values with a live scope | avoids callbacks into recycled slots |
| Dimensions | `Auto` type or sentinel, zero stays a valid dimension | removes the current ambiguity |
| v1 list | automatically measured size, typed source, stable key recommended | verifiable, efficient virtualisation |
| Input | native bridge | realistic IME and accessibility |
| Extensibility | closed internals, public factories and modifiers | allows evolution without freezing the slabs |
| Performance | budgets measured per scenario | verifiable commitments |

## 6. Test strategy

Unit tests cover memory structures, constraint computation, arrangement, navigation and state. They avoid reproducing the implementation line by line and check invariants: aligned slabs, invalidated handles, finite geometry, stable order and no callback after unmounting.

Integration tests mount an engine with a controlled render backend, run input sequences and inspect the final tree. Visual tests render a small corpus of screens at fixed densities and compare the images with a tolerance suited to Skia and fonts.

Platform tests start the packaged application, navigate, scroll, type text and activate accessibility nodes. An application published with trimming is required: Microsoft notes that analysing a library on its own does not have the implementations of all its dependencies and also recommends a standalone trimmed test application.^6

Benchmarks publish at least: median and p95 build/layout/render time, managed allocations per stable frame, native memory after stabilisation, the number of entities in a virtualised list and Release startup time. Initial thresholds are set after a baseline, then any regression beyond the defined tolerance blocks the relevant milestone.

## 7. Sequencing and versions

| Version | Content | Promotion condition |
|---|---|---|
| `0.1-foundations` | M1 and M2 | clean build and safe memory |
| `0.2-state-layout` | M3 and M4 | state/layout/input contracts covered |
| `0.3-scroll` | M5 | large list stable and bounded |
| `1.0.0-beta.1` | start of M6 | functional reference application |
| `1.0.0-rc.1` | M6 complete | packages and platform flows validated |
| `1.0.0` | API frozen | no blockers and documentation aligned |

A release is not promoted on the basis of a successful compilation alone. Every gate of the milestone must produce kept evidence: test report, package, published application, memory profile or accessibility result as appropriate.

## 8. Outside the v1 scope

The v1 does not promise desktop, web, advanced multi-window, a general animation engine, full keyed preservation of virtualised subtrees, a rich editor, CSS compatibility, a visual designer or a framework-specific hot reload. This work can start experimentally, but it does not gate the release and must not widen the public contracts before the foundations are stabilised.

## 9. Sources

1. Flutter. "[Inside Flutter](https://docs.flutter.dev/resources/inside-flutter)," sections *Linear reconciliation* and *Tree surgery*. Accessed September 2026.
2. Microsoft. "[Implement a Dispose method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)." Accessed September 2026.
3. Microsoft. "[Create a NuGet package using MSBuild](https://learn.microsoft.com/en-us/nuget/create-packages/creating-a-package-msbuild)." Accessed September 2026.
4. Android Developers. "[Make custom views more accessible](https://developer.android.com/guide/topics/ui/accessibility/views/custom-views)." Accessed September 2026.
5. Apple Developer. "[UIAccessibilityContainer](https://developer.apple.com/documentation/uikit/uiaccessibilitycontainer)." Accessed September 2026.
6. Microsoft. "[Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)." Accessed September 2026.
