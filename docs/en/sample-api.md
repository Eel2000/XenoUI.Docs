# SampleApp — portable API calls

[Documentation](index.md) · [Developer guide](developer-guide.md) · [Xeno.SDK](xeno-sdk.md)

Select **Live API** in the tab bar, the rail or the drawer (hamburger menu), then tap an article to open its detail. The **Live API · load cloud articles** shortcut on Home selects the same destination without pushing a new page. The data comes from [JSONPlaceholder](https://jsonplaceholder.typicode.com/), a free demo API with no key. The data is fake; server-side writes are simulated. This demo uses `GET /posts` and `GET /posts/{id}/comments` over HTTPS.

## Boundaries

- `Services/ArticleService.cs`: injectable contract, reused `HttpClient`, HTTP checks, source-generated JSON to avoid run-time reflection. No XenoUI, Android or iOS types.
- `Services/ArticleFeed.cs`: `RemoteFeed<T>` shared by articles/comments, idle/loading/ready/error states, in-memory retention, refresh, cancellation and a request number that rejects stale results. No platform or ECS types.
- `Pages/ApiPage.cs`: XenoUI presentation, virtualised list with stable keys, Refresh/Retry and lifecycle.
- `ArticleDetailsModel`: selection owned by the application, no singleton; changing article invalidates the old comments.
- `ArticleDetailsArgs`: typed payload passed by `Navigate<ArticleDetailsPage, ArticleDetailsArgs>()`, kept by the navigation entry. The model receives the selection in `OnNavigatedTo`; see [constrained text and typed navigation](text-layout-navigation.md).
- `ArticleDetailsPage`: full title and body, article/author identifiers, comments (name, email, text), Refresh/Retry and Back. The article text comes from the list already loaded; only its comments trigger an extra request.
- `IUiDispatcherCapability`: the NativeTunnel only posts state publication and the rebuild to the UI thread. Android/iOS provide the adapter; they do not carry the HTTP client or the business models.

Networking and deserialisation run neither in `Body()` nor in frames. The tasks, delegates, strings and DTOs allocated for a request belong to the cold path. The zero-allocation requirement per settled frame does not mean zero allocation during a download or a composition.

## Behaviour

- Loading on first display; coming back with a result already loaded reuses memory.
- Refresh cancels the previous request; old results never replace the newest one.
- Previous data stays on screen during a refresh or after an error.
- Empty response, timeout, HTTP error and invalid JSON are distinguished in the UI.
- Leaving the page, going to the background and destruction cancel operations; callbacks already queued are invalidated.
- The 20-second timeout also covers reading the response body.
- Android requires `INTERNET`, already declared in SampleApp's manifest. No secret/API key is embedded.
- List → detail uses the existing `Navigate<T>()` and a horizontal slide; Back returns to the list without losing its data or its scroll.
- The detail keeps its scroll for the same article and resets it to the top for another selection. The comment cache only concerns the last selection; no old comment is shown for a new article.

No SQLite, disk cache, persistent favourites, sync or offline guarantee at this stage. Restarting the process loses the data.

## Tests

The portable tests compile the services for `net10.0`, without a platform project. They cover HTTP, JSON, cancellation, deferred publication, out-of-order results, error/retry, empty response, selection and disposal. Comments from another article and duplicate IDs are rejected. The live test reads the articles then the comments of the first article. It does not prove SampleApp runs on a phone: separately check loading, airplane mode, Retry, quick Refresh, list → detail → Back, changing article while loading and background/foreground on a device. Full iOS validation requires macOS/Xcode.
