# Lifecycle

[Documentation](index.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [State](state.md) · [Side effects](side-effects.md) · [Navigation](navigation-scaffold.md)

This guide describes **when** your application's code runs: at startup, when a page appears or disappears, when the
application goes to the background, and when `Body()` is called. Knowing where to put each piece of work (creating a
model, starting a request, releasing a player) avoids most leaks and late writes. For the engine-level view, see
[Runtime §9–10](architecture/runtime.md#9-state-and-lifetime); the older [Application Lifecycle](app-lifecycle.md)
note is historical.

## Contents

1. [The three levels](#1-the-three-levels)
2. [Application lifecycle](#2-application-lifecycle)
3. [Page lifecycle](#3-page-lifecycle)
4. [When is `Body()` called?](#4-when-is-body-called)
5. [Navigation and retention](#5-navigation-and-retention)
6. [Background and foreground](#6-background-and-foreground)
7. [Where to put what](#7-where-to-put-what)
8. [Components: no lifecycle](#8-components-no-lifecycle)
9. [A worked timeline](#9-a-worked-timeline)

---

## 1. The three levels

| Level | Type | Hooks |
|---|---|---|
| Application | `XenoApp` | `OnStart`, `OnForeground`, `OnBackground`, `OnLowMemory`, `OnDispose` (and `OnPreview` in preview) |
| Page | `XenoPage` | `OnCreate`, `OnActivated`, `OnDeactivated`, `OnDestroy` (and `OnNavigatedTo(args)` for `XenoPage<T>`) |
| Component | function | **none**: it is a function called by `Body()` |

## 2. Application lifecycle

```text
host starts
  └─ OnStart()               register pages, create models and services
       └─ Page<T>() × N      each page is instantiated once → page.OnCreate()
       └─ UseNavigationScaffold(...)
  └─ first composition (initial destination: Body() then OnActivated())
  …
OnBackground()  ⇄  OnForeground()      the app leaves / regains the foreground
  …
OnLowMemory()                          memory pressure reported by the system
OnDispose()                            end: release players, clients, models; page.OnDestroy() of every page
```

```csharp
public class App : XenoApp
{
    public override void OnStart()
    {
        ApiClient = new HttpClient();
        ArticleDetails = new ArticleDetailsModel(new ArticleService(ApiClient),
            Native.Require<IUiDispatcherCapability>().Post);
        Page<HomePage>();
        Page<SettingsPage>();
        UseNavigationScaffold(_navigation);
    }

    public override void OnBackground() { if (ActivePage is FeedPage feed) feed.Suspend(); }
    public override void OnForeground() { if (ActivePage is FeedPage feed) feed.Resume(); }
    public override void OnDispose()    { ApiClient.Dispose(); }
}
```

Points of attention:

- **`OnStart` is the right place** to build what several pages share (HTTP client, models). No long blocking work:
  the application displays nothing while it runs.
- **`Page<T>()` calls `OnCreate()` immediately.** Pages registered in `OnStart` are therefore all created at
  startup, even those the user will never open: keep `OnCreate` light.
- **In preview** (`[Preview]`, VS / VS Code), the application is *constructed* but not *started*: `OnStart` does not
  run, `OnPreview()` does. Put in it only what the pages read, no network
  ([sample](sample-app.md#41-it-declares-the-navigation-scaffold)).
- `OnLowMemory`: release rebuildable caches (decoded images, in-memory results).

## 3. Page lifecycle

```csharp
public sealed class ApiPage : XenoPage, ITopBarPage
{
    public override void OnCreate()      { /* once: create the feed, subscribe */ }
    public override void OnActivated()   { /* each time the page becomes visible */ }
    public override void OnDeactivated() { /* when it stops being so: cancel, suspend */ }
    public override void OnDestroy()     { /* once: unsubscribe, release */ }
}
```

| Hook | When | Do | Do not |
|---|---|---|---|
| `OnCreate` | Once, at `Page<T>()` | Create internal objects, subscribe to a model | Load data, start a player |
| `OnNavigatedTo(args)` | On arrival on a `XenoPage<T>` with its parameters | Hand the selection to the model | Compose the UI |
| `OnActivated` | Each time it is displayed (after the navigation commit) | Start/resume work: request, player | Long operations on the UI thread |
| `OnDeactivated` | When the page is no longer the active one | Cancel requests, suspend players | Release for good (it will return) |
| `OnDestroy` | When the application ends | Unsubscribe, release | Assume it precedes every navigation |

Two important points:

- **Pages are singletons**: instantiated once, reused. `OnDestroy` is not called on each back navigation. Do not
  use it to "clean up when leaving the screen": that is `OnDeactivated`'s role.
- Page callbacks only change at the **navigation commit**: a transition in progress is not a lifecycle state.

### `IsActive`

`IsActive` is true if the page is the foreground one. Test it in any callback that may arrive afterwards (network
response, timer) before modifying the UI ([Side effects](side-effects.md)).

## 4. When is `Body()` called?

| Situation | `Body()` called? |
|---|---|
| First appearance of a destination's root page (lazy composition) | Yes, once |
| Returning to an already-visited destination (tab) | **No**: the scene is kept |
| Push of a detail page | Yes (new scene mounted on top) |
| Pop | No for the revealed page; the removed scene is released |
| Page `Rebuild()` | Yes, and its scene is replaced |
| A bound `State` changes (`Text(state)`) | **No** |
| Rotation, size change | No (layout only) |
| Preview | Yes, for rendering |

Consequence: do not expect `Body()` to run "each time the page appears". For work on every appearance, use
**`OnActivated`**.

Components (`Chip.View(...)`) are called **every time their caller** is.

## 5. Navigation and retention

- **Tabs / scaffold destinations**: keep their ECS tree, scroll, list window and text controllers. Leaving a tab
  calls `OnDeactivated`, not `OnDestroy`.
- **A tab's stack**: `Navigate<T>()` pushes; `GoBack()` pops. `ReselectionBehavior = PopToRoot` returns to a
  tab's root when it is tapped again.
- **System back**: Android 7–12 through `OnBackPressed`, Android 13+ through `OnBackInvokedDispatcher` (registered
  only when `CanGoBack`). `CanGoBack` accounts for modal overlays and the drawer.
- **Parameters**: `Navigate<TPage, TArgs>(args)`; the payload is kept by the stack entry and restored on return.

Details: [Navigation and NavigationScaffold](navigation-scaffold.md#lifecycle).

## 6. Background and foreground

When the user presses Home:

1. `App.OnBackground()` is called; suspend whatever consumes (player, request, timer, camera);
2. the active page receives the calls you relay to it (the sample calls `Suspend()` on the current page);
3. on return, `App.OnForeground()` resumes.

The framework does not suspend your tasks for you. **Any work that continues in the background is your choice.**
See [Android graphics recovery](android-graphics-lifecycle.md) for GPU context loss (screen lock): scenes are kept
without recomposition.

## 7. Where to put what

| Need | Hook |
|---|---|
| Create a client, a shared model | `App.OnStart` |
| Subscribe to a model event | `OnCreate` (unsubscribe in `OnDestroy`) |
| Load the list on first display | `OnActivated` (idempotent: "already loaded?") |
| Receive navigation parameters | `OnNavigatedTo` |
| Cancel a request when leaving the screen | `OnDeactivated` |
| Suspend a player in the background | `App.OnBackground` → the page's `Suspend()` method |
| Release a player, a client | `OnDestroy` / `App.OnDispose` |
| React to memory pressure | `App.OnLowMemory` |
| Rebuild when data changes | model callback → `if (IsActive) Rebuild()` |

## 8. Components: no lifecycle

A component has no observable creation or destruction: it is a function call. If a component needs a resource
(timer, subscription), **the page that owns it** creates and releases it. Components with internal state (classes)
are objects the page holds in a field ([State](state.md#2-where-to-keep-state)).

## 9. A worked timeline

The sample's `ApiPage`, end to end:

```text
App.OnStart            Page<ApiPage>()  ──▶  ApiPage.OnCreate()     creates ArticleFeed (dispatcher injected)
User opens the screen
  navigation commit    ──▶  ApiPage.OnActivated()  ──▶  _feed.Activate()  : HTTP request
  Body() (1st time)    shows "Loading…"
Response received      (background thread) ──▶ dispatcher.Post ──▶ callback : if (IsActive) { Rebuild(); RebuildTopBar(); }
User leaves
  navigation commit    ──▶  ApiPage.OnDeactivated() ──▶ _feed.Deactivate() : cancels
App to background      App.OnBackground() ──▶ api.Suspend()
Back                   App.OnForeground() ──▶ api.Resume()
App ends               ApiPage.OnDestroy() ──▶ _feed.Dispose()
```
