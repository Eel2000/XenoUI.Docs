# State and rebuilding

[Documentation](index.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [Lifecycle](lifecycle.md) · [Side effects](side-effects.md) · [Architecture](app-architecture.md)

**State** is any value that can change during the application's life and that the interface depends on: a
counter, a field's text, the list of loaded items, the selected tab. This guide explains where to put it, how to
expose it to a component, how to derive from it, and when it triggers a rebuild.

## Contents

1. [State objects](#1-state-objects)
2. [Where to keep state](#2-where-to-keep-state)
3. [Hoisting state](#3-hoisting-state)
4. [Binding state to the UI](#4-binding-state-to-the-ui)
5. [When to call `Rebuild()`](#5-when-to-call-rebuild)
6. [Derived values](#6-derived-values)
7. [Collections and objects](#7-collections-and-objects)
8. [Per-list-item state](#8-per-list-item-state)
9. [Keeping state](#9-keeping-state)
10. [Common pitfalls](#10-common-pitfalls)

---

## 1. State objects

| Object | Factory | Use |
|---|---|---|
| `State<T>` | `UseState(initial)`, `UseStates(n, initial)` | An observable value bound to the UI |
| `ScrollState` | `UseScroll()` | A container's scroll position |
| `LazyListState` | `UseLazyList()` | Position, page and window of a virtualised list or pager |
| `TextEditController` | `UseTextEdit("initial")` | A field's text, selection and history |

They share three rules:

1. **They are persistent**: you create them once and keep them.
2. **They are never created in `Body()`**: the `Use…` factories return a **new** object on each call; in `Body()`
   you would create a new one at every rebuild, and the state would go back to its initial value.
3. **They are modified on the UI thread.**

### `State<T>`

```csharp
public class State<T>
{
    public event Action? OnChanged;     // raised when the value actually changes
    public T Value { get; set; }        // assigning an equal value is ignored
}
```

Equality uses `EqualityComparer<T>.Default`: reassigning the same value is free and notifies nobody. For a mutable
reference type (a `List<T>` modified in place), **the value does not change** as far as equality goes: see
[§7](#7-collections-and-objects).

## 2. Where to keep state

| Scope | Where | Example | Lifetime |
|---|---|---|---|
| One screen | **Page field** | `_scroll`, `_category`, a `TextEditController` | The page's (the whole application, since pages are instantiated once) |
| A component with internal state | **Field of a component class**, held by the page | `Expander._open` | That of the page holding the instance |
| An item of a virtualised list | **Inside the item function** | `PeoplePage.PersonRow` | While the row is materialised |
| Several screens | **A model / service** created in `App` | `ArticleDetailsModel` | The application's |

```csharp
public sealed class SettingsPage : XenoPage
{
    private readonly ScrollState _scroll = UseScroll();         // screen
    private readonly State<bool> _autoplay = UseState(true);    // screen
    private readonly State<bool> _dataSaver = UseState(false);  // screen
    …
}
```

> Pages are created **only once** (`Page<T>()`): their fields keep their value when the user navigates away and
> comes back. An already-mounted scaffold destination even keeps its scene (its tree, scroll, controllers) without
> calling `Body()` again.

## 3. Hoisting state

*State hoisting*: take state out of a component and hand it to its caller. The component becomes a pure function
that **receives** the value and **exposes** events.

Before (internal state, hard to test, impossible to share):

```csharp
public sealed class Counter
{
    private readonly State<int> _n = UseState(0);
    public XenoComponent Body() =>
        Row(Text(_n, 20), Button("+", () => _n.Value++));
}
```

After (hoisted state):

```csharp
public static class Counter
{
    public static XenoComponent View(State<int> count, Action increment) =>
        Row(Text(count, 20), Button("+", increment)).Gap(8);
}

// In the page
private readonly State<int> _count = UseState(0);
… Counter.View(_count, () => _count.Value++)
```

Benefits: the component is reusable (two counters bound to the same state), previewable
(`View(UseState(42), () => { })`) and the logic lives in the page. **Hoist state to the lowest common ancestor** of
all the components that read or modify it — no higher.

Two variants depending on need:

| Variant | Signature | When |
|---|---|---|
| **Read-only + event** (controlled) | `View(bool on, Action<bool> onChange)` | Components whose shape changes (switch, tab); the page calls `Rebuild()` |
| **Observable state** | `View(State<int> count, …)` | Values that change often or locally (text, colour, progress) |

## 4. Binding state to the UI

Binding is **explicit**. Only these entry points observe a `State`:

| Binding | Updates |
|---|---|
| `Text(State<T>)`, `Text($"…{state}…")` | The text (and its measurement) |
| `.TextColor(State<uint>)` | The text colour |
| `.Background(State<uint>)` | The background colour |
| `LinearProgress(State<float>)` | The filled part (0–1) |
| `ImageFile(State<string?>)` | The displayed local image |
| `.NativeVideo(State<MediaHandle>)` or `(handle, State<bool>)` | The native video output |

```csharp
Text($"{_followers} followers")                // updates when _followers changes
Text($"{price:N2} €", 16)                      // `price` not reactive: formatted once
```

In an interpolated string only the embedded `State<T>`s are tracked; the other values are formatted at
composition (format specifiers and alignment accepted: `{state:N2}`, `{state,8}`).

Reading `_count.Value` in `Body()` gives the value **at that moment**; it creates no subscription. For the display
to follow, pass the `State` itself to the binding.

## 5. When to call `Rebuild()`

`Rebuild()` calls the active page's `Body()` again and replaces its transient tree. Call it when the **structure**
depends on the state:

- a conditional branch flips (`_expanded ? A : B`);
- items are added, removed or reordered;
- a list's content changes (the list itself only recomposes its visible window);
- a colour or size **computed at composition** depends on a value that changes.

```csharp
private int _category;

private XenoComponent CategoryChip(string name) { /* … */
    return Text(name, 12)…Clickable(() => { _category = index; Rebuild(); });
}
```

Good to know:

- `Rebuild()` can be called from any thread: it **reschedules itself** onto the control thread. `State`
  assignments do not ([Side effects](side-effects.md)).
- It does not touch the navigation stack, open overlays or persistent objects.
- It is **more expensive** than a fine-grained binding: do not overuse it during a gesture (scrolling, dragging)
  or on every keystroke.
- `RebuildTopBar()` only rebuilds the application bar when what it displays (title, action icon) changed.
- No `Rebuild()` if the page is no longer active: test `IsActive` in late callbacks.

## 6. Derived values

There is no automatic equivalent of `derivedStateOf`. Three ways to derive a value from a state:

**a) Compute in `Body()` (the most frequent case).** Fine if the derived value does not need to update without a
rebuild:

```csharp
bool canSubmit = _name.Value.Text.Length > 0 && _terms.Value;
return … Button("Submit", Submit).Background(canSubmit ? Theme.Accent : Theme.Faint) …
```

**b) Compute on write** and publish into a second `State`:

```csharp
_name.Changed += (_, change) =>
    _namePreview.Value = change.Value.Text.Length == 0 ? "Nothing typed yet"
                                                       : $"{change.Value.Text.Length} characters";
```

`TextInputPage` does exactly this: the controller raises `Changed`, the handler updates a `State<string>` shown by
`Text(_namePreview, 13)`.

**c) Subscribe to a `State`'s `OnChanged`** to produce another:

```csharp
_a.OnChanged += () => _sum.Value = _a.Value + _b.Value;
_b.OnChanged += () => _sum.Value = _a.Value + _b.Value;
```

Unsubscribe when the subscribing object goes away (page destroyed) if the lifetimes differ.

## 7. Collections and objects

`State<List<T>>` does not notify when you `Add` to the list: the **reference** has not changed. Three options:

1. **Keep the list in a field** and call `Rebuild()` after modifying it — this is the sample pages' choice
   (`PeoplePage._following`, `OverlaysPage._last`).
2. **Replace the value**: `_items.Value = _items.Value.Append(x).ToList();` (new reference → notification; useful if a
   binding depends on the list).
3. **Use an immutable snapshot** published by a model:
   `public sealed record RemoteSnapshot<T>(FeedStatus Status, T[] Items, string? Error = null)`; the model replaces
   the whole snapshot, the page calls `Rebuild()` on `Changed`.

Prefer **immutable types** (`record`, copied arrays) for what the UI reads: it avoids races between the loading
thread and the UI thread.

## 8. Per-list-item state

In a virtualised list, an item's function is called when the item enters the window:

```csharp
private XenoComponent PersonRow(int index)
{
    bool following = _following.Contains(index);            // source of truth: page field
    var label = UseState(following ? "Following" : "Follow");     // row states
    var background = UseState(following ? Theme.SurfaceHigh : Theme.Accent);
    return Row(…, Text(label, 12).Background(background)…Clickable(() =>
    {
        bool now = _following.Add(index);
        if (!now) _following.Remove(index);
        label.Value = now ? "Following" : "Follow";
        background.Value = now ? Theme.SurfaceHigh : Theme.Accent;
    }));
}
```

- The **truth** is in a page field (`_following`); the row's `State`s are only a **live reflection** while the row
  exists.
- Always give a **stable key** (`key: item => item.Id`): it identifies the item as the window moves.
- Do not put state in them that you want to keep when the row leaves the screen.

## 9. Keeping state

| Event | Is the state kept? |
|---|---|
| Page `Rebuild()` | Yes, for fields; objects created in `Body()` are lost |
| Navigating to another page and back | Yes (the page is a singleton) |
| Switching scaffold tab | Yes (the scene stays mounted) |
| Rotation / size change | Yes (layout recomputed, no state recomposition) |
| App in the background then back | Yes as long as the process lives |
| Process killed by the system, cold restart | **No**: no automatic state restoration is provided |

To survive process death, persist what matters yourself (preferences, draft) with the platform's APIs and reload it in
`App.OnStart()` or a page's `OnCreate()`. The framework does not yet provide an equivalent of `rememberSaveable`
([V1 limits](navigation-scaffold.md#v1-limits)).

## 10. Common pitfalls

| Pitfall | Symptom | Fix |
|---|---|---|
| `UseState` in `Body()` | Value goes back to initial on every `Rebuild()` | Page field |
| Reading `state.Value` in `Body()` expecting tracking | Screen does not follow | Pass the `State` to `Text(state)` / `.Background(state)` |
| Modifying a `List` in place inside a `State<List<T>>` | No notification | Field + `Rebuild()`, or replace the reference |
| Assigning a `State` from a background thread | Visual corruption | `IUiDispatcherCapability.Post` |
| `Rebuild()` on every keystroke | Lost focus, jank | Fine-grained binding (`Text(state)`) |
| Two copies of the same data | Inconsistent screens | One source of truth, the other derived |
| Keeping a `XenoComponent` in a field | Dead entity after `Rebuild()` | Keep the data, recompose |
