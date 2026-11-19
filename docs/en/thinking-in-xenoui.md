# Thinking in XenoUI

[Documentation](index.md) · [State](state.md) · [Lifecycle](lifecycle.md) · [Side effects](side-effects.md) · [Layouts](layouts.md) · [Comparison with Jetpack Compose](compose-comparison.md)

XenoUI is a **declarative** UI framework: you describe *what the screen looks like* for a given state, not *how to
change it step by step*. This page sets out the mental model that every other guide builds on. If you come from
Jetpack Compose, most ideas will feel familiar; the important differences are called out
[below](#4-how-it-differs-from-jetpack-compose) and summarised in
[Comparison with Jetpack Compose](compose-comparison.md).

## Contents

1. [The declarative paradigm](#1-the-declarative-paradigm)
2. [A UI is a function of state](#2-a-ui-is-a-function-of-state)
3. [The two ways to update the screen](#3-the-two-ways-to-update-the-screen)
4. [How it differs from Jetpack Compose](#4-how-it-differs-from-jetpack-compose)
5. [Unidirectional data flow](#5-unidirectional-data-flow)
6. [Under the hood: entities and drawing](#6-under-the-hood-entities-and-drawing)
7. [Rules to remember](#7-rules-to-remember)

---

## 1. The declarative paradigm

With classic imperative views you build a tree of objects and then **mutate** it: `label.Text = "…"`,
`button.Visible = false`. Every mutation is a chance for the screen and the data to drift apart.

XenoUI reverses that. A **function** describes the screen from the data; when the data changes, the description is
redone or a bound value is updated:

```csharp
public override XenoComponent Body() =>
    Column(
        Text($"Total: {_count}", 24),
        Button("+1", () => _count.Value++)
    ).Gap(12).Padding(16);
```

You never go and find "the total label" to change it. The text is **bound** to `_count`: when the value changes,
it updates. The button only changes the state; the display follows.

## 2. A UI is a function of state

A page or a component is a function: same inputs, same output.

```csharp
public static XenoComponent Greeting(string name, bool online) =>
    Row(
        Box(10, 10).Background(online ? 0xFF22C55E : 0xFF9CA3AF).CornerRadius(5),
        Text($"Hello, {name}", 16)
    ).Gap(8).CrossAxis(CrossAxisAlignment.Center);
```

Consequences:

- **No hidden state** in the function: everything it depends on comes from its parameters or persistent fields.
- **No side effects**: `Body()` and components neither read disk or network, nor start tasks, nor change state.
  They can be called again at any time (rebuild, preview).
- **Predictability**: to understand a screen, read its function and the state it receives.

### Composing is calling functions

There is no `Button` class to instantiate and then add to a parent. `Column(a, b, c)` calls functions `a`, `b`, `c`,
which return `XenoComponent`s; `Column` adopts them as children. The tree is simply the trace of those calls. That
is why a component is an ordinary function that you can extract, name, parameterise and test.

## 3. The two ways to update the screen

This is the most important difference from what many developers expect. XenoUI **does not compare** the old and
the new tree to redraw only what differs. Two distinct mechanisms exist:

### 3.1 Fine-grained binding (`State<T>`)

A value bound to a `State` updates **a single entity** when it changes, without calling `Body()` again:

```csharp
private readonly State<int> _count = UseState(0);

Text(_count, 28)                                // text
Text($"Counter: {_count}")                      // reactive interpolated string
Box(10, 10).Background(_dotColor)               // State<uint>
LinearProgress(_progress, 4)                    // State<float>
```

This is the **fast** path: prefer it for anything that changes often (counters, progress, colours, messages).

### 3.2 Rebuild (`Rebuild()`)

When the **structure** changes (an element appears, a list changes content, a conditional branch flips), the page
calls `Rebuild()`: `Body()` is called again and the page's transient tree is replaced.

```csharp
private bool _expanded;

Button(_expanded ? "Collapse" : "Expand", () => { _expanded = !_expanded; Rebuild(); }),
_expanded ? Text("Details…") : XenoComponent.None
```

`Rebuild()` keeps nothing from the previous tree: **persistent objects** (states, scroll, text controllers) must
therefore live **outside** `Body()`, in page fields.

### 3.3 Which one to choose?

| What changes | Tool |
|---|---|
| A text, a colour, a progress value | Fine-grained binding: `State<T>` |
| Several independent values at once | Several `State<T>` |
| An element appears / disappears | `Rebuild()` |
| The content of a list changes | `Rebuild()` (then the virtualised list only recomposes the visible window) |
| A theme changes | `Rebuild()` + the scaffold's `RefreshChrome()` |

Rule of thumb: **start with a `State`; move to `Rebuild()` only if the shape changes.** Details:
[State and rebuilding](state.md).

## 4. How it differs from Jetpack Compose

| Compose idea | In XenoUI |
|---|---|
| Smart recomposition: only functions whose inputs changed run again | **No diff**: `Rebuild()` calls the whole page's `Body()` again; fine-grained updates go through `State<T>` |
| `remember { }` inside a composable | Persistent objects are **fields** of the page (or of the component's class); a `UseState` factory called in `Body()` creates a new object on each call |
| `mutableStateOf` automatically observed by every composable that reads it | A binding is **explicit**: `Text(state)`, `.Background(state)`; reading `state.Value` in `Body()` subscribes to nothing |
| Effects (`LaunchedEffect`, `DisposableEffect`) tied to composition | Async work is attached to the **page lifecycle** ([Side effects](side-effects.md)) |
| `CompositionLocal` | No equivalent: pass parameters or read `Application` / `Native` in the page |
| `Modifier` as a composable value (`Modifier.padding(8).clickable {}`) | Modifiers **chained on the result** (`Box().Padding(8).Clickable(…)`), which mutate the entity |
| Ambient `MaterialTheme` | The theme is **your own class**, applied explicitly ([Customising](customization.md)) |
| `rememberSaveable` / restoration after process death | **Not provided** yet; state is in memory |

These differences are not flaws on either side: XenoUI chooses simple, predictable execution with no per-frame
allocation over a system that tracks state reads.

## 5. Unidirectional data flow

As in modern UI architectures, data flows down and events flow up:

```text
      state (State<T>, parameters)           events (Action, callbacks)
  Model / Page  ─────────────────▶  Component  ─────────────────▶  Model / Page
        ▲                                                               │
        └───────────────────  updates the state  ◀──────────────────────┘
```

- A component **receives** what it displays (values, `State<T>`) and **declares** what happens (`Action`).
- It does not decide about navigation, deletion or saving: the page, or a model it owns, does.
- State has **a single source of truth**. If two places store the same data, one of them will be wrong.

See [Application architecture](app-architecture.md) for the split into layers (UI, models, services).

## 6. Under the hood: entities and drawing

You do not need this to use XenoUI, but it explains its guarantees:

- Every factory call (`Text`, `Column`…) creates an **entity** in a component engine (ECS); a `XenoComponent` is
  merely its identifier (8 bytes, no allocation).
- An in-house **layout** engine measures then places the entities (horizontal/vertical flex, stacks).
- A **Skia** renderer draws them; text and fields are drawn by XenoUI, the native keyboard goes through a typed
  bridge (`NativeTunnel`).
- Visual changes (colour, shadow) only mark the entity "to repaint"; size changes mark "to re-measure". That is
  what makes a fine-grained binding cheap.

Details: [Architecture](architecture/index.md).

## 7. Rules to remember

1. **`Body()` and components are pure**: same inputs, same output, no side effects.
2. **Persistent objects are fields** (`State`, `ScrollState`, `LazyListState`, `TextEditController`).
3. **A `State` first, `Rebuild()` second.**
4. **Data flows down, events flow up**; a single source of truth.
5. **Async work lives in the page lifecycle**, and its results come back on the UI thread.
6. **Never keep a `XenoComponent`** beyond the composition that created it.
7. **Everything is in dp** and colours are `0xAARRGGBB`.

## Going further

- [State and rebuilding](state.md): where to put state, hoisting, deriving, collections.
- [Lifecycle](lifecycle.md): application, pages, components.
- [Side effects and async work](side-effects.md): equivalents of `LaunchedEffect` and `DisposableEffect`.
- [Layouts](layouts.md): containers, sizes, alignment, lists.
- [Creating a component](components.md), [Customising](customization.md), [Animations](animations.md).
- [Learning from the SampleApp](sample-app.md): all of this in a complete application.
