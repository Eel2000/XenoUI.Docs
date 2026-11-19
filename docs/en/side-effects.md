# Side effects and async work

[Documentation](index.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [State](state.md) · [Lifecycle](lifecycle.md) · [Architecture](app-architecture.md)

A **side effect** is anything that leaves the pure computation of a UI: a network call, a file read, a timer, an
event subscription, an audio player. `Body()` and components must **never** produce one. This guide shows where to put
them, how to bring their results back to the UI thread, and how to avoid late writes to a page that is no longer on
screen. For the engine's own threading rules see [Runtime §11](architecture/runtime.md#11-threading-snapshots-and-concurrency).

If you come from Jetpack Compose, the [correspondence table](#1-correspondence-with-compose-effects) gives the
equivalent of `LaunchedEffect`, `DisposableEffect`, `rememberCoroutineScope`…

## Contents

1. [Correspondence with Compose effects](#1-correspondence-with-compose-effects)
2. [Golden rule: no effect in `Body()`](#2-golden-rule-no-effect-in-body)
3. [Threads: who may do what](#3-threads-who-may-do-what)
4. [Starting and cancelling work with the page](#4-starting-and-cancelling-work-with-the-page)
5. [Subscribing and unsubscribing](#5-subscribing-and-unsubscribing)
6. [Late results: `IsActive`, versions and serial numbers](#6-late-results-isactive-versions-and-serial-numbers)
7. [Timers and "after N ms"](#7-timers-and-after-n-ms)
8. [A complete remote feed](#8-a-complete-remote-feed)
9. [Background](#9-background)
10. [Pitfalls](#10-pitfalls)

---

## 1. Correspondence with Compose effects

| Compose | XenoUI |
|---|---|
| `LaunchedEffect(key)` | Start in `OnActivated()` (or `OnNavigatedTo`); cancel in `OnDeactivated()` |
| `DisposableEffect` | Subscribe in `OnCreate()`, unsubscribe in `OnDestroy()`; or an `OnActivated`/`OnDeactivated` pair |
| `rememberCoroutineScope()` | A `CancellationTokenSource` page field |
| `produceState` / `collectAsState` | An object that publishes a snapshot + `changed` callback → `Rebuild()` or `State.Value = …` ([§8](#8-a-complete-remote-feed)) |
| `snapshotFlow { state }` | `State<T>.OnChanged` (event) |
| `rememberUpdatedState` | Read a page **field** in the callback, at the time it runs |
| `SideEffect` | No equivalent: the UI has no effect at composition |
| `rememberCoroutineScope` + `delay` | `Task.Delay(ms)` then `Post` ([§7](#7-timers-and-after-n-ms)) |

## 2. Golden rule: no effect in `Body()`

`Body()` is called again on each `Rebuild()` and in previews. An effect inside it would be repeated, and run even at
design time.

```csharp
// ✗ Do not
public override XenoComponent Body()
{
    _ = LoadAsync();                       // restarted on every rebuild
    File.WriteAllText(path, text);         // side effect during composition
    return Text(_status, 14);
}

// ✓ Work starts at lifecycle time, composition only reads
public override void OnActivated() => _feed.Activate();
public override XenoComponent Body() => Text(_status, 14);
```

## 3. Threads: who may do what

| Operation | Thread |
|---|---|
| `Body()`, `Clickable` callbacks, `OnActivated`… | UI / control thread |
| Assign a `State<T>`, a `TextEditController`, a `ScrollState` | **UI thread only** |
| `Rebuild()` | Any thread: it **reschedules itself** onto the control thread |
| Network, JSON, disk, heavy computation | **Background thread** (`Task.Run`) |

To get back to the UI thread from a background thread, inject the **dispatcher**:

```csharp
var ui = Native.Require<IUiDispatcherCapability>();     // in OnCreate()
…
_ = Task.Run(async () =>
{
    var data = await service.GetAsync(token);           // background
    ui.Post(() =>                                       // back on the UI thread
    {
        _items = data;
        Rebuild();
    });
});
```

The sample hands `dispatcher.Post` to its services: **the service knows neither XenoUI nor the platform**, it just
receives an `Action<Action>`.

## 4. Starting and cancelling work with the page

A `CancellationTokenSource` field plays the role of the scope:

```csharp
public sealed class SearchPage : XenoPage
{
    private CancellationTokenSource? _search;
    private readonly State<string> _status = UseState("");
    private IUiDispatcherCapability _ui = null!;

    public override void OnCreate() => _ui = Native.Require<IUiDispatcherCapability>();

    public override void OnDeactivated() => _search?.Cancel();    // leaving the screen cancels

    private void Search(string query)
    {
        _search?.Cancel();                                        // only one search at a time
        var source = _search = new CancellationTokenSource();
        _status.Value = "Searching…";
        _ = Task.Run(async () =>
        {
            try
            {
                var results = await _service.SearchAsync(query, source.Token);
                _ui.Post(() => { if (!source.IsCancellationRequested) { _results = results; _status.Value = ""; Rebuild(); } });
            }
            catch (OperationCanceledException) { }
        });
    }
}
```

Key points: cancel the previous one **before** starting; ignore the result if cancellation was requested; never let
a task write into a `State` without going through `Post`.

## 5. Subscribing and unsubscribing

Any subscription to an event of an object that outlives the page must be undone:

```csharp
public override void OnCreate()  { _model = ((App)Application).ArticleDetails; _model.Changed += OnChanged; }
public override void OnDestroy() { _destroyed = true; _model.Changed -= OnChanged; }

private void OnChanged() { if (!_destroyed && IsActive) { Rebuild(); RebuildTopBar(); } }
```

This is `ArticleDetailsPage`'s pattern. Variants:

| Source | Subscription |
|---|---|
| Application model (`Changed`) | `OnCreate` / `OnDestroy` |
| Media player (`player.Changed`) | `OnActivated` (resubscribe without duplicates: `-=` then `+=`) / `OnDeactivated` |
| `TextEditController.Changed` | The page's constructor (same lifetime as the controller) |
| `State<T>.OnChanged` | Same: same lifetime as both objects |

## 6. Late results: `IsActive`, versions and serial numbers

A response may arrive after the user left the screen, restarted a search or changed item. Three complementary
guards:

1. **`IsActive`**: only act if the page is in the foreground.
2. **A (volatile) `_destroyed` flag** set in `OnDestroy`.
3. **A version / serial number** incremented on each new request; the result is only published if its number is
   still the current one:

```csharp
int serial = ++_toastSerial;
After(1600, () =>
{
    if (serial != _toastSerial) return;       // a newer toast took over
    _toast.Value = "";
});
```

The sample's `RemoteFeed<T>` applies all three: it checks `_disposed`, `_active` and `version` **twice** (before and
inside the `Post`), because the state may change between the two.

## 7. Timers and "after N ms"

There is no timer built into composition. The sample's pattern:

```csharp
private Action<Action>? _post;                         // = Native.Require<IUiDispatcherCapability>().Post

private void After(int milliseconds, Action action)
{
    var post = _post;
    if (post == null) return;
    _ = Task.Delay(milliseconds).ContinueWith(_ => post(action), TaskScheduler.Default);
}
```

Always pair it with a serial number so that two successive timers do not cross. For stepwise animations (colour
interpolation, progress), see [Animations §5](animations.md#5-animating-yourself-step-by-step).

## 8. A complete remote feed

The sample separates three roles; reuse them as they are:

```csharp
// 1. Portable service: no UI dependency
public interface IArticleService { Task<Article[]> GetArticlesAsync(CancellationToken ct); }

// 2. Presentation model: immutable snapshot + event, dispatcher injected
public sealed record RemoteSnapshot<T>(FeedStatus Status, T[] Items, string? Error = null);
public class RemoteFeed<T>(Func<CancellationToken, Task<T[]>> load, Action<Action> post, Action changed)
{
    public RemoteSnapshot<T> Snapshot { get; }
    public void Activate(); public void Deactivate(); public void Refresh(); // cancellation + versions
}

// 3. Page: lifecycle + rendering
public override void OnCreate()
{
    _feed = new ArticleFeed(service, Native.Require<IUiDispatcherCapability>().Post, () =>
    {
        if (_destroyed || !IsActive) return;
        Rebuild(); RebuildTopBar();
    });
}
public override void OnActivated()   => _feed.Activate();
public override void OnDeactivated() => _feed.Deactivate();
public override void OnDestroy()     { _destroyed = true; _feed.Dispose(); }
public override XenoComponent Body() => /* reads _feed.Snapshot and displays Loading / Ready / Error */;
```

The snapshot keeps the **previous data** during a refresh or after an error, which avoids emptying the screen.
Details: [API demo](sample-api.md).

## 9. Background

The system may suspend the application at any moment. The framework suspends nothing for you:

```csharp
public override void OnBackground() { if (ActivePage is VideoLabPage v) v.Suspend(); }
public override void OnForeground() { if (ActivePage is VideoLabPage v) v.Resume(); }
```

`Suspend()` cancels or pauses (player, requests); `Resume()` continues without losing position. See
[Lifecycle §6](lifecycle.md#6-background-and-foreground).

## 10. Pitfalls

| Pitfall | Symptom | Fix |
|---|---|---|
| Effect in `Body()` | Repeated requests, preview crashes | Page lifecycle |
| Assigning a `State` from `Task.Run` | Visual corruption | `ui.Post(...)` |
| No cancellation when leaving the screen | Wasted work and battery, late writes | `OnDeactivated` cancels |
| Subscription never removed | Leak, calls on a destroyed page | `OnDestroy` unsubscribes |
| Result applied without a guard | Old result overwriting the new one | Version / serial / `IsActive` |
| `async void` that throws | Process crash | `Task` + `try/catch` at boundaries |
| Long work in a `Clickable` callback | Frozen UI | `Task.Run` then `Post` |
