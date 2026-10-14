# XenoUI troubleshooting by symptom

[Docs index](index.md) · [Developer guide](developer-guide.md) · [Architecture](architecture/index.md)

Commands run from your application folder (`MyApp/`). Always start with the least destructive step; only delete `bin/obj` after capturing the useful error.

## No device shows up

### The CLI sees nothing

```powershell
adb devices -l
xeno devices --json
```

- If `adb` cannot be found, set `ANDROID_SDK_ROOT`/`ANDROID_HOME` or add `platform-tools` to the `PATH`.
- If `adb` lists nothing, change cable/port, enable USB debugging and choose the data connection mode.
- If the phone is `unauthorized`, unlock it, revoke USB authorisations if needed, reconnect and accept the RSA fingerprint.
- If the emulator does not appear, let it boot completely before refreshing.
- If several `adb` installations exist, check that the IDE and the CLI use the same Android SDK.

### The CLI sees the target, Visual Studio does not

- select your XenoUI project, the `net10.0-android` TFM and a deployable configuration;
- reload the solution after installing the workload;
- check that the evaluated project exposes `AndroidApplication=true`;
- confirm that the project is ticked *Deploy* in the solution Configuration Manager;
- only close old `adb` servers as a last resort, then let the IDE restart it.

### The CLI sees the target, VS Code does not

Check that the [XenoUI extension](vscode.md) is installed and that `xenoui.cliPath` points to the same `xeno` tool as the terminal (**XenoUI: Refresh Devices** runs the detection again). Meanwhile, launch from the integrated terminal with `xeno run --project MyApp.csproj --device <id>`.

## `xeno run` says the device is offline or unauthorized

The CLI deliberately refuses a target that is not `online`.

```powershell
adb reconnect
adb devices -l
```

For `unauthorized`, the required action happens on the phone. For `offline`, wait for the boot to finish, reconnect or restart the emulator. Do not use `--no-build` to work around this state: it only concerns compilation.

## No signed APK is found

The CLI looks for the APK under `bin/<Configuration>/<TFM>/<RID>/` and prefers `<ApplicationId>-Signed.apk`.

```powershell
dotnet build MyApp.csproj `
  -f net10.0-android -c Debug `
  -p:RuntimeIdentifier=android-arm64
```

Then check:

- that the RID matches the device ABI;
- that `ApplicationId` is resolved;
- that the project is an Android application (`OutputType=Exe`, `AndroidApplication=true`);
- that `--no-build` is not used after changing RID/configuration.

## The build cannot find `XenoUI.BuildTasks.dll`

With `Xeno.SDK`, this DLL is embedded in the package under `tools/netstandard2.0`. Check the resolved version in `global.json`, then restore and rebuild:

```powershell
dotnet restore MyApp.csproj
dotnet build MyApp.csproj -f net10.0-android
```

A path into `bin/Debug` points to the old local imports from before `Xeno.SDK`: migrate the project to `<Project Sdk="Xeno.SDK">` ([Xeno.SDK](xeno-sdk.md)). Do not delete the whole NuGet cache.

## The project is not recognised as a mobile application

Check the SDK, its version and the TFM:

```xml
<Project Sdk="Xeno.SDK/{{ xeno_version }}">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

Then inspect the evaluation:

```powershell
dotnet msbuild path/to/App.csproj `
  -p:TargetFramework=net10.0-android `
  -getProperty:AndroidApplication `
  -getProperty:ApplicationId
```

## The application shows a blank screen

1. Capture `adb logcat` and look for the startup exception.
2. Check that `OnStart()` calls `Page<T>()` then `Navigate<T>()`.
3. Check that `Body()` returns a visible component with a usable size/constraint.
4. Temporarily add an opaque background to the root component.
5. Check asset names and decoding errors.
6. Reproduce with a minimal `Column(Text("OK"))` page to separate composition from content.

## Intermittent crash during Back/navigation

- every target page must have been registered once in `OnStart()`;
- do not keep an entity ID or a `XenoComponent` in a field: they belong to a transient tree;
- only keep persistent models (`State`, `ScrollState`, `TextEditController`), declared as fields with `UseState`, `UseScroll`, `UseTextEdit`;
- do not manually trigger several nested rebuilds from `Body()`;
- check asynchronous callbacks: a callback from an old tree must be cancelled or version-guarded;
- repeat the flow on a device with `adb logcat` logs.

The engine publishes the old or the new complete tree under the same lock. A crash that observes a mix usually points to an external reference to a stale entity or a late native callback.

## Scrolling never stops or goes past the content

- use a single persistent `ScrollState` per viewport;
- do not recreate the `ScrollState` in `Body()`: `UseScroll()` goes in a field initialiser; a call in `Body()` creates a new state on each rebuild;
- give the viewport a finite height/width (`Height` or `FlexGrow` in a constrained parent);
- check that the measured content is not artificially infinite;
- test content shorter than the viewport: the expected offset is `0` and the animation must stop;
- after the content shrinks, check that the rebuild triggers a new clamp.

If the problem persists, log offset, content extent, viewport, velocity and `NeedsAnimationFrame` at the moment the bound is reached.

## A `LazyColumn` uses too much memory or builds everything

Check that you use the typed overload `LazyColumn(items, item => ...)`. The old overload `LazyColumn(scrollState, count, estimatedHeight, builder)` is kept for compatibility and still materialises every item.

With the typed overload, the number of entities must stay proportional to the viewport and its overscan, not to the collection. Add `key: item => item.Id` for a source that is reordered or replaced. A builder called again when the window moves is normal; it must not trigger I/O or an external effect. Load the data beforehand and keep the builder pure.

## An image or a font does not show up

- put the file under `Assets/`;
- check that it appears as `XenoAsset` in the MSBuild evaluation;
- try the full relative path rather than the short name;
- avoid two files with the same name without extension;
- respect case, especially on macOS/Linux;
- check that the font is `.ttf` or `.otf` and that `FontFamily` matches the indexed name.

After adding an asset, rebuild: it is embedded at compile time.

## The keyboard does not open

- tap the `TextField`/`TextArea` area, not an overlay that intercepts the hit-test;
- give the field a visible height;
- check that the host registered `ITextInputCapability` before the first build;
- on Android, check `AdjustResize` and look for an exception from the hidden `EditText`;
- check that the active session was not replaced by another field;
- after a rebuild, confirm that the same `TextEditController` persists.

## Text, caret or selection drift

- do not change the text outside `TextEditController`;
- use UTF-16 offsets, not a rune count, for `TextSelection`;
- do not place a caret in the middle of a surrogate pair;
- check density, scroll transform and viewport geometry;
- reproduce with emoji, combining accents and RTL text;
- make sure layout, paragraph shape and selection come from the same snapshot.

## Undo/redo or composition misbehaves

- group related changes in `BeginBatchEdit()`;
- do not run Undo while a batch is open;
- let the IME finish/cancel its composition before an aggressive external rewrite;
- inspect `TextEditOrigin` and `Version` in the `Changed` event;
- use `SetValue`/`ApplyNativeSnapshot` according to the origin, not a parallel string mutation.

## The Back button closes the app instead of going back

- check that the stack holds more than one page (`CanGoBack`);
- do not call `GoBack()` twice for a single gesture;
- on Android 13+, confirm the callback is registered only when `CanGoBack`;
- at the root, closing/delegating to the OS is the expected behaviour.

## The iOS build fails with `CS8805`

With `Xeno.SDK` `{{ xeno_version }}`, the `OutputType=Exe` default is not applied: `Microsoft.NET.Sdk` sets `Library` before it. Android still builds an application (`AndroidApplication=true`), but on iOS the generated `Main.cs` is compiled as a library and the build fails with `CS8805`.

Workaround: declare the value in the project.

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
</PropertyGroup>
```

Fixed on the main branch on 14 October 2026, shipped with the next preview; the line can then be removed.

## The iOS build creates/modifies `Info.plist`

When the project has no `Info.plist`, the SDK target currently copies the default file into the project root because the iOS SDK does not simply accept the linked file. This write into the consuming project is known and must be replaced before V1. Add an explicit `Info.plist` if you need stable control.

## Clean-up as a last resort

After saving the logs and checking that no useful work lives in the generated folders:

```powershell
dotnet clean MyApp.csproj -f net10.0-android
```

Prefer `dotnet clean` to a broad manual deletion. If you must delete `bin/obj`, target only the project concerned.

## Information to attach to a bug report

- `Xeno.SDK` version (`global.json`);
- host OS, `dotnet --info`, `dotnet workload list`;
- device model/API/ABI or emulator image;
- exact command and the full relevant output;
- `xeno devices --json`;
- platform logs around the crash;
- minimal reproduction and frequency;
- video capture for intermittent scrolling, selection or navigation issues.
