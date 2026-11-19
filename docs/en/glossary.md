# Glossary

[Documentation](index.md) · [Tutorial](tutorial-first-app.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [FAQ](faq.md)

Terms used across the documentation, in alphabetical order.

| Term | Meaning |
|---|---|
| **Action** | A C# `Action` delegate passed to a component to report an event (a tap, a choice). Events flow **up** as actions; data flows **down** as values and states. |
| **Anchor / anchored overlay** | A [popup](component-catalog.md#overlays) placed next to an element (usually the one last tapped) and following it when it moves. |
| **App (`XenoApp`)** | The application class: registers pages in `OnStart`, owns shared services, receives lifecycle callbacks. See [Lifecycle](lifecycle.md). |
| **Arrange** | The second layout pass: the parent gives each child its final position and size. See [Layouts](layouts.md). |
| **Body** | The method of a page (or component class) that returns its interface. It must be pure: no side effects, no persistent objects created inside. |
| **Bound value** | A value tied to a `State<T>` (for example `Text(state)`): it updates when the state changes, without rebuilding. |
| **Capability** | A typed OS service exposed through `NativeTunnel` (clipboard, text input, UI dispatcher, media…). See [Native capabilities](native-capabilities.md). |
| **Chrome** | The persistent navigation surfaces around pages: bottom bar, rail, drawer, top bar. |
| **Component** | A function that returns a `XenoComponent`. Reusable, parameterised, with no lifecycle of its own. See [Creating a component](components.md). |
| **Composition** | The act of running `Body()` and component functions to build the interface tree. |
| **Cross axis** | The axis perpendicular to a `Row`/`Column`'s flow (vertical for a `Row`, horizontal for a `Column`). |
| **Destination** | One top-level entry of the navigation scaffold (a tab). It keeps its own stack and its scene. |
| **dp** | Density-independent pixel, the logical unit used for every size (points on iOS). |
| **ECS (entity-component system)** | The data-oriented engine under XenoUI: each element is an *entity* with *components* (style, visual, text…). You never touch it directly. |
| **Elevation** | A Material-style shadow computed from a value in dp: `.Elevation(4)`. |
| **Entity** | The engine's record of one element. A `XenoComponent` is its 8-byte handle. |
| **Flex / `FlexGrow`** | Distribution of the remaining space among children along the main axis. |
| **Hoisting (state hoisting)** | Moving state out of a component into its caller, so the component receives a value and exposes events. See [State](state.md). |
| **Hook (`Use…`)** | A factory such as `UseState` or `UseScroll` that creates a persistent object. Call it in a **field initialiser**, never in `Body()`. |
| **IME** | Input Method Editor: the native keyboard and its text composition. XenoUI uses it for its custom-painted fields. |
| **Key** | A stable identity given to a list item (`key: item => item.Id`) so the list can track it as its window moves. |
| **Lazy list** | A virtualised list (`LazyColumn`, `LazyRow`) that only composes the visible window. |
| **Main axis** | The axis along which a `Row`/`Column` lays out its children. |
| **Measure** | The first layout pass: each element computes the size it wants. |
| **Modifier** | A chained call that mutates an element (`.Padding(8)`, `.Background(…)`, `.Clickable(…)`). Order rarely matters. |
| **Modal** | An overlay that blocks the page behind it until dismissed (alert, dialog, sheet). |
| **`NativeTunnel`** | The typed bridge between XenoUI and platform services. See [Native capabilities](native-capabilities.md). |
| **`None` (`XenoComponent.None`)** | "Nothing": modifiers on it are no-ops and containers skip it. Used for conditional rendering. |
| **Overlay** | UI shown above the page and chrome: snackbar, alert, dialog, popup, bottom sheet. |
| **Page (`XenoPage`)** | A navigable screen. Instantiated **once**, so its fields persist. |
| **Persistent object** | State that must survive rebuilds (`State`, `ScrollState`, `LazyListState`, `TextEditController`). Always a field. |
| **Preview (`[Preview]`)** | An attribute that lets the IDE or a test render a component or page headlessly. See [Testing](testing.md). |
| **Rebuild (`Rebuild()`)** | Runs the active page's `Body()` again and replaces its scene. Use it when the *structure* changes; use a `State` for values. |
| **Retained scene** | A page tree kept mounted (for example a tab you left) so returning to it is instant and state is preserved. |
| **RTL** | Right-to-left layout (Arabic, Hebrew). The subtree mirrors; prefer `Start`/`End` to left/right. |
| **Scaffold (`NavigationScaffoldState`)** | The adaptive shell that hosts destinations and picks bottom bar, rail or drawer. |
| **Scene** | The rendered tree of a page, overlay or chrome. Scene-level animation fades, moves or clips a whole scene. |
| **Skia** | The 2D graphics library XenoUI draws with (through SkiaSharp). |
| **Slot** | A parameter that carries content (`XenoComponent` or `Func<…, XenoComponent>`) so a component can host the caller's UI. |
| **Snapshot** | An immutable value published by a model (for example `RemoteSnapshot<T>`) and read by a page. |
| **State (`State<T>`)** | An observable value. Assigning a different value notifies bound elements. |
| **Track** | One animated property in a transition: from, to, time window and easing. |
| **UI thread** | The thread on which `State`s are assigned and callbacks run. Bring background results back with `IUiDispatcherCapability.Post`. |
| **Virtualisation** | Building only what is visible. |
| **`XenoComponent`** | The 8-byte handle returned by every factory and modifier. Valid only within the composition that created it. |
| **ZStack** | A container that overlaps its children; the last one is on top. |
