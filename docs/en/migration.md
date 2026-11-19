# Migration guide

[Documentation](index.md) · [Release notes](release-notes.md) · [Installation](installation.md) · [Troubleshooting](troubleshooting.md)

Step by step upgrades between XenoUI versions. Each section lists what to change, what to check, and the errors you may
meet. XenoUI is pre-V1: read the [release notes](release-notes.md) before each upgrade.

## Contents

1. [To .NET 11 and CoreCLR](#1-to-net-11-and-coreclr)
2. [To preview.6 (from preview.5 or earlier)](#2-to-preview6-from-preview5-or-earlier)
3. [General upgrade checklist](#3-general-upgrade-checklist)

---

## 1. To .NET 11 and CoreCLR

### What changes (from .NET 10 and Mono)

| Before | After |
|---|---|
| `net10.0-android`, `net10.0-ios`, `net10.0` | `net11.0-android`, `net11.0-ios`, `net11.0` |
| .NET 10 SDK, Android 36 workload packs | .NET 11 SDK, Android 37 workload packs and platform |
| Mono runtime on Android | **CoreCLR** (`UseMonoRuntime=false`, set by `Xeno.SDK`) |

### Steps

1. **Install the .NET 11 SDK** and check it: `dotnet --version`.
2. **Install the Android workload and platform**:
   ```powershell
   dotnet workload restore MyApp.csproj
   dotnet build MyApp.csproj -t:InstallAndroidDependencies -f net11.0-android -p:AndroidSdkDirectory=<android-sdk>
   ```
   (`InstallAndroidDependencies` downloads the API 37 platform and asks you to accept the Android SDK licences.)
3. **Retarget** every project. In `.csproj` files:
   ```xml
   <TargetFrameworks>net11.0-android;net11.0-ios</TargetFrameworks>
   ```
   Library projects that only use portable code become `net11.0`. Search the repository for `net10.0` — scripts, CI files,
   `launch.json`, documentation and paths such as `bin/Release/net10.0-android/…`.
4. **Update `global.json`** if you pin the SDK or the `Xeno.SDK` version, and update to the `Xeno.SDK`/`XenoUI.*` packages that
   target .NET 11 (keep all `XenoUI.*` packages on the **same version**).
5. **Restore and rebuild** from a clean state: `dotnet clean`, delete `bin`/`obj` if needed, `dotnet build`.
6. **Run on a device** and walk through your main flows.

### CoreCLR on Android

`Xeno.SDK` turns CoreCLR on for you. To stay on Mono for now:

```xml
<PropertyGroup>
  <UseMonoRuntime>true</UseMonoRuntime>
</PropertyGroup>
```

What to verify after the switch:

- **Start-up time and memory** on a low-end device (they can differ between runtimes);
- **Release builds** with the options you use (trimming, AOT): re-test, do not assume;
- **Third-party packages** and native libraries: most `net10.0` packages work on `net11.0`; native assets must exist for the
  Android ABIs you ship;
- **Reflection-based code** (serialisers): prefer source-generated ones;
- **Performance numbers**: the published benchmarks were measured on .NET 10 with Mono; re-measure on your target
  ([Performance §10](performance.md)).

### Errors you may meet

| Message | Cause | Fix |
|---|---|---|
| `NETSDK1147` — the following workloads must be installed: android | Workload packs for API 37 missing | `dotnet workload restore <project>` |
| `XA5207` — could not find android.jar for API level 37 | Android SDK platform 37 missing | `-t:InstallAndroidDependencies` (command above) |
| `NETSDK1045` — the current SDK does not support targeting .NET 11 | An older SDK is selected | Install SDK 11 or fix `global.json` |
| Restore error on a `net10.0`-only package | A dependency has no `net11.0` asset | Update it; most compatible packages restore on 11 |
| Visual Studio does not list the Android 37 workload | IDE older than the workload | Update the IDE, or build from the CLI |

## 2. To preview.6 (from preview.5 or earlier)

preview.6 adds overlays and the scaffold top bar. Nothing breaks existing pages, but two things are worth knowing:

- The SDK now imports `XenoUI.StandardLibrary.Overlays` and `XenoUI.StandardLibrary.Shell` implicitly. If you defined your own
  type named `Snackbar`, `Alert`, `Popup`, `BottomSheet` or `TopBar`, qualify it or rename it. On Android, `Dialog` is aliased
  to the XenoUI one: use `Android.App.Dialog` explicitly if you need the platform's.
- To adopt the top bar, set `TopBar = TopBars.ForActivePage` on the scaffold state and implement `ITopBarPage` on the pages that
  want one. Pages that do not implement it have no bar.
- Navigation back is now the mirror of going forward; if you compensated for the old behaviour with custom pop transitions,
  review them.

## 3. General upgrade checklist

- [ ] Read the [release notes](release-notes.md) for every version between yours and the target.
- [ ] Upgrade **all** `XenoUI.*` packages, the templates, the `xeno` tool and the IDE extensions **together**.
- [ ] Update `global.json` if you pin the SDK.
- [ ] `dotnet clean`, then restore and build for every target.
- [ ] Run your tests, including the headless rendering tests ([Testing](testing.md)); regenerate **golden** images only after
      reviewing the differences.
- [ ] Run on at least one real device: navigation, back, rotation, background/foreground, keyboard.
- [ ] Re-measure performance in Release if the runtime or the SDK changed.
- [ ] Commit the upgrade on its own, so it can be reverted cleanly.

If an upgrade breaks something that the documentation describes as a contract, that is a bug worth reporting
([Debugging §12](debugging.md#12-what-to-include-in-a-bug-report)).
