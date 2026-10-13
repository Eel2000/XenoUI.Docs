# XenoUI developer guide

[Home](https://github.com/Eel2000/XenoUI/blob/master/README.md) · [Docs index](index.md) · [Troubleshooting](troubleshooting.md) · [Architecture](architecture/index.md)

This guide describes how to use the code in this repository. It is meant for application developers and framework contributors. New projects use `Xeno.SDK`, a NuGet SDK versioned as a local preview; it is not yet published on nuget.org. To install packages, SDK, templates, the `xeno` tool and the previewer outside the repository, follow the [installation guide](installation.md); the [SDK guide](xeno-sdk.md) details the build configuration.

## 1. Scope and maturity

XenoUI currently provides:

- a declarative C# runtime and an internal flex-like layout engine;
- SkiaSharp rendering shared by Android and iOS;
- stack navigation, with the native Android Back button;
- scrolling, images, fonts, icons, buttons and effects;
- a text editor drawn by XenoUI, connected to the native IME;
- an Android discovery/launch CLI and an experimental VS Code extension.

Android is the reference path validated in the repository. iOS requires macOS/Xcode and is not validated by CI or by the current Windows machine. `LazyColumn<T>` and `LazyRow<T>` only build the viewport and its overscan.

## 2. Prerequisites

### Common

- .NET 10 SDK (`dotnet --version`);
- Git;
- the workload matching the target;
- an optional IDE: Visual Studio or VS Code with C# Dev Kit.

```powershell
dotnet workload install android
dotnet workload install ios
dotnet workload list
```

Only install `ios` on a machine able to build iOS. Compiling and running iOS requires macOS, Xcode, compatible simulators/runtimes and, for a physical device, Apple signing configuration.

### Android

- Android SDK, platform-tools and at least one Android platform;
- `adb` reachable through `ANDROID_SDK_ROOT`, `ANDROID_HOME`, the standard user SDK location or the `PATH`;
- a running emulator or a phone with Developer options and USB debugging.

Check:

```powershell
adb devices -l
dotnet run --project XenoUI.Tooling/XenoUI.Tooling.csproj -- devices
```

An `unauthorized` phone must be unlocked; then accept the "Allow USB debugging" RSA fingerprint.

## 3. Clone, restore and validate the repository

From the root:

```powershell
./tools/Pack-XenoSdk.ps1 -SkipIos
dotnet restore XenoUI.Core/XenoUI.Core.csproj
dotnet restore XenoUI.BuildTasks/XenoUI.BuildTasks.csproj
dotnet restore samples/SampleApp/SampleApp.csproj -p:TargetFramework=net10.0-android
dotnet run --project tests/XenoUI.Core.RegressionTests/XenoUI.Core.RegressionTests.csproj
dotnet run --project tests/XenoUI.Tooling.RegressionTests/XenoUI.Tooling.RegressionTests.csproj
dotnet build samples/SampleApp/SampleApp.csproj -f net10.0-android
```

The regression suites are console executables: use `dotnet run`, not `dotnet test`.

## 4. Creating an application in the repository

The consuming project uses the `Xeno.SDK` MSBuild SDK. The local feed must be produced before the first restore. See [Xeno.SDK](xeno-sdk.md) to create a project outside the repository, configure NuGet and centralise the version.

```xml
<Project Sdk="Xeno.SDK/0.1.0-preview.5">

  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <XenoAppId>com.example.myapp</XenoAppId>
    <XenoAppName>My Xeno App</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>

</Project>
```

No relative path to the framework sources is required. In the repository, SampleApp uses an explicit source mode to develop the runtime; external projects use the NuGet packages. Overridable defaults:

| Property | Default | Role |
|---|---:|---|
| `XenoAppId` | `com.xenoui.unnamed` | Application identifier, used as `ApplicationId` |
| `XenoAppName` | project name | Display name |
| `XenoMinAndroidVersion` | `24` | Minimum Android version |
| `XenoMinIosVersion` | `13.0` | Minimum iOS version |

The SDK injects the XenoUI references, global `using`s, native entry points, assets and platform properties. You normally do not need to create `MainActivity.cs` or `Main.cs`.

### Suggested layout

```text
MyApp/
├── MyApp.csproj
├── App.cs
├── Pages/
│   └── HomePage.cs
└── Assets/
    ├── Images/
    └── Fonts/
```

### Application and first page

```csharp
public sealed class App : XenoApp
{
    public override void OnStart()
    {
        Page<HomePage>();
        Page<DetailsPage>();
        Navigate<HomePage>();
    }
}

public sealed class HomePage : XenoPage
{
    public override XenoComponent Body() =>
        Column(
            Text("Hello XenoUI", 28),
            Button("Open", () => Navigate<DetailsPage>())
        ).Padding(24).Gap(16);
}
```

`OnStart()` must navigate to an initial page. A page not registered in `OnStart()` causes an `InvalidOperationException` on `Navigate<TPage>()`.

## 5. Building and launching

### Android

List the targets:

```powershell
dotnet run --project XenoUI.Tooling/XenoUI.Tooling.csproj -- devices
dotnet run --project XenoUI.Tooling/XenoUI.Tooling.csproj -- devices --json
dotnet run --project XenoUI.Tooling/XenoUI.Tooling.csproj -- devices --watch
```

Build then install/launch:

```powershell
dotnet run --project XenoUI.Tooling/XenoUI.Tooling.csproj -- run `
  --project samples/SampleApp/SampleApp.csproj `
  --device R58X70F8NPN `
  --framework net10.0-android `
  --configuration Debug
```

The CLI picks the RID from the target ABI (`android-arm64`, `android-x64`, etc.), builds the signed debug APK, runs `adb install -r -t`, then launches the activity. `--no-build` reuses an APK already produced for the same RID.

Once the tool is packaged and installed as a .NET tool, the same commands become `xeno devices` and `xeno run ...`. That distribution is not yet published in the current state of the repository.

### iOS

On macOS:

```bash
dotnet restore samples/SampleApp/SampleApp.csproj -p:TargetFramework=net10.0-ios
dotnet build samples/SampleApp/SampleApp.csproj -f net10.0-ios
```

The `xeno` CLI has no iOS provider yet. Use the .NET/iOS tools and Xcode to select, sign and launch the target. Do not treat an iOS compilation from Windows as a runtime validation.

## 6. Composition model

A page returns a `XenoComponent`. Factories create entities in the engine; modifiers adjust their style, rendering or interaction components.

### Primitives

| Factory | Use |
|---|---|
| `Column(children)` | Vertical stacking |
| `Row(children)` | Horizontal stacking |
| `Box(width, height)` | Rectangle or container |
| `ZStack(children)` | Overlay in child order |
| `Text(value, size)` | Static or reactive text |
| `Spacer()` | Flexible space (`FlexGrow(1)`) |
| `Image(asset)` | Embedded image |
| `Icon(icon, size)` | Font glyph |
| `LazyColumn(items, item => ...)` | Virtualised, automatically measured vertical list |
| `LazyRow(items, item => ...)` | Virtualised, automatically measured horizontal list |
| `TextField(...)` | Custom-painted single-line field |
| `TextArea(...)` | Custom-painted multi-line field |

### Layout and centring

```csharp
Row(
    Box(48, 48).Background(0xFF6D28D9).CornerRadius(12),
    Column(
        Text("Title", 18),
        Text("Subtitle", 13).TextColor(0xFF6B7280)
    ),
    Spacer(),
    Text("42")
)
.Gap(12)
.Padding(16)
.CrossAxis(CrossAxisAlignment.Center)
.Background(0xFFFFFFFF);
```

Main modifiers:

- size/space: `Width`, `Height`, `Size`, `Padding`, `Margin`, `Gap`, `FlexGrow`;
- alignment: `MainAxis`, `CrossAxis`, `AlignSelf`, `CenterContent`;
- visual: `Background`, `CornerRadius`, `Elevation`, `Shadow`;
- text: `TextColor`, `FontSize`, `FontFamily`, `TextAlign`;
- interaction: `Clickable`, `Scrollable`;
- image: `ScaleMode`.

Colours are ARGB: `0xFF6D28D9` is opaque; `0x336D28D9` is translucent. Dimensions are logical, not physical pixels.

### Buttons and effects

```csharp
Button("Save", Save, fontSize: 16, textColor: 0xFFFFFFFF)
    .Background(0xFF2563EB)
    .CornerRadius(16)
    .Padding(20, 14)
    .Elevation(4)
    .FlexGrow(1);
```

The standard button gives pressed feedback by darkening. It returns a normal component: every modifier remains available.

## 7. State, updates and rebuilding

### Fine-grained text updates

```csharp
private readonly State<int> _count = UseState(0);

public override XenoComponent Body() =>
    Column(
        Text($"Total: {_count}"),
        Text(_count, 28),
        Button("+1", () => _count.Value++)
    );
```

`Text(State<T>)` and reactive interpolated strings subscribe to the state. A new value only marks the affected text for measurement/rendering; the framework does not automatically rebuild the whole tree.

### Structural change

To add/remove components or change a conditional branch, keep the state in a page field and call `Rebuild()`:

```csharp
private bool _expanded;

public override XenoComponent Body() =>
    Column(
        Button(_expanded ? "Collapse" : "Expand", () =>
        {
            _expanded = !_expanded;
            Rebuild();
        }),
        _expanded ? Text("Details") : XenoComponent.None
    );
```

A rebuild resets the transient ECS tree and calls `Body()` again under a lock. It does not diff the old and new trees. Persistent objects (`State`, `ScrollState`, `TextEditController`) must be page fields, never variables recreated in `Body()`.

> In `SampleApp`, a local `UseState` is used for demonstration purposes. For an unambiguous identity and lifetime across rebuilds, prefer a field.

### Declaring without `new`

The DSL never requires `new`: every persistent object or option has its factory, imported by `using static` with the SDK.

| Need | Factory | Type created |
|---|---|---|
| Reactive state | `UseState(value)`, `UseStates(n, value)` | `State<T>`, `State<T>[]` |
| Scroll | `UseScroll()` | `ScrollState` |
| Lazy list / pager | `UseLazyList()` | `LazyListState` |
| Text field | `UseTextEdit("initial")` | `TextEditController` |
| Keyboard/IME | `InputOptions(...)` | `TextInputConfiguration` |
| Selection | `Selection(a, b)`, `Caret(i)` | `TextSelection` |
| Icon | `Glyph(glyph, font)` | `IconDef` |
| Bar action | `NavigationAction.For(icon, tap) with { ... }` | `NavigationAction` |
| Bar style | `DockedNavigationBarStyle.Default with { ... }` | `DockedNavigationBarStyle` |
| Media player | `Native.CreatePlayer(MediaKind.Video)`, `Configure(loop: true)` | `MediaController` |

A `Use…` factory creates a new object on each call: call it in a field initialiser, never in `Body()`. Only `NavigationScaffoldState`, configured once in the `App`, keeps an object initialiser.

## 8. Navigation and native Back

Pages are instantiated only once, in `Page<T>()`, then reused. Their fields therefore keep their value between navigations.

```csharp
public override void OnStart()
{
    Page<HomePage>();
    Page<SettingsPage>();
    Navigate<HomePage>();
}
```

In a page:

```csharp
Button("Settings", () => Navigate<SettingsPage>());
Button("Back", GoBack);
```

On Android 7–12, the host intercepts `OnBackPressed`. From Android 13, it registers an `OnBackInvokedDispatcher` callback only when `CanGoBack` is true. `TryGoBack()` pops a page; at the root, the OS resumes its normal behaviour.

On iOS, use a visible `GoBack()` action for now. Integration of the native interactive gesture is not yet equivalent.

`Navigate<TPage>(transition, popTransition)` sets the forward and back transitions separately, for example `Navigate<DetailsPage>(NavTransitions.SlideVertical(320), NavTransitions.Fade(220))`; `NavigationOptions` is still accepted. For adaptive main navigation with one stack per destination, BottomBar, Rail, permanent Drawer and FAB, see [Navigation and NavigationScaffold](navigation-scaffold.md).

## 9. Scrolling and lists

### Scrollable container

```csharp
private readonly ScrollState _scroll = UseScroll();

public override XenoComponent Body() =>
    Column(
        Text("Section 1"),
        Text("Section 2"),
        Text("Section 3")
    )
    .CrossAxis(CrossAxisAlignment.Stretch)
    .Scrollable(_scroll);
```

The engine bounds the offset to the content, stops the motion when the content fits in the viewport, applies inertia/bounce and clips rendering and hit-testing. If the content shrinks, the offset is clamped again.

### List

```csharp
private readonly LazyListState _peopleState = UseLazyList(); // optional

public override XenoComponent Body() =>
    LazyColumn(_people, person =>
        Row(
            Text(person.Name),
            Spacer(),
            Text(person.Status)
        ).Padding(16),
        key: person => person.Id,
        state: _peopleState
    ).FlexGrow(1);
```

Providing the items is enough. The builder is optional (`ToString()` is then displayed), as are `key` and `state`. The engine builds a small window around the viewport, measures each item it meets and refines its extent index. Items can therefore have different heights or widths without a default value imposed by the API.

Use a stable key when the collection can be reordered or replaced: known measurements then follow the item's logical identity. Only keep a `LazyListState` explicitly to observe scrolling or to share its identity outside the composition; otherwise XenoUI retains one automatically per call site, with no offset leaking between two pages or two lists. The old overloads `LazyColumn(ScrollState, count, estimatedHeight, builder)` and `LazyRow(...)` remain compatible, but materialise every item and are reserved for migration.

## 10. Assets, images, icons and fonts

Every file under `Assets/**/*` becomes an embedded resource with the logical prefix `XenoAsset/`.

```text
Assets/
├── Images/avatar.png
└── Fonts/Inter-Regular.ttf
```

```csharp
Image("avatar")
    .Size(96, 96)
    .ScaleMode(ImageScaleMode.Cover)
    .CornerRadius(48);

Text("Hello", 24).FontFamily("Inter-Regular");
```

Image modes: `Cover`, `Fit`, `Fill`, `None`. The image uses its natural size if no size is imposed. `.ttf` and `.otf` files are loaded and cached by the runtime.

The short name without extension is convenient, but two assets with the same name can collide; the first one found wins. Use an explicit relative path if needed.

`Icon(IconDef)` uses the standard library definitions. `Icon(glyph, fontName, size)` also accepts an embedded icon font.

## 11. Text input and IME

XenoUI follows a hybrid architecture close to Flutter's: the operating system stays the owner of the input session (IME, language, composition, dictation, autocorrection, emoji), while XenoUI draws the text, caret, selection and background. The native control is hidden and synchronises a complete snapshot.

### Single-line field

```csharp
private readonly TextEditController _email = UseTextEdit();

TextField(
    _email,
    "name@example.com",
    InputOptions(
        NativeKeyboardType.Email,
        NativeInputAction.Next,
        autoCorrect: false,
        autofill: NativeAutofillHint.Email,
        label: "Email address"),
    onAction: action => HandleAction(action)
).Height(54);
```

### Multi-line field

```csharp
private readonly TextEditController _note = UseTextEdit("Initial text");

TextArea(
    _note,
    "Write a note…",
    InputOptions(
        NativeKeyboardType.Multiline,
        NativeInputAction.None,
        label: "Note")
).Height(160);
```

### Customisable password

```csharp
private readonly TextEditController _password = UseTextEdit();
private bool _reveal;

Column(
    TextField(
        _password,
        "Password",
        InputOptions(
            NativeKeyboardType.Password,
            secure: true,
            autofill: NativeAutofillHint.Password),
        obscureText: !_reveal),
    Button(_reveal ? "Hide" : "Show", () =>
    {
        _reveal = !_reveal;
        Rebuild();
    })
);
```

`secure` configures the native semantics; `obscureText` separately decides masked rendering. This separation allows a show/hide button without giving up the native IME.

### Controller and editing

`TextEditController.Value` holds the text, the selection and the composing range. Indexes are UTF-16 offsets. Useful APIs:

- `SetText` (caret at the end), `SelectAll`, `SetValue`, `SetSelection`, `ReplaceSelection`;
- `CommitText`, `SetComposingText`, `FinishComposition`;
- `DeleteBackward`, `DeleteSurroundingText`, `DeleteSurroundingCodePoints`;
- `GetTextBeforeSelection`, `GetTextAfterSelection`, `GetSelectedText`;
- `Undo`, `Redo`, `CanUndo`, `CanRedo`, `BeginBatchEdit`;
- `Changed` event with origin and version.

```csharp
_note.Changed += (_, change) =>
    Console.WriteLine($"v{change.Version}: {change.Value.Text}");

_note.Undo();
_note.Redo();
_note.SelectAll();
_note.SetSelection(Caret(0));
```

The engine handles composition, surrogate pairs, deletion by code point, emoji/grapheme clusters, word selection, handles, the context toolbar and internal multi-line scrolling. Secure fields do not expose Copy/Cut.

### Text limits

- internal style is still barely configurable: size, padding and base colours are partly fixed;
- the global accessibility tree is incomplete despite the hidden native editor;
- hardware keyboards, dictation and every language still need to be covered by a device test matrix;
- caret/selection geometry is only valid because of the atomic layout-paragraph snapshot; do not bypass the controller.

## 12. NativeTunnel

`XenoApp.Native` exposes a typed registry of capabilities provided by the host (`Get<T>()` for an optional capability, `Require<T>()` for a mandatory need).

The current registry mainly contains `ITextInputCapability` and `IClipboardCapability`, consumed by the runtime. Note: `XenoPage.App` is still internal; an application page therefore has no clean public access to the tunnel today. Exposing/injecting capabilities to pages is part of the public contract to finalise before V1. Do not work around this limit with a platform singleton.

For a future feature (battery, notification, audio, calls, specialised networking), define an `INativeCapability` contract in the core, implement it in each host, then register it before the first build. Do not introduce Android/UIKit types into `XenoUI.Core`.

## 13. Visual Studio

1. Open `XenoUI.slnx`.
2. Let the workload/project restore finish.
3. Set `SampleApp` as the startup project.
4. Select the Android TFM and a standard Visual Studio Android target.
5. Build/deploy.

The SDK exposes the `XenoUI`, `Android` and `AndroidApplication` capabilities needed for project evaluation. The solution's `<Deploy />` entry allows `SampleApp` to be deployed.

If no target shows up, first validate `adb devices -l`, then `xeno devices`. If the CLI sees the phone but Visual Studio does not, reload the solution after choosing the Android TFM and check the Visual Studio Android workload installation.

## 14. VS Code

The experimental extension lives in `tools/vscode-xenoui`. It detects a workspace where a `.csproj` targets Android and imports XenoUI. It provides:

- `XenoUI: Refresh Devices`;
- `XenoUI: Select Device`;
- `XenoUI: Run on Selected Device`;
- a target indicator in the status bar.

It refreshes the devices periodically and calls the CLI. In this repository, it uses the `XenoUI.Tooling` project; in a consumer, it expects `xeno` on the `PATH` or an `xenoui.cliPath` setting.

To install its dependencies and produce the VSIX:

```powershell
Set-Location tools/vscode-xenoui
npm install
npm run package
```

`npm test` checks the discovery of `Xeno.SDK` projects and compatibility with the old imports. Validation of the Extension Host, devices and debugger remains separate and necessary before a Marketplace release.

## 15. Debugging

### Detailed build

```powershell
dotnet build samples/SampleApp/SampleApp.csproj `
  -f net10.0-android -v:diagnostic
```

### Android logs

```powershell
adb logcat
adb shell pidof com.xenoui.sample
adb logcat --pid=<pid>
```

### MSBuild inspection

```powershell
dotnet msbuild samples/SampleApp/SampleApp.csproj `
  -p:TargetFramework=net10.0-android `
  -getProperty:ApplicationId
```

Investigate a blank screen or a navigation crash in this order: exception in `Body()`, unregistered page, persistent object created in `Body()`, access to an entity from a previous tree, then layout/render error. See [troubleshooting](troubleshooting.md).

## 16. Tests and validation strategy

Before any runtime contribution:

```powershell
dotnet run --project tests/XenoUI.Core.RegressionTests/XenoUI.Core.RegressionTests.csproj
dotnet run --project tests/XenoUI.Tooling.RegressionTests/XenoUI.Tooling.RegressionTests.csproj
dotnet build samples/SampleApp/SampleApp.csproj -f net10.0-android
```

Then validate the affected flows on a device or emulator:

- startup and back at the root;
- repeated forward/back navigation;
- scrolling to both bounds, short content and shrinking content;
- rotation/resizing where relevant;
- input with accents/emoji/CJK/RTL, composition, selection, undo/redo;
- keyboard shown/hidden and switching fields;
- rebuild during a text session.

An Android change must at least keep iOS compiling; a native contract change must get two implementations or explicitly declare the unsupported target.

## 17. Contributing to the framework

1. Read the [architecture module](architecture/index.md).
2. Identify the right boundary: Core, StandardLibrary, platform host, BuildTasks, Tooling or IDE.
3. Add a reproducible regression first.
4. Keep native types out of `XenoUI.Core`.
5. Never store a reference to a slab entry beyond a mutation/reset.
6. Ensure snapshot/layout/render atomicity and ignore callbacks from an old tree version.
7. Update the canonical documentation and the debt section if the contract changes.

High-risk changes are: native allocation/release, rebuilding, text/IME, UTF-16 offset computation, animated scrolling, host lifecycle and MSBuild generation.

## 18. Versioning, releases and compatibility

The repository currently contains no stable release tag, no consumable SDK package and no formal cross-platform CI matrix. Until V1:

- treat the Git commit as the only reproducible version;
- do not promise source or binary compatibility between commits;
- pin SkiaSharp/RichTextKit versions through the projects;
- document every breaking change in the pull request and in `decisions-v1.md`;
- validate Android on at least one arm64 target and one x64 emulator when the change touches packaging;
- validate iOS on macOS before announcing iOS compatibility.

A credible V1 release requires at least: versioned packages, an installable SDK, templates, a changelog, a reviewed public API, Android/iOS CI tests, an OS/.NET support policy and a reproducible publishing procedure.

## 19. Further reading

- [Troubleshooting by symptom](troubleshooting.md)
- [Runtime architecture](architecture/runtime.md)
- [Text and NativeTunnel architecture](architecture/text-input-and-native.md)
- [SDK, platforms and tooling architecture](architecture/tooling-and-platforms.md)
- [Decisions and the V1 trajectory](architecture/decisions-v1.md)
