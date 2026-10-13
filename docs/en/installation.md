# Installing XenoUI: packages, SDK, the `xeno` tool and the previewer

[Documentation](index.md) · [Xeno.SDK](xeno-sdk.md) · [Visual Studio](visual-studio.md) · [Developer guide](developer-guide.md) · [Troubleshooting](troubleshooting.md)

This guide installs everything needed to create, build and preview a XenoUI application **outside the repository**. To contribute to the framework itself, see the [developer guide](developer-guide.md#3-clone-restore-and-validate-the-repository).

> **Status: `0.1.0-preview.5`, local preview.** The packages are not yet published on nuget.org, nor is the extension on the Visual Studio Marketplace. Today they are produced from a clone of the repository (§ 3 B). The "nuget.org" commands (§ 3 A) will apply unchanged once published.

## 1. What gets installed, and how

| Item | Role | Installation |
|---|---|---|
| `Xeno.SDK` | The application's MSBuild SDK: runtimes, entry points, assets | **Nothing to do**: restored by NuGet from `global.json` |
| `XenoUI.Core`, `XenoUI.StandardLibrary`, `XenoUI.Platforms`, `XenoUI.Media*` | Runtime and Android/iOS hosts | **Nothing to do**: referenced by `Xeno.SDK` |
| `XenoUI.Templates` | "XenoUI App / Page / Component" templates (CLI and Visual Studio) | `dotnet new install` (§ 4) |
| `XenoUI.Tooling` | The `xeno` tool: Android devices, build, install, launch | `dotnet tool install` (§ 6) |
| **XenoUI Preview** extension | `[Preview]` previews in Visual Studio | `.vsix` file (§ 7) |

Runtimes are never added as `PackageReference`: `Xeno.SDK` picks the right packages and the platform host (`XenoUI.Platforms`) from `TargetFramework`.

## 2. Prerequisites

| For… | You need |
|---|---|
| Everything | .NET 10 SDK (`dotnet --version` ≥ 10.0) |
| Android | `dotnet workload install android`, an Android SDK with platform-tools, an emulator or a phone with USB debugging |
| iOS | `dotnet workload install ios`; building, signing and launching an app requires **macOS + Xcode** |
| Previewer | Visual Studio 2022 17.14+ or Visual Studio 2026 (Windows x64/arm64); the .NET 10 runtime (shipped with the .NET 10 SDK) |

```powershell
dotnet --version
dotnet workload install android
dotnet workload list
```

## 3. Getting the packages

### A. From nuget.org (once published)

No configuration: the commands in the following sections find the packages on nuget.org. Simply omit `--feed`, `--add-source` and step B.

### B. From a clone of the repository (today)

```powershell
git clone https://github.com/Eel2000/XenoUI.git
cd XenoUI
./tools/Pack-XenoSdk.ps1 -IncludeMedia -IncludeVisualStudio
```

| Option | Effect |
|---|---|
| `-IncludeMedia` | Adds `XenoUI.Media` and `XenoUI.Media.Android` (`<XenoEnableMedia>true</XenoEnableMedia>`) |
| `-IncludeVisualStudio` | Also builds the Visual Studio extension |
| `-SkipIos` | Machine without the iOS workload: `XenoUI.Platforms` then only contains the Android target |

Result:

- **local feed** `artifacts/packages/`: `Xeno.SDK`, `XenoUI.Core`, `XenoUI.StandardLibrary`, `XenoUI.Platforms`, `XenoUI.Media*`, `XenoUI.Tooling`, `XenoUI.Templates`;
- **extension** `artifacts/visualstudio/XenoUI.VisualStudio.0.1.0-preview.5.vsix`.

Below, `<feed>` stands for the absolute path of `artifacts/packages`, for example `D:/src/XenoUI/artifacts/packages`.

## 4. Installing the templates

```powershell
# nuget.org
dotnet new install XenoUI.Templates::0.1.0-preview.5
# local feed
dotnet new install <feed>/XenoUI.Templates.0.1.0-preview.5.nupkg
```

Check: `dotnet new list xenoui` shows `xenoui`, `xenoui-page` and `xenoui-component`. Visual Studio shows them in *Create a new project* and *Add › New Item* after a restart.

Update: install the new version (`dotnet new install ...` replaces the old one). Uninstall: `dotnet new uninstall XenoUI.Templates`.

## 5. Creating an application and using the SDK

### With the template

```powershell
# nuget.org
dotnet new xenoui -n MyApp
# local feed: generates a NuGet.config pointing at the feed
dotnet new xenoui -n MyApp --feed <feed>

cd MyApp
dotnet build -f net10.0-android
```

In Visual Studio: *Create a new project* › "XenoUI App"; the same options appear in the wizard. `--ios` adds `net10.0-ios`, `--appId com.example.myapp` sets the identifier. See [Visual Studio](visual-studio.md#creating-a-project).

### What the template sets up

```text
MyApp/
├─ MyApp.csproj        <Project Sdk="Xeno.SDK">, TargetFramework, XenoAppId…
├─ global.json         { "msbuild-sdks": { "Xeno.SDK": "0.1.0-preview.5" } }
├─ NuGet.config        (only with --feed) "xeno-local" source
├─ App.cs
├─ Pages/HomePage.cs   with [Preview]
└─ Components/Counter.cs
```

The **SDK installs itself**: on the first `restore`/`build`, MSBuild reads the version in `global.json` and NuGet downloads `Xeno.SDK`, which in turn references the runtimes of the same version.

### Without the template (existing project)

1. Add `global.json` at the root of the solution:

   ```json
   { "msbuild-sdks": { "Xeno.SDK": "0.1.0-preview.5" } }
   ```

2. Replace the project header with `<Project Sdk="Xeno.SDK">` and set `TargetFramework` (`net10.0-android`, `net10.0-ios` or both).
3. For the local feed, add a `NuGet.config`:

   ```xml
   <configuration>
     <packageSources>
       <add key="xeno-local" value="D:/src/XenoUI/artifacts/packages" />
     </packageSources>
   </configuration>
   ```

   The inherited nuget.org source is still needed for SkiaSharp and the other dependencies; do not add `<clear />` without declaring it again.

All properties (`XenoAppId`, `XenoAppName`, OS minimums, permissions, assets, media) are described in [Xeno.SDK](xeno-sdk.md).

### Changing version

Change the version in `global.json`, then run `dotnet restore`. A version already restored is never downloaded again: NuGet caches are immutable per version. A fixed package must therefore carry a **new** number; do not delete the global cache to "force" an update.

## 6. Installing the `xeno` tool

```powershell
# nuget.org
dotnet tool install --global XenoUI.Tooling --version 0.1.0-preview.5
# local feed
dotnet tool install --global XenoUI.Tooling --version 0.1.0-preview.5 --add-source <feed>
```

```powershell
xeno devices
xeno run --project MyApp/MyApp.csproj --device <device-id>
```

`xeno devices` lists the Android devices seen by `adb` (state, API, ABI). Update: `dotnet tool update --global XenoUI.Tooling --version <version> [--add-source <feed>]`. Uninstall: `dotnet tool uninstall --global XenoUI.Tooling`. For a per-repository tool rather than a global one, use `dotnet new tool-manifest` then `dotnet tool install XenoUI.Tooling` without `--global`.

## 7. Installing the previewer (Visual Studio extension)

1. Close Visual Studio.
2. Double-click `XenoUI.VisualStudio.0.1.0-preview.5.vsix` and confirm the installation, or from the command line:

   ```powershell
   & "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" artifacts\visualstudio\XenoUI.VisualStudio.0.1.0-preview.5.vsix
   ```

   (Adjust `18\Community` to your edition: `18\Professional`, `2022\Enterprise`, etc.)
3. Open Visual Studio: *Extensions › Manage Extensions › Installed* must list **XenoUI Preview**.
4. Open a C# file of a `Xeno.SDK` project, then *View › Other Windows › XenoUI Preview* (**Ctrl+Alt+P**). With the template, `Pages/HomePage.cs` shows two previews.

The extension neither builds nor launches the project: it starts its own render server (`dotnet XenoUI.PreviewServer.dll`), isolated from the IDE, the first time the window opens. How it works, the `[Preview]` attribute and limits: [Visual Studio](visual-studio.md).

Update: install the new `.vsix` (it replaces the old one). Uninstall: *Extensions › Manage Extensions › Installed › XenoUI Preview › Uninstall*, then restart Visual Studio.

## 8. Checking the whole setup

```powershell
dotnet new xenoui -n Check --feed <feed>   # without --feed with nuget.org
cd Check
dotnet build -f net10.0-android            # SDK + runtimes restored, APK compiled
xeno devices                               # tool installed
```

Then open `Check/Pages/HomePage.cs` in Visual Studio with the XenoUI Preview window: the page appears, and updates when you edit a text.

## 9. Common problems

| Symptom | Cause and fix |
|---|---|
| `Could not resolve SDK "Xeno.SDK"` | Version missing from the feed: check `global.json`, the `NuGet.config` (`--feed`) and that `Pack-XenoSdk.ps1` was run with that version |
| `NETSDK1147`: missing workload | `dotnet workload install android` (or `ios`) |
| `NU1101 XenoUI.Platforms` | Local feed produced without this package or an old version: run `Pack-XenoSdk.ps1` again, then `dotnet restore` |
| Change not picked up after a new pack | Same version number already cached: bump the version (`-Version 0.1.0-preview.5`) and update `global.json` |
| `dotnet new xenoui` unknown | Templates not installed (§ 4) or another hive: `dotnet new list xenoui` |
| `xeno` not found | Open a new terminal (global tools PATH: `%USERPROFILE%\.dotnet\tools`) |
| XenoUI Preview window: "This file does not belong to a Xeno.SDK project" | The nearest `.csproj` does not use `Sdk="Xeno.SDK"` |
| XenoUI Preview window: "The preview server stopped" | .NET 10 runtime missing from the `dotnet` on the PATH, or a preview blocked for more than 10 s; the server restarts on the next change |
| XenoUI Preview window: "… is not restored … embedded runtime" | The project was never restored: run a build or `dotnet restore` so the preview uses the project's XenoUI version |
| XenoUI Preview window: errors on recent XenoUI APIs | Extension older than the project runtime resolution: install the up-to-date `.vsix` |
| Page preview: `NullReferenceException` with an "OnPreview" hint | The state the page reads is prepared in `OnStart`, which does not run in a preview: prepare it in `protected override void OnPreview()` of your `App` ([details](visual-studio.md#pages-and-the-application-in-a-preview)) |
| XenoUI Preview window: "No [Preview] in this file" | Add `[Preview]` to a parameterless `static` method returning `XenoComponent`, or to a `XenoPage` |

More cases, by symptom: [Troubleshooting](troubleshooting.md).
