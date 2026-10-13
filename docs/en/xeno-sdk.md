# Xeno.SDK — creating and distributing an application

[Documentation](index.md) · [Developer guide](developer-guide.md) · [Tooling architecture](architecture/tooling-and-platforms.md)

`Xeno.SDK` is an MSBuild SDK distributed as a NuGet package. It imports `Microsoft.NET.Sdk`, references the runtime packages matching the platform, provides the native entry points and embeds the assets. No import relative to the repository is needed in a consuming project.

## Status and prerequisites

Local preview: `0.1.0-preview.5`. No package is published on nuget.org yet. You need .NET 10 and the workload of the targeted platform. The SDK does not silently download a workload, the Android SDK or Xcode. Building/signing/running an iOS application always requires macOS and Xcode; producing an iOS library or evaluating its project on Windows is not a validation of the iOS application.

## Producing the local feed

From the repository root, with PowerShell:

```powershell
./tools/Pack-XenoSdk.ps1
# Android-only machine, without the iOS workload:
./tools/Pack-XenoSdk.ps1 -SkipIos
```

The script produces in `artifacts/packages`: `Xeno.SDK`, `XenoUI.Core`, `XenoUI.StandardLibrary`, `XenoUI.Platforms` and `XenoUI.Templates`. `XenoUI.Platforms` groups the native hosts into a single multi-target package (`lib/net10.0-android` and `lib/net10.0-ios`); NuGet picks the one for the application's TFM. With `-SkipIos`, it only contains the Android target. Runtimes, SDK and templates share the same version. The MSBuild task is embedded in the SDK package, not in the application's dependencies. The repository's existing licence is kept in the packages.

For another version: `-Version 0.1.0-preview.6`. Do not reuse a version already distributed: NuGet caches treat packages as immutable. Change the version consumed in the project or in `global.json`. A global NuGet cache must not be deleted to update the SDK.

## Consuming outside the repository

The simplest way is the `dotnet new xenoui --feed <feed>` template, which generates `global.json` and `NuGet.config`: see the [installation guide](installation.md#5-creating-an-application-and-using-the-sdk). By hand:

Add the absolute path of your local feed to the new project's `NuGet.Config`, keeping the sources needed for SkiaSharp and the other dependencies:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="xeno-local" value="D:/path/XenoUI/artifacts/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

Create `MyApp.csproj`:

```xml
<Project Sdk="Xeno.SDK/0.1.0-preview.5">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
    <XenoAppId>com.example.myapp</XenoAppId>
    <XenoAppName>My App</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>
</Project>
```

For Android and iOS: `TargetFrameworks=net10.0-android;net10.0-ios`, with the matching workloads/packages available. The SDK version can also be centralised in `global.json`:

```json
{ "msbuild-sdks": { "Xeno.SDK": "0.1.0-preview.5" } }
```

In that case use `<Project Sdk="Xeno.SDK">`, without repeating the version. Do not add `Xeno.SDK` as a plain `PackageReference`: the SDK takes part from the project evaluation onwards.

Add your `XenoApp`, your `XenoPage`s and `Assets/`, then `dotnet restore` and `dotnet build -f net10.0-android`. The identifier, versions and signing options remain the application's responsibility.

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
| `XenoUseProjectReferences` | `false` | Explicit contributor mode; requires `XenoSourceRoot` |

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

When you use `XenoPermission`, declare custom manifest paths in the `.csproj`, not with `-p:AndroidManifest=...` or `-p:AppBundleManifest=...`: these MSBuild global properties would lock the source path and prevent its replacement by the merged derived file. The verifier covers project properties.

## Developing the framework with SampleApp

SampleApp uses `<Project Sdk="Xeno.SDK">`. The repository's `Directory.Build.props` enables `XenoUseProjectReferences=true` for this project only and sets `XenoSourceRoot`. This mode lets you test runtime changes immediately without republishing its DLLs. It is not packed into the SDK and never applies to a consumer outside the repository.

To check SampleApp as a NuGet consumer: add `-p:XenoUseProjectReferences=false` to the restore/build commands and use the aligned packages from the feed. After changing SDK files or packed native entry points, produce a new SDK version and update `global.json`; source mode does not bypass the SDK cache.

The old `Sdk.props`/`Sdk.targets` at the root are kept temporarily for older consumers. They are no longer the official path and are not included in `Xeno.SDK`.

## Reproducible verification

```powershell
./tools/Pack-XenoSdk.ps1
./tools/Test-XenoSdk.ps1
# Evaluation only, without compiling an APK:
./tools/Test-XenoSdk.ps1 -SkipAndroidBuild
```

The verifier creates an application in the system temporary folder **outside the checkout**, with a NuGet cache isolated from its sources. It checks that there is no `ProjectReference`, the versions, the design-time properties, the Android/iOS selection, disabling the native entry point, assets and manifests. It compiles a Release Android APK without AOT/trimming to isolate packaging risks. The `-SkipIos`/`-SkipAndroidBuild` parameters explicitly narrow the scope.

The consumer is kept for inspection, and the result is written to `artifacts/xeno-sdk-verification.json`. These artefacts are not versioned. Having an MSBuild SDK does not by itself guarantee an IDE's device picker or debugger: that part stays in [tooling](architecture/tooling-and-platforms.md).
