# Application architecture

[Documentation](index.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [State](state.md) · [Side effects](side-effects.md) · [Learning from the SampleApp](sample-app.md)

This guide proposes a **layered structure** for a XenoUI application, the one the `SampleApp` follows: a UI that
displays state and raises events, presentation models that hold that state, and portable services that do the work.
It does not impose a framework: XenoUI provides neither a dependency-injection container nor a `ViewModel`; it
provides **attachment points** (lifecycle, dispatcher, navigation parameters) on which this architecture rests. For
the framework's *internal* architecture, see [Architecture](architecture/index.md); for the extension points it
allows, [Decisions §6](architecture/decisions-v1.md#6-allowed-extension-points).

## Contents

1. [The three layers](#1-the-three-layers)
2. [Data flow](#2-data-flow)
3. [Services layer](#3-services-layer)
4. [Presentation models layer](#4-presentation-models-layer)
5. [UI layer](#5-ui-layer)
6. [Assembly and injection](#6-assembly-and-injection)
7. [Navigation and parameters](#7-navigation-and-parameters)
8. [File organisation](#8-file-organisation)
9. [Testing each layer](#9-testing-each-layer)
10. [Common architecture mistakes](#10-common-architecture-mistakes)
11. [Correspondence with Android architecture](#11-correspondence-with-android-architecture)

---

## 1. The three layers

```text
┌────────────────────────────────────────────────────────────┐
│ UI                  Pages · Components · Design            │  XenoUI, Skia
│   displays state, calls Actions                             │
├────────────────────────────────────────────────────────────┤
│ Presentation models   RemoteFeed<T> · ArticleDetailsModel   │  portable
│   hold the state, orchestrate, publish snapshots            │
├────────────────────────────────────────────────────────────┤
│ Services            ArticleService · repositories · storage │  portable
│   HTTP, JSON, disk; no notion of UI                         │
└────────────────────────────────────────────────────────────┘
```

| Layer | Knows XenoUI? | Knows the platform? | Where |
|---|---|---|---|
| UI | Yes | Only through `Native` | `Pages/`, `Components/`, `Design/` |
| Presentation models | **No** (they receive an `Action<Action>` to get back to the UI thread) | No | The sample's `Services/` |
| Services | No | No | `Services/` |

The boundary "a lower-layer type never references the upper layer" makes the two bottom layers testable by a plain
`net11.0` project.

## 2. Data flow

```text
User ─ touch ─▶ Component ─ Action ─▶ Page ─▶ Model ─▶ Service
                                                │          │
                       Rebuild() / State.Value ◀┴─ snapshot ◀┘ (background thread → dispatcher → UI)
```

1. **Events flow up**: a component calls an `Action`; the page turns it into a call to the model.
2. **The model** calls a service, on a background thread, then **publishes a new state** (immutable snapshot).
3. **State flows down**: the page receives the notification on the UI thread and updates the screen
   (`State.Value =` or `Rebuild()`).

One single source of truth per piece of data; the UI keeps **only** purely visual state (open tab, scroll position,
text being typed).

## 3. Services layer

A service is a class **with no UI dependency**: it takes parameters, returns values (or `Task`s), accepts a
`CancellationToken`.

```csharp
public interface IArticleService
{
    Task<Article[]> GetArticlesAsync(CancellationToken cancellationToken);
}

public sealed class ArticleService(HttpClient client) : IArticleService
{
    public async Task<Article[]> GetArticlesAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await client.GetAsync(Endpoint, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        /* … source-generated deserialisation, validation at the boundary … */
    }
}
```

Good practices (all in the sample): **interface + implementation** for testing; **one reused `HttpClient`** owned by
the application; **source-generated JSON** (no runtime reflection); **overall timeout** including reading the body;
**validation** of received data (unique identifiers for stable list keys); **typed** errors turned into messages
higher up.

## 4. Presentation models layer

The model holds the state of a flow or a screen and **publishes it as an immutable snapshot**. The equivalent of
Android's `ViewModel` + `StateFlow`.

```csharp
public enum FeedStatus { Idle, Loading, Ready, Error }
public sealed record RemoteSnapshot<T>(FeedStatus Status, T[] Items, string? Error = null);

public class RemoteFeed<T>(Func<CancellationToken, Task<T[]>> load, Action<Action> post, Action changed)
    : IDisposable
{
    public RemoteSnapshot<T> Snapshot { get; }       // read by the page
    public void Activate();                          // loads if needed
    public void Refresh();                           // cancels the previous one, restarts
    public void Deactivate();                        // cancels, keeps the data
    public void Dispose();
}
```

What this model guarantees, and what you should guarantee in turn:

- **Idempotent** `Activate()` ("already loaded?": nothing to do);
- **cancellation** of the previous request and a **version number**: a stale result is ignored;
- **the old data stays** during a reload or after an error;
- **publishing** goes through `post` (the injected dispatcher) then the `changed` callback;
- **no XenoUI type**: only `Action<Action>` to get back to the UI thread.

## 5. UI layer

The page **observes** the model and **declares** the interface:

```csharp
public sealed class ApiPage : XenoPage, ITopBarPage
{
    private ArticleFeed _feed = null!;
    private volatile bool _destroyed;

    public override void OnCreate()
    {
        var dispatcher = Native.Require<IUiDispatcherCapability>();
        _feed = new ArticleFeed(new ArticleService(((App)Application).ApiClient), dispatcher.Post, () =>
        {
            if (_destroyed || !IsActive) return;
            Rebuild();
            RebuildTopBar();
        });
    }
    public override void OnActivated()   => _feed.Activate();
    public override void OnDeactivated() => _feed.Deactivate();
    public override void OnDestroy()     { _destroyed = true; _feed.Dispose(); }

    public override XenoComponent Body()
    {
        var state = _feed.Snapshot;                         // read-only
        return Column(StatusBar(state), LazyColumn(state.Items, ArticleCard, key: a => a.Id));
    }
}
```

The page:

- **contains no business logic** (no HTTP, no JSON);
- **translates** the model's state into UI (`FeedStatus.Error` → text and colour);
- **translates** UI events into calls to the model (`Refresh`);
- only keeps **display** state (scroll, inner tab, text being typed).

Reusable components, for their part, know **neither model nor page**: values, `State<T>` and `Action`
([Creating a component](components.md)).

## 6. Assembly and injection

XenoUI does not impose an injection container. The sample's pattern is a manual **composition root** in `App`:

```csharp
public class App : XenoApp
{
    internal HttpClient ApiClient { get; } = new();                   // owned by the application
    internal ArticleDetailsModel ArticleDetails { get; private set; } = null!;

    public override void OnStart()
    {
        ArticleDetails = new ArticleDetailsModel(new ArticleService(ApiClient),
            Native.Require<IUiDispatcherCapability>().Post);          // explicit dependencies
        Page<HomePage>(); /* … */
        UseNavigationScaffold(_navigation);
    }

    public override void OnDispose() { ArticleDetails?.Dispose(); ApiClient.Dispose(); }
}
```

- **A single composition root**: `App.OnStart`. Pages fetch what they use through `(App)Application`.
- **Constructor dependencies** for models and services; **interfaces** where you want to substitute in tests.
- **Lifetime**: what `App` owns lives for the whole application; what a page creates lives as long as it does
  (`OnCreate` → `OnDestroy`).
- If the application grows, replace the `(App)Application` cast with a **services interface** exposed by `App`; an
  injection container remains possible (nothing in XenoUI forbids it), but it must be built in `OnStart`.

Platform services (`NativeTunnel`): `Native.Require<T>()` (fails if missing) and `Native.Get<T>()` (`null` if
absent), for example `IUiDispatcherCapability`, `IMotionPreferencesCapability`, `IMediaPlaybackCapability`. They are
read in the UI layer and **injected** lower down as plain delegates.

## 7. Navigation and parameters

- **Navigation**: called from the page (`Navigate<T>()`), never from a model or a component. The component receives
  an `Action`.
- **Typed parameters**: `XenoPage<TArgs>` + `Navigate<TPage, TArgs>(args)`; the page hands them to the model in
  `OnNavigatedTo(args)`.
- **Application intents**: `App` methods (`SelectTab`, `OpenArticle`, `OpenFeedClip`) keep pages from knowing the
  scaffold:

```csharp
internal void OpenArticle(Article article) =>
    Navigate<ArticleDetailsPage, ArticleDetailsArgs>(new(article), NavTransitions.SlideHorizontal(280));
```

- **Results**: XenoUI provides no typed navigation result ([V1 limits](navigation-scaffold.md#v1-limits)); go
  through a shared model or an `Action` in the parameters.

## 8. File organisation

```text
MyApp/
├─ App.cs                       composition root, scaffold, lifecycle
├─ Pages/                       one XenoPage per screen (+ args)
├─ Components/                  reusable components, one file each
├─ Design/                      Theme, Ui (application building blocks), AppChrome
├─ Models/                      presentation models (feeds, selection)
└─ Services/                    portable services (HTTP, storage)
```

The sample groups models and services in `Services/`; separate them as soon as the project grows. If you want to test
models and services without the platform, put them in a **`net11.0` library** referenced by the application.

## 9. Testing each layer

| Layer | How | See |
|---|---|---|
| Services | Unit test with a fake `HttpMessageHandler` | `tests/SampleApp.Api.RegressionTests` |
| Models | Unit test: fake dispatcher (`a => a()`), snapshots, cancellation, out-of-order results | same |
| Components / pages | Headless PNG rendering (`PreviewRenderer.Render`) | [Testing](testing.md) |
| Device integration | Manual / `xeno` tool | [Troubleshooting](troubleshooting.md) |

## 10. Common architecture mistakes

| Mistake | Consequence | Fix |
|---|---|---|
| HTTP and JSON in the page | Untestable; network triggered by composition | Service + model |
| The model references `XenoPage` or `Rebuild` | Coupling; no test without UI | Injected `changed` callback |
| Duplicated state (model and page) | Inconsistent screens | The model publishes, the page reads |
| A component that navigates | Non-reusable component | It receives an `Action` |
| Global static singleton | Leaks between tests; unclear lifetime | Instance owned by `App` |
| Work in `Body()` | Repeated at every rebuild | `OnActivated` / model |
| Mutating the snapshot | Races between threads | Immutable snapshots (`record`, arrays) |

## 11. Correspondence with Android architecture

| Android (architecture guide) | XenoUI |
|---|---|
| UI layer (Composables) | Pages and components |
| `ViewModel` | Presentation model (`RemoteFeed<T>`, `ArticleDetailsModel`) |
| `StateFlow<UiState>` | Immutable `Snapshot` + `changed` callback |
| `collectAsStateWithLifecycle` | `OnCreate`/`OnDestroy` subscription + `IsActive` + `Rebuild()` |
| Data layer / Repository | Services (`ArticleService`) |
| Hilt / injection | Composition root in `App.OnStart` |
| `viewModelScope` | The model's `CancellationTokenSource`, cancelled in `Deactivate` |
| `SavedStateHandle` | Not provided (no restoration after process death) |
| Unidirectional data flow | Same: events flow up, state flows down |
