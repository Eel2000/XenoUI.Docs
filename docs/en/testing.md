# Testing a XenoUI application

[Documentation](index.md) · [Application architecture](app-architecture.md) · [Creating a component](components.md) · [Visual Studio](visual-studio.md) · [Troubleshooting](troubleshooting.md)

This guide presents what XenoUI lets you test **today**, at which level, and how. There is no equivalent of
`createComposeRule()` (yet): no on-device interaction harness and no queryable semantics tree. What exists is solid
and enough for a good test pyramid: unit tests on the portable layers, **headless PNG rendering** for components and
pages, and a manual on-device routine. For the checklist of flows to validate by hand, see also
[Developer guide §16](developer-guide.md#16-validating-your-application).

## Contents

1. [The test pyramid](#1-the-test-pyramid)
2. [Testing services and models](#2-testing-services-and-models)
3. [Rendering a component to an image](#3-rendering-a-component-to-an-image)
4. [Golden tests (image comparison)](#4-golden-tests-image-comparison)
5. [Checking pixels and sizes](#5-checking-pixels-and-sizes)
6. [Testing pages](#6-testing-pages)
7. [Previews as visual tests](#7-previews-as-visual-tests)
8. [On-device tests](#8-on-device-tests)
9. [How the framework tests itself](#9-how-the-framework-tests-itself)
10. [Continuous integration](#10-continuous-integration)
11. [Current limits](#11-current-limits)

---

## 1. The test pyramid

| Level | What | Tool | Speed |
|---|---|---|---|
| 1 — Unit | Services, presentation models, pure functions | Your test framework (xUnit, NUnit, MSTest) | Very fast |
| 2 — Headless rendering | A component or page rendered to PNG; pixels, sizes | `PreviewRenderer` | Fast, no device |
| 3 — Device / emulator | Navigation, keyboard, media, background | Manual, `xeno` tool | Slow |

The architectural choice that pays off most ([Application architecture](app-architecture.md)): services and models
**with no XenoUI type**, hence testable at level 1 by an ordinary `net11.0` project.

## 2. Testing services and models

Services and models receive their dependencies (HTTP, dispatcher): replace them with doubles.

```csharp
public sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        Task.FromResult(respond(request));
}

[Fact]
public async Task Articles_are_parsed_and_validated()
{
    var json = """[{"userId":1,"id":1,"title":"t","body":"b"}]""";
    var client = new HttpClient(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(json) }));
    var service = new ArticleService(client);

    var articles = await service.GetArticlesAsync(CancellationToken.None);

    Assert.Single(articles);
}

[Fact]
public async Task Stale_results_never_replace_the_latest()
{
    // The test dispatcher runs everything right away: `post = a => a()`.
    var feed = new RemoteFeed<int>(ct => Task.FromResult(new[] { 1 }), post: a => a(), changed: () => { });
    feed.Activate();
    feed.Refresh();
    /* wait for publication, then assert on feed.Snapshot */
}
```

To cover for a remote feed (the repository does so in `tests/SampleApp.Api.RegressionTests`): empty response, HTTP
error, invalid JSON, timeout, cancellation, out-of-order results, rapid `Refresh`, `Dispose`, duplicate identifiers.

## 3. Rendering a component to an image

`PreviewRenderer` — the one that powers the Visual Studio and VS Code previews — is **public** and usable in a
test: it composes a UI in a private engine, with no window or device, and returns a PNG.

```csharp
using XenoUI.Core.Preview;

PreviewImage image = PreviewRenderer.Render(
    () => Chip.View("Swift"),                            // Func<XenoComponent>
    new PreviewAttribute { Device = "android" },         // optional: device, width, height, RTL
    assets: typeof(App).Assembly,                        // optional: assembly holding Assets/
    scale: 2f);                                          // pixel factor

Assert.True(image.Succeeded, image.Error?.Message);
File.WriteAllBytes("chip.png", image.Png!);
```

`PreviewImage` provides: `Png` (bytes), `PixelWidth`/`PixelHeight`, `Width`/`Height` (dp), `Elapsed`, `Error`, `Hint`
(advice when a page typically fails) and `Succeeded`.

| `PreviewAttribute` | Effect |
|---|---|
| `Device` | `android` (360×800), `pixel` (412×915), `iphone` (393×852), `iphone-se` (375×667), `foldable` (673×841), `tablet` (800×1280) |
| `Width`, `Height` | Force a size in dp (0 = the device's value; for a component the height wraps the content) |
| `RightToLeft` | Right-to-left layout |

A component is rendered **at the device width, at its content's height**. Fonts and images are loaded from the
assembly passed as `assets`.

## 4. Golden tests (image comparison)

A golden test compares the current rendering to a versioned reference image.

```csharp
static void AssertGolden(string name, Func<XenoComponent> content, PreviewAttribute? spec = null)
{
    var image = PreviewRenderer.Render(content, spec, scale: 1f);
    Assert.True(image.Succeeded, image.Error?.Message);

    string path = Path.Combine("Goldens", name + ".png");
    if (!File.Exists(path) || Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "1")
    {
        Directory.CreateDirectory("Goldens");
        File.WriteAllBytes(path, image.Png!);          // first run or deliberate update
        return;
    }
    using var expected = SKBitmap.Decode(path);
    using var actual = SKBitmap.Decode(image.Png);
    Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
    int differing = 0;
    for (int y = 0; y < actual.Height; y++)
        for (int x = 0; x < actual.Width; x++)
            if (expected.GetPixel(x, y) != actual.GetPixel(x, y)) differing++;
    Assert.True(differing <= actual.Width * actual.Height / 1000, $"{differing} pixels differ in {name}");
}

[Fact] public void Chip_default()   => AssertGolden("chip", () => Chip.View("Swift"));
[Fact] public void Chip_rtl()       => AssertGolden("chip-rtl", () => Chip.View("Swift"), new() { RightToLeft = true });
[Fact] public void Chip_tablet()    => AssertGolden("chip-tablet", () => Chip.View("Swift"), new() { Device = "tablet" });
```

Tips:

- **One golden per variant**: default, long text, RTL, large screen, error state.
- Pin the scale (`scale: 1f`) and fonts: pixel comparison depends on text rendering; tolerate a small margin (0.1 %
  here) and **generate goldens on the same platform as CI**.
- Version the goldens and show the diff in CI when a test fails.
- Do not compare components that contain running animations, dates or random data.

## 5. Checking pixels and sizes

Without a reference image you can assert on the rendering:

```csharp
var image = PreviewRenderer.Render(() => Badge.View("3", 0xFFDC2626), scale: 1f);
using var bitmap = SKBitmap.Decode(image.Png);

Assert.Equal(new SKColor(0xFFDC2626), bitmap.GetPixel(12, 8));         // colour at the badge's centre
Assert.InRange(image.Height, 20, 40);                                    // the height wraps the content
Assert.Equal(360f, image.Width);                                         // the android device's width (dp)
```

Useful for component contracts: "the badge is red", "long text is truncated to two lines (bounded height)", "the
button is at least 44 dp".

## 6. Testing pages

A page annotated `[Preview]` with a parameterless constructor is rendered **attached to its application,
constructed but not started** (`IsPreview`): `OnStart` does not run, native services are design stand-ins.

```csharp
var entry = PreviewRenderer.Discover(typeof(SettingsPage)).Single();
var image = PreviewRenderer.Render(entry, scale: 1);
Assert.True(image.Succeeded, image.Error?.Message);
```

`PreviewRenderer.Discover(typeof(T))` lists a type's `[Preview]`s (page or component class); a failed `Render`
becomes a **result** (`Error`, `Hint`), not an exception: perfect for a test that checks **all of the project's
previews render**.

```csharp
[Fact]
public void Every_preview_renders()
{
    foreach (var type in typeof(App).Assembly.GetTypes())
        foreach (var entry in PreviewRenderer.Discover(type))
        {
            var image = PreviewRenderer.Render(entry, typeof(App).Assembly, scale: 1);
            Assert.True(image.Succeeded, $"{entry.DisplayName}: {image.Error?.Message} {image.Hint}");
        }
}
```

A page that requires `OnStart`, the network or missing native services will fail with a `Hint`: design pages so
they render with sample data (the `App`'s `OnPreview()` prepares the models the pages read).

## 7. Previews as visual tests

`[Preview]`s serve both as a design tool and as a **catalogue of visual tests**: one preview per variant (empty,
full, error, RTL, tablet). The "all previews render" test above turns this catalogue into a compile-and-render
safety net. See [Creating a component §12](components.md#12-previewing-preview).

## 8. On-device tests

What cannot be tested headless: real keyboard and IME, system back gesture, audio/video players, background and
resume, GPU context loss, performance. Procedure:

1. `xeno devices` then `xeno run --project … --device <id>` ([Installation](installation.md));
2. follow a written **scenario list**: open each lab, switch tabs and come back (state kept), press Home and come
   back (players suspended/resumed), rotate, cut the network (error states);
3. note the device, Android version and mode (Debug/Release);
4. for performance, measure in **Release, without a debugger** ([Performance](performance.md)).

An automated on-device UI test (UI Automator, Appium) can drive the application like any native application;
XenoUI draws its own pixels: **elements are not native views** (only text fields carry an accessibility label), so
target by coordinates or screenshots.

## 9. How the framework tests itself

Useful as a model for your own tests:

- `tests/XenoUI.Core.RegressionTests`: an executable suite (`dotnet run -c Release`, not `dotnet test`) with a
  `(name, action)` table and a `Check(condition, message)` function. It composes in a `XenoEngine`, calls
  `Refresh(engine, width, height)`, then reads entity positions (`engine.XenoUICacheMemory.Transforms`). It
  covers layout (RTL, flex), navigation, overlays, text, GPU recovery, **zero allocation on stable frames**, and
  previews. See also [Runtime §12](architecture/runtime.md#12-expected-tests).
- `tests/SampleApp.Api.RegressionTests`: services and remote feeds, with no platform project.
- `tests/SampleApp.Overlays.RegressionTests`: the overlay layer.
- `tests/XenoUI.Media.RegressionTests`, `Xeno.SDK.RegressionTests`, `XenoUI.Tooling.RegressionTests`: media, the
  MSBuild SDK, the `xeno` tool.

These suites access internal types (`InternalsVisibleTo`); **a client application must stick to the public API**
(`PreviewRenderer`, your own services).

## 10. Continuous integration

A typical CI job for an application:

```yaml
- run: dotnet test MyApp.Tests/MyApp.Tests.csproj -c Release     # levels 1 and 2 (net11.0, no mobile workload)
- run: dotnet build MyApp/MyApp.csproj -f net11.0-android -c Release   # compiles the app (Android workload required)
```

Level 1 and 2 tests need **neither a device nor a mobile workload** if they target `net11.0` and reference the
`XenoUI.Core` libraries / your component library. Generate goldens on the CI's OS and publish failing images as
artifacts.

## 11. Current limits

- No interaction harness (simulated click, typing, assertion on a semantics tree) provided for a client
  application: interaction tests go through the device.
- Elements drawn by XenoUI are not queryable native views (apart from text fields' labels); no `testTag`
  equivalent.
- Image comparisons depend on the platform's font rendering.
- The framework is pre-V1: treat `PreviewRenderer` as tooling rather than a frozen API.
