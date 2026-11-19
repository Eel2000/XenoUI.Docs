# Creating a component

[Documentation](index.md) · [Developer guide](developer-guide.md) · [Customising](customization.md) · [Animations](animations.md)

This guide explains, end to end, how to design a reusable XenoUI component: its shape, parameters, state,
children, events, layout, preview and pitfalls. The examples only use the current public API; member names are
the ones in the code (`XenoUI.Core`, `XenoUI.StandardLibrary`). To change the look of an existing component, see
[Customising](customization.md); for motion, see [Animations](animations.md).

## Contents

1. [The mental model](#1-the-mental-model)
2. [Anatomy of a component](#2-anatomy-of-a-component)
3. [What a `XenoComponent` is](#3-what-a-xenocomponent-is)
4. [Parameters: values, states, callbacks, slots](#4-parameters-values-states-callbacks-slots)
5. [State: three possible homes](#5-state-three-possible-homes)
6. [Fine-grained update or rebuild?](#6-fine-grained-update-or-rebuild)
7. [Layout inside a component](#7-layout-inside-a-component)
8. [Interaction: touch, visual feedback, scrolling](#8-interaction-touch-visual-feedback-scrolling)
9. [Text, icons and images](#9-text-icons-and-images)
10. [Lists and item identity](#10-lists-and-item-identity)
11. [A component with options (the `Options` pattern)](#11-a-component-with-options-the-options-pattern)
12. [Previewing: `[Preview]`](#12-previewing-preview)
13. [Right-to-left, densities and accessibility](#13-right-to-left-densities-and-accessibility)
14. [Organisation, templates and distribution](#14-organisation-templates-and-distribution)
15. [Complete recipes](#15-complete-recipes)
16. [Pitfalls and checklist](#16-pitfalls-and-checklist)
17. [Current limits](#17-current-limits)

---

## 1. The mental model

XenoUI is **declarative, with no instantiated widgets**. A user interface is the result of function calls:

- the `Compose` *factories* (`Column`, `Row`, `Box`, `ZStack`, `Text`, `Image`, `Icon`, `Spacer`, `LazyColumn`,
  `TextField`…) create **entities** in the engine (ECS) and return a handle;
- the *modifiers* (`.Padding(16)`, `.Background(0xFF…)`, `.Clickable(…)`) **modify that entity in place** and
  return the same handle, which allows chaining;
- a **component** is simply a function that assembles those calls and returns a `XenoComponent`.

So there is no base class to inherit, no widget tree to diff and no `new`. From the caller's point of view a
component is a pure function: same parameters, same subtree.

```csharp
public static class Badge
{
    public static XenoComponent View(string label, uint color) =>
        Text(label, 11).TextColor(0xFFFFFFFF)
            .Background(color).CornerRadius(10).Padding(9, 4);
}
```

Use it from any `Body()` or another component:

```csharp
Row(Text("Messages"), Badge.View("3", 0xFFDC2626)).Gap(8)
```

> `Compose` is statically imported by `Xeno.SDK` (`using static`), as are `Widgets` (`Button`, `TextButton`) and the
> `Use…` factories. In a project that does not use the SDK, add `using static XenoUI.Core.Compositor.Compose;`.

## 2. Anatomy of a component

### 2.1 The recommended shape: a static class with `View`

This is the shape of the `xenoui-app` (`Components/Counter.cs`) and `xenoui-component` templates:

```csharp
using XenoUI.Core.Preview;

namespace MyApp.Components;

/// <summary>Profile card: avatar, name, role.</summary>
public static class ProfileCard
{
    public static XenoComponent View(string name, string role) =>
        Row(
            Box(48, 48, Text(name[..1], 20).TextColor(0xFFFFFFFF))
                .Background(0xFF6D28D9).CornerRadius(24).CenterContent(),
            Column(
                Text(name, 16).TextColor(0xFF111827),
                Text(role, 12).TextColor(0xFF6B7280))
            .Gap(2).FlexGrow(1))
        .Gap(12).Padding(12).CrossAxis(CrossAxisAlignment.Center)
        .Background(0xFFF3F4F6).CornerRadius(16);

    [Preview("ProfileCard")]
    private static XenoComponent Preview() => View("Amina", "Designer");
}
```

Shape rules:

| Rule | Reason |
|---|---|
| One `View(...)` method (or a descriptive name: `Chip(...)` if the component is a variant) that **returns `XenoComponent`** | The caller can chain modifiers on the result |
| Content parameters first, then optional ones with defaults | Readability at the call site |
| No side effects in `View` (no network, no disk, no state allocation) | `View` is called again on every `Rebuild()` |
| The component's root carries the "default" look; the caller may override it | See [Customising](customization.md) |

### 2.2 The class shape with `Body()` (like `Button`)

The standard library writes some components as a class that captures its parameters and then produces the tree
in `Body()`; a static factory in `Widgets` hides it:

```csharp
public class Button
{
    private readonly string _label;
    private readonly Action _onTap;
    private readonly float _fontSize;
    private readonly uint _textColor;

    public Button(string label, Action onTap, float fontSize = 16f, uint textColor = 0xFFFFFFFF)
    { /* … */ }

    public XenoComponent Body() =>
        ZStack(Text(_label, _fontSize).TextColor(_textColor).AlignSelf(CrossAxisAlignment.Center))
            .Background(0xFF6200EE).CornerRadius(8).Elevation(2).Padding(20, 12)
            .CenterContent().Clickable(_onTap);
}

public static partial class Widgets
{
    public static XenoComponent Button(string label, Action onTap, float fontSize = 16f, uint textColor = 0xFFFFFFFF)
        => new Button(label, onTap, fontSize, textColor).Body();
}
```

Choose this shape when the component has **its own state** to keep (see [§5](#5-state-three-possible-homes)) or
when the tree splits naturally into several private methods. `Widgets` is a `partial` class: you can add your own
factories to it in your project so they are called like the standard components.

### 2.3 Component or page?

| | Component | Page (`XenoPage`) |
|---|---|---|
| Role | Reusable piece of interface | Navigable screen |
| Lifecycle | None: called by `Body()` | `OnCreate`, `OnActivated`, `OnDeactivated`, `OnDestroy` |
| Rebuilding | Depends on its caller | `Rebuild()`, `RebuildTopBar()` |
| Navigation | Receives an `Action` | `Navigate<T>()`, `GoBack()` |
| Where state lives | Parameter or instance held by the page | Page fields |

If a piece needs `Navigate`, `Native`, `Application` or `IsActive`, it is a page; hand its components an action
(`Action onOpen`) rather than the page itself.

## 3. What a `XenoComponent` is

`XenoComponent` is an 8-byte `readonly struct`: the entity id and a validity flag. Keep five consequences in
mind:

1. **No allocation** to compose: copying a `XenoComponent` does not copy the tree.
2. **Modifiers mutate the entity** in the engine. `a.Padding(8)` and `var b = a; b.Padding(8)` act on the same
   entity.
3. **Modifier order is free**, except when two modifiers write the same property: the last wins
   (`Padding(8).Padding(16)` → 16; `Elevation(4).Shadow(…)` → the manual shadow replaces the elevation;
   `CornerRadius` is ignored if `Shape(...)` is set).
4. **`XenoComponent.None`** means "nothing": every modifier is a no-op on it and containers skip it. It is the
   mechanism for conditional rendering.
5. A `XenoComponent` **is only valid in the composition that created it**. Do not keep it in a field to reuse
   after a `Rebuild()`: the transient tree is destroyed and recreated. Store *data* or persistent objects
   (`State<T>`), not handles.

```csharp
// Conditional rendering: None is skipped by Column/Row/ZStack/Box.
Column(
    Text(title),
    showSubtitle ? Text(subtitle, 12) : XenoComponent.None,
    items.Count == 0 ? EmptyState.View() : XenoComponent.None
)
```

`default(XenoComponent)` is also `None`. Converting `None` to `int` (the entity id), however, throws an
`InvalidOperationException`: test `IsNone` first.

Containers take their children as `params ReadOnlySpan<XenoComponent>`: pass an argument list, an array
(`XenoComponent[]`) or build an array from data:

```csharp
XenoComponent[] rows = tags.Select(t => Badge.View(t, 0xFF2563EB)).ToArray();
Row(rows).Gap(6)
```

## 4. Parameters: values, states, callbacks, slots

### 4.1 Plain values

`string`, `float`, `uint` (ARGB colour), enums. They are read **at composition time**: a value changed later only
takes effect at the next `Rebuild()`.

```csharp
public static XenoComponent Price(decimal amount, string currency = "€") =>
    Text($"{amount:N2} {currency}", 16).TextColor(0xFF111827);
```

> In `Text($"…")`, an interpolated string **without** a `State` is formatted once; with an embedded `State<T>`
> it becomes reactive (see [§6](#6-fine-grained-update-or-rebuild)).

### 4.2 Reactive states: `State<T>`

Pass a `State<T>` when the value must be able to change **without rebuilding the tree**:

```csharp
public static XenoComponent Counter(State<int> count, Action increment) =>
    Column(
        Text($"Count: {count}", 20),
        Button("+1", increment)
    ).Gap(12);
```

The component reads `count`; the caller owns it. This inversion ("lifted state") is the simplest way to keep a
component free of its own state.

### 4.3 Callbacks

Use `Action` (or `Action<T>`) for anything the component must not decide itself: navigation, deletion,
validation. The component declares the intent, the page decides.

```csharp
public static XenoComponent ArticleRow(Article a, Action<Article> open, Action<Article> delete) => …
```

A callback is stored once in the engine's callback table (`Clickable`); it is replaced, not stacked, if you
call `Clickable` twice on the same entity.

### 4.4 Slots: content provided by the caller

A *slot* is a parameter that carries content. Two forms are idiomatic.

**By `XenoComponent` (already composed content)** — simple, for content that does not depend on a value the
component computes:

```csharp
public static XenoComponent Card(params XenoComponent[] content) =>
    Column(content).Gap(10).Padding(16)
        .Background(0xFF15151D).CornerRadius(20)
        .CrossAxis(CrossAxisAlignment.Stretch);
```

This is exactly `Ui.Card` from the sample application. Call it:

```csharp
Card(Text("Title", 18), Text("Detail", 13), Button("OK", Ok))
```

**By `Func<…, XenoComponent>` (content built on demand)** — needed when the component gives the slot a value (a
dismissal handle, an index, a state) or builds the content later. The library uses it for overlays:

```csharp
Snackbar.Show(app, handle => MyCustomBody(handle.Dismiss), options);
```

Rule: if the slot must be able to receive information from the component, use a `Func`; otherwise pass a
`XenoComponent`. Always pass `XenoComponent.None` rather than `null` for "no content".

## 5. State: three possible homes

Persistent objects (`State<T>`, `ScrollState`, `LazyListState`, `TextEditController`) must **survive rebuilds**.
They therefore never live in `View()`/`Body()`.

> The `UseState`, `UseStates`, `UseScroll`, `UseLazyList`, `UseTextEdit` factories create a **new object on every
> call**. Call them in a field initialiser, never in a composition method.

### 5.1 State owned by the caller (recommended)

The component receives `State<T>` as a parameter (see [§4.2](#42-reactive-states-state)). The component is a plain
function, trivial to preview (`View(UseState(42), () => { })`).

### 5.2 State owned by an instance held by the page

When the state is internal to the component (a menu open or closed, a current tab), make it a class and have the
**page hold an instance in a field**:

```csharp
public sealed class Expander
{
    private readonly State<bool> _open = UseState(false);
    private readonly string _title;
    private readonly Func<XenoComponent> _content;
    private readonly Action _rebuild;

    public Expander(string title, Func<XenoComponent> content, Action rebuild)
    { _title = title; _content = content; _rebuild = rebuild; }

    public XenoComponent Body() =>
        Column(
            Row(
                Text(_title, 16).FlexGrow(1),
                Icon(Glyph(_open.Value ? "" : "", "Font Awesome 5 Free-Solid-900"), 12)
            ).CrossAxis(CrossAxisAlignment.Center).Padding(12)
             .Clickable(() => { _open.Value = !_open.Value; _rebuild(); }),
            _open.Value ? _content() : XenoComponent.None
        ).Background(0xFFF3F4F6).CornerRadius(14);
}

// In the page:
private readonly Expander _faq;
public FaqPage() => _faq = new Expander("How does it work?", () => Text("…"), Rebuild);
public override XenoComponent Body() => Column(_faq.Body());
```

Here opening/closing is **structural** (the content appears or disappears), so it calls `Rebuild()`, which the
page passes to the component. The instance itself survives: the state is not lost.

### 5.3 Global state (application service)

State shared by several screens (signed-in user, basket) belongs to a service created in `XenoApp`; pages read it
and subscribe to its changes (see the sample's `ArticleDetailsPage`: `OnChanged` → `Rebuild()` only if
`IsActive`). A component should not read a service directly: hand it the values.

## 6. Fine-grained update or rebuild?

XenoUI does not diff two trees. Two mechanisms exist, to be chosen by what changes:

| What changes | Mechanism | Cost |
|---|---|---|
| The text of a `Text` | `Text(State<T>)` or `Text($"…{state}")` | Re-measures that text only |
| A text colour | `.TextColor(State<uint>)` | Repaints the entity |
| A background colour | `.Background(State<uint>)` | Repaints the entity |
| The filled part of a bar | `LinearProgress(State<float>)` | Re-layout of the bar |
| The source of a local image | `ImageFile(State<string?>)` | Repaint |
| The native video of an area | `.NativeVideo(State<…>)` | Repaint |
| Adding, removing, reordering items, switching a conditional branch | The page's `Rebuild()` | Recomposition of the page |

Example of a fully reactive component, without any rebuild:

```csharp
public static XenoComponent StatusDot(State<uint> color, State<string> label) =>
    Row(
        Box(10, 10).Background(color).CornerRadius(5),
        Text(label, 13)
    ).Gap(8).CrossAxis(CrossAxisAlignment.Center);
```

`StatusDot` is never rebuilt: changing `color.Value` or `label.Value` only repaints the two entities concerned.
The `State<T>` setter only notifies when the value **actually changes** (`EqualityComparer<T>.Default`);
reassigning the same value is free.

A rebuild calls `Body()` again under a lock and resets the transient tree. It does **not** rebuild the navigation
stack, the open overlays or the persistent objects. See
[Which operation rebuilds what?](navigation-scaffold.md#which-operation-rebuilds-what).

**Threading**: any mutation of a `State` that has a visual effect must happen on the UI thread. From a background
task, go through `Native.Require<IUiDispatcherCapability>().Post(...)` and, for a late response, check
`IsActive` before acting.

## 7. Layout inside a component

All dimensions are in **logical units** (Android dp, iOS points). Colours are `0xAARRGGBB`.

### 7.1 Containers

| Factory | Layout | Notes |
|---|---|---|
| `Column(children)` | Vertical | Transparent background by default |
| `Row(children)` | Horizontal | RTL `FlowDirection`: starts on the right |
| `ZStack(children)` | Stacked | First child at the bottom, last on top; see the alignment rule below |
| `Box(w, h)` / `Box(w, h, children)` | Rectangle or container of a given size | `0` = automatic size |
| `Spacer()` | Flexible space | Same as `FlexGrow(1)` |
| `LazyColumn` / `LazyRow` / `VerticalPager` | Virtualised lists | See [§10](#10-lists-and-item-identity) |

**Alignment inside a `ZStack`.** It does not follow the same rules as a `Row`/`Column`:

- the **horizontal** position of *all* children comes from the `ZStack`'s `MainAxis` (`Start` by default,
  `Center`, `End`; the `Space…` variants behave like `Start`);
- the **vertical** position of each child comes from its `AlignSelf`, otherwise from the `ZStack`'s `CrossAxis`
  (`Start`, `Center`, `End`);
- `Stretch` (through `AlignSelf` or `CrossAxis`) makes the child fill the whole inside of the `ZStack`;
- `Margin` offsets a child within that space; there is no per-child horizontal alignment.

That is why `Button` centres its text with `.CenterContent()` on the `ZStack` and `AlignSelf(Center)` on the text.
To put an element in a corner, make it fill the `ZStack` with a `Row`/`Column`: see the "corner badge" recipe
below.

### 7.2 Layout modifiers

| Modifier | Effect |
|---|---|
| `Width(w)`, `Height(h)`, `Size(w, h)` | Explicit size; `0` = automatic |
| `Padding(all)` / `Padding(h, v)` / `Padding(Spacing)` | Inner margin |
| `Margin(all)` / `Margin(Spacing)` | Outer margin |
| `Gap(g)` | Space between children |
| `MainAxis(MainAxisAlignment)` | Distribution on the main axis: `Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly` |
| `CrossAxis(CrossAxisAlignment)` | Placement on the cross axis: `Start`, `Center`, `End`, `Stretch` |
| `AlignSelf(CrossAxisAlignment)` | Override for **this** child |
| `CenterContent()` | `MainAxis(Center)` + `CrossAxis(Center)` |
| `FlexGrow(f)` | Share of the remaining space (0 = does not grow) |
| `FlexShrink(f)` | Horizontal shrinking (auto-width children shrink by default) |
| `FlowDirection(dir)` | `Inherit`, `LeftToRight`, `RightToLeft` for the subtree |

`Spacing`: `Spacing.All(v)`, `Spacing.Symmetric(horizontal, vertical)`, `Spacing.Only(left, top, right, bottom)`
and `Spacing.Directional(start, top, end, bottom)` (names that read well in RTL; `Left` is the start side).

### 7.3 Layout recipes

```csharp
// Row: icon + text that takes the room + value on the right
Row(Icon(i, 18), Text(label).FlexGrow(1), Text(value))
    .Gap(10).CrossAxis(CrossAxisAlignment.Center);

// Centred content in a surface (button, empty state, avatar)
Box(64, 64, Text("A", 24)).Background(0xFF6D28D9).CornerRadius(32).CenterContent();

// Corner badge, top right: a Row that fills the ZStack, with a Spacer
ZStack(
    Icon(bell, 24),
    Row(Spacer(), Text("3", 10).TextColor(0xFFFFFFFF)
            .Background(0xFFDC2626).CornerRadius(8).Padding(5, 2))
        .AlignSelf(CrossAxisAlignment.Stretch)
);

// Page in three zones: fixed header, body taking the rest, fixed footer
Column(Header(), Body().FlexGrow(1), Footer()).CrossAxis(CrossAxisAlignment.Stretch);
```

For a text that must stay in a row without pushing its neighbours, give it `.FlexGrow(1)` **and**
`.FlexShrink(1)`, and bound it (`.MaxLines(n, ellipsis: true)`); that is how `Snackbar.Body` is built.

### 7.4 Sizing: what the engine decides

- A `Text` measures itself (maximum width = available width, wrapping on by default).
- An `Image(asset)` takes its natural size divided by the screen density; `.Size`, `.Width` or a `Stretch` parent
  replace it.
- A container without an explicit size takes the size of its content plus its padding.
- `Stretch` enlarges children on the cross axis; `FlexGrow` distributes the main axis.

A well-behaved component **does not fix a width** when the caller may want to stretch it: leave that to
`FlexGrow`/`Stretch` and only fix what gives the component its identity (a field's height, an avatar's
diameter).

## 8. Interaction: touch, visual feedback, scrolling

### 8.1 `Clickable`

`.Clickable(Action)` makes **any entity** touchable. The callback fires on release, if the finger is still inside
the entity. The touch area is the entity's: its shape if it has a `Shape`, otherwise its rectangle (rounded
corners included).

**Visual feedback**: a clickable entity whose background is not transparent darkens by 15 % while pressed (no layout
cost). Practical consequence: a `Text(...).Clickable(…)` with a **fully transparent** background (alpha 0) has
**no** visual feedback, because nothing is drawn to tint. Give it a background (even a translucent colour like
`0x1F6750A4`, which is what `TextButton`'s documentation does) or wrap it in a coloured surface. Text fields do not
have this tint.

For a comfortable touch target, aim for **at least 44–48 dp** per side (`Box(44, 44, icon).CenterContent()
.Clickable(tap)` is `Ui.IconButton`'s pattern).

### 8.2 Nested clickable elements

Each entity has a single touch callback. The engine resolves a touch in reverse Z order (the element drawn on
top is tested first) and fires **only one** entity. Avoid nesting two touch targets that overlap and multiplying
tiny targets in a row; if an entire row is clickable *and* contains a button, check the behaviour on a device.

### 8.3 Scrolling

`.Scrollable(ScrollState)` makes a container scroll; the axis follows the container (`Row` horizontal, the
others vertical) unless you force it. `ScrollState` comes from `UseScroll()` in a **field**, to keep the position
across rebuilds.

```csharp
private readonly ScrollState _scroll = UseScroll();

Column(items).Gap(12).Padding(16).Scrollable(_scroll)
```

For long lists, prefer `LazyColumn` (virtualised) over a scrollable `Column` that materialises everything.

### 8.4 Text input

`TextField(controller, placeholder, InputOptions(...))` and `TextArea(...)` are engine-drawn fields driven by a
persistent `TextEditController` (`UseTextEdit("")` in a field). A form component receives the controller as a
parameter:

```csharp
public static XenoComponent LabeledField(string label, TextEditController text, string hint) =>
    Column(
        Text(label, 12).TextColor(0xFF6B7280),
        TextField(text, hint).Height(48)
    ).Gap(6);
```

IME, keyboard and password details: [Developer guide §11](developer-guide.md#11-text-input-and-ime).

## 9. Text, icons and images

### 9.1 Text

| Modifier | Effect |
|---|---|
| `TextColor(uint)` / `TextColor(State<uint>)` | ARGB colour |
| `FontSize(float)` | Size in dp (also a parameter of `Text`) |
| `FontFamily("Name")` | Font placed in `Assets/Fonts/`, loaded at startup |
| `TextAlign(Start / Center / End)` | Alignment in the text's box |
| `TextWrap(bool)` | Line wrapping (on by default) |
| `MaxLines(n, ellipsis)` | Line limit, with "…" if `ellipsis` |
| `AutoSize(minimumFontSize)` | Bounded reduction of the size to fit |

Text modifiers apply **to the entity that carries the text**. They do not propagate to its children:
`ZStack(Text("OK")).TextColor(0xFFFF0000)` does not colour the child `Text`. That is why `Button` takes `fontSize`
and `textColor` as **parameters**. A component that contains text must therefore expose the text parameters the
caller will need (see [Customising](customization.md#31-what-a-modifier-cannot-reach)).

### 9.2 Icons

An icon is a font glyph:

```csharp
Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), 20).TextColor(0xFF111827)
```

`IconDef` (`Glyph(glyph, fontName)`) pairs a glyph with its font so they travel together: take an `IconDef`
parameter rather than two strings. Fonts go in `Assets/Fonts/`; an unknown font falls back to the system font.

### 9.3 Images

`Image("avatar")` loads `Assets/Images/avatar.*` (short name, no extension) and takes its natural size (in dp).
`ScaleMode(ImageScaleMode.Cover | Fit | Fill | None)` sets the fit. `ImageFile(State<string?>)` shows a reactive
local file (`null` hides the image).

```csharp
public static XenoComponent Avatar(string asset, float size = 48) =>
    Image(asset).Size(size, size).CornerRadius(size / 2).ScaleMode(ImageScaleMode.Cover);
```

## 10. Lists and item identity

```csharp
LazyColumn(
    items,                                   // IReadOnlyList<T>
    item => ArticleRow.View(item, open),     // builds one item
    key: item => item.Id,                    // stable identity (recommended)
    state: _list)                            // persistent LazyListState (optional)
```

- Only the visible items (plus a margin) are composed; their sizes are **measured**.
- `key` gives a stable identity: without a key, the identity is the index. A duplicate key in the materialised
  window throws an `InvalidOperationException` during composition.
- The key identifies the item when the window moves; use a business identifier (id), never an index or an
  object recreated at every composition.
- `LazyListState` (`UseLazyList()`) lets you observe or drive scrolling; `VerticalPager` provides a full-screen
  pager that snaps to a page (`PageChanged`, raised on the UI thread).
- Items are rebuilt when the window moves: their `itemContent` must stay **fast and free of side effects**.

A row component is an ordinary function:

```csharp
public static class ArticleRow
{
    public static XenoComponent View(Article a, Action<Article> open) =>
        Row(
            Image(a.Cover).Size(72, 72).CornerRadius(12).ScaleMode(ImageScaleMode.Cover),
            Column(
                Text(a.Title, 15).MaxLines(2, ellipsis: true),
                Text(a.Author, 12).TextColor(0xFF6B7280)
            ).Gap(4).FlexGrow(1))
        .Gap(12).Padding(12).CrossAxis(CrossAxisAlignment.Center)
        .Background(0xFFFFFFFF).CornerRadius(16)
        .Clickable(() => open(a));
}
```

## 11. A component with options (the `Options` pattern)

As soon as a component has more than three or four optional parameters, group them in an options object where
**every property has a default value**. This is the pattern of `SnackbarOptions`, `TopBarOptions`,
`PopupOptions`, `DialogOptions`, `AlertOptions`, `BottomSheetOptions`.

```csharp
public sealed class ChipOptions
{
    public uint Background { get; set; } = 0xFFEDE9FE;
    public uint Foreground { get; set; } = 0xFF5B21B6;
    public float FontSize { get; set; } = 13;
    public float CornerRadius { get; set; } = 16;
    public float PaddingX { get; set; } = 12;
    public float PaddingY { get; set; } = 6;
    public IconDef? Icon { get; set; }
}

public static class Chip
{
    public static XenoComponent View(string label, Action? onTap = null, ChipOptions? options = null)
    {
        options ??= new ChipOptions();
        var chip = Row(
            options.Icon is { } icon
                ? Icon(icon, options.FontSize + 2).TextColor(options.Foreground)
                : XenoComponent.None,
            Text(label, options.FontSize).TextColor(options.Foreground)
        ).Gap(6).CrossAxis(CrossAxisAlignment.Center)
         .Padding(options.PaddingX, options.PaddingY)
         .Background(options.Background).CornerRadius(options.CornerRadius);
        return onTap is null ? chip : chip.Clickable(onTap);
    }
}
```

Call: `Chip.View("Swift", onTap, new ChipOptions { Background = 0xFFDCFCE7, Foreground = 0xFF166534 })`.

If the object must be **immutable and derivable**, use a `record`: the caller writes `Chip.Default with {
Background = … }` (this is the style of `NavigationAction` and `DockedNavigationBarStyle`).

Also provide, when useful, a method that produces **only the visual** (`Snackbar.Body`, `Dialog.Surface`),
separate from the one that shows it (`Show`): it serves previews and composition elsewhere.

## 12. Previewing: `[Preview]`

A **static, parameterless method returning `XenoComponent`** that carries `[Preview]` appears in Visual Studio's
*XenoUI Preview* window and in the VS Code preview.

```csharp
[Preview("Chip")]
[Preview("Chip (RTL)", RightToLeft = true)]
[Preview("Chip, tablet", Device = "tablet")]
private static XenoComponent ChipPreview() =>
    Row(Chip.View("Swift"), Chip.View("Rust")).Gap(8).Padding(20);
```

Properties: `Name` (caption), `Device` (`android`, `pixel`, `iphone`, `iphone-se`, `foldable`, `tablet`),
`Width`, `Height` (dp; 0 = the device's value, or the content's height for a component), `RightToLeft`. A method
is a **component** preview (device width, content height); a page defaults to `android`.

Good habits:

- one preview per **variant** (empty, full, long text, RTL, error);
- hard-coded sample data: a preview receives no parameter (`View(UseState(42), () => { })`);
- no network, disk or `Native` access in a preview;
- preview code is compiled in memory by the extension; see [Visual Studio](visual-studio.md) for what it covers
  and its limits.

Details: [Visual Studio](visual-studio.md), [Visual Studio Code](vscode.md).

## 13. Right-to-left, densities and accessibility

**RTL.** Use `Row`, `Spacing.Directional(start, …)` and `TextAlign(Start/End)` rather than absolute positions: the
subtree mirrors when `FlowDirection` is `RightToLeft` (or the application is RTL). Always add an RTL preview to a
component that contains a row or a directional arrow (for a chevron, change the glyph according to
`IsRightToLeft` if the component has access to the application).

**Densities.** Everything is in dp. Never convert to pixels; font sizes are also in dp.

**Accessibility.** The engine draws its own pixels: legibility rests on your choices. Sufficient contrast (≥ 4.5:1
for body text), targets ≥ 44 dp, information never carried by colour alone, text that can grow (avoid fixed
heights around text; prefer padding). Text fields receive an accessibility label derived from their placeholder
(`InputOptions(label: …)` to set it). The system's reduced motion is honoured by the library's animations (see
[Animations](animations.md#9-reduced-motion)).

## 14. Organisation, templates and distribution

Suggested project structure:

```
MyApp/
  App.cs                 XenoApp: pages, scaffold
  Pages/                 one XenoPage per screen
  Components/            one file per component (Chip.cs, ProfileCard.cs…)
  Design/Theme.cs        constants for colours, sizes, fonts
  Assets/Fonts, Images
```

Create a component from the template:

```powershell
dotnet new xenoui-component -n Avatar -o Components
```

Visual Studio: *Add › New Item › XenoUI Component*. VS Code: the XenoUI extension's create-component command.

Reuse between applications: put the components in a class library that references `XenoUI.Core` (and
`XenoUI.StandardLibrary` if you compose `Button`/`TextButton`) and targets the same TFM as the application
(`net11.0-android` / `net11.0-ios`, or `net11.0` if the code is portable and has no platform type). For a shared
theme, expose it from that library; each application can override it with its own options.

## 15. Complete recipes

### 15.1 Field with a label and an error message

```csharp
public static class FormField
{
    public static XenoComponent View(string label, TextEditController text, string hint,
        State<string> error)
        => Column(
            Text(label, 12).TextColor(0xFF6B7280),
            TextField(text, hint).Height(48),
            Text(error, 12).TextColor(0xFFDC2626)       // empty = invisible, no rebuild
        ).Gap(6).CrossAxis(CrossAxisAlignment.Stretch);
}
```

`error` is a `State<string>`: validating a field updates the message without touching the structure.

### 15.2 Controlled switch

```csharp
public static class Toggle
{
    public static XenoComponent View(bool on, Action<bool> onChange)
    {
        uint track = on ? 0xFF6D28D9 : 0xFFD1D5DB;
        var thumb = Box(24, 24).Background(0xFFFFFFFF).CornerRadius(12).Elevation(1);
        return Row(on ? Spacer() : XenoComponent.None, thumb, on ? XenoComponent.None : Spacer())
            .Width(52).Height(32).Padding(4).CrossAxis(CrossAxisAlignment.Center)
            .Background(track).CornerRadius(16)
            .Clickable(() => onChange(!on));
    }
}
```

A **controlled** component: it remembers nothing, the page owns the boolean and calls `Rebuild()` after the
change. It is the most predictable choice for any component whose shape changes.

### 15.3 Star rating

```csharp
public static class Rating
{
    private const string Fa = "Font Awesome 5 Free-Solid-900";

    public static XenoComponent View(int value, Action<int> onRate, int max = 5)
    {
        var stars = new XenoComponent[max];
        for (int i = 0; i < max; i++)
        {
            int rating = i + 1;
            stars[i] = Icon(Glyph("", Fa), 24)
                .TextColor(rating <= value ? 0xFFF59E0B : 0xFFD1D5DB)
                .Padding(4)
                .Clickable(() => onRate(rating));
        }
        return Row(stars);
    }
}
```

### 15.4 Segmented bar

```csharp
public static XenoComponent Segmented(string[] labels, int selected, Action<int> onSelect)
{
    var items = new XenoComponent[labels.Length];
    for (int i = 0; i < labels.Length; i++)
    {
        int index = i;
        bool active = i == selected;
        items[i] = Text(labels[i], 14)
            .TextColor(active ? 0xFFFFFFFF : 0xFF374151)
            .TextAlign(TextAlignment.Center)
            .Padding(14, 10).FlexGrow(1)
            .Background(active ? 0xFF6D28D9 : 0x00000000)
            .CornerRadius(10)
            .Clickable(() => onSelect(index));
    }
    return Row(items).Gap(4).Padding(4).Background(0xFFE5E7EB).CornerRadius(14);
}
```

An inactive segment has a transparent background: it therefore has no press tint (see [§8.1](#81-clickable)). If
you want visual feedback, give it a non-zero translucent background.

### 15.5 Card with an empty state

```csharp
public static XenoComponent InboxList(IReadOnlyList<Message> messages, Action<Message> open) =>
    messages.Count == 0
        ? Column(
            Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), 40).TextColor(0xFF9CA3AF),
            Text("No messages", 16),
            Text("New messages will appear here.", 13).TextColor(0xFF6B7280)
              .TextAlign(TextAlignment.Center))
          .Gap(8).CenterContent().Padding(32)
        : LazyColumn(messages, m => MessageRow.View(m, open), key: m => m.Id);
```

## 16. Pitfalls and checklist

| Pitfall | Symptom | Fix |
|---|---|---|
| `UseState(…)` called in `View()`/`Body()` | State goes back to its initial value on every `Rebuild()` | Initialise in a field |
| Storing a `XenoComponent` in a field | Dead entity after `Rebuild()` | Store data, recompose |
| Changing a `State` from a background thread | Visual corruption or an exception | `IUiDispatcherCapability.Post` |
| Clickable text without a background | No visual feedback | Non-transparent background or wrapping surface |
| `.TextColor` on a container | The child text keeps its colour | Pass the colour to the `Text`, through a parameter |
| Duplicate key in a lazy list | `InvalidOperationException` at composition | Unique keys per item |
| Fixed widths everywhere | Overflow with large fonts or narrow widths | `FlexGrow`, `Stretch`, `MaxLines` |
| `Rebuild()` on every keystroke | Loss of focus or scroll position | Fine-grained state (`State`); rebuild only if the structure changes |
| Late async callback | Update of a page that was left | Test `IsActive` before acting |
| `CornerRadius` ignored | `Shape(...)` set on the entity | Remove the shape (`Shape(null)`) or build the radius into the shape |

Checklist before publishing a component:

- [ ] `View` returns `XenoComponent` and has no side effect;
- [ ] no state created in `View`; states are parameters or fields;
- [ ] sensible defaults; look overridable with modifiers;
- [ ] text parameters exposed (colour, size) if the component contains text;
- [ ] no needless fixed width; tested with long text;
- [ ] touch area ≥ 44 dp;
- [ ] previews: default, variants, RTL;
- [ ] contrast checked on light and dark backgrounds.

## 17. Current limits

- **No style inheritance**: a parent's colour or font does not flow into its children.
- **No public free drawing**: apart from shapes (`XenoShape`, [Customising](customization.md#5-shapes-shadows-and-elevation))
  the engine exposes no extension point to paint directly on the Skia canvas.
- **No tree diff**: a `Rebuild()` recomposes the page; fine-grained updates go through `State`.
- **Components have no lifecycle**: there is no `OnCreate`/`OnDestroy` equivalent for a component; release
  resources in the page that owns it.
- The framework is **pre-V1**: an API is only stable if a test covers it and it is presented as a contract (see
  [Decisions and V1 trajectory](architecture/decisions-v1.md)).
