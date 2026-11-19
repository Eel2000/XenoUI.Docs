# From Jetpack Compose to XenoUI

[Documentation](index.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [State](state.md) · [Side effects](side-effects.md) · [Layouts](layouts.md)

Do you know Jetpack Compose (or SwiftUI, Flutter)? This page is a **dictionary**: for each Compose concept, its
XenoUI equivalent, and the gaps to know about. It does not replace the detailed guides: each line points to the page
that explains it.

## Contents

1. [What is the same, what differs](#1-what-is-the-same-what-differs)
2. [Concept dictionary](#2-concept-dictionary)
3. [Layout and modifiers](#3-layout-and-modifiers)
4. [Material components and widgets](#4-material-components-and-widgets)
5. [Navigation](#5-navigation)
6. [One screen, written both ways](#6-one-screen-written-both-ways)
7. [Habits to unlearn](#7-habits-to-unlearn)
8. [What does not exist (yet)](#8-what-does-not-exist-yet)

---

## 1. What is the same, what differs

**The same**: declarative UI, components = functions, hoisted state, unidirectional data flow, lazy keyed lists,
previews (`@Preview`), one C#/Kotlin codebase describing the screen.

**Different**:

| | Jetpack Compose | XenoUI |
|---|---|---|
| Language | Kotlin | C# (.NET 11) |
| Rendering | Android's canvas (Compose Multiplatform: Skia) | Skia, on **Android and iOS** |
| Updating | Smart recomposition, tracked by state reads | **Explicitly bound** `State<T>` + page `Rebuild()` |
| Composable | Function annotated `@Composable` | Function returning `XenoComponent` |
| Modifiers | A `Modifier` value chain passed as a parameter | Chain **on the result**: `Box().Padding(8)` |
| OS anchoring | Native views under the hood for some APIs | Everything is drawn; native keyboard/IME through a typed bridge |

## 2. Concept dictionary

| Jetpack Compose | XenoUI | Details |
|---|---|---|
| `@Composable fun Foo()` | `static XenoComponent Foo(…)` | [Creating a component](components.md) |
| Recomposition | Bound `State<T>` (fine) or `Rebuild()` (page) | [Thinking in XenoUI §3](thinking-in-xenoui.md#3-the-two-ways-to-update-the-screen) |
| `remember { mutableStateOf(0) }` | `private readonly State<int> _n = UseState(0);` (field) | [State §1](state.md#1-state-objects) |
| `by remember` (delegate) | `state.Value` | |
| `rememberSaveable` | Not provided | [State §9](state.md#9-keeping-state) |
| State hoisting | Pass `State<T>` + `Action` as parameters | [State §3](state.md#3-hoisting-state) |
| `derivedStateOf` | Compute in `Body()`, or a second `State` updated in a handler | [State §6](state.md#6-derived-values) |
| `LaunchedEffect(key)` | `OnActivated` / `OnNavigatedTo` + cancellation in `OnDeactivated` | [Effects §1](side-effects.md#1-correspondence-with-compose-effects) |
| `DisposableEffect` | Subscribe in `OnCreate`, unsubscribe in `OnDestroy` | [Effects §5](side-effects.md#5-subscribing-and-unsubscribing) |
| `rememberCoroutineScope` | `CancellationTokenSource` field | [Effects §4](side-effects.md#4-starting-and-cancelling-work-with-the-page) |
| `collectAsState()` | A model's `changed` callback → `Rebuild()` / `State.Value` | [Architecture §4](app-architecture.md#4-presentation-models-layer) |
| `snapshotFlow` | `State<T>.OnChanged` | |
| `CompositionLocal` | No equivalent: parameters, or `Application`/`Native` from the page | |
| `ViewModel` | Presentation model (`RemoteFeed<T>`) | [Architecture](app-architecture.md) |
| `Dispatchers.Main` | `IUiDispatcherCapability.Post` | [Effects §3](side-effects.md#3-threads-who-may-do-what) |
| `@Preview` | `[Preview]` | [Creating a component §12](components.md#12-previewing-preview) |
| Hot reload / Live Edit | VS / VS Code previews | [Visual Studio](visual-studio.md), [VS Code](vscode.md) |
| `MaterialTheme` | Your own `Theme` class, applied explicitly | [Customising §6](customization.md#6-colours-theme-and-dark-mode) |
| `key(id) { }` | `key: item => item.Id` on lists | [Layouts §8](layouts.md#8-virtualised-lists) |
| `Modifier.testTag` + `createComposeRule` | `PreviewRenderer` (headless rendering) | [Testing](testing.md) |
| `Modifier.semantics` | Accessibility label on fields; the rest to come | [Testing §11](testing.md#11-current-limits) |

## 3. Layout and modifiers

| Compose | XenoUI |
|---|---|
| `Column { }` / `Row { }` | `Column(…)` / `Row(…)` |
| `Box { }` (stack) | `ZStack(…)` (and `Box(w, h, …)` for a rectangle-container) |
| `Spacer(Modifier.weight(1f))` | `Spacer()` |
| `Modifier.weight(1f)` | `.FlexGrow(1)` |
| `Modifier.fillMaxWidth()` | `.CrossAxis(Stretch)` (parent) or `.AlignSelf(Stretch)` |
| `Modifier.fillMaxSize()` | `.FlexGrow(1)` + `Stretch` |
| `Modifier.size(48.dp)` / `width` / `height` | `.Size(48, 48)` / `.Width` / `.Height` |
| `Modifier.padding(8.dp)` | `.Padding(8)` / `.Padding(h, v)` / `.Padding(Spacing…)` |
| Outer margin (padding before `background`) | `.Margin(…)` |
| `Arrangement.spacedBy(8.dp)` | `.Gap(8)` |
| `Arrangement.SpaceBetween` | `.MainAxis(MainAxisAlignment.SpaceBetween)` |
| `Alignment.CenterVertically` | `.CrossAxis(CrossAxisAlignment.Center)` |
| `Modifier.align(Alignment.End)` | `.AlignSelf(CrossAxisAlignment.End)` |
| `contentAlignment = Center` | `.CenterContent()` |
| `Modifier.background(color)` | `.Background(0xAARRGGBB)` |
| `Modifier.clip(RoundedCornerShape(12.dp))` | `.CornerRadius(12)` |
| `Modifier.clip(CustomShape)` | `.Shape(XenoShape…)` |
| `Modifier.shadow(8.dp)` / `Card(elevation)` | `.Elevation(8)` / `.Shadow(x, y, blur, color)` |
| `Modifier.clickable { }` | `.Clickable(() => …)` |
| `Modifier.verticalScroll(state)` | `.Scrollable(scroll)` |
| `Modifier.alpha(0.5f)` | The colour's alpha; otherwise a scene ([Animations §4](animations.md#4-opacity-and-fading-a-component)) |
| `Modifier.rotate / scale / offset` | Scene transition tracks; no per-entity modifier |
| `Text(fontSize, color, maxLines, overflow)` | `Text(s, size).TextColor(c).MaxLines(n, ellipsis: true)` |
| `Image(painter)` / `contentScale` | `Image("asset")` / `.ScaleMode(Cover/Fit/Fill/None)` |
| `Icon(imageVector)` | `Icon(IconDef, size)` (font glyph) |
| `LazyColumn { items(list, key) }` | `LazyColumn(list, item => …, key: …)` |
| `rememberLazyListState()` | `UseLazyList()` |
| `VerticalPager` | `VerticalPager(items, (item, i) => …)` |
| `rememberScrollState()` | `UseScroll()` |
| Divider (`Modifier.height(1.dp).background(c)`) | `Box(0, 1).Background(c)` or `Ui.Divider()` |

## 4. Material components and widgets

| Compose / Material | XenoUI |
|---|---|
| `Button`, `TextButton` | `Button(label, tap)`, `TextButton(label, tap)` |
| `FloatingActionButton` | `NavigationFab.Standard/Extended` (scaffold) |
| `Scaffold`, `TopAppBar` | `NavigationScaffoldState` + `ITopBarPage` / `TopBarOptions` |
| `NavigationBar`, `NavigationRail`, `ModalNavigationDrawer` | The scaffold's `BottomBar`, `NavigationRail`, `Drawer` slots |
| `SnackbarHost` | `Snackbar.Show(app, message, options)` |
| `AlertDialog` | `Alert.Show(…)` / `Dialog` |
| `ModalBottomSheet` | `BottomSheet.Show(…)` |
| `DropdownMenu` | `Popup` (anchored) |
| `TextField` / `OutlinedTextField` | `TextField(controller, placeholder, InputOptions(…))`, `TextArea` |
| `Switch`, `Checkbox`, `Slider`, `Card`, `Chip` | To compose: [recipes](components.md#15-complete-recipes) (no ready-made widget) |
| `CircularProgressIndicator` | To compose; `LinearProgress(State<float>)` exists |
| `AnimatedVisibility`, `Crossfade` | Condition + `Rebuild()`; `NavTransitions.FadeThrough` for pages |
| `animate*AsState` | Application interpolation ([Animations §5](animations.md#5-animating-yourself-step-by-step)) |
| `rememberInfiniteTransition` | Application timer |

## 5. Navigation

| Jetpack Navigation / Compose | XenoUI |
|---|---|
| `NavHost` + routes | `Page<T>()` registered in `OnStart` |
| `navController.navigate("detail/42")` | `Navigate<DetailsPage, DetailsArgs>(new(42))` |
| Route arguments | `XenoPage<TArguments>` + `OnNavigatedTo(args)` |
| `popBackStack()` | `GoBack()` |
| `popUpTo` | `ReselectionBehavior = PopToRoot` |
| `NavigationBar` + per-tab back stack | `NavigationScaffoldState`: one stack per destination |
| `BackHandler` | System back handled by the scaffold; `CanGoBack` |
| `enterTransition` / `exitTransition` | `NavTransition` (`Enter` / `Exit`) |
| Deep links | Not provided ([V1 limits](navigation-scaffold.md#v1-limits)) |

## 6. One screen, written both ways

**Jetpack Compose**

```kotlin
@Composable
fun CounterScreen(viewModel: CounterViewModel = viewModel()) {
    val count by viewModel.count.collectAsStateWithLifecycle()
    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Total: $count", fontSize = 24.sp)
        Button(onClick = viewModel::increment) { Text("+1") }
    }
}
```

**XenoUI**

```csharp
public sealed class CounterPage : XenoPage
{
    private readonly State<int> _count = UseState(0);                 // remember { mutableStateOf }

    public override XenoComponent Body() =>
        Column(
            Text($"Total: {_count}", 24),                             // bound: updates by itself
            Button("+1", () => _count.Value++)
        ).Gap(12).Padding(16);
}
```

With a model (the `ViewModel` equivalent):

```csharp
public sealed class CounterPage : XenoPage
{
    private CounterModel _model = null!;
    public override void OnCreate()    { _model = ((App)Application).Counter; _model.Changed += OnChanged; }
    public override void OnDestroy()   => _model.Changed -= OnChanged;
    private void OnChanged()           { if (IsActive) Rebuild(); }

    public override XenoComponent Body() =>
        Column(Text($"Total: {_model.Count}", 24), Button("+1", _model.Increment)).Gap(12).Padding(16);
}
```

## 7. Habits to unlearn

| Compose habit | In XenoUI |
|---|---|
| "State read in the composable is tracked on its own" | Bind explicitly (`Text(state)`) or call `Rebuild()` |
| "I call `remember` wherever I like" | Create `State`s in **fields**; never in `Body()` |
| "Lambdas capture the current state" | Same, but a `Rebuild()` recreates the tree: read fields at execution time |
| "Recomposition skips what did not change" | `Rebuild()` recomposes the whole page; granularity = your `State`s |
| "I pass a `Modifier` as a parameter" | The component returns a `XenoComponent`: the caller chains modifiers on the result |
| "`MaterialTheme` provides the colours" | Your `Theme` class, applied explicitly |
| "`LaunchedEffect` starts when the composable enters" | Start in `OnActivated`; no effect at composition |
| "`dp`/`sp` are types" | Everything is in dp (floats); fonts in dp |
| "Modifier after modifier = order matters (`padding` before `background`)" | Modifiers write into the entity; **order has no effect** except overwriting the same property |

## 8. What does not exist (yet)

To plan honestly: no state restoration after process death, no deep links, no typed navigation results, no shared
element transitions, no generic animation modifier or public springs, no `CompositionLocal`, no interaction test
harness, no semantics tree for drawn elements, no ready-made Material widgets for `Switch`/`Checkbox`/`Slider`. The
framework is **pre-V1**: see [Decisions and V1 trajectory](architecture/decisions-v1.md#3-technical-debt-and-risks).
