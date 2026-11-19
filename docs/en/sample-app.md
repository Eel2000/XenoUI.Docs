# Learning from the SampleApp

[Documentation](index.md) · [Developer guide](developer-guide.md) · [Creating a component](components.md) · [Customising](customization.md) · [Animations](animations.md) · [API demo](sample-api.md)

The [`SampleApp`](https://github.com/Eel2000/XenoUI/tree/master/samples/SampleApp) is the repository's reference application: about 2,900 lines of C# that
exercise almost the whole public API. This guide turns it into a **developer walkthrough**: how the application
is built, which file illustrates which concept, which patterns to copy, and which to handle with care. Every
excerpt comes from the repository's code; file and member names are the project's.

## Contents

1. [Running the application](#1-running-the-application)
2. [Project map](#2-project-map)
3. [The project: `SampleApp.csproj`](#3-the-project-sampleappcsproj)
4. [`App.cs`: the entry point](#4-appcs-the-entry-point)
5. [The in-house design: `Theme`, `Ui`, `AppChrome`](#5-the-in-house-design-theme-ui-appchrome)
6. [Page by page](#6-page-by-page)
7. [Pattern catalogue](#7-pattern-catalogue)
8. [Cautions: what the sample does for demonstration](#8-cautions-what-the-sample-does-for-demonstration)
9. [Step by step: adding a "Notes" page](#9-step-by-step-adding-a-notes-page)
10. [Tests related to the sample](#10-tests-related-to-the-sample)

---

## 1. Running the application

```powershell
dotnet build samples/SampleApp/SampleApp.csproj -f net11.0-android
xeno devices
xeno run --project samples/SampleApp/SampleApp.csproj --device <id> --framework net11.0-android
```

Prerequisites (.NET 11 SDK, Android workload, API 37 platform, CoreCLR runtime):
[Developer guide §2](developer-guide.md#2-prerequisites). The sample can also be previewed without a device thanks
to its pages' `[Preview]`s ([Visual Studio](visual-studio.md), [VS Code](vscode.md)).

The application opens on five destinations: **Home**, **Explore**, **Feed** (centre button), **People** and
**Studio**. On a phone it is a bottom bar; on a tablet a rail; a modal drawer is available everywhere (the top
bar's hamburger button). **Studio** pushes the labs: video, audio, live API, text input, the performance lab,
overlays and preferences.

## 2. Project map

```text
samples/SampleApp/
├─ SampleApp.csproj        Sdk="Xeno.SDK", TFMs, app identity
├─ App.cs                  XenoApp: navigation scaffold, pages, lifecycle
├─ AndroidManifest.xml     permissions (INTERNET…); Info.plist for iOS
├─ Assets/                 Fonts (Font Awesome 5), Images (avatar, logo)
├─ Design/
│  ├─ Theme.cs             palette, fonts, IconDef
│  ├─ Ui.cs                reusable building blocks (buttons, cards, rows, bars)
│  └─ AppChrome.cs         bottom bar, rail, drawer (presentation slots)
├─ Pages/                  one XenoPage per screen (13)
└─ Services/               portable code, no XenoUI type (HTTP, remote feeds, catalogue)
```

The separation to remember: **`Services/` knows neither XenoUI nor the platform**; **`Design/` defines the
look**; **`Pages/` assembles the interface and orchestrates**.

## 3. The project: `SampleApp.csproj`

```xml
<Project Sdk="Xeno.SDK">
  <PropertyGroup>
    <TargetFrameworks>net11.0-android;net11.0-ios</TargetFrameworks>
    <XenoEnableMedia>true</XenoEnableMedia>

    <XenoAppId>com.xenoui.sample</XenoAppId>
    <XenoAppName>XenoUI Sample</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>
</Project>
```

Everything else comes from `Xeno.SDK`: the `XenoUI.*` references, the generated `MainActivity`, the default
manifest, and the **implicit imports**. That is why pages have almost no XenoUI `using`: `Compose` (static),
`Hooks` (static), `Widgets` (static: `Button`, `TextButton`), `XenoUI.Core.Compositor`, `Components.Enums`,
`StandardLibrary.Controls/Overlays/Shell`. On Android, `Dialog` is aliased to
`XenoUI.StandardLibrary.Overlays.Dialog`. `XenoEnableMedia` adds `XenoUI.Media` (audio/video players). See
[Xeno.SDK](xeno-sdk.md). On Android, the SDK runs the application on CoreCLR (`UseMonoRuntime=false`).

## 4. `App.cs`: the entry point

`App : XenoApp` does four things.

### 4.1 It declares the navigation scaffold

`NavigationScaffoldState` receives the destinations (`NavigationDestination.For<HomePage>("Home",
Theme.Glyph(""))`, with an optional badge) and an object initialiser that sets up the whole chrome:

| Property | Role in the sample |
|---|---|
| `Mode = NavigationMode.Automatic` | Bottom bar, rail or drawer depending on window size |
| `ContentOverlaysNavigationBar = false` | Content (and the feed's video) stops above the bar |
| `EnableModalDrawer`, `DrawerGesturesEnabled`, `DrawerDirection`, `DrawerNavigationDelay` | Modal drawer, gesture, opening side, delay before navigating |
| `DestinationTransition = NavTransitions.FadeThrough(220)` | Tab change |
| `ChromeTransition` | Custom `NavTransition` for the chrome's appearance |
| `BottomBarSelectionTransition`, `RailSelectionTransition`, `DrawerSelectionTransition` | Selection animation per mode ([Animations §7](animations.md#7-animating-selection-in-navigation)) |
| `DrawerOpenTransition`, `DrawerCloseTransition` | `DrawerTransitions.Open/Close(direction, ms)` |
| `ReselectionBehavior = PopToRoot` | Tapping the current tab again returns to its root |
| `BottomBar`, `NavigationRail`, `Drawer` | Slots: `AppChrome` functions ([§5](#5-the-in-house-design-theme-ui-appchrome)) |
| `TopBar = TopBars.ForActivePage` | Application bar built from `ITopBarPage` |

Real excerpt (bottom bar selection, with a spring-like easing and no per-frame call):

```csharp
BottomBarSelectionTransition = NavigationSelectionTransition.Create()
    .Easing(t => 1 - MathF.Pow(1 - t, 3) + MathF.Sin(t * MathF.PI) * 0.08f)
    .Surface(x => x.Scale(0.92f, 1))
    .Icon(x => x.ForegroundColor(0xFF8A8A9C, Theme.White).Scale(0.86f, 1))
    .Label(x => x.ForegroundColor(0xFF8A8A9C, Theme.White))
    .Badge(x => x.Scale(0.8f, 1))
    .Duration(240).Build(),
```

### 4.2 It registers the pages

```csharp
public override void OnStart()
{
    AppChrome.Destinations = _navigation.Destinations.Select(x => x.PageType).ToArray();
    ArticleDetails = new ArticleDetailsModel(new ArticleService(ApiClient),
        Native.Require<IUiDispatcherCapability>().Post);
    Page<HomePage>();
    Page<ExplorePage>();
    /* … the 13 pages … */
    UseNavigationScaffold(_navigation);
}
```

`Page<T>()` instantiates the page **once**: its fields (`State`, `ScrollState`, lists) therefore keep their value
across navigations. A page that is not registered cannot be targeted by `Navigate<T>()`.
`UseNavigationScaffold` activates the scaffold.

### 4.3 It carries the application's state and services

`ApiClient` (a single `HttpClient`), `ArticleDetails` (shared model), `Feed` (reference to the feed page),
`GetDemoVideo()` (lazy video player). Pages reach them through the `(App)Application` cast, and `App` exposes
**intent methods** rather than letting pages manipulate the scaffold:

```csharp
internal void SelectTab(int index) => _navigation.SelectDestination(index);
internal void OpenApi() => Navigate<ApiPage>(NavTransitions.SlideHorizontal(280));
internal void OpenArticle(Article article) =>
    Navigate<ArticleDetailsPage, ArticleDetailsArgs>(new(article), NavTransitions.SlideHorizontal(280));
```

`OpenFeedClip(int)` illustrates cross-tab navigation: select the feed destination, then call
`Feed?.ShowClip(clip)`.

### 4.4 It relays the application lifecycle

```csharp
public override void OnBackground()
{
    if (ActivePage is AudioLabPage audio) audio.Suspend();
    if (ActivePage is VideoLabPage video) video.Suspend();
    if (ActivePage is FeedPage feed) feed.Suspend();
    if (ActivePage is ApiPage api) api.Suspend();
    else if (ActivePage is ArticleDetailsPage) ArticleDetails.Deactivate();
}
```

`OnForeground` does the opposite (`Resume`/`Activate`), `OnDispose` releases the player, the model and the client.
Rule to remember: **anything running in the background (player, request) must stop at `OnBackground` and resume
at `OnForeground`**.

`OnPreview()` is called instead of `OnStart()` when the previewer builds the application without starting it:
only put in it what pages read, never network access.

## 5. The in-house design: `Theme`, `Ui`, `AppChrome`

### 5.1 `Theme.cs`

A static class of constants (`Canvas`, `Surface`, `SurfaceHigh`, `Ink`, `Muted`, `Accent`, `Cyan`…, each with a
translucent "Soft" variant for icon backgrounds), the font names (`Fa`, `FaRegular`, `FaBrands`) and
`Theme.Glyph(string)`, which returns a Font Awesome `IconDef`. See
[Customising §6](customization.md#6-colours-theme-and-dark-mode).

### 5.2 `Ui.cs`: the "wrapper" level

Static functions that apply the theme to the standard components: `Primary`, `Secondary`, `Pill`, `Card`, `Stat`,
`ReactiveStat(State<string>, …)`, `NavRow`, `IconButton`, `SectionTitle`, `Eyebrow`, `Divider`, plus bar option
factories (`DrawerBar`, `BackBar`, `BarAction`). Examples:

```csharp
internal static XenoComponent Primary(string label, Action tap, float size = 14) =>
    Button(label, tap, size).Background(Theme.Accent).CornerRadius(14).Padding(16, 13);

internal static XenoComponent IconButton(string glyph, Action tap, uint background = Theme.SurfaceHigh,
    uint foreground = Theme.Ink) =>
    Box(42, 42, Icon(Theme.Glyph(glyph), 16).TextColor(foreground))
        .Background(background).CornerRadius(14).CenterContent().Clickable(tap);
```

`Ui.cs` also holds two `[Preview]` methods (buttons, statistics cards, with an RTL variant): the model to follow to
preview your own building blocks ([Creating a component §12](components.md#12-previewing-preview)).

### 5.3 `AppChrome.cs`: the presentation slots

Three functions receive a `NavigationPresentationContext` and return a chrome's tree: `BottomBar`, `Rail`,
`Drawer`. They read `context.Destinations` and `context.SelectedIndex`, call `context.Select(i)` on touch, then
**register each element** so selection transitions know what to animate:

```csharp
var surface = Column(iconSlot, label).Gap(4).CenterContent().FlexGrow(1)
    .Clickable(() => context.Select(index));
return context.RegisterItem(index, surface, icon, label, badge: badge);
```

Notable details: the feed's item is a distinct centre button (`FeedItem`); the rail reserves
`context.SafeAreaTop`; in "Arabic demo" mode, `NavigationBars.Docked(context, style, action)` produces a bar with
a centre button using `DockedNavigationBarStyle.Default with { … }`, and `SetArabicNavigation` switches the
application's `FlowDirection` to RTL then calls `_navigation.RefreshChrome()`.

## 6. Page by page

| Page | Concepts illustrated | Copy it for… |
|---|---|---|
| `HomePage` | `ITopBarPage`, `UseScroll`, keyed `LazyRow`, reactive state, `OpenNavigationDrawer` | A scrolling home screen |
| `ExplorePage` | Filtering by `Rebuild()`, conditional sections, `Navigate` with transitions, controls catalogue | A catalogue / a components page |
| `PeoplePage` | 10,000-row `LazyColumn`, **per-row** states, reactive counter | A large interactive list |
| `SettingsPage` | Controlled switch, groups, `[Preview]` with `Device`, RTL toggle | A preferences screen |
| `StudioPage` | Navigation hub, generic `Navigate<T>`, tiles | A feature menu |
| `TextInputPage` | `UseTextEdit`, `TextField`/`TextArea`, `InputOptions`, `Changed`, keyboard actions | A form |
| `ApiPage` | Remote feed, idle/loading/ready/error states, `LazyListState`, `RebuildTopBar` | A network list |
| `ArticleDetailsPage` | `XenoPage<TArguments>`, `OnNavigatedTo`, shared model, per-selection scroll | A detail with typed parameters |
| `FeedPage` | `VerticalPager`, native video, per-page states, `After` timer | A short-video style vertical feed |
| `OverlaysPage` | Every overlay, custom `OverlayAnimation`, full `TopBarOptions` | Snackbar, dialog, sheet |
| `LabPage` | Runtime pressure: 10,000 rows + 120 chips | Performance testing |
| `VideoLabPage`, `AudioLabPage` | `MediaController`, `Suspend`/`Resume` lifecycle | Media players |

### 6.1 `HomePage`: a scrolling screen

The skeleton shared by nearly every page of the sample:

```csharp
public sealed class HomePage : XenoPage, ITopBarPage
{
    private readonly ScrollState _scroll = UseScroll();          // persistent: a field

    public string TopBarTitle => "XenoUI";
    public TopBarOptions? TopBarOptions => Ui.DrawerBar("Native UI · one C# codebase", OpenNavigationDrawer,
        Ui.BarAction("", () => Navigate<SettingsPage>(NavTransitions.SlideHorizontal())));

    public override XenoComponent Body() =>
        Column(Column(/* content */).Gap(16).Padding(...).CrossAxis(CrossAxisAlignment.Stretch))
            .Background(Theme.Canvas).CrossAxis(CrossAxisAlignment.Stretch).Scrollable(_scroll);
}
```

- `ITopBarPage` (`TopBarTitle`, `TopBarOptions`) provides the scaffold's fixed bar.
- The page scrolls through `.Scrollable(_scroll)` on the root container.
- A horizontal rail of clips: `LazyRow(FeedCatalog.Clips, clip => TrendingCard(app, clip), key: clip => clip.Url)
  .Height(210).Gap(10)`; each card calls `app.OpenFeedClip(index)`.
- Sub-components (`Hero`, `Shortcut`, `Feature`) are private static methods returning `XenoComponent`, i.e. local
  components ([Creating a component](components.md)).

### 6.2 `ExplorePage`: filtering by rebuild

```csharp
private int _category;                                         // page state, a field
private bool Show(int category) => _category == 0 || _category == category;

Show(2) ? Section("TYPOGRAPHY", "…", Typography()) : XenoComponent.None,

private XenoComponent CategoryChip(string name) { /* … */
    return Text(name, 12)…Clickable(() => { _category = index; Rebuild(); });
}
```

Changing category changes the **structure**: the page calls `Rebuild()`. The sections are small functions:
`Surfaces()` shows `Elevation(0 | 4 | 10)`, `Media()` shows `ScaleMode(Cover)` and icons, `Layout()` shows `Spacer()`,
`Transitions()` triggers `NavTransitions.SlideHorizontal/ScaleFade/FadeThrough`.

### 6.3 `PeoplePage`: 10,000 rows without rebuilding the list

```csharp
LazyColumn(People, PersonRow, key: index => index)
    .FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)

private XenoComponent PersonRow(int index)
{
    var label = UseState(following ? "Following" : "Follow");           // states bound to the row
    var background = UseState(following ? Theme.SurfaceHigh : Theme.Accent);
    return Row(/* … */,
        Text(label, 12).Background(background)…Clickable(() =>
        {
            bool now = _following.Add(index);
            if (!now) _following.Remove(index);
            label.Value = now ? "Following" : "Follow";                  // repaints only this button
            background.Value = now ? Theme.SurfaceHigh : Theme.Accent;
            _followingCount.Value = _following.Count.ToString();         // repaints the counter at the top
        }));
}
```

Lessons: (1) the **source of truth** (`HashSet<int> _following`) is a page field; (2) the `State`s created in
`PersonRow` live **as long as the row is materialised**; a recreated row re-reads `_following` to recover its
state; (3) a page-level `State<string>` (`_followingCount`) updates a statistic elsewhere on the screen through
`Ui.ReactiveStat`.

### 6.4 `SettingsPage`: a controlled switch

```csharp
Row(Box(22, 22).Background(Theme.White).CornerRadius(11))
    .MainAxis(toggle.Value ? MainAxisAlignment.End : MainAxisAlignment.Start)
    .Width(48).Height(28).Padding(3)
    .Background(toggle.Value ? Theme.Accent : 0xFF3A3A48).CornerRadius(14)
```

The thumb moves through the row's `MainAxis(End/Start)` (simpler than a `Spacer`), and the whole line is
`.Clickable(() => { toggle.Value = !toggle.Value; changed?.Invoke(...); Rebuild(); })`: the state is a page
`State<bool>`, the visual depends on its value, a `Rebuild()` refreshes it. `Group(title, rows…)` interleaves
`Ui.Divider()`s by building a `XenoComponent[]`. The page carries two `[Preview]`s, one with `Device = "iphone"`.

### 6.5 `StudioPage` and `TextInputPage`

`StudioPage` factors navigation with a generic method:

```csharp
private void Push<TPage>() where TPage : XenoPage =>
    Navigate<TPage>(NavTransitions.SlideHorizontal(280));
```

`TextInputPage` shows a form's cycle:

```csharp
private readonly TextEditController _name = UseTextEdit();
private readonly State<string> _namePreview = UseState("Nothing typed yet");

public TextInputPage()
{
    _name.Changed += (_, change) => _namePreview.Value = /* derived from change.Value.Text */;
}

TextField(_name, "Your name", InputOptions(action: NativeInputAction.Next,
    autofill: NativeAutofillHint.Name, label: "Full name"),
    onAction: action => _actionPreview.Value = $"Name: {action}").Height(54),
Text(_namePreview, 13)                                    // reactive
```

Also demonstrated: email keyboard, password with `obscureText: !_revealSecret` (revealing = `Rebuild()`),
`TextArea`, `SetText`, `Undo`/`Redo`/`SelectAll`, `CommitText`. The text and selection survive rebuilds because
the controllers are fields.

### 6.6 Network: `ApiPage`, `ArticleDetailsPage` and `Services/`

Three layers, from the bottom up:

1. **`ArticleService`**: HTTP + *source-generated* JSON, validation, 20 s timeout. No XenoUI type.
2. **`RemoteFeed<T>`**: `Idle/Loading/Ready/Error` states, cancellation, a request number that rejects stale
   results, publishing through an injected `Action<Action> post` (the **UI dispatcher**).
3. **The page**: subscribes, rebuilds, displays.

The page's lifecycle is the model to follow:

```csharp
public override void OnCreate()
{
    var dispatcher = Native.Require<IUiDispatcherCapability>();
    _feed = new ArticleFeed(new ArticleService(((App)Application).ApiClient), dispatcher.Post, () =>
    {
        if (_destroyed || !IsActive) return;       // never write to a page that was left
        Rebuild();
        RebuildTopBar();                           // the Refresh icon follows the feed state
    });
}
public override void OnActivated()   => _feed.Activate();     // loads on first display
public override void OnDeactivated() => _feed.Deactivate();   // cancels the request in flight
public override void OnDestroy()     { _destroyed = true; _feed.Dispose(); }
```

The list is `LazyColumn(state.Items, ArticleCard, key: article => article.Id, state: _listState)`, and touch calls
`((App)Application).OpenArticle(article)`.

`ArticleDetailsPage : XenoPage<ArticleDetailsArgs>` receives a **typed payload**:

```csharp
public sealed record ArticleDetailsArgs(Article Article);
public override void OnNavigatedTo(ArticleDetailsArgs arguments) => _model.Select(arguments.Article);
```

Scrolling goes back to the top when the article changes (`_scroll = UseScroll()` on selection change, explicitly a
"cold" path). Network details: [API demo](sample-api.md); typed parameters:
[Constrained text and typed navigation](text-layout-navigation.md).

### 6.7 `FeedPage`: a vertical feed

```csharp
return ZStack(
    Items.Length == 0 ? FollowingEmpty() :
        VerticalPager(Items, BuildPage, key: clip => clip.Url, state: ActiveState),
    TopBar(), Toast(),
    _commentsOpen ? CommentsSheet() : XenoComponent.None
).Background(Theme.Black).CrossAxis(CrossAxisAlignment.Stretch);
```

- **One `State` per property and per page** (`_likeColor[i]`, `_progress[i]`, `_videoHandle[i]`…): settling on a clip
  flips a few values, without rebuilding the screen mid-gesture. The arrays are created by
  `UseStates(n, value)`.
- `LazyListState.PageChanged` announces the clip that arrived; `ScrollToPage(index)` drives the feed from code;
  `UserScrollEnabled = false` makes the comments sheet master of vertical gestures.
- Video is a native output attached to the current page (`.NativeVideo(_videoHandle[i])`); a **pool of two
  players** keeps the previous clip.
- A "like" double-tap with a heart that disappears uses the `After(ms, action)` timer:
  `Task.Delay(ms).ContinueWith(_ => post(action))`, `post` being `Native.Require<IUiDispatcherCapability>().Post`,
  with a **serial number** (`_heartSerial`) to ignore stale timers
  ([Animations §5](animations.md#5-animating-yourself-step-by-step)).
- `OnActivated`/`OnDeactivated` resume and suspend the players; `OnDestroy` releases them.

### 6.8 `OverlaysPage`

Each button calls a widget: `Snackbar.Show(Application, "Saved to your library", options)`, alerts, dialogs,
anchored popups, sheets. The page adds a motion selector, one entry of which is a **custom** animation built once:

```csharp
private static readonly OverlayAnimation Bounce = OverlayAnimation.Custom(NavTransition.Create()
    .Enter(x => x.Opacity(0, 1)
        .Animate(NavAnimatedProperty.ScaleX, 0.6f, 1, OvershootEase)
        .Animate(NavAnimatedProperty.ScaleY, 0.6f, 1, OvershootEase))
    .Exit(x => x.Opacity(1, 0).Scale(1, 0.85f))
    .Duration(380).Build());
```

Its `TopBarOptions` property returns a **complete** options object (navigation icon, actions, colours, elevation):
the reference for customising the bar. See [Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md) (French).

## 7. Pattern catalogue

| I want… | Look at |
|---|---|
| A scrolling screen with an application bar | `HomePage` (`ITopBarPage` + `UseScroll`) |
| State that repaints a single element | `HomePage.Counter`, `PeoplePage` (`State<string>`, `State<uint>`) |
| Change the structure (filter, inner tab) | `ExplorePage` (`_category` + `Rebuild()`) |
| A large list | `PeoplePage`, `LabPage` (`LazyColumn(..., key: …)`) |
| A horizontal list | `HomePage`, `ExplorePage` (`LazyRow`) |
| Per-list-item states | `PeoplePage.PersonRow` |
| A switch / preferences | `SettingsPage.Setting` |
| A form and the keyboard | `TextInputPage` |
| Call an API without blocking the UI | `Services/`, `ApiPage` |
| A detail with a typed parameter | `ArticleDetailsPage`, `App.OpenArticle` |
| Navigate between tabs from a page | `App.SelectTab`, `HomePage` (`app.SelectTab(…)`) |
| A custom page transition | `ExplorePage.Transitions`, `App.ChromeTransition` |
| Customise the bottom bar / rail / drawer | `AppChrome` |
| RTL | `App.SetArabicNavigation`, `SettingsPage` |
| An animated overlay | `OverlaysPage` |
| Native video | `FeedPage`, `VideoLabPage` |
| Resume/suspend in the background | `App.OnBackground/OnForeground`, pages' `Suspend()/Resume()` |
| A theme and reusable building blocks | `Design/Theme.cs`, `Design/Ui.cs` |
| Preview without a device | `[Preview]` on pages and on `Ui` |

## 8. Cautions: what the sample does for demonstration

The sample favours readability; four things are **not** to be reproduced as they are in production:

1. **`UseState` in `Body()`.** `HomePage.Body` (`var taps = UseState(24)`) and `ExplorePage.Body`
   (`var count = UseState(7)`) create their state during composition: it goes back to its initial value on every
   `Rebuild()` (for example when changing category in Explore). In your code, make it a field
   (`private readonly State<int> _taps = UseState(24);`). The [developer guide](developer-guide.md#7-state-updates-and-rebuilding) says so too.
2. **The `(App)Application` cast.** Convenient here; in a large application, expose a services interface or pass
   `Action`s to components instead.
3. **`UseScroll()` reassigned** in `ArticleDetailsPage`: a deliberate choice (new scroll when the article changes),
   not a general pattern.
4. **Hard-coded strings and glyphs** (`""`): in production, name them (`Icons.Home`).

Conversely, several choices deserve imitation as they are: keeping `Services/` separate from the rest, injecting
the UI dispatcher, guarding late callbacks with `IsActive`, validating data at the boundary, and keeping
persistent objects (`UseScroll`, `UseLazyList`, `UseTextEdit`, `State`) in page fields.

## 9. Step by step: adding a "Notes" page

An exercise that reuses the sample's idioms. *This example was not compiled for this documentation (the Android
API 37 workload was not available on the authoring machine): adapt it if the compiler reports a difference.*

**1. Create the page** `Pages/NotesPage.cs`:

```csharp
using SampleApp.Design;
using XenoUI.Core.Hooks;

namespace SampleApp.Pages;

public sealed class NotesPage : XenoPage, ITopBarPage
{
    private sealed record Note(int Id, string Text);

    private readonly TextEditController _input = UseTextEdit();
    private readonly List<Note> _notes = new();
    private readonly LazyListState _list = UseLazyList();
    private int _nextId;

    public string TopBarTitle => "Notes";
    public TopBarOptions? TopBarOptions => Ui.BackBar($"{_notes.Count} notes", GoBack);

    public override XenoComponent Body() =>
        Column(
            Row(
                TextField(_input, "New note").Height(48).FlexGrow(1),
                Ui.Primary("Add", Add, 13))
            .Gap(8).CrossAxis(CrossAxisAlignment.Center).Padding(16),
            _notes.Count == 0
                ? Text("Nothing yet. Write something above.", 13).TextColor(Theme.Muted).Padding(16)
                : LazyColumn(_notes, NoteRow, key: note => note.Id, state: _list)
                    .FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)
                    .Padding(Spacing.Only(left: 16, right: 16, bottom: 12)))
        .Background(Theme.Canvas).CrossAxis(CrossAxisAlignment.Stretch);

    private static XenoComponent NoteRow(Note note) =>
        Text(note.Text, 14).TextColor(Theme.Ink).MaxLines(3, ellipsis: true)
            .Padding(14).Background(Theme.Surface).CornerRadius(14)
            .Margin(Spacing.Only(bottom: 8));

    private void Add()
    {
        string text = _input.Value.Text.Trim();
        if (text.Length == 0) return;
        _notes.Insert(0, new Note(_nextId++, text));
        _input.SetText("");
        Rebuild();
        RebuildTopBar();                               // the subtitle shows the number of notes
        Snackbar.Show(Application, "Note added");
    }
}
```

**2. Register it** in `App.OnStart()`: `Page<NotesPage>();`.

**3. Reach it** from `StudioPage`, with one more line after the other `Ui.NavRow`s:

```csharp
Ui.NavRow("", "Notes", "A tiny list with a text field",
    Theme.Amber, Theme.AmberSoft, () => Push<NotesPage>()),
```

**4. What the exercise exercises:** a persistent object in a field (`TextEditController`, `LazyListState`), a list
with stable keys, a bar whose subtitle depends on state (`RebuildTopBar()`), an overlay (`Snackbar`), and
navigation through `Push<T>()`. For a version with a detail view, model it on
`ArticleDetailsPage : XenoPage<TArguments>`. To preview it, add `[Preview]` on the class.

## 10. Tests related to the sample

```powershell
dotnet run --project tests/SampleApp.Api.RegressionTests -c Release
dotnet run --project tests/SampleApp.Overlays.RegressionTests -c Release
dotnet run --project tests/XenoUI.Core.RegressionTests -c Release
```

The API tests cover HTTP, JSON, cancellation, deferred publishing and out-of-order results without a platform
project; the overlay tests cover the overlay layer. Validating the sample on a device (navigation, players,
keyboard, background) remains manual: [Troubleshooting](troubleshooting.md).
