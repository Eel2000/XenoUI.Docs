# Layouts

[Documentation](index.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [Creating a component](components.md) · [Customising](customization.md) · [Navigation](navigation-scaffold.md)

This guide explains how XenoUI places elements on screen: containers, how sizes are decided, alignment, margins,
scrolling, lists, text and right-to-left. It complements
[Creating a component §7](components.md#7-layout-inside-a-component) with the layout **model** and a set of
ready-to-use patterns. For the engine's layout pipeline see [Runtime §5](architecture/runtime.md#5-layout-pipeline).

## Contents

1. [The layout model](#1-the-layout-model)
2. [Containers](#2-containers)
3. [Sizes](#3-sizes)
4. [Distribution and alignment](#4-distribution-and-alignment)
5. [Margins and spacing](#5-margins-and-spacing)
6. [Overlapping: `ZStack`](#6-overlapping-zstack)
7. [Scrolling](#7-scrolling)
8. [Virtualised lists](#8-virtualised-lists)
9. [Text and images in layouts](#9-text-and-images-in-layouts)
10. [Right-to-left](#10-right-to-left)
11. [Varied screens: sizes, safe areas, keyboard](#11-varied-screens-sizes-safe-areas-keyboard)
12. [Common patterns](#12-common-patterns)
13. [Layout troubleshooting](#13-layout-troubleshooting)
14. [Compose equivalents](#14-compose-equivalents)

---

## 1. The layout model

Layout takes two passes over the entity tree:

1. **Measure**: top to bottom, each entity receives the available space and computes the size it *would like*
   (measured text, accumulated children, explicit size).
2. **Arrange**: the parent distributes the real space (distribution, flex, alignment) and gives each child its final
   position and size.

Resulting rules:

- An **explicit** size (`Width`, `Height`, `Size`) wins over the natural size; `0` means *automatic*.
- A container without an explicit size takes the size of its content plus its padding.
- **Flex** (`FlexGrow`) is distributed along the parent's main axis **after** non-flexible children have taken their
  place.
- Any change touching size or position (`Width`, `Padding`, `Gap`, alignments, text) marks the entity "to
  re-measure / re-arrange"; a purely visual change (colour, shadow) does not trigger layout
  ([Customising §2](customization.md#2-level-1-modifiers)).
- **Units** are logical (Android dp, iOS points); colours are ARGB.

## 2. Containers

| Container | Main axis | Use |
|---|---|---|
| `Column(…)` | Vertical | Stack top to bottom |
| `Row(…)` | Horizontal | Line up left to right (right to left in RTL) |
| `ZStack(…)` | — (stack) | Overlap; the last child is on top |
| `Box(w, h)` / `Box(w, h, children…)` | — | Rectangle of a given size; with children, it is a stack |
| `LazyColumn` / `LazyRow` | Vertical / horizontal | Virtualised lists |
| `VerticalPager` | Vertical | Full-screen pages |
| `Spacer()` | — | Flexible space (`FlexGrow(1)`) |

Children are passed as arguments (`Column(a, b, c)`), as an array (`Column(items)`) or built by a loop.
`XenoComponent.None` is skipped:

```csharp
Column(
    Header(),
    _loading ? Spinner() : XenoComponent.None,
    Content())
```

## 3. Sizes

| I want… | I write |
|---|---|
| Natural size | Nothing |
| Fixed size | `Width(120)`, `Height(48)`, `Size(120, 48)`, `Box(120, 48)` |
| Fill the remaining space on the main axis | `FlexGrow(1)` |
| Share the space (2 : 1) | `FlexGrow(2)` and `FlexGrow(1)` |
| Fill the cross axis | `CrossAxis(Stretch)` on the parent, or `AlignSelf(Stretch)` on the child |
| Shrink if space runs out (horizontal) | `FlexShrink(1)` (default for automatic widths) |

```csharp
// Search bar: the icon keeps its size, the text takes the rest
Row(Icon(search, 16), Text("Search", 13).FlexGrow(1))
    .Gap(10).Padding(14, 12).CrossAxis(CrossAxisAlignment.Center);
```

> **Avoid fixing the width** of a reusable component: let the caller stretch it with `FlexGrow` or `Stretch`. Only
> fix what gives identity (an avatar's diameter, a field's height).

## 4. Distribution and alignment

In a `Row` or a `Column`, two axes:

- **main axis** (horizontal for `Row`, vertical for `Column`): `MainAxis(…)`
  — `Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly`;
- **cross axis**: `CrossAxis(…)` — `Start`, `Center`, `End`, `Stretch`; overridable **per child** with
  `AlignSelf(…)`.

```csharp
Row(Text("Left"), Text("Right")).MainAxis(MainAxisAlignment.SpaceBetween);       // at the ends
Column(Icon(x, 40), Text("Title")).CenterContent();                               // centred everywhere
Column(Text("Title"), Text("Detail").AlignSelf(CrossAxisAlignment.End));          // one child on the right
```

`CenterContent()` is the shortcut for `MainAxis(Center)` + `CrossAxis(Center)`.

## 5. Margins and spacing

| Tool | Effect |
|---|---|
| `Padding(…)` | **Inner** space of the container |
| `Margin(…)` | **Outer** space, around the element |
| `Gap(g)` | **Between** the children of a `Row`/`Column` |

`Spacing`: `All(v)`, `Symmetric(horizontal, vertical)`, `Only(left, top, right, bottom)`,
`Directional(start, top, end, bottom)`. `Padding(a, b)` sets horizontal/vertical; `Padding(Spacing)` replaces all
four sides.

Prefer `Gap` to individual margins: a `Column(...).Gap(12)` adapts when a child is added or removed.

## 6. Overlapping: `ZStack`

The children of a `ZStack` share the same frame. Alignment rule (different from a `Row`):

- horizontal: the **`ZStack`'s** `MainAxis` (same position for all);
- vertical: each child's `AlignSelf`, otherwise the `ZStack`'s `CrossAxis`;
- `Stretch` fills the whole inside; `Margin` offsets.

```csharp
// Top-right corner: a Row that fills the stack
ZStack(
    Icon(bell, 24),
    Row(Spacer(), Text("3", 10).Background(0xFFDC2626).CornerRadius(8).Padding(5, 2))
        .AlignSelf(CrossAxisAlignment.Stretch));

// Image with text on top
ZStack(
    Image("cover").ScaleMode(ImageScaleMode.Cover).AlignSelf(CrossAxisAlignment.Stretch),
    Column(Spacer(), Text("Title", 20).TextColor(0xFFFFFFFF)).Padding(16)
        .AlignSelf(CrossAxisAlignment.Stretch));
```

This is the structure of `TrendingCard` (`HomePage`) and the feed pages.

## 7. Scrolling

`.Scrollable(ScrollState)` makes a container scroll (the axis follows the container: `Row` horizontal, otherwise
vertical). The `ScrollState` comes from `UseScroll()` in a **field** to keep the position across rebuilds.

```csharp
private readonly ScrollState _scroll = UseScroll();

public override XenoComponent Body() =>
    Column(/* long content */).Gap(16).Padding(16)
        .Background(Theme.Canvas).CrossAxis(CrossAxisAlignment.Stretch)
        .Scrollable(_scroll);
```

- The scrolling container is the page's **root** in the sample, under the scaffold's fixed bar.
- A scrolling container inside a scrolling container of the same axis is not advised; a `LazyRow` inside a vertical
  page is normal (`HomePage`).
- For a fixed header + scrolling body: `Column(Header(), Body().FlexGrow(1).Scrollable(_scroll))`.

## 8. Virtualised lists

`LazyColumn`/`LazyRow` only compose the visible window (plus a margin); item sizes are **measured**, there is no
height to declare.

```csharp
LazyColumn(People, PersonRow, key: index => index)       // 10,000 items
    .FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)
    .Padding(Spacing.Only(left: 16, right: 16, bottom: 12))
```

| Need | Option |
|---|---|
| Stable identity | `key: item => item.Id` |
| Observe / drive scrolling | `state: _list` (`UseLazyList()`) |
| Full-screen page that snaps | `VerticalPager(items, (item, index) => …, key, state)`; `PageChanged`, `ScrollToPage` |
| Gap between items | `Margin` at the bottom of each item, or a `Gap` on the container |
| List padding | `Padding(...)` on the list itself |

The list needs a **bounded size** on its axis: give it `FlexGrow(1)` in a `Column`, or an explicit `Height`/`Width`
for a list inside a scrolling page (`LazyRow(...).Height(210)`).

## 9. Text and images in layouts

**Text** measures itself; the maximum width is the available space and wrapping is on.

| Need | Tool |
|---|---|
| Limit lines | `MaxLines(2, ellipsis: true)` |
| A single line | `TextWrap(false)` |
| Shrink the font rather than cut | `AutoSize(minimumFontSize)` |
| Text next to an icon without pushing | `FlexGrow(1)` + `FlexShrink(1)` + `MaxLines` |
| Centre the text | `TextAlign(Center)` (inside its box); the box must have width |

**Images**: `Image("name")` takes its natural size (in dp). `.Size(w, h)` replaces it; `ScaleMode` sets the fit
(`Cover` crops, `Fit` includes, `Fill` distorts, `None` natural size centred).

A centred text that does not centre: its box is narrow; give it `AlignSelf(Stretch)` or put it in a
`CenterContent()` container.

## 10. Right-to-left

The subtree **mirrors** when its direction is `RightToLeft` (`FlowDirection(...)`, or the application's direction):
rows start on the right, the left side of padding/margin is the "start" side, `TextAlign(Start)` aligns right.
`Spacing.Directional(start, …, end, …)` and `Start/End` are therefore **always preferable** to absolute
left/right notions. Hit-testing follows the mirrored bounds. Add a `[Preview("…", RightToLeft = true)]` to your
components.

## 11. Varied screens: sizes, safe areas, keyboard

- **Widths**: design with `FlexGrow`/`Stretch`; check with `[Preview(Device = "tablet")]`, `"iphone-se"`,
  `"foldable"` (`Width`/`Height` force a size).
- **Adaptive navigation**: the scaffold picks bottom bar, rail or permanent drawer (`NavigationMode.Automatic`).
- **Overlays**: `FillWidth` + `MaxWidth` to stay centred and bounded ([Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md)).
- **Safe area**: `NativeWindowMetrics.SafeAreaTop` (iOS notch) and `NavigationPresentationContext.SafeAreaTop`; on
  Android the window starts below the status bar (value 0).
- **Keyboard**: the viewport accounts for IME overlap; plan a scrolling container for long forms
  ([Text input](developer-guide.md#11-text-input-and-ime)).

## 12. Common patterns

**List row**

```csharp
Row(
    Image(a.Cover).Size(56, 56).CornerRadius(12).ScaleMode(ImageScaleMode.Cover),
    Column(Text(a.Title, 15).MaxLines(2, ellipsis: true), Text(a.Author, 12).TextColor(Theme.Muted))
        .Gap(3).FlexGrow(1),
    Icon(chevron, 11).TextColor(Theme.Faint))
.Gap(12).Padding(12).CrossAxis(CrossAxisAlignment.Center)
.Background(Theme.Surface).CornerRadius(16).Clickable(() => open(a));
```

**Two-column grid**

```csharp
Column(
    Row(Tile(a).FlexGrow(1), Tile(b).FlexGrow(1)).Gap(10),
    Row(Tile(c).FlexGrow(1), Tile(d).FlexGrow(1)).Gap(10)).Gap(10);
```

**Header, body, footer**

```csharp
Column(Header(), Body().FlexGrow(1), Footer()).CrossAxis(CrossAxisAlignment.Stretch);
```

**Centred on the screen**

```csharp
Column(Icon(emptyIcon, 48), Text("Nothing here"), Button("Retry", Retry))
    .Gap(12).CenterContent().FlexGrow(1);
```

**A floating button over content**: `ZStack(Content(), Column(Spacer(), Row(Spacer(), Fab())).AlignSelf(Stretch).Padding(16))`
— or the scaffold's FAB ([Navigation](navigation-scaffold.md#presentation-slots-and-fab)).

## 13. Layout troubleshooting

| Symptom | Likely cause | Lead |
|---|---|---|
| Nothing shows | Size 0 (list without height, `Box(0, 0)` with no stretching parent) | `FlexGrow(1)`, `Height`, `Stretch` |
| A child overflows | Fixed width too large | `FlexGrow`/`FlexShrink`, `MaxLines` |
| Text is not centred | Text's box is narrow | `Stretch`/`CenterContent` on the parent |
| `Gap` has no effect in a stack | A `ZStack` has no gap | `Margin` or another structure |
| All children of a `ZStack` share one horizontal position | Horizontal alignment comes from the container | Wrap in a `Row` with a `Spacer` |
| List does not scroll | List in a scrolling container without bounded size | `Height`/`FlexGrow` |
| Element misplaced in RTL | Absolute left/right | `Spacing.Directional`, `Start/End` |

## 14. Compose equivalents

| Compose | XenoUI |
|---|---|
| `Column` / `Row` | `Column` / `Row` |
| `Box` (stack) | `ZStack`; `Box(w, h, …)` is a rectangle-container |
| `Modifier.weight(1f)` | `FlexGrow(1)` |
| `Modifier.fillMaxWidth()` | The parent's `CrossAxis(Stretch)` or `AlignSelf(Stretch)` |
| `Modifier.fillMaxSize()` | `FlexGrow(1)` + `Stretch` |
| `Modifier.padding(8.dp)` | `Padding(8)` |
| `Arrangement.spacedBy(8.dp)` | `Gap(8)` |
| `Arrangement.SpaceBetween` | `MainAxis(MainAxisAlignment.SpaceBetween)` |
| `Alignment.CenterHorizontally` | `CrossAxis(Center)` / `AlignSelf(Center)` |
| `Spacer(Modifier.weight(1f))` | `Spacer()` |
| `LazyColumn { items(list, key = { }) }` | `LazyColumn(list, item => …, key: item => …)` |
| `Modifier.verticalScroll(state)` | `Scrollable(scrollState)` |
| `Modifier.size(48.dp)` | `Size(48, 48)` |

Full table: [Comparison with Jetpack Compose](compose-comparison.md).
