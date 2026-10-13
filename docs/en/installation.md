# Installing XenoUI: packages, SDK, the `xeno` tool and the previewer

[Documentation](index.md) · [Xeno.SDK](xeno-sdk.md) · [Visual Studio](visual-studio.md) · [Developer guide](developer-guide.md) · [Troubleshooting](troubleshooting.md)

This guide installs everything needed to create, build and preview a XenoUI application. Every package comes from [nuget.org](https://www.nuget.org/packages?q=XenoUI): no local feed or build is needed.

> **Status: `{{ xeno_version }}`, preview published on nuget.org.** The API may still change before V1. The **XenoUI Preview** Visual Studio extension is coming soon to the Visual Studio Marketplace.

## 1. What gets installed, and how

| Item | Role | Installation |
|---|---|---|
| [`Xeno.SDK`](https://www.nuget.org/packages/Xeno.SDK) | The application's MSBuild SDK: runtimes, entry points, assets | **Nothing to do**: restored by NuGet from `global.json` |
| [`XenoUI.Core`](https://www.nuget.org/packages/XenoUI.Core), [`XenoUI.StandardLibrary`](https://www.nuget.org/packages/XenoUI.StandardLibrary), [`XenoUI.Platforms`](https://www.nuget.org/packages/XenoUI.Platforms) | Runtime and Android/iOS hosts | **Nothing to do**: referenced by `Xeno.SDK` |
| [`XenoUI.Media`](https://www.nuget.org/packages/XenoUI.Media), [`XenoUI.Media.Android`](https://www.nuget.org/packages/XenoUI.Media.Android) | Optional media module (audio, video) | `<XenoEnableMedia>true</XenoEnableMedia>` in the project; `Xeno.SDK` then references them ([details](media-controller.md)) |
| [`XenoUI.Templates`](https://www.nuget.org/packages/XenoUI.Templates) | "XenoUI App / Page / Component" templates (CLI and Visual Studio) | `dotnet new install` (§ 3) |
| [`XenoUI.Tooling`](https://www.nuget.org/packages/XenoUI.Tooling) | The `xeno` tool: Android devices, build, install, launch | `dotnet tool install` (§ 5) |
| **XenoUI Preview** extension | `[Preview]` previews in Visual Studio | Coming soon to the Visual Studio Marketplace (§ 6) |

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

## 3. Installing the templates

```powershell
dotnet new install XenoUI.Templates::{{ xeno_version }}
```

Check: `dotnet new list xenoui` shows `xenoui`, `xenoui-page` and `xenoui-component`. Visual Studio shows them in *Create a new project* and *Add › New Item* after a restart.

Update: install the new version (`dotnet new install ...` replaces the old one). Uninstall: `dotnet new uninstall XenoUI.Templates`.

## 4. Creating an application and using the SDK

### With the template

```powershell
dotnet new xenoui -n MyApp
cd MyApp
dotnet build -f net10.0-android
```

In Visual Studio: *Create a new project* › "XenoUI App"; the same options appear in the wizard. `--ios` adds `net10.0-ios`, `--appId com.example.myapp` sets the identifier. See [Visual Studio](visual-studio.md#creating-a-project).

### What the template sets up

```text
MyApp/
├─ MyApp.csproj        <Project Sdk="Xeno.SDK">, TargetFramework, XenoAppId…
├─ global.json         { "msbuild-sdks": { "Xeno.SDK": "{{ xeno_version }}" } }
├─ App.cs
├─ Pages/HomePage.cs   with [Preview]
└─ Components/Counter.cs
```

The **SDK installs itself**: on the first `restore`/`build`, MSBuild reads the version in `global.json` and NuGet downloads `Xeno.SDK` from nuget.org, which in turn references the runtimes of the same version.

### Without the template (existing project)

1. Add `global.json` at the root of the solution:

   ```json
   { "msbuild-sdks": { "Xeno.SDK": "{{ xeno_version }}" } }
   ```

2. Replace the project header with `<Project Sdk="Xeno.SDK">` and set `TargetFramework` (`net10.0-android`, `net10.0-ios` or both).

No `NuGet.config` is needed: the default nuget.org source is enough. All properties (`XenoAppId`, `XenoAppName`, OS minimums, permissions, assets, media) are described in [Xeno.SDK](xeno-sdk.md).

### Changing version

Change the version in `global.json`, then run `dotnet restore`. Available versions are listed on [nuget.org](https://www.nuget.org/packages/Xeno.SDK#versions-body-tab). A version already restored is never downloaded again: NuGet caches are immutable per version.

## 5. Installing the `xeno` tool

```powershell
dotnet tool install --global XenoUI.Tooling --version {{ xeno_version }}
```

```powershell
xeno devices
xeno run --project MyApp.csproj --device <device-id>
```

`xeno devices` lists the Android devices seen by `adb` (state, API, ABI). Update: `dotnet tool update --global XenoUI.Tooling --version <version>`. Uninstall: `dotnet tool uninstall --global XenoUI.Tooling`. For a per-repository tool rather than a global one, use `dotnet new tool-manifest` then `dotnet tool install XenoUI.Tooling` without `--global`.

## 6. The previewer (Visual Studio extension)

The **XenoUI Preview** extension shows the `[Preview]`s of your pages and components as you type. It is coming soon to the Visual Studio Marketplace; once published:

1. In Visual Studio: *Extensions › Manage Extensions*, search for **XenoUI Preview** and install it, then restart Visual Studio.
2. Open a C# file of a `Xeno.SDK` project, then *View › Other Windows › XenoUI Preview* (**Ctrl+Alt+P**). With the template, `Pages/HomePage.cs` shows two previews.

The extension neither builds nor launches the project: it starts its own render server, isolated from the IDE, the first time the window opens. How it works, the `[Preview]` attribute and limits: [Visual Studio](visual-studio.md).

`[Preview]` attributes can already be used in your code: they have no effect at run time.

## 7. Checking the whole setup

```powershell
dotnet new xenoui -n Check
cd Check
dotnet build -f net10.0-android            # SDK + runtimes restored from nuget.org, APK compiled
xeno devices                               # tool installed
xeno run --project Check.csproj --device <device-id>
```

## 8. Common problems

| Symptom | Cause and fix |
|---|---|
| `Could not resolve SDK "Xeno.SDK"` | Version unknown on nuget.org or NuGet source unreachable: check the version in `global.json` and that nuget.org is an enabled source (`dotnet nuget list source`) |
| `NETSDK1147`: missing workload | `dotnet workload install android` (or `ios`) |
| `NU1101` on a XenoUI package | Misaligned versions: `Xeno.SDK` and the runtimes must share the same version; fix `global.json`, then `dotnet restore` |
| `dotnet new xenoui` unknown | Templates not installed (§ 3) or another hive: `dotnet new list xenoui` |
| `xeno` not found | Open a new terminal (global tools PATH: `%USERPROFILE%\.dotnet\tools`) |
| XenoUI Preview window: "This file does not belong to a Xeno.SDK project" | The nearest `.csproj` does not use `Sdk="Xeno.SDK"` |
| XenoUI Preview window: "The preview server stopped" | .NET 10 runtime missing from the `dotnet` on the PATH, or a preview blocked for more than 10 s; the server restarts on the next change |
| XenoUI Preview window: "… is not restored … embedded runtime" | The project was never restored: run a build or `dotnet restore` so the preview uses the project's XenoUI version |
| Page preview: `NullReferenceException` with an "OnPreview" hint | The state the page reads is prepared in `OnStart`, which does not run in a preview: prepare it in `protected override void OnPreview()` of your `App` ([details](visual-studio.md#pages-and-the-application-in-a-preview)) |
| XenoUI Preview window: "No [Preview] in this file" | Add `[Preview]` to a parameterless `static` method returning `XenoComponent`, or to a `XenoPage` |

More cases, by symptom: [Troubleshooting](troubleshooting.md).
