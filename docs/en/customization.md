# Customising a component

[Documentation](index.md) · [Developer guide](developer-guide.md) · [Creating a component](components.md) · [Animations](animations.md)

This guide covers everything that lets you adapt the look and behaviour of a XenoUI component: modifiers,
parameters, options objects, themes, shapes, shadows, fonts, icons, variants, right-to-left, and how to make
**your own** components customisable by others. It assumes you have read [Creating a component](components.md)
for the vocabulary (`XenoComponent`, `State<T>`, `Body()`).

## Contents

1. [The six levels of customisation](#1-the-six-levels-of-customisation)
2. [Level 1: modifiers](#2-level-1-modifiers)
3. [What modifiers cannot do](#3-what-modifiers-cannot-do)
4. [Levels 2 and 3: parameters and options objects](#4-levels-2-and-3-parameters-and-options-objects)
5. [Shapes, shadows and elevation](#5-shapes-shadows-and-elevation)
6. [Colours, theme and dark mode](#6-colours-theme-and-dark-mode)
7. [Typography, fonts and icons](#7-typography-fonts-and-icons)
8. [Variants and sizes](#8-variants-and-sizes)
9. [Level 4: wrapping a standard component](#9-level-4-wrapping-a-standard-component)
10. [Level 5: full composition (slots and overlays)](#10-level-5-full-composition-slots-and-overlays)
11. [Level 6: writing your own component](#11-level-6-writing-your-own-component)
12. [Customising navigation and the top bar](#12-customising-navigation-and-the-top-bar)
13. [Visual states: pressed, disabled, selected](#13-visual-states-pressed-disabled-selected)
14. [Right-to-left and adapting to the screen](#14-right-to-left-and-adapting-to-the-screen)
15. [Designing a component that is easy to customise](#15-designing-a-component-that-is-easy-to-customise)
16. [Modifier quick reference](#16-modifier-quick-reference)

---

## 1. The six levels of customisation

From simplest to deepest; pick the first level that is enough.

| Level | Tool | Example | When |
|---|---|---|---|
| 1 | Chained modifiers | `Button("OK", ok).Background(0xFF16A34A).CornerRadius(16)` | Change colour, size, margin of one instance |
| 2 | Component parameters | `Button("OK", ok, fontSize: 18, textColor: 0xFF000000)` | Whatever touches the inner text |
| 3 | Options object | `new SnackbarOptions { Background = …, Icon = … }` | Many settings, with defaults |
| 4 | Wrapper | `Primary("OK", ok)` calling `Button(...)` | An application style reused everywhere |
| 5 | Slots / custom overlay | `Snackbar.Show(app, h => MyContent(h), options)` | Replace the content, keep the behaviour |
| 6 | Your own component | `static class Chip { View(...) }` | No existing component fits |

## 2. Level 1: modifiers

A standard component returns a "normal" `XenoComponent`: every modifier in the API applies to it and
**replaces** the default value the component set.

```csharp
// Button defaults: background 0xFF6200EE, radius 8, elevation 2, padding 20/12, white 16 text.
Button("Save", Save)
    .Background(0xFF16A34A)      // replaces the purple
    .CornerRadius(16)            // replaces 8
    .Padding(28, 16)             // replaces 20/12
    .Elevation(6)                // replaces 2
    .Margin(Spacing.Only(top: 12))
    .FlexGrow(1);                // fills the width of a Row
```

What each family of modifiers invalidates:

| Family | Modifiers | Invalidates |
|---|---|---|
| Layout | `Width`, `Height`, `Size`, `Padding`, `Margin`, `Gap`, `FlexGrow`, `FlexShrink`, `MainAxis`, `CrossAxis`, `AlignSelf`, `CenterContent`, `FlowDirection` | Measure + arrange |
| Visual | `Background`, `CornerRadius`, `Shape`, `Elevation`, `Shadow` | Drawing only |
| Text | `TextColor`, `FontSize`, `FontFamily`, `TextAlign`, `TextWrap`, `MaxLines`, `AutoSize` | Measure (size, font) or drawing (colour) |
| Image | `ScaleMode` | Drawing |
| Interaction | `Clickable`, `Scrollable` | No re-layout |

A purely visual change (colour, shadow, shape) does not trigger layout: it is cheap, including when bound to a
`State` (see [§13](#13-visual-states-pressed-disabled-selected)).

### Order and overwriting

Modifiers write into the entity at the moment of the call:

```csharp
Box(100, 40).Background(0xFFFF0000).Background(0xFF0000FF);   // blue: the last one wins
Box(100, 40).CornerRadius(12).Shape(myShape);                  // CornerRadius ignored: the shape takes over
Box(100, 40).Elevation(8).Shadow(0, 2, 4);                     // the manual shadow replaces the elevation
Box(100, 40).Clickable(a).Clickable(b);                        // b replaces a (a is released)
```

`Padding(horizontal, vertical)` and `Padding(Spacing)` replace **all four sides**: to change a single side, use
`Spacing.Only(...)` and repeat the other values.

## 3. What modifiers cannot do

### 3.1 What a modifier cannot reach

A modifier acts on **the entity it is applied to**, not on its children. There is no style inheritance:

```csharp
Button("OK", ok).TextColor(0xFFFF0000)   // does NOT colour the label: TextColor targets the ZStack,
                                          // the Text is a child
Button("OK", ok, textColor: 0xFFFF0000)  // correct: a parameter of the component
```

Consequences for whoever customises:

- a component's **inner text** is set through its **parameters** (`fontSize`, `textColor`, options);
- if a component does not expose that parameter, use level 4, 5 or 6 (rebuild the content).

Consequences for whoever writes a component: expose everything that touches an inner text or icon (colour, size,
font). See [§15](#15-designing-a-component-that-is-easy-to-customise).

### 3.2 No selectors and no style sheet

There is no CSS, no classes and no implicit global theme: customisation is **explicit** and lives in your C#
code. This is deliberate (no allocation, no runtime resolution); the theme is a class of yours (see
[§6](#6-colours-theme-and-dark-mode)).

## 4. Levels 2 and 3: parameters and options objects

### 4.1 Parameters

Optional parameters with defaults are the most readable way:

```csharp
TextButton("Cancel", Cancel, color: 0xFFDC2626, fontSize: 15)
Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), size: 32).TextColor(0xFF111827)
```

### 4.2 Options objects

Overlays and the top bar are configured through an object whose properties all have defaults:

```csharp
Snackbar.Show(Application, "Message deleted", new SnackbarOptions
{
    ActionLabel = "Undo",
    OnAction = Restore,
    Icon = Glyph("", "Font Awesome 5 Free-Solid-900"),
    Background = 0xFF1F2937,
    TextColor = 0xFFF9FAFB,
    ActionColor = 0xFF93C5FD,
    CornerRadius = 12,
    Elevation = 8,
    Margin = 20,
    Placement = OverlayPlacement.Top,
    TopInset = 24,                       // below the status bar
    Duration = TimeSpan.FromSeconds(6),  // null: stays until removed
    Animation = OverlayAnimation.Scale(200),
    Closed = () => { }
});
```

`SnackbarOptions` properties: `Duration`, `ActionLabel`, `OnAction`, `DismissOnAction`, `Icon`, `IconColor`,
`Background`, `TextColor`, `ActionColor`, `FontSize`, `CornerRadius`, `Elevation`, `Margin`, `MaxWidth`,
`TopInset`, `PaddingX`, `PaddingY`, `Placement`, `AvoidNavigationBar`, `Animation`, `SwipeToDismiss`, `Closed`.
The other overlays follow the same pattern: see [Overlays and TopBar](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md) (French).

`TopBarOptions` (application bar): `Subtitle`, `NavigationIcon`, `OnNavigation`, `Actions`, `CenterTitle`,
`Background`, `Foreground`, `SubtitleColor`, `Height`, `TopInset`, `Elevation`, `TitleSize`, `SubtitleSize`,
`IconSize`, `CornerRadius`, `TitleContent` (replaces title and subtitle with any content).

### 4.3 Deriving options (`with`)

When the object is a `record` (e.g. `NavigationAction`, `DockedNavigationBarStyle`), derive it without mutating
the original:

```csharp
DockedNavigationBarStyle.Default with { /* properties to change */ }
```

For your own options classes, expose shared named instances (`ChipOptions.Success`, `ChipOptions.Danger`) built
once (immutable in practice) and pass them to components.

## 5. Shapes, shadows and elevation

### 5.1 Radius

`CornerRadius(r)` rounds the background, the shadow, the children's clip and the touch area. A radius equal to
half the shortest side gives a pill; for a circle, `Size(d, d).CornerRadius(d / 2)`.

### 5.2 Elevation

`Elevation(dp)` sets a Material shadow computed from `dp` (recommended range 0–24):

| Shadow parameter | Value |
|---|---|
| X offset | 0 |
| Y offset | `dp × 0.5` |
| Blur (sigma) | `dp × 0.8` |
| Colour | Black, alpha = `0.10 + 0.012 × dp`, capped at 0.5 |

`Elevation(0)` or no call: no shadow (the engine skips the shadow when the blur is zero).

### 5.3 Manual shadow

```csharp
Box(120, 80).Background(0xFFFFFFFF).CornerRadius(16)
    .Shadow(offsetX: 0, offsetY: 8, blur: 16, color: 0x60000000);
```

`Shadow` sets the elevation to 0 and overrides any `Elevation` setting. The default colour is 25 % black. The
shadow can be coloured (`0x556D28D9` for a violet glow). It follows the shape (radius or `Shape`).

### 5.4 Free shapes: `XenoShape`

`Shape(XenoShape?)` replaces the rounded rectangle with any outline for **the background, the shadow, the
children's clip and the touch area**. The outline is built in logical units, origin at the entity's top-left, and
**only when the size changes**; one instance can be shared by entities of different sizes.

```csharp
using SkiaSharp;

// Tag: top-right corner cut
static readonly XenoShape Tag = XenoShape.Create((path, w, h) =>
{
    float cut = Math.Min(16, Math.Min(w, h) / 2);
    path.MoveTo(0, 0);
    path.LineTo(w - cut, 0);
    path.LineTo(w, cut);
    path.LineTo(w, h);
    path.LineTo(0, h);
    path.Close();
});

Text("NEW", 11).TextColor(0xFFFFFFFF).Padding(14, 8)
    .Background(0xFFDC2626).Shape(Tag).Elevation(2);
```

Subclass for a parameterised, testable shape:

```csharp
public sealed class RoundedTriangle : XenoShape
{
    protected override void BuildPath(SKPath path, float width, float height)
    {
        path.MoveTo(width / 2, 0);
        path.LineTo(width, height);
        path.LineTo(0, height);
        path.Close();
    }
}
```

Built in: `XenoShapes.TopNotch(notchRadius, notchCenterY, notchCenterX, shoulderRadius, topCornerRadius,
bottomCornerRadius)` — a bar with a circular notch for a floating button, used by center-action navigation
([details](navigation-scaffold.md#custom-shapes)).

Rules: `CornerRadius` is ignored on an entity that has a shape; `Shape(null)` restores the rounded rectangle; the
outline also serves as the children's clip and the touch area.

## 6. Colours, theme and dark mode

### 6.1 Format

ARGB `uint`: `0xAARRGGBB`. `0xFF6D28D9` is opaque; `0x336D28D9` ≈ 20 %; `0x00000000` is transparent. There is no
per-entity opacity modifier: you vary the colour's alpha (see
[Animations](animations.md#4-opacity-and-fading-a-component)).

### 6.2 A theme is a class of yours

The sample application centralises its palette (`Design/Theme.cs`):

```csharp
internal static class Theme
{
    internal const uint Canvas  = 0xFF0A0A0F;
    internal const uint Surface = 0xFF15151D;
    internal const uint Ink     = 0xFFF4F4F8;
    internal const uint Muted   = 0xFF8D8DA1;
    internal const uint Accent  = 0xFFFE2C55;
    internal const string Fa    = "Font Awesome 5 Free-Solid-900";
    internal static IconDef Glyph(string glyph) => Compose.Glyph(glyph, Fa);
}
```

and **application building blocks** (`Design/Ui.cs`) that apply this theme to the standard components:

```csharp
internal static XenoComponent Primary(string label, Action tap, float size = 14) =>
    Button(label, tap, size).Background(Theme.Accent).CornerRadius(14).Padding(16, 13);

internal static XenoComponent Card(params XenoComponent[] children) =>
    Column(children).Gap(10).Padding(16).Background(Theme.Surface).CornerRadius(20)
        .CrossAxis(CrossAxisAlignment.Stretch);
```

Pages no longer call `Button(...)` directly but `Ui.Primary(...)`: changing a button's style across the whole
application means editing one line. This is level 4.

### 6.3 Light / dark, or several themes at run time

`const` values do not change at run time. For a dynamic theme, make the palette an **object**:

```csharp
public sealed record Palette(uint Canvas, uint Surface, uint Ink, uint Muted, uint Accent)
{
    public static readonly Palette Dark  = new(0xFF0A0A0F, 0xFF15151D, 0xFFF4F4F8, 0xFF8D8DA1, 0xFFFE2C55);
    public static readonly Palette Light = new(0xFFFAFAFB, 0xFFFFFFFF, 0xFF111827, 0xFF6B7280, 0xFFFE2C55);
}

public static class Theme
{
    public static Palette Current { get; private set; } = Palette.Dark;
    public static void Use(Palette palette) => Current = palette;
}
```

Components read `Theme.Current.Surface` **at composition time**; switching theme therefore requires
recomposition:

```csharp
Theme.Use(Palette.Light);
Rebuild();                 // the current page
// Already-mounted scaffold destinations and its slots (bar, rail, drawer):
// _navigation.RefreshChrome();  (see Navigation and NavigationScaffold)
```

A scaffold's destinations keep their tree once mounted: for them to pick up the new theme, rebuild them (a slot
context's `RefreshChrome()` rebuilds the chrome; pages rebuild through their `Rebuild()`).

For a colour change **without a rebuild**, bind the colour to a `State<uint>`:

```csharp
private readonly State<uint> _surface = UseState(Palette.Dark.Surface);
Column(...).Background(_surface);
// later:
_surface.Value = Palette.Light.Surface;
```

This suits a few key surfaces; beyond that, rebuilding is simpler.

### 6.4 Contrast

Check the text/background pairs (≥ 4.5:1 for body text, ≥ 3:1 for large text and icons). A translucent colour
over a variable background changes contrast: test on both themes.

## 7. Typography, fonts and icons

### 7.1 Size and formatting

`Text(value, size)`; then `FontSize`, `TextAlign`, `TextWrap(false)` for a single line, `MaxLines(n,
ellipsis: true)`, `AutoSize(minimumFontSize)` to reduce the font down to a minimum rather than cut. `AutoSize`
requires `minimumFontSize ≤ size` (otherwise `ArgumentOutOfRangeException`) and never goes below that minimum.

### 7.2 Custom fonts

Drop the file in `Assets/Fonts/` (in the application project); it is loaded at startup and referenced by name:

```csharp
Text("Title", 24).FontFamily("Roboto-Bold")
```

The name is the file name without its extension (the sample uses `Font Awesome 5 Free-Solid-900`). An unknown name
falls back to the system font. The same `FontFamily` applies to icons (which are text).

### 7.3 Icons

```csharp
Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), 20).TextColor(0xFFE11D48)
```

- The glyph is a Unicode character of the icon font (`""`).
- Change the colour with `TextColor`. Change the size with `Icon`'s second argument or `FontSize`.
- Group glyph and font in an `IconDef` (`Glyph(...)`): overlays, the top bar and navigation expect an `IconDef`.
- Several icon fonts coexist (`Fa`, `FaRegular`, `FaBrands` in the sample): each `IconDef` carries its own.

## 8. Variants and sizes

Model variants with an **enum** rather than several booleans:

```csharp
public enum ButtonKind { Primary, Secondary, Danger, Ghost }
public enum ButtonSize { Small, Medium, Large }

public static class Btn
{
    public static XenoComponent View(string label, Action tap,
        ButtonKind kind = ButtonKind.Primary, ButtonSize size = ButtonSize.Medium)
    {
        (uint bg, uint fg) = kind switch
        {
            ButtonKind.Primary   => (Theme.Accent, 0xFFFFFFFF),
            ButtonKind.Secondary => (Theme.SurfaceHigh, Theme.Ink),
            ButtonKind.Danger    => (0xFFDC2626, 0xFFFFFFFF),
            _                    => (0x00000000, Theme.Accent)       // Ghost: transparent background
        };
        (float font, float px, float py) = size switch
        {
            ButtonSize.Small => (13f, 14f, 8f),
            ButtonSize.Large => (17f, 28f, 16f),
            _                => (15f, 20f, 12f)
        };
        return Button(label, tap, font, fg).Background(bg).CornerRadius(py + 4).Padding(px, py);
    }
}
```

A note on `Ghost`: a **transparent** background has no press tint. Give it a translucent background
(`0x1AFE2C55`) if you want visual feedback.

## 9. Level 4: wrapping a standard component

Wrapping keeps the component's behaviour (tap, press tint, centring) and imposes your style:

```csharp
public static XenoComponent Cta(string label, Action tap) =>
    Button(label, tap, fontSize: 16, textColor: 0xFFFFFFFF)
        .Background(Theme.Current.Accent)
        .CornerRadius(28)
        .Padding(32, 16)
        .Elevation(6);
```

The result is a `XenoComponent`: the caller can still chain `.FlexGrow(1)`, `.Margin(…)`. **Do not freeze** what the
caller will want to adjust (width, margin): leave that to the call-site modifiers.

`Widgets` is a `partial` class of the standard library: you cannot *redefine* `Button` in it (name conflict). Give
your wrappers another name (`Primary`, `Cta`) or put them in your own class (`Ui`).

## 10. Level 5: full composition (slots and overlays)

Overlays offer two overloads: the default content, and **content you provide** that keeps placement, timing,
animation, scrim and gestures:

```csharp
// Default content
Snackbar.Show(app, "Saved");

// Your content, the snackbar's behaviour
Snackbar.Show(app, handle =>
    Row(
        Icon(Glyph("", Theme.Fa), 18).TextColor(0xFF22C55E),
        Text("Synced", 14).TextColor(0xFFFFFFFF).FlexGrow(1),
        TextButton("Close", handle.Dismiss, 0xFF93C5FD)
    ).Gap(10).CrossAxis(CrossAxisAlignment.Center)
     .Padding(16, 12).Background(0xFF111827).CornerRadius(14).Elevation(8),
    new SnackbarOptions { Duration = TimeSpan.FromSeconds(3) });

// Completely free overlay
Application.Overlays.Show(h => MyCard(h.Dismiss), new OverlayOptions
{
    Placement = OverlayPlacement.Center, Modal = true, ScrimColor = 0x99000000,
    Animation = OverlayAnimation.Scale(220).WithFade(),
    SwipeToDismiss = OverlaySwipe.Down, Closed = () => { }
});
```

Each overlay also exposes its visual alone (`Snackbar.Body`, `Dialog.Surface`, `Popup.Surface`,
`BottomSheet.Surface`, `Alert.Content`, `Popup.Menu`) for previews or composition elsewhere.

## 11. Level 6: writing your own component

When nothing fits, [write a component](components.md). The customisability template:

```csharp
public static class Banner
{
    public static XenoComponent View(string message, BannerOptions? options = null)
    {
        options ??= new BannerOptions();
        return Row(
                options.Icon is { } icon ? Icon(icon, 20).TextColor(options.Foreground) : XenoComponent.None,
                Text(message, options.FontSize).TextColor(options.Foreground).FlexGrow(1).FlexShrink(1))
            .Gap(10).CrossAxis(CrossAxisAlignment.Center)
            .Padding(options.PaddingX, options.PaddingY)
            .Background(options.Background).CornerRadius(options.CornerRadius);
    }
}
```

The root carries defaults; the caller adjusts the rest with modifiers (`Banner.View(...).Margin(16)`), and the text
with `BannerOptions`.

## 12. Customising navigation and the top bar

- **Top bar**: `TopBarOptions` (see [§4.2](#42-options-objects)) provided by the page that implements
  `ITopBarPage`; the scaffold mounts it through `TopBars.ForActivePage`. `TitleContent` replaces title and
  subtitle. `RebuildTopBar()` rebuilds it when the state it reads changes.
- **Tab bar, rail, drawer**: scaffold presentation slots (`NavigationPresentationContext`),
  `DockedNavigationBarStyle`, shapes (`XenoShapes.TopNotch`), centre action and FAB. See
  [Navigation and NavigationScaffold](navigation-scaffold.md#presentation-slots-and-fab).
- **Tab selection change**: `NavigationSelectionTransition` (icon, label, indicator, surface, badge) — see
  [Animations §7](animations.md#7-animating-selection-in-navigation).
- **Page transitions**: `NavTransition` — see [Animations §6](animations.md#6-page-transitions-navtransition).

## 13. Visual states: pressed, disabled, selected

**Pressed.** Automatic for any clickable entity with a non-transparent background: a 15 % black tint (computed in
the engine, no layout). To change the appearance on touch beyond the tint, drive the colour with a `State` in
the callback.

**Selected / active.** Pass the state as a parameter and choose the colours at composition (see the segmented bar
in [Creating a component §15.4](components.md#154-segmented-bar)), or bind the colours to a `State<uint>` to avoid
the rebuild:

```csharp
public static XenoComponent Tab(string label, State<bool> selected, Action tap) { … }
```

**Disabled.** There is no `Disabled` modifier: simply do not set `Clickable`, and dim the colours (reduced
alpha):

```csharp
public static XenoComponent ActionButton(string label, bool enabled, Action tap)
{
    var surface = ZStack(
            Text(label, 16).TextColor(enabled ? 0xFFFFFFFF : 0x99FFFFFF)
                .AlignSelf(CrossAxisAlignment.Center))
        .Background(enabled ? 0xFF6D28D9 : 0x666D28D9)
        .CornerRadius(8).Padding(20, 12).CenterContent();
    return enabled ? surface.Clickable(tap) : surface;   // disabled: no callback, no tint
}
```

`Button` always sets its `Clickable`: for a truly inert version, recompose the same tree as above, or ignore the
action in the callback when `enabled` is false.

**Error / success.** Vary the border/background/text colour and the message; do not rely on colour alone (add an
icon or text).

## 14. Right-to-left and adapting to the screen

- **RTL**: `FlowDirection(RightToLeft)` on a subtree, or the application's global direction. `Row`s mirror, the
  left side of padding/margin is the "start" side. Use `Spacing.Directional(start, top, end, bottom)`.
- **Screen size**: everything is logical (dp). Use `FlexGrow` and `Stretch`; fixed dp `Width`s suit identity
  elements (avatar, icon), not content columns.
- **Tablet / large screen**: overlays accept `FillWidth` + `MaxWidth` to stay centred and bounded; the scaffold
  switches bottom / rail / permanent drawer ([Navigation](navigation-scaffold.md)).
- **Safe area**: `NativeWindowMetrics.SafeAreaTop` (iOS notch) and the options' `TopInset`; on Android the window
  starts below the status bar.

## 15. Designing a component that is easy to customise

1. **Defaults on the root, overridable.** Set `Background`, `CornerRadius`, `Padding` on the returned root; the
   caller replaces them with a modifier.
2. **Expose the inner text.** Colour, size, font, number of lines: parameters or options.
3. **An `IconDef` rather than two strings** for icons; `XenoComponent.None` for "no icon".
4. **Do not fix what the caller sizes** (width, margin, `FlexGrow`).
5. **Mutate nothing external**: everything goes through parameters and callbacks.
6. **Offer a slot** (`XenoComponent` or `Func`) when the content genuinely varies (header, footer, action).
7. **Separate visual from behaviour**: one method that only composes (`Body`, `Surface`), another that shows or
   activates it (`Show`).
8. **Preview the variants**: default, customised, RTL, long text, light and dark theme.
9. **Document** the defaults in the XML comment (the library's code does).

## 16. Modifier quick reference

| Modifier | Parameters | Notes |
|---|---|---|
| `Width(w)` / `Height(h)` / `Size(w, h)` | dp | 0 = auto |
| `Padding(all)` / `(h, v)` / `(Spacing)` | dp | Replaces all four sides |
| `Margin(all)` / `(Spacing)` | dp | |
| `Gap(g)` | dp | Between children (not applied in a `ZStack`) |
| `MainAxis(a)` | `Start/Center/End/SpaceBetween/SpaceAround/SpaceEvenly` | In a `ZStack`: horizontal position |
| `CrossAxis(a)` | `Start/Center/End/Stretch` | |
| `AlignSelf(a)` | same | One child; in a `ZStack`: vertical position |
| `CenterContent()` | | Centres on both axes |
| `FlexGrow(f)` / `FlexShrink(f)` | ≥ 0 | `FlexShrink` rejects negative or non-finite values |
| `FlowDirection(d)` | `Inherit/LeftToRight/RightToLeft` | Subtree |
| `Background(uint)` / `(State<uint>)` | ARGB | |
| `CornerRadius(r)` | dp | Ignored if `Shape` |
| `Shape(XenoShape?)` | | `null` restores the radius |
| `Elevation(dp)` | 0–24 | Computed shadow |
| `Shadow(x, y, blur, color)` | dp, dp, sigma, ARGB | Replaces the elevation |
| `TextColor(uint)` / `(State<uint>)` | ARGB | Entity carrying the text only |
| `FontSize(s)` | dp | |
| `FontFamily(name)` | file in `Assets/Fonts` | |
| `TextAlign(a)` | `Start/Center/End` | |
| `TextWrap(bool)` | | On by default |
| `MaxLines(n, ellipsis)` | `n ≥ 0` | |
| `AutoSize(min)` | `0 < min ≤ size` | |
| `ScaleMode(m)` | `Cover/Fit/Fill/None` | Images |
| `Clickable(Action)` | | Replaces the previous callback |
| `Scrollable(ScrollState, horizontal?)` | | Default axis follows the container |
| `NativeVideo(...)` | `MediaHandle` or `State` | Native video output (reference backend) |

`XenoComponent.None` accepts all these modifiers and does nothing: you can chain without testing.
