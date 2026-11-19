# Animations and motion

[Documentation](index.md) · [Developer guide](developer-guide.md) · [Creating a component](components.md) · [Customising](customization.md) · [Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md) (French) · [Navigation](navigation-scaffold.md)

This guide describes **everything XenoUI can animate today**, how to configure it, its units, its limits, and how
to get motion on your own components where the framework has no dedicated API (yet). It is written from the code
of the current branch; where a behaviour does not exist, this guide says so.

## Contents

1. [Overview: who animates what](#1-overview-who-animates-what)
2. [Common principles](#2-common-principles)
3. [Touch feedback and state changes](#3-touch-feedback-and-state-changes)
4. [Opacity and fading a component](#4-opacity-and-fading-a-component)
5. [Animating yourself, step by step](#5-animating-yourself-step-by-step)
6. [Page transitions: `NavTransition`](#6-page-transitions-navtransition)
7. [Animating selection in navigation](#7-animating-selection-in-navigation)
8. [Animating an overlay: `OverlayAnimation`](#8-animating-an-overlay-overlayanimation)
9. [Reduced motion](#9-reduced-motion)
10. [Easing curves](#10-easing-curves)
11. [Recipes](#11-recipes)
12. [Performance and diagnosis](#12-performance-and-diagnosis)
13. [Current limits](#13-current-limits)

---

## 1. Overview: who animates what

| Need | Mechanism | Animated by the engine? | Section |
|---|---|---|---|
| Tint of a pressed element | Automatic on `Clickable` | Yes (instant: pressed / released) | [§3](#3-touch-feedback-and-state-changes) |
| Change text, text colour, background colour, a progress bar | Bound `State<T>` (`Text(state)`, `.Background(state)`…) | **Immediate** update, no interpolation | [§3](#3-touch-feedback-and-state-changes) |
| Fade / interpolation of a component | State driven by your timer | No: it is your code (recipe provided) | [§4](#4-opacity-and-fading-a-component), [§5](#5-animating-yourself-step-by-step) |
| Moving from one page to another (push/pop) | `NavTransition` | **Yes**, cold-compiled, no per-frame allocation | [§6](#6-page-transitions-navtransition) |
| Changing main destination (tabs, rail, drawer) | `DestinationTransition`, `TransitionFromTo` | Yes | [§6](#6-page-transitions-navtransition) |
| Selected navigation item (icon, label, indicator, background, badge, FAB) | `NavigationSelectionTransition` | Yes | [§7](#7-animating-selection-in-navigation) |
| A snackbar, dialog, popup, bottom sheet appearing / disappearing | `OverlayAnimation` | Yes | [§8](#8-animating-an-overlay-overlayanimation) |
| Drag to dismiss an overlay, drawer, scrolling | Internal gestures + physics | Yes | [Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md) (French) |

There is **no** generic `Animate(...)`, `Opacity(...)`, `TranslateX(...)` or `Scale(...)` modifier on an arbitrary
`XenoComponent` today, and no public spring engine. "Native" motion applies to *scenes* (pages, navigation chrome,
overlays); the motion of an individual component is done with driven states
([§5](#5-animating-yourself-step-by-step)).

## 2. Common principles

**Presentation values, after layout.** Navigation and overlay animations change the *presentation* (translation,
scale, opacity, rotation, clipping) of a scene or element **after** layout. They neither re-measure nor reposition
anything; for the elements of a navigation chrome, they do not move a destination's touch target.

**Cold compilation.** A `NavTransition`, a `NavigationSelectionTransition` and an `OverlayAnimation` are built
**once** by a builder; during frames the engine only walks arrays of tracks and never calls your code again. In
particular, an easing function you supply is **sampled into a table (LUT) at build time**: it is not called per
frame.

**Immutability.** These objects are immutable and shareable; create them once (`static readonly`) and reuse them.

**Track.** An animation is a set of tracks. A track = one property, a start value (`From`), an end value (`To`), a
progress interval (`StartFraction`…`EndFraction`, from 0 to 1) and an easing. Several tracks with staggered
intervals produce staggered motion.

**Duration in milliseconds**, **normalised fractions** for progress, **easing** among `Linear`, `Standard`,
`Emphasized`, `EaseIn`, `EaseOut` or a function of yours.

## 3. Touch feedback and state changes

### 3.1 Touch tint

Any `Clickable` entity whose background is not transparent is darkened by 15 % while pressed. It is a plain colour
change at draw time (no layout): no interpolation, the tint appears on press and disappears on release. It does not
apply to text fields.

### 3.2 Bound state changes

These bindings update the entity **immediately** when the `State` changes:

| Binding | Effect |
|---|---|
| `Text(State<T>)`, `Text($"…{state}")` | Replaces the text and re-measures it |
| `.TextColor(State<uint>)` | Changes the text colour |
| `.Background(State<uint>)` | Changes the background colour |
| `LinearProgress(State<float>)` | Resizes the filled part (0–1) |
| `ImageFile(State<string?>)` | Changes the displayed image |
| `.NativeVideo(State<…>)` | Enables or changes the video output |

They do **not transition**: it is a jump. For a gradual change, evolve the `State` yourself
([§5](#5-animating-yourself-step-by-step)). Assigning a `State` an *identical* value is ignored.

## 4. Opacity and fading a component

XenoUI has no opacity modifier on an arbitrary entity. Two techniques apply depending on the target.

### 4.1 Fading through the colour's alpha channel (one element)

Text and background are ARGB colours: varying the alpha byte gives a fade.

```csharp
private readonly State<uint> _titleColor = UseState(0x00111827u);   // invisible text
private readonly State<uint> _cardColor  = UseState(0x00FFFFFFu);   // invisible background

Column(Text("Welcome", 20).TextColor(_titleColor))
    .Padding(16).Background(_cardColor).CornerRadius(16);
```

Going from `0x00…` to `0xFF…` in successive steps (see [§5](#5-animating-yourself-step-by-step)) makes the text and
background appear. Limit: the binding exists for `Background` and `TextColor`, not for images (`Icon(...)
.TextColor(State<uint>)` does work: an icon is text).

### 4.2 Fading a whole subtree: go through a scene

The opacity of an **entire scene** (page, overlay, chrome) is rendered through a composited layer: the whole
subtree fades as one block, with no overlap artefact. To fade a group of overlapping components, show it as a
non-modal overlay with `OverlayAnimation.Fade(...)` ([§8](#8-animating-an-overlay-overlayanimation)).

## 5. Animating yourself, step by step

The mechanism is: **a `State` + a timer + `IUiDispatcherCapability.Post`**. The `SampleApp`'s `FeedPage` already
uses it (`After(ms, action)` for a heart that disappears, a toast that withdraws). What follows generalises that
pattern into interpolation.

> This is **application code**, not a framework API. The timer is not synchronised with the screen refresh: steps
> are discrete (about 60 per second at best). Reserve this mechanism for short, cheap animations (colour, alpha,
> progress); to move or resize panels, prefer scenes ([§6](#6-page-transitions-navtransition),
> [§8](#8-animating-an-overlay-overlayanimation)).

### 5.1 Toolbox

```csharp
using System.Diagnostics;
using XenoUI.Core.NativeTunnel;

namespace MyApp.Design;

/// <summary>Easing functions and colour interpolation (application code).</summary>
public static class Motion
{
    public static float Linear(float t) => t;
    public static float EaseOutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
    public static float EaseInOut(float t) => t * t * (3 - 2 * t);

    private static int Channel(uint color, int shift) => (int)((color >> shift) & 0xFF);

    private static uint Mix(uint from, uint to, float t, int shift)
    {
        int a = Channel(from, shift), b = Channel(to, shift);
        return (uint)Math.Clamp((int)MathF.Round(a + (b - a) * t), 0, 255);
    }

    /// <summary>Channel-by-channel interpolation of two ARGB colours.</summary>
    public static uint Lerp(uint from, uint to, float t) =>
        (Mix(from, to, t, 24) << 24) | (Mix(from, to, t, 16) << 16) |
        (Mix(from, to, t, 8) << 8) | Mix(from, to, t, 0);

    /// <summary>The same colour with an alpha from 0 to 1.</summary>
    public static uint WithAlpha(uint color, float alpha) =>
        ((uint)Math.Clamp((int)MathF.Round(alpha * 255), 0, 255) << 24) | (color & 0x00FFFFFF);
}

/// <summary>Runs a 0→1 interpolation and applies each step on the UI thread.</summary>
public static class Tween
{
    /// <returns>Cancellation source: call <c>Cancel()</c> to interrupt.</returns>
    public static CancellationTokenSource Run(IUiDispatcherCapability ui, int milliseconds,
        Func<float, float> ease, Action<float> apply, Action? done = null)
    {
        var source = new CancellationTokenSource();
        var token = source.Token;
        int pending = 0;                                  // at most one step waiting on the UI side

        _ = Task.Run(async () =>
        {
            var clock = Stopwatch.StartNew();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    float t = milliseconds <= 0 ? 1f
                        : Math.Min(1f, clock.ElapsedMilliseconds / (float)milliseconds);
                    float eased = ease(t);
                    bool last = t >= 1f;
                    if (last || Interlocked.Exchange(ref pending, 1) == 0)
                    {
                        ui.Post(() =>
                        {
                            Volatile.Write(ref pending, 0);
                            if (token.IsCancellationRequested) return;
                            apply(eased);
                            if (last) done?.Invoke();
                        });
                    }
                    if (last) break;
                    await Task.Delay(16, token);
                }
            }
            catch (OperationCanceledException) { /* cancelled: nothing to do */ }
        });
        return source;
    }
}
```

Design points:

- `apply` runs **on the UI thread** (through `Post`): that is where you assign the `State`s.
- `pending` avoids queueing steps if the UI thread is late; the **last** step is always posted.
- `ease` is called on the background thread: keep it pure and fast.
- Cancel the previous animation before starting a new one on the same states.

### 5.2 Example: fade-in of a card

```csharp
public sealed class WelcomePage : XenoPage
{
    private readonly State<uint> _surface = UseState(0x00FFFFFFu);
    private readonly State<uint> _title   = UseState(0x00111827u);
    private CancellationTokenSource? _anim;

    public override void OnActivated() => FadeIn();
    public override void OnDeactivated() => _anim?.Cancel();
    public override void OnDestroy() => _anim?.Cancel();

    private void FadeIn()
    {
        _anim?.Cancel();
        var ui = Native.Require<IUiDispatcherCapability>();
        bool reduced = Native.Get<IMotionPreferencesCapability>()?.ReducedMotion == true;
        _anim = Tween.Run(ui, reduced ? 0 : 320, Motion.EaseOutCubic, t =>
        {
            _surface.Value = Motion.WithAlpha(0xFFFFFFFF, t);
            _title.Value = Motion.WithAlpha(0xFF111827, t);
        });
    }

    public override XenoComponent Body() =>
        Column(Text("Welcome", 22).TextColor(_title))
            .Padding(24).Background(_surface).CornerRadius(20).Margin(24);
}
```

### 5.3 Example: colour moving from one state to another

```csharp
private readonly State<uint> _chip = UseState(0xFFE5E7EB);
private CancellationTokenSource? _chipAnim;

private void SetSelected(bool selected)
{
    uint from = _chip.Value, to = selected ? 0xFF6D28D9 : 0xFFE5E7EB;
    _chipAnim?.Cancel();
    _chipAnim = Tween.Run(Native.Require<IUiDispatcherCapability>(), 180, Motion.EaseInOut,
        t => _chip.Value = Motion.Lerp(from, to, t));
}

// Body(): Text("Swift", 13).Padding(12, 6).CornerRadius(16).Background(_chip).Clickable(() => SetSelected(true))
```

### 5.4 Example: animated progress

```csharp
private readonly State<float> _progress = UseState(0f);

private void ProgressTo(float target)
{
    float from = _progress.Value;
    Tween.Run(Native.Require<IUiDispatcherCapability>(), 400, Motion.EaseOutCubic,
        t => _progress.Value = from + (target - from) * t);
}

// Body(): LinearProgress(_progress, height: 4, trackColor: 0x33000000, fillColor: 0xFF6D28D9)
```

Each `LinearProgress` step triggers a layout of the bar (two `FlexGrow`s): acceptable for a thin bar, avoid the
equivalent on large containers.

### 5.5 Mandatory safeguards

| Rule | Why |
|---|---|
| Change `State`s on the UI thread only | Callbacks mark entities dirty; it is not thread-safe from a background thread |
| Cancel in `OnDeactivated`/`OnDestroy` | Avoid late writes to a page that was left |
| Test `IsActive` if you resume an async animation | A background page has no need to animate |
| Honour reduced motion ([§9](#9-reduced-motion)) | Accessibility |
| Do not animate a `Rebuild()` | A rebuild recomposes the page; animate `State`s |
| Do not start a timer per list item | Drive a shared `State`, or animate on selection only |

## 6. Page transitions: `NavTransition`

This is the most complete "native" animation: it runs in the render loop, on whole scenes.

### 6.1 Vocabulary

A `NavTransition` has:

- **`Enter`** tracks (the scene that arrives, "incoming");
- **`Exit`** tracks (the scene that leaves, "outgoing");
- a **duration** (the builder's default is 280 ms);
- its compiled inverse, `Reversed()`, used for going back.

A scene starts in the identity state: scale 1, opacity 1, translation 0, pivot at the centre, no clipping. Each
track evolves one property of that state.

### 6.2 Animatable properties (`NavAnimatedProperty`)

| Property | Unit / meaning for a **page** | Neutral value |
|---|---|---|
| `TranslationX` | Fraction of the screen **width** (1 = one full width) | 0 |
| `TranslationY` | Fraction of the screen **height** | 0 |
| `ScaleX`, `ScaleY` | Factor around the pivot | 1 |
| `Opacity` | 0–1, rendered through a layer over the whole scene | 1 |
| `Rotation` | Degrees around the pivot | 0 |
| `PivotX`, `PivotY` | Fraction of width/height (0.5 = centre) | 0.5 |
| `ClipLeft`, `ClipTop` | Left/top edge of the clip, fraction (0 = edge) | 0 |
| `ClipRight`, `ClipBottom` | Right/bottom edge of the clip, fraction (1 = edge) | 1 |
| `CornerRadius` | dp, rounds the scene's clip | 0 |
| `Elevation` | Defined and evaluated, but **not used by scene rendering** in this version | 0 |

> In an **overlay** with `OverlayAnimation.Custom(...)`, translations are in **dp** relative to the panel's final
> position (see [§8](#8-animating-an-overlay-overlayanimation)). For a page they are fractions of the screen.

### 6.3 Building a transition

```csharp
public static class AppTransitions
{
    public static readonly NavTransition SlideAndFade = NavTransition.Create()
        .Enter(scene => scene
            .TranslateX(1, 0, NavEasing.Emphasized)     // arrives from the right
            .Opacity(0.4f, 1))
        .Exit(scene => scene
            .TranslateX(0, -0.18f)                      // leaves slightly to the left
            .Opacity(1, 0.8f))
        .Duration(300)
        .Build();
}
```

Scene builder shortcuts (`NavSceneTransitionBuilder`):

| Method | Equivalent to |
|---|---|
| `TranslateX(from, to, easing)` | `Animate(TranslationX, …)` |
| `TranslateY(from, to, easing)` | `Animate(TranslationY, …)` |
| `Opacity(from, to, easing)` | `Animate(Opacity, …)` |
| `Scale(from, to, easing)` | Two tracks: `ScaleX` and `ScaleY` |
| `Animate(property, from, to, easing, startFraction, endFraction)` | Full track |
| `Animate(property, from, to, Func<float,float> easing, startFraction, endFraction, samples)` | Your own easing (sampled into a table) |

Constraints validated by the builder: `from` and `to` finite; `0 ≤ startFraction ≤ endFraction ≤ 1`; `samples`
between 2 and 4096; your own easing must return finite values. A track with `startFraction == endFraction` jumps to
its final value at that instant.

### 6.4 Staggering: progress fractions

Each track lives in its own `[StartFraction, EndFraction]` window of the total duration:

```csharp
NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.TranslationY, 0.08f, 0,
                 NavEasing.EaseOut, startFraction: 0.00f, endFraction: 0.70f)
        .Animate(NavAnimatedProperty.Opacity, 0, 1,
                 NavEasing.Linear, startFraction: 0.20f, endFraction: 1.00f))
    .Exit(scene => scene
        .Animate(NavAnimatedProperty.Opacity, 1, 0,
                 NavEasing.Linear, startFraction: 0.00f, endFraction: 0.25f))
    .Duration(320)
    .Build();
```

This is how `FadeThrough` is built: the outgoing scene fades during the first 35 per cent, then the incoming scene
appears (and grows from 0.96 to 1).

### 6.5 Presets: `NavTransitions`

| Preset | Enter | Exit | Default duration |
|---|---|---|---|
| `None` | — | — | 0 |
| `Fade(d)` | Opacity 0→1 | Opacity 1→0 | 220 |
| `FadeThrough(d)` | Opacity 0→1 over the last 65 %, scale 0.96→1 | Opacity 1→0 over the first 35 % | 260 |
| `SlideHorizontal(d)` | `TranslateX` 1→0 | `TranslateX` 0→−0.25, opacity 1→0.85 | 280 |
| `SlideHorizontalBack(d)` | `TranslateX` −0.25→0, opacity 0.85→1 | `TranslateX` 0→1 | 260 |
| `SlideVertical(d)` | `TranslateY` 1→0 | `TranslateY` 0→−0.15, opacity 1→0.85 | 280 |
| `ScaleFade(d)` | Scale 0.92→1, opacity 0→1 | Scale 1→0.96, opacity 1→0 | 260 |
| `PlatformDefault` | = `SlideHorizontal()` | | |
| `PlatformBack` | = `SlideHorizontalBack()` | | |

### 6.6 Applying a transition

**Push/pop navigation** (from a page):

```csharp
// Separate forward and back
Navigate<DetailsPage>(NavTransitions.SlideVertical(320), NavTransitions.Fade(220));

// A single transition (going back uses the compiled inverse)
Navigate<DetailsPage>(AppTransitions.SlideAndFade);

// Through options
Navigate<DetailsPage>(new NavigationOptions
{
    Transition = AppTransitions.SlideAndFade,
    PopTransition = NavTransitions.Fade()
});
```

**Scaffold main destinations** (tabs, rail, drawer):

```csharp
private readonly NavigationScaffoldState _navigation = new(/* … */)
{
    DestinationTransition = NavTransitions.FadeThrough()
};

// Cold path: a specific transition between two destinations
_navigation.TransitionFromTo<HomePage, SearchPage>(
    forward: NavTransitions.SlideHorizontal(),
    backward: NavTransitions.SlideHorizontalBack());
```

Without an explicit pair, the order of the destinations gives the direction: the configured transition is used
forward and its compiled inverse backward.

### 6.7 `Reversed()`

`transition.Reversed()` returns the temporal inverse: the enter and exit tracks swap roles, `From`/`To` are
swapped, progress windows are flipped (`1 − end` … `1 − start`), `EaseIn` and `EaseOut` are exchanged, and the
easing LUTs are reversed. The result is cached. Navigating back is therefore the **exact mirror** of going forward
(the leaving page stays on top of the one it reveals).

### 6.8 Example transitions

**Reveal from the centre (clip + radius)**:

```csharp
public static readonly NavTransition RevealFromCenter = NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.ClipLeft,   0.5f, 0, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.ClipTop,    0.5f, 0, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.ClipRight,  0.5f, 1, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.ClipBottom, 0.5f, 1, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.CornerRadius, 48, 0, NavEasing.EaseOut))
    .Exit(scene => scene.Opacity(1, 0.7f))
    .Duration(360)
    .Build();
```

**Bounce (your own easing)**:

```csharp
public static readonly NavTransition Bounce = NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.TranslationY, 1, 0,
                 easing: t => 1 - MathF.Exp(-6 * t) * MathF.Cos(10 * t))
        .Animate(NavAnimatedProperty.Opacity, 0, 1, NavEasing.EaseOut,
                 startFraction: 0, endFraction: 0.4f))
    .Exit(scene => scene.Opacity(1, 0))
    .Duration(480)
    .Build();
```

**Rotation + scale (a card that half flips)**:

```csharp
NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.Rotation, -8, 0, NavEasing.EaseOut)
        .Scale(0.85f, 1, NavEasing.EaseOut)
        .Opacity(0, 1))
    .Exit(scene => scene.Scale(1, 1.05f).Opacity(1, 0))
    .Duration(280)
    .Build();
```

## 7. Animating selection in navigation

`NavigationSelectionTransition` describes how **each element of a navigation chrome** (tab bar, rail, drawer)
changes when its destination is selected or deselected. It owns no scene and never calls your code during a frame.

### 7.1 Elements and properties

Elements (`NavigationSelectionElement`): `Surface`, `Indicator`, `Icon`, `Label`, `Badge`, `Fab`,
`SharedIndicator`.

Properties (`NavigationSelectionProperty`): `TranslationX/Y` (dp), `ScaleX/Y`, `Opacity`, `CornerRadius` (dp),
`Elevation` (dp), `BackgroundColor`, `ForegroundColor` (ARGB, interpolated), and `MatchBounds*` (follow the
selected item's bounds, for a shared indicator).

Each track gives an **inactive** value (unselected item) and an **active** value (selected), and can have its
own progress window.

### 7.2 Building

```csharp
var transition = NavigationSelectionTransition.Create()
    .Surface(x => x
        .BackgroundColor(0x00000000, 0xFF29274D)
        .Scale(0.94f, 1)
        .CornerRadius(14, 22))
    .Icon(x => x
        .Scale(0.88f, 1)
        .ForegroundColor(0xFF98A2BD, 0xFFFFFFFF))
    .Label(x => x.Opacity(0.65f, 1, startFraction: 0.2f))
    .Fab(x => x.Opacity(0, 1).Scale(0.8f, 1))
    .Duration(260)
    .Delay(40)
    .Easing(NavEasing.Emphasized)
    .Build();
```

Element builder methods (`NavigationSelectionElementBuilder`): `TranslateX`, `TranslateY`, `Scale`, `Opacity`,
`CornerRadius`, `Elevation`, `BackgroundColor`, `ForegroundColor`, `MatchSelectedBounds`, each with optional
`startFraction`/`endFraction`.

Main builder methods: `Surface`, `Indicator`, `Icon`, `Label`, `Badge`, `Fab`, `SharedIndicator`, `Duration(ms)`
(240 by default), `Delay(ms)`, `Easing(NavEasing)`, `Easing(Func<float,float>, samples)`, `Enabled(bool)`.

### 7.3 Presets: `NavigationSelectionTransitions`

| Preset | Effect |
|---|---|
| `None` | Disabled (immediate change) |
| `FadeScale(d)` | Icon: scale 0.86→1 and opacity; label: opacity; tinted surface background |
| `SpringIndicator(d, overshoot)` | Easing with overshoot; the surface grows and its radius goes from 14 to 22 |
| `SlideHighlight(d)` | Surface and icon slide a few dp, the label appears |

### 7.4 Shared indicator

A single indicator that slides from one item to the next:

```csharp
// In a presentation slot: context.RegisterSharedIndicator(indicator);
NavigationSelectionTransition.Create()
    .SharedIndicator(x => x.MatchSelectedBounds())
    .Duration(280)
    .Build();
```

Its position and size follow the items' arranged bounds, including in RTL and with items of different sizes.

### 7.5 Where to apply it

On `NavigationScaffoldState`: `SelectionTransition` (default), `BottomBarSelectionTransition`,
`RailSelectionTransition`, `DrawerSelectionTransition`, `SelectionTransitionFromTo<TFrom, TTo>(transition)`, and
per destination at creation. In a slot: `context.UseSelectionTransition(transition)`.

Resolution order: source/destination pair → target destination → presentation slot → mode (BottomBar / Rail /
Drawer) → `SelectionTransition`.

## 8. Animating an overlay: `OverlayAnimation`

`OverlayAnimation` sets the enter and exit of an overlay **panel**; the scrim fades with the same progress.
Instances are immutable: the `With…` methods return a copy.

### 8.1 Motions (`OverlayMotion`)

| Factory | Motion | Default duration |
|---|---|---|
| `OverlayAnimation.None` | Instant appearance | 0 |
| `Fade(ms)` | Opacity | 180 |
| `Scale(ms, from)` | Grows from `from` (0.88) to 1 while fading | 220 |
| `SlideUp(ms)` | Rises from the bottom of the screen (sheets, snackbars) | 300 |
| `SlideDown(ms)` | Drops from the top (banners) | 300 |
| `SlideStart(ms)` / `SlideEnd(ms)` | Enters from the start / end edge (mirrored in RTL) | 300 |
| `Custom(NavTransition)` | Your tracks (translations in **dp**) | The transition's |

Defaults of the standard factories: exit at **75 %** of the enter duration; `EaseOut` enter easing, `EaseIn` exit
easing.

### 8.2 Adjustments

```csharp
OverlayAnimation.SlideUp(320)
    .WithFade()                                       // slides while fading
    .WithDuration(enterMilliseconds: 320, exitMilliseconds: 200)
    .WithEasing(NavEasing.Emphasized, NavEasing.EaseIn);
```

### 8.3 A fully custom overlay animation

```csharp
var pop = OverlayAnimation.Custom(NavTransition.Create()
    .Enter(x => x.Opacity(0, 1).Scale(0.6f, 1, NavEasing.EaseOut).TranslateY(24, 0))
    .Exit(x => x.Opacity(1, 0).TranslateY(0, 16))
    .Duration(250)
    .Build());

Snackbar.Show(Application, "Copied", new SnackbarOptions { Animation = pop });
Application.Overlays.Show(h => MyCard(h.Dismiss), new OverlayOptions
{
    Placement = OverlayPlacement.Center,
    Animation = pop
});
```

In a `Custom` overlay, `Enter` plays on opening and `Exit` on closing; scale and opacity are the panel's, and the
translations are in dp **relative to the placed position**. `Custom` motions have a `Linear` easing on the overlay
side: put the acceleration **in the tracks**.

Snackbar default: `SlideUp(260).WithFade()` at the bottom, `SlideDown(260).WithFade()` at the top. Drag to dismiss
([Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md), French) makes the panel follow the finger, fades the scrim, then finishes the exit or
springs back.

## 9. Reduced motion

When the user asks for less motion:

- **Android**: the system's animator duration scale (`animator_duration_scale`);
- **iOS**: `UIAccessibility.IsReduceMotionEnabled`.

The framework relays this setting through `IMotionPreferencesCapability` (`NativeTunnel`):

- **Overlays**: any animation is replaced by `None`;
- **Navigation**: `ReducedMotion = true` applies the final selection state immediately without removing colour or
  accessibility changes (the scaffold feeds it automatically); the drawer goes to a zero duration;
- **Your animations**: read it yourself and set the duration to 0:

```csharp
bool reduced = Native.Get<IMotionPreferencesCapability>()?.ReducedMotion == true;
Tween.Run(ui, reduced ? 0 : 320, Motion.EaseOutCubic, apply);
```

With a duration of 0, `Tween.Run` applies the final state directly.

## 10. Easing curves

### 10.1 Built in (`NavEasing`)

| Value | Formula (`t` from 0 to 1) | Use |
|---|---|---|
| `Linear` | `t` | Opacity fades, constant rotation |
| `Standard` | `t²(3 − 2t)` (*smoothstep*) | Default of navigation tracks |
| `Emphasized` | `4t³` for `t < ½`, otherwise `1 − (−2t + 2)³ / 2` | Large movements |
| `EaseIn` | `t²` | Exits (accelerates) |
| `EaseOut` | `1 − (1 − t)²` | Entrances (decelerates) |

### 10.2 Your own (sampled table)

```csharp
// Slight overshoot
static float Back(float t) { const float s = 1.70158f; t -= 1; return t * t * ((s + 1) * t + s) + 1; }

NavTransition.Create()
    .Enter(x => x.Animate(NavAnimatedProperty.ScaleX, 0.6f, 1, Back)
                 .Animate(NavAnimatedProperty.ScaleY, 0.6f, 1, Back))
    .Duration(320).Build();
```

The function is evaluated at 129 points by default (`samples`, up to 4096) **during `Build()`**; it must return
finite values. At run time a linear interpolation between samples is read, without calling your code. A value
outside [0, 1] (overshoot) is accepted.

## 11. Recipes

### 11.1 Page stack that slides with parallax

```csharp
public static readonly NavTransition Parallax = NavTransition.Create()
    .Enter(x => x.TranslateX(1, 0, NavEasing.Emphasized))
    .Exit(x => x.TranslateX(0, -0.3f, NavEasing.Emphasized).Opacity(1, 0.7f))
    .Duration(320).Build();
```

### 11.2 A slower sheet, with a fade

A `BottomSheet`'s default is `SlideUp(320)` with `Emphasized` easing on enter and `EaseIn` on exit. For a longer
entrance that fades in:

```csharp
BottomSheet.Show(Application, h => MyContent(h.Dismiss), new BottomSheetOptions
{
    Animation = OverlayAnimation.SlideUp(420).WithFade()
        .WithEasing(NavEasing.Emphasized, NavEasing.EaseIn)
});
```

### 11.3 A toast that contracts

```csharp
Snackbar.Show(Application, "Sent", new SnackbarOptions
{
    Animation = OverlayAnimation.Scale(220, from: 0.7f),
    Duration = TimeSpan.FromSeconds(2)
});
```

### 11.4 A tab that "puffs up"

```csharp
BottomBarSelectionTransition = NavigationSelectionTransition.Create()
    .Icon(x => x.Scale(0.9f, 1.1f).ForegroundColor(0xFF98A2BD, 0xFF6D28D9))
    .Label(x => x.Opacity(0.5f, 1))
    .Duration(220)
    .Easing(t => 1 - MathF.Exp(-7 * t) * MathF.Cos(9 * t))
    .Build()
```

### 11.5 Pulsing a status dot

```csharp
private readonly State<uint> _dot = UseState(0xFF22C55E);
private CancellationTokenSource? _pulse;

private void Pulse()
{
    _pulse?.Cancel();
    var ui = Native.Require<IUiDispatcherCapability>();
    _pulse = Tween.Run(ui, 600, t => MathF.Sin(t * MathF.PI), s =>
        _dot.Value = Motion.Lerp(0xFF22C55E, 0xFFBBF7D0, s));
}
```

The easing function serves here as a waveform (0 → 1 → 0); the animation stops by itself.

## 12. Performance and diagnosis

- **Build once**: put `NavTransition`, `NavigationSelectionTransition` and `OverlayAnimation` in `static readonly`
  fields; do not rebuild them on every navigation (the compilation cost is "cold path", but it allocates).
- **Scene opacity** is rendered through a layer (`SaveLayer`) while it is below 1: on large scenes, prefer
  `Opacity` on the incoming scene only, or short durations.
- **Clip and radius** (`Clip*`, `CornerRadius`) cut out on every frame: cheap, but avoid stacking them with very
  large shadows.
- **Application timers**: one step = one `Post` + one invalidation. Colours (`Background`, `TextColor`) do not
  trigger layout; widths (`LinearProgress`, `FlexGrow`) do.
- **One timer per property**: cancel the old one before creating a new one.
- **Measuring**: on a device, Release, no debugger; the render baseline is in
  [A15 benchmark](runtime-benchmark-a15.md) (.NET 10 Mono measurements, not replayed under CoreCLR).
- **Interaction during motion**: do not expect an element that is mid-animation to be a reliable anchor; for
  navigation elements, selection animations do not move a destination's touch target.

## 13. Current limits

- No generic animation modifier, no per-entity opacity/translation/scale, no physical springs, no *shared
  element transition* between pages.
- `NavAnimatedProperty.Elevation` has no visual effect on page scenes in this version; the `Elevation` property
  of **selection** transitions works.
- Application timer animations ([§5](#5-animating-yourself-step-by-step)) are not tied to the screen refresh; their
  regularity depends on the system scheduler.
- Translations of `Custom` overlays are in dp, those of pages are fractions of the screen: do not mix the two
  units by reusing one `NavTransition` for both uses without checking it.
- The framework is pre-V1: these APIs are described from the code and its tests, not yet as a stable contract (see
  [Decisions and V1 trajectory](architecture/decisions-v1.md)).
