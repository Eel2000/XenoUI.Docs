# Xeno.SDK — creating and configuring an application

[Documentation](index.md) · [Installation](installation.md) · [Developer guide](developer-guide.md) · [Tooling architecture](architecture/tooling-and-platforms.md)

[`Xeno.SDK`](https://www.nuget.org/packages/Xeno.SDK) is an MSBuild SDK distributed on nuget.org. It imports `Microsoft.NET.Sdk`, references the runtime packages matching the platform, provides the native entry points and embeds the assets.

## Status and prerequisites

Current version: `{{ xeno_version }}`, published on nuget.org. You need .NET 10 and the workload of the targeted platform. The SDK does not silently download a workload, the Android SDK or Xcode. Building/signing/running an iOS application always requires macOS and Xcode; evaluating an iOS project on Windows is not a validation of the iOS application.

All published packages share the same version:

| Package | Content |
|---|---|
| `Xeno.SDK` | MSBuild SDK: properties, targets, entry points, build task |
| `XenoUI.Core`, `XenoUI.StandardLibrary` | Portable runtime |
| `XenoUI.Platforms` | Android and iOS native hosts in a single multi-target package (`lib/net10.0-android`, `lib/net10.0-ios`); NuGet picks the one for the application's TFM |
| `XenoUI.Media`, `XenoUI.Media.Android` | Optional media module (`XenoEnableMedia`) |
| `XenoUI.Templates` | `dotnet new` / Visual Studio templates |
| `XenoUI.Tooling` | The `xeno` .NET tool |

A published version is immutable: a fix always carries a new version number.

## Using the SDK

The simplest way is the `dotnet new xenoui` template, which generates the project and `global.json`: see the [installation guide](installation.md#4-creating-an-application-and-using-the-sdk). By hand, create `MyApp.csproj`:

```xml
<Project Sdk="Xeno.SDK/{{ xeno_version }}">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
    <XenoAppId>com.example.myapp</XenoAppId>
    <XenoAppName>My App</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>
</Project>
```

For Android and iOS: `TargetFrameworks=net10.0-android;net10.0-ios`, with the matching workloads installed. The SDK version can also be centralised in `global.json`:

```json
{ "msbuild-sdks": { "Xeno.SDK": "{{ xeno_version }}" } }
```

In that case use `<Project Sdk="Xeno.SDK">`, without repeating the version. Do not add `Xeno.SDK` as a plain `PackageReference`: the SDK takes part from the project evaluation onwards. No `NuGet.config` is needed: the default nuget.org source provides the SDK, the runtimes, SkiaSharp and the other dependencies.

Add your `XenoApp`, your `XenoPage`s and `Assets/`, then `dotnet restore` and `dotnet build -f net10.0-android`. The identifier, versions and signing options remain the application's responsibility.

To move to a new version, change `global.json` (or the `Sdk` attribute) then run `dotnet restore`. Do not delete the global NuGet cache to update the SDK.

## Configuration and extensions

| Property | Default | Use |
|---|---|---|
| `XenoAppId` | `com.xenoui.unnamed` | Identity, passed to `ApplicationId` if not explicitly set |
| `XenoAppName` | project name | Android display name and generated iOS plist |
| `XenoMinAndroidVersion` | `24` | Android minimum |
| `XenoMinIosVersion` | `13.0` | iOS minimum |
| `XenoGenerateEntryPoint` | `true` | Set to `false` to provide your own Activity or iOS entry point |
| `XenoEnableDefaultAssets` | `true` | Automatic collection of `Assets/**/*` |
| `XenoEnableImplicitUsings` | `true` | XenoUI imports and factories; can be disabled without touching the references |
| `XenoEnableMedia` | `false` | Optional media module; foreground audio and reference Media3 native video output on Android. See the [capabilities and limits](media-controller.md). |

Explicitly set native properties, notably `ApplicationId`, `ApplicationTitle` and `SupportedOSPlatformVersion`, take precedence. In a multi-target project, condition the overrides on the TFM. Your other `PackageReference`s, C# components and NativeTunnel extensions remain free: the SDK does not impose a closed list of controls.

`Platforms/ios/**/*.cs` files are excluded from Android and vice versa. With `XenoGenerateEntryPoint=false`, your files for the active platform are still compiled. The SDK's linked files are visible to the design-time evaluation under `XenoGenerated`.

## Assets and permissions

The path `Assets/Images/logo.png` becomes the resource `XenoAsset/Images/logo.png`. Disable automatic collection to manage the `XenoAsset` items yourself, without preventing other native resources.

```xml
<ItemGroup>
  <XenoPermission Include="CAMERA"
                  IosKey="NSCameraUsageDescription"
                  Reason="Photograph a document chosen by the user" />
  <XenoPermission Include="android.permission.RECORD_AUDIO" />
</ItemGroup>
```

Android accepts a short name (`CAMERA`) or the qualified name and adds `uses-permission` at the manifest root, without duplicates. iOS exclusively uses the explicit `IosKey` and requires a non-empty `Reason`; it does not invent a user-facing justification. Declaring a permission does not request it at run time: that belongs to native/NativeTunnel code.

An `AndroidManifest.xml` and `Info.plist` supplied by the application take precedence, or can be selected through `AndroidManifest` and `AppBundleManifest`. When a merge is needed, it produces a derived file in `obj` and never modifies the source file. Without a custom plist, the SDK generates its default in `obj`. Android receives project-relative paths, as its workload expects. The generation tasks do not run in `DesignTimeBuild`.

When you use `XenoPermission`, declare custom manifest paths in the `.csproj`, not with `-p:AndroidManifest=...` or `-p:AppBundleManifest=...`: these MSBuild global properties would lock the source path and prevent its replacement by the merged derived file.

## Checking a project

```powershell
dotnet msbuild MyApp.csproj -p:TargetFramework=net10.0-android `
  -getProperty:ApplicationId -getProperty:AndroidApplication
dotnet build MyApp.csproj -f net10.0-android
```

Having an MSBuild SDK does not by itself guarantee an IDE's device picker or debugger: to launch on a device, use `xeno run` ([installation](installation.md#5-installing-the-xeno-tool)) or your IDE's Android tools; see also [tooling](architecture/tooling-and-platforms.md).
