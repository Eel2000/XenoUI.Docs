# Why XenoUI?

[Documentation](index.md) · [Tutorial](tutorial-first-app.md) · [Thinking in XenoUI](thinking-in-xenoui.md) · [From Jetpack Compose](compose-comparison.md) · [FAQ](faq.md)

XenoUI is a **declarative mobile UI framework for .NET**. You describe the interface in C# by calling functions; XenoUI
runs its own layout engine and draws everything with **SkiaSharp** on Android and iOS. This page tells you what that
means in practice, who it suits, and — just as important — what it does **not** do yet.

## In one minute

```csharp
public override XenoComponent Body() =>
    Column(
        Text($"Total: {_count}", 24),
        Button("+1", () => _count.Value++)
    ).Gap(12).Padding(16);
```

- **One C# codebase, no XAML or markup.** The interface is code: functions, modifiers, state.
- **Drawn, not wrapped.** Buttons, lists, text fields and animations are painted by XenoUI, so they look and behave the
  same on Android and iOS. Only what the OS does better (the keyboard and IME, clipboard, video decoding) stays native,
  through a typed bridge called `NativeTunnel`.
- **Built for smooth lists and gestures.** Virtualised lists measured on demand, no per-frame allocation on stable
  frames, scenes retained across tabs.

## How it compares

| | XenoUI | .NET MAUI | Jetpack Compose | Flutter |
|---|---|---|---|---|
| Language | C# | C# + XAML | Kotlin | Dart |
| Platforms | Android, iOS | Android, iOS, Windows, macOS | Android (+ Multiplatform) | Android, iOS, web, desktop |
| UI model | Declarative functions | Native controls, XAML or C# | Declarative functions | Declarative widgets |
| Rendering | Own engine on Skia | Native controls | Skia / Android canvas | Own engine (Impeller/Skia) |
| Look | Identical on both platforms | Platform-native | Material / custom | Material / custom |
| Updates | Explicit `State<T>` bindings + page `Rebuild()` | Data binding | Smart recomposition | Widget rebuild |
| Maturity | **Pre-V1 preview** | Production | Production | Production |

If you know one of these, the [comparison with Jetpack Compose](compose-comparison.md) maps concepts one by one.

## Where XenoUI is a good fit

- You are a **.NET team** and want declarative UI without XAML.
- You want a **pixel-identical** interface on Android and iOS, with custom visuals and animation freedom.
- Your app is **list- and gesture-heavy** (feeds, galleries, short-video style screens, chat).
- You like **small, testable building blocks**: components are plain functions, and screenshots can be rendered in a
  unit test ([Testing](testing.md)).

## Where it is not (yet) the right choice

Be honest with yourself before starting:

- **It is pre-V1.** APIs can still change; a behaviour is only a contract when a test covers it and the docs say so
  ([Decisions and V1 trajectory](architecture/decisions-v1.md)).
- **Android is the validated path.** iOS builds require macOS and Xcode and are not yet validated end to end.
- **No desktop or web target**, and no Windows/macOS app shell.
- **No built-in accessibility tree** for drawn elements: TalkBack/VoiceOver support is partial
  ([Accessibility](accessibility.md)).
- **No ready-made Material widgets** for switches, checkboxes or sliders; you compose them
  ([Component catalogue](component-catalog.md)).
- **No state restoration after process death**, no deep links, no typed navigation results
  ([Lifecycle](lifecycle.md), [State](state.md)).
- **No visual designer.** There are previews in Visual Studio and VS Code, not a drag-and-drop editor.

## What you get today

| Area | Included |
|---|---|
| Composition | `Column`, `Row`, `ZStack`, `Box`, `Text`, `Image`, `Icon`, lists, pager — [catalogue](component-catalog.md) |
| State | `State<T>`, scroll and list state, text controllers — [State](state.md) |
| Navigation | Stacks, adaptive scaffold (bar / rail / drawer), transitions — [Navigation](navigation-scaffold.md) |
| Overlays | Snackbar, alert, dialog, popup, bottom sheet, top bar — [Overlays](component-catalog.md#overlays) |
| Text input | Custom-painted fields with the native IME, RTL, emoji, CJK |
| Motion | Page, selection and overlay animations — [Animations](animations.md) |
| Media | Audio and video players behind a portable controller — [Media](media-controller.md) |
| Tooling | `Xeno.SDK`, templates, `xeno` CLI, Visual Studio and VS Code previews — [Tooling](xeno-sdk.md) |

## Next

1. [Tutorial: your first app](tutorial-first-app.md) — 30 minutes, hands-on.
2. [Installation](installation.md) if you have not set up the tools.
3. [Thinking in XenoUI](thinking-in-xenoui.md) for the mental model.
