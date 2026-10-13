# Navigation and NavigationScaffold

[Home](https://github.com/Eel2000/XenoUI/blob/master/README.md) · [Docs index](index.md) · [Developer guide](developer-guide.md) · [Scene architecture](architecture/navigation-and-scenes.md)

`NavigationScaffold` adds adaptive main navigation without replacing the historical stack. A destination owns its own stack; `Navigate<TPage>()` always pushes into the selected stack. `XenoPage`, `ScrollState`, `LazyListState` and `TextEditController` instances stay persistent.

`XenoPage<TArguments>` pages accept [typed parameters per stack entry](text-layout-navigation.md#typed-navigation-parameters) through `Navigate<TPage, TArguments>(arguments, options)`. By default the BottomBar reserves its real height, margins included, so that the last content stays reachable; `ContentOverlaysNavigationBar = true` allows an explicit overlay.

## Minimal configuration

```csharp
public sealed class App : XenoApp
{
    private const string Font = "Font Awesome 5 Free-Solid-900";

    private readonly NavigationScaffoldState _navigation = new(
        NavigationDestination.For<HomePage>("Home", Glyph("", Font)),
        NavigationDestination.For<SearchPage>("Search", Glyph("", Font)),
        NavigationDestination.For<ProfilePage>("Profile", Glyph("", Font)))
    {
        Mode = NavigationMode.Automatic,
        DestinationTransition = NavTransitions.FadeThrough(),
        ReselectionBehavior = NavigationReselectionBehavior.PreserveStack
    };

    public override void OnStart()
    {
        Page<HomePage>();
        Page<SearchPage>();
        Page<ProfilePage>();
        Page<DetailsPage>();
        UseNavigationScaffold(_navigation);
    }
}
```

There is no need to call `Navigate<HomePage>()`: `InitialDestinationIndex` (0 by default) designates the initial destination. `Automatic` picks a BottomBar below 600 dp, a Rail from 600 to 839 dp and a modal Drawer from 840 dp. Changing window class only recomposes the chrome and re-lays out the roots if their usable viewport changes; no destination is recomposed.

A destination is composed lazily on its first selection. Once mounted, it keeps exactly the same ECS root, its handles, its scroll, its lazy window and its text controllers. Coming back to an already visited destination does not call `Body()` again.

## Transitions between destinations

Main transitions are independent of push/pop transitions:

```csharp
private readonly NavigationScaffoldState _navigation = new(...)
{
    DestinationTransition = NavTransitions.FadeThrough()
};

// Cold path: both transitions are already compiled before the first frame.
_navigation.TransitionFromTo<HomePage, SearchPage>(
    forward: NavTransitions.SlideHorizontal(),
    backward: NavTransitions.SlideHorizontalBack());
```

Without an explicit pair, the order of the destinations determines the direction: the configured transition is used forwards and its compiled inverse backwards. A selection runs no slot in the frame loop.

## Secondary navigation and Back

The existing APIs remain valid:

```csharp
Navigate<DetailsPage>();
GoBack();
bool consumed = TryGoBack();
```

From a page, `Navigate<DetailsPage>()` pushes the detail onto the active destination's stack. Android delegates to the system only when `TryGoBack()` returns `false`.

### Back at the root: platform conventions

`RootBackBehavior` is `PlatformDefault`. The host provides the convention through `INavigationPlatformCapability` in NativeTunnel, without Android/UIKit types in Core:

- Android: close the modal drawer, pop the details, then return to the initial destination before delegating the exit to the system.
- iOS: close the drawer or pop the details; a root does not trigger a tab change or a simulated exit.
- Host without the capability: `Exit` behaviour (the framework returns `false`; it does not close the application itself).

The keyboard is still handled by the native IME before the Android navigation callback. Returning to the initial destination restores its existing stack, including a detail still open, without emptying it or recomposing its page.

```csharp
private readonly NavigationScaffoldState _navigation = new(...)
{
    InitialDestinationIndex = 0,
    RootBackBehavior = NavigationRootBackBehavior.PlatformDefault
};
```

The explicit alternatives are `Exit`, `ReturnToInitialDestination` and `PreviousDestination`. The latter keeps an MRU history without duplicates, bounded by the number of destinations: Back consumes this history without recording its own change in it. A tab change never adds a page to a detail stack. `CanGoBack` includes the root policy, so that the Android host registers its callback at the right time.

A selection through the chrome **or** `SelectDestination()` closes the modal drawer before activating the destination, including on reselection. Horizontal edge and close gestures are available (see "Drawer: gestures and deferred selection"). Full predictive Back, the permanent drawer and the navigation accessibility bridge are not implemented yet. The ☰ hamburger button is the drawer trigger, not a navigation destination.

### Hamburger with tab navigation

`EnableModalDrawer = true` allows the drawer to be opened explicitly in BottomBar or Rail mode. From a `XenoPage`, wire the ☰ button to `OpenNavigationDrawer()`; the same public method is available from the application. The modal panel temporarily replaces the interactive chrome, covers the content and hides the FAB. Closing restores the adaptive presentation; the page and its stack are not rebuilt. Chrome slots may be recomposed on open/close, on the cold path only.

```csharp
private readonly NavigationScaffoldState _navigation = new(...)
{
    EnableModalDrawer = true,
    DrawerOpenTransition = DrawerTransitions.Open(DrawerSlideDirection.FromLeft),
    DrawerCloseTransition = DrawerTransitions.Close(DrawerSlideDirection.FromLeft)
};

// In a page's Body():
Box(44, 44, Icon(Glyph("", iconFont), 19))
    .CenterContent().Clickable(OpenNavigationDrawer);
```

SampleApp shows this flow in the Home, Gallery, People and Lab headers: ☰ → "Xeno Studio" panel → select a destination or close with the cross, an outside tap or Android Back. The drawer keeps the same destinations as the BottomBar.

Validation on 5 October 2026: 108/108 Core tests pass, including a pixel → hit-testing test on a drawer destination. The Release SampleApp compiles without warnings or errors and was deployed on the Galaxy A15: opening from Home, selecting Gallery, reopening from Gallery and closing with Back confirmed. The final package fixes the header clearance under the system bar; opening and closing with an outside tap were also checked visually. This flow is not a physical iOS/tablet validation nor one of the edge gestures.

An operation can override the transition:

```csharp
Navigate<DetailsPage>(NavTransitions.SlideHorizontal());

// Forward, then back:
Navigate<DetailsPage>(NavTransitions.SlideVertical(320), NavTransitions.Fade(220));
```

The priority is: the operation's option, source/target pair, target page, `PlatformDefault`. `DestinationTransition` only applies to BottomBar/Rail/Drawer changes. An application can define the two intermediate levels in `OnStart()`:

```csharp
TransitionFor<DetailsPage>(NavTransitions.ScaleFade());
TransitionFromTo<HomePage, DetailsPage>(NavTransitions.SlideHorizontal());
```

## Custom transition

The builder runs once on the cold path and produces contiguous tracks. No lambda is called back during frames:

```csharp
var transition = NavTransition.Create()
    .Enter(scene => scene
        .TranslateX(1, 0, NavEasing.Emphasized)
        .Opacity(0.4f, 1))
    .Exit(scene => scene
        .TranslateX(0, -0.18f)
        .Opacity(1, 0.8f))
    .Duration(300)
    .Build();
```

The V1 animatable properties include translation, scale, opacity, rotation, pivot, normalised clip, radius and elevation. Presets use the same public pipeline.

A developer easing is sampled into a LUT during `Build()` and is never called back by the hot path:

```csharp
.Enter(scene => scene.Animate(
    NavAnimatedProperty.TranslationX, 1, 0,
    easing: t => 1 - MathF.Pow(1 - t, 4),
    samples: 129))
```

## Presentation slots and FAB

`BottomBar`, `NavigationRail` and `PermanentDrawer` fully replace the corresponding presentation. The context provides the destinations, the selection, the mode, the window class, the active page and `Select(index)`. The framework keeps ownership of behaviour and stacks.

```csharp
BottomBar = context =>
{
    var icon = Icon(context.Destinations[0].Icon, 18);
    var label = Text(context.Destinations[0].Label, 11);
    var surface = Column(icon, label).Clickable(() => context.Select(0));

    // Registers, once, the entities the runtime will then animate.
    return context.RegisterItem(0, surface, icon, label);
},
FloatingActionButton = context =>
{
    var fab = NavigationFab.Standard(Glyph("", Font),
        () => context.Select(1));
    return context.RegisterFab(fab, 0); // only visible on Home
}
```

`NavigationFab.Standard` and `NavigationFab.Extended` provide the two V1 presentations. `FabPlacement` picks `BottomEnd` or `BottomCenter`; a slot can still fully replace the rendering.

`FabBottomBarGap` sets the vertical space between the FAB row and the top of the BottomBar slot (16 dp by default, a finite, positive or zero value). Positioning follows the slot's real height, without an absolute offset or per-frame computation. A custom slot's top margins and paddings add to this space; for a floating pill, keep its wrapper transparent and without top padding. This option does not change Rail/Drawer modes.

```csharp
FabPlacement = FloatingActionButtonPlacement.BottomEnd,
FabBottomBarGap = 16
```

The destination transition and the selection transition are independent:

```csharp
DestinationTransition = NavTransitions.SlideHorizontal(),
BottomBarSelectionTransition = NavigationSelectionTransition.Create()
    .Surface(x => x
        .BackgroundColor(0x00000000, 0xFF29274D)
        .Scale(0.94f, 1)
        .CornerRadius(14, 22))
    .Icon(x => x
        .Scale(0.88f, 1)
        .ForegroundColor(0xFF98A2BD, 0xFFFFFFFF))
    .Label(x => x.Opacity(0.65f, 1))
    .Fab(x => x.Opacity(0, 1).Scale(0.8f, 1))
    .Duration(260)
    .Easing(NavEasing.Emphasized)
    .Build(),
RailSelectionTransition = NavigationSelectionTransitions.FadeScale(),
DrawerSelectionTransition = NavigationSelectionTransitions.SlideHighlight()
```

The roles that can be registered are `surface`, `indicator`, `icon`, `label` and `badge`. A single moving indicator can be registered with `context.RegisterSharedIndicator(indicator)`, then configured with `.SharedIndicator(x => x.MatchSelectedBounds())`: its position and size follow the items' arranged bounds, including in RTL and with items of different sizes. Available properties are translation, scale, opacity, radius, elevation, background colour and foreground colour. A developer easing is compiled into a LUT during `Build()`.

Resolution follows the order source/destination pair, target destination, BottomBar/Rail/Drawer mode, then `SelectionTransition`. `ReducedMotion = true` applies the final state immediately without removing colour or accessibility changes. Android (`animator_duration_scale`) and iOS (`UIAccessibility.IsReduceMotionEnabled`) feed this preference automatically through `NativeTunnel`.

Slots are only called on the initial cold path or on a structural change. Changing `SelectedIndex` keeps the same root, the same ECS generations and the same callbacks. The hot path only walks contiguous tracks and writes the values into the slabs. An interruption restarts from the current visual values. Presentation transforms never change hit-testing bounds; a hidden FAB is also logically disabled so that no invisible touch area is created.

`ChromeTransition` remains available for a genuine structural replacement, for example BottomBar → Rail → Drawer. It is never used for a simple selection.

## Center action, notched bar and RTL

### Center action (not a destination)

`CenterAction` adds a button to the BottomBar that runs an action ("create", "scan"…) instead of selecting a destination. It takes a slot in the row but never becomes `SelectedIndex`. When it is set and no `BottomBar` slot is provided, the default bar becomes `NavigationBars.Docked`: the button sticks out above the bar and sits in a notch cut into it.

```csharp
private readonly NavigationScaffoldState _navigation = new(
    NavigationDestination.For<HomePage>("الرئيسية", home),
    NavigationDestination.For<CategoriesPage>("الأقسام", categories),
    NavigationDestination.For<FavoritesPage>("المفضلة", favorites),
    NavigationDestination.For<AccountPage>("حسابي", account))
{
    CenterAction = NavigationAction.For(Glyph("", Font), OpenComposer) with
    {
        Background = 0xFFF5B301, Size = 60
    },
    CenterActionPosition = -1, // middle of the visible tabs (default)
    DockedBarStyle = DockedNavigationBarStyle.Default with
    {
        Background = 0xFFFFFFFF,
        SelectedColor = 0xFFF5B301,
        UnselectedColor = 0xFF9A9AA8,
        LabelFontFamily = "Cairo-Regular", // registered Arabic font (optional)
        TopCornerRadius = 22
    }
};
```

The button is a sibling painted after the bar: it wins the tap, including on its part above the edge. The notch around it is not a touch area. The content is only inset by the bar's height; the protruding button covers the content.

A custom `BottomBar` slot can reuse the same rendering, for example to hide a destination that is only reached through the action:

```csharp
BottomBar = context => NavigationBars.Docked(context, style with
{
    ShowDestination = index => index != FeedIndex,
    LabelFor = destination => Localize(destination.Label)
}, NavigationAction.For(feedIcon, () => context.Select(FeedIndex)))
```

The context also exposes `CenterAction`, `CenterActionPosition`, `SafeAreaBottom`, `IsRightToLeft`, `ReserveBottomBarBounds(surface)` (reserves the height of that element only) and `UseSelectionTransition(transition)` (the transition of this slot's items; pair or per-destination transitions still take precedence). `RefreshChrome()` rebuilds the slot when the state it reads changes (language, theme), without touching the pages.

### Safe area

`SafeAreaBottom` is the part of the surface under the home indicator (iOS) or under the navigation bar (Android, only if the app draws edge-to-edge). The docked bar and the default bar add this height under their content and rebuild when it changes (rotation).

### Custom shapes

`.Shape(XenoShape)` replaces an entity's rounded rectangle for the background, the shadow, the children's clip and the touch area. `XenoShapes.TopNotch(...)` is the built-in notch; `DockedNavigationBarStyle.Shape` fully replaces the bar's outline:

```csharp
var wave = XenoShape.Create((path, width, height) =>
{
    path.MoveTo(0, 12);
    path.CubicTo(width * 0.3f, -8, width * 0.7f, 28, width, 12);
    path.LineTo(width, height);
    path.LineTo(0, height);
    path.Close();
});

DockedBarStyle = DockedNavigationBarStyle.Default with { Shape = wave }
// or on any element:
Box(0, 80).Background(0xFF111827).Shape(wave)
```

The path is built in logical units, with the origin at the top left of the entity, only when the size changes: stable frames allocate nothing. A shape is not mirrored automatically in RTL; an asymmetric shape reads `context.IsRightToLeft` to mirror itself.

### Right to left

`XenoApp.FlowDirection` is `Inherit`: the direction follows the device language (`ILayoutDirectionCapability`, updated live on Android). `LeftToRight` or `RightToLeft` force it; `.FlowDirection(...)` changes it for a subtree.

An RTL scene is the exact mirror of its LTR layout: `Row`s start at the right edge, left padding and margin become the "start" side (`Spacing.Directional(start:, end:)` names them so), a column's `CrossAxisAlignment.Start` is on the right, and hit-testing follows since it reads the arranged positions. `TextAlignment.Start` follows the paragraph direction; Hebrew and Arabic are always shaped, even with a registered font, so that letters join. Changing direction re-lays out the scenes without recomposing the pages.

Limits: horizontally scrolling containers keep a left → right extent; the Rail and the drawer do not switch sides automatically (`DrawerDirection = FromRight` stays explicit); a registered font missing a glyph does not fall back to another font yet.

## Which operation rebuilds what?

1. **Chrome recomposition**: only for an adaptive BottomBar/Rail/Drawer change or an explicit structural change. Content roots stay intact.
2. **Activating an existing destination**: selects its retained root, updates interactivity and starts the tracks. No `Body()` or layout if its viewport is unchanged.
3. **First lazy composition**: the first access to a destination calls its root page's `Body()` once and mounts its scene.
4. **Push/pop on the active stack**: a push mounts a new detail scene on top of the previous one; a pop reactivates the previous scene then releases only the removed detail.
5. **Explicit page rebuild**: only a structural request from that page replaces its active scene. Other destinations and the standard chrome do not change.

## Lifecycle

`XenoApp` exposes `OnStart`, `OnForeground`, `OnBackground`, `OnLowMemory` and `OnDispose`. `XenoPage` exposes `OnCreate`, `OnActivated`, `OnDeactivated` and `OnDestroy`. Page callbacks only change when the navigation commits; a scene that is merely entering/exiting is not presented publicly as a new lifecycle state. Leaving a tab produces `OnDeactivated`, not `OnDestroy`: its scene stays mounted.

## V1 limits

The Drawer is a modal overlay, including with BottomBar/Rail when `EnableModalDrawer = true`. Not provided yet: full predictive back, deep links, restoration after process death, shared elements, typed results, multi-pane, Snackbar, dialogs and bottom sheets. Choreographed animation of a page's elements is a separate debt item; it will reuse the clock and the tracks without entering the V1 navigation engine.

## Drawer: gestures and deferred selection

```csharp
EnableModalDrawer = true,
DrawerGesturesEnabled = true,
DrawerDirection = DrawerSlideDirection.FromLeft,
DrawerEdgeWidth = 24, // dp
DrawerNavigationDelay = TimeSpan.FromMilliseconds(90),
DrawerOpenTransition = DrawerTransitions.Open(DrawerSlideDirection.FromLeft, 340),
DrawerCloseTransition = DrawerTransitions.Close(DrawerSlideDirection.FromLeft, 280),
```

A selection from the drawer first closes the menu, waits for the chrome transition to actually finish, then for the configured delay (80 ms by default), and finally activates the destination. The page and its lifecycle do not change during the close. Extra taps are ignored during this transaction; Back cancels it. A zero delay only removes the pause, not the wait for the close. Going to the background, a reset and destruction cancel a selection still pending.

Gesture opening starts in the band of the configured edge; closing starts on the panel. After an 8 dp threshold, only a dominant horizontal movement captures the gesture and cancels the previous tap/scroll. The panel follows the finger, the scrim stays fixed and its opacity follows the progress. On release, a velocity above 400 dp/s picks the anchor; otherwise the threshold is 50 %. A system cancellation returns to the starting anchor. Settling durations come from the configured transitions and respect `ReducedMotion`.

Gestures are available for `FromLeft` and `FromRight`; set the direction and both transitions consistently. Custom programmatic transitions remain available. Gesture settling uses a numeric ease-out easing, without calling the transitions' delegates back. Vertical drawer gestures are still to be handled.

On Android 10+, the `NativeDrawerGesturePolicy` contract is translated into a Back gesture exclusion limited to a central band 200 dp tall on the drawer's edge. The system keeps Back elsewhere, and Home is not excluded. The region is updated on the UI thread on structural, viewport or keyboard changes, never during drag frames. It disappears when gestures are disabled, the keyboard hides the chrome or a selection is pending. This limit follows the [Android rules for gesture coexistence](https://developer.android.com/develop/ui/views/touch-and-input/gestures/gesturenav).

Chrome composition happens only once on acquisition/close, never during the drag. The hot path only changes the panel/scrim ECS values and the settling clock. Publishing a deferred navigation runs at the cold-path boundary after rendering, outside the engine lock. This model follows the interactions of the [Android modal drawer](https://developer.android.com/develop/ui/compose/components/drawer), without using a native view or claiming to implement predictive back.

Validation on 2026-10-05: 111 Release Core tests, including the delay after closing, cancellation by Back, left/right gestures, pointer cancellation, vertical arbitration and disabled gestures. After acquisition/warm-up: 1,000 drag updates and 200 render frames tested at 0 managed bytes allocated. This result does not cover chrome composition allocations.

Physical flow verified on a Galaxy Note20 SM-N981B (`RFCR91ELK6Y`, gesture system navigation): opening from the edge in the reserved region, closing by dragging the panel, opening with the hamburger then selecting Gallery, and system Back outside the reserved region returning to Home. The initial conflict with system Back was reproduced, then fixed by the bounded exclusion. No AndroidRuntime crash observed on this flow. No new physical iOS, tablet or A15 test is claimed.
