# Publishing your app

[Documentation](index.md) · [Xeno.SDK](xeno-sdk.md) · [Installation](installation.md) · [Native capabilities](native-capabilities.md) · [Performance](performance.md)

From a working debug build to a store-ready package. XenoUI apps are standard .NET Android and iOS applications built
through `Xeno.SDK`, so packaging uses the **standard .NET for Android / iOS MSBuild properties**; XenoUI adds identity and
permission helpers only. This page lists what to set and what to check, and where the framework's own limits apply.

!!! warning "Pre-V1"
    XenoUI is a preview. The SDK's verification (`tools/Test-XenoSdk.ps1`) builds an **Android Release APK without AOT or
    trimming**; AOT, trimming and iOS packaging are **not validated** by the framework. Test every option you enable on a
    real device before shipping, and consult Microsoft's .NET for Android/iOS publishing documentation for the exact,
    current property names.

## Contents

1. [Checklist](#1-checklist)
2. [App identity and versions](#2-app-identity-and-versions)
3. [Permissions](#3-permissions)
4. [Android: signing and packaging](#4-android-signing-and-packaging)
5. [Publishing to Google Play](#5-publishing-to-google-play)
6. [iOS](#6-ios)
7. [Release-build pitfalls](#7-release-build-pitfalls)
8. [Continuous delivery](#8-continuous-delivery)

---

## 1. Checklist

- [ ] Unique `XenoAppId` (reverse-domain), name and version set ([§2](#2-app-identity-and-versions)).
- [ ] Minimum OS versions decided (`XenoMinAndroidVersion`, `XenoMinIosVersion`).
- [ ] Only the permissions you need, each justified ([§3](#3-permissions)).
- [ ] Release signing key created and **backed up** ([§4](#4-android-signing-and-packaging)).
- [ ] Release build tested on at least two real devices, including a low-end one.
- [ ] Back, rotation, background/foreground and no-network behaviour checked ([Debugging §12](debugging.md)).
- [ ] No debug logging of user data, no test endpoints or keys in the package.
- [ ] Accessibility, localization and RTL reviewed ([Accessibility](accessibility.md), [Localization](localization.md)).
- [ ] Store listing assets and privacy policy ready.

## 2. App identity and versions

In your `.csproj` (see [Xeno.SDK](xeno-sdk.md)):

```xml
<PropertyGroup>
  <XenoAppId>com.example.notes</XenoAppId>        <!-- becomes ApplicationId -->
  <XenoAppName>Notes</XenoAppName>                <!-- becomes ApplicationTitle -->
  <ApplicationVersion>12</ApplicationVersion>     <!-- Android versionCode: integer, increases with every upload -->
  <ApplicationDisplayVersion>1.4.0</ApplicationDisplayVersion>  <!-- shown to users -->
  <XenoMinAndroidVersion>24</XenoMinAndroidVersion>
  <XenoMinIosVersion>13.0</XenoMinIosVersion>
</PropertyGroup>
```

`XenoAppId` is passed to `ApplicationId` unless you set `ApplicationId` yourself. **Never change the application id after
publishing**: a new id is a different app. `ApplicationVersion` must increase for every build you upload.

## 3. Permissions

Declare what your app needs with `XenoPermission` items; the SDK merges them into a **derived** manifest (it never edits
your source manifest):

```xml
<ItemGroup>
  <XenoPermission Include="INTERNET" />
  <XenoPermission Include="CAMERA"
                  IosKey="NSCameraUsageDescription"
                  Reason="Photograph a document chosen by the user" />
</ItemGroup>
```

- Android accepts a short name (`CAMERA`) or the full name and adds a `uses-permission` entry, without duplicates.
- iOS uses only the explicit `IosKey` and needs a non-empty `Reason` shown to the user; the SDK does not invent one.
- **Declaring is not requesting.** Dangerous permissions (camera, location…) must be requested at run time, which is
  native code: write a [capability](native-capabilities.md) that asks and returns the answer.
- Request a permission **when the feature is used**, explain why, and handle refusal.
- Ask for as few permissions as you can; stores review them.
- Provide your own `AndroidManifest.xml` / `Info.plist` in the `.csproj` if you need more; do not set the manifest paths with
  global `-p:` properties when using `XenoPermission` ([Xeno.SDK](xeno-sdk.md)).

## 4. Android: signing and packaging

**Create a release key once** with the JDK's `keytool`, and keep the keystore and its passwords safe: losing the key means you
can no longer update the app (unless you enrolled in Play App Signing).

```powershell
keytool -genkeypair -v -keystore notes-release.keystore -alias notes -keyalg RSA -keysize 2048 -validity 10000
```

Reference it from the build **without committing secrets**. Pass passwords from the environment or your CI secret store:

```powershell
dotnet publish NotesApp.csproj -f net11.0-android -c Release `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore=notes-release.keystore `
  -p:AndroidSigningKeyAlias=notes `
  -p:AndroidSigningKeyPass=env:KEY_PASS `
  -p:AndroidSigningStorePass=env:STORE_PASS `
  -p:AndroidPackageFormats=aab
```

(`env:NAME` makes the Android build read the password from an environment variable; these are the standard .NET for Android
properties.)

| Package | Property value | Use |
|---|---|---|
| **AAB** (Android App Bundle) | `AndroidPackageFormats=aab` | Upload to Google Play (required for new apps) |
| **APK** | `AndroidPackageFormats=apk` | Direct install, testing, other stores |

`-c Release` matters: Debug builds are larger, slower and debuggable. XenoUI's verification builds Release **without AOT or
trimming**; enable `RunAOTCompilation` or `PublishTrimmed` only after testing the result, because trimming can remove code
reached by reflection (JSON, for instance: prefer source-generated serialisers, as the sample does).

## 5. Publishing to Google Play

1. Create the app in the Play Console and complete the store listing, content rating, data-safety form and privacy policy.
2. Prefer **Play App Signing**: Google keeps the final signing key; you sign uploads with an *upload key*.
3. Upload the **AAB** to an **internal testing** track first, then closed/open testing, then production.
4. Raise `ApplicationVersion` for every upload.
5. Check the pre-launch report (crashes, accessibility, permissions) before promoting a release.
6. Target the API level Google currently requires for new releases: set the supported/target OS in your project and keep the
   Android SDK platform current ([Installation](installation.md)).

## 6. iOS

Building, signing and running an iOS app **requires macOS and Xcode**, an Apple Developer account, a distribution
certificate and a provisioning profile. XenoUI's iOS path is **not yet validated end to end**; treat it as experimental.

```powershell
dotnet publish NotesApp.csproj -f net11.0-ios -c Release
```

Provide `Info.plist` usage descriptions for each permission (`XenoPermission … IosKey/Reason`). Upload to TestFlight through
Xcode or `xcrun altool`/Transporter, test, then submit for review. Follow Microsoft's .NET for iOS signing and archive
documentation for the exact properties.

## 7. Release-build pitfalls

| Pitfall | Symptom | Fix |
|---|---|---|
| Debug package shipped | Slow, large, debuggable | `-c Release` |
| Lost keystore | Cannot update the app | Back it up; use Play App Signing |
| Same `ApplicationVersion` | Upload rejected | Increase it |
| Trimming/AOT enabled untested | Crashes only in Release | Test on device; use source generators; keep what you reflect over |
| Missing `INTERNET` permission | Network works in debug, fails in release | Declare it with `XenoPermission` |
| Permission declared but never requested | Feature silently fails | Request at run time ([native capability](native-capabilities.md)) |
| Hard-coded test endpoint or key | Leak or wrong backend | Per-configuration settings; no secrets in the package |
| Performance measured in Debug | False alarms | Measure Release ([Performance §9](performance.md)) |

## 8. Continuous delivery

A minimal pipeline, with signing material from secrets:

```yaml
- run: dotnet test MyApp.Tests -c Release
- run: >
    dotnet publish MyApp/MyApp.csproj -f net11.0-android -c Release
    -p:AndroidPackageFormats=aab -p:AndroidKeyStore=true
    -p:AndroidSigningKeyStore=$RUNNER_TEMP/release.keystore
    -p:AndroidSigningKeyAlias=notes
    -p:AndroidSigningKeyPass=env:KEY_PASS -p:AndroidSigningStorePass=env:STORE_PASS
```

Restore the keystore from a secret into a temporary file, never commit it, and upload the AAB with your store's tooling.
Keep the build number monotonic (for example derive `ApplicationVersion` from the CI run number).
