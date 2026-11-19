# FAQ

[Documentation](index.md) · [Tutorial](tutorial-first-app.md) · [Glossary](glossary.md) · [Troubleshooting](troubleshooting.md) · [Debugging your UI](debugging.md)

Short answers to the questions beginners ask most. For build and device problems, see
[Troubleshooting](troubleshooting.md); for tracing a UI bug, [Debugging your UI](debugging.md).

## Concepts

### Why doesn't my screen update when I change a value?

Either the value is not **bound**, or the structure changed without a rebuild.

- A value that changes often should be a `State<T>` passed to a binding: `Text(state)`, `Text($"{state}")`,
  `.Background(state)`. Reading `state.Value` inside `Body()` does **not** subscribe to anything.
- If an element appears or disappears, call `Rebuild()`.

See [State](state.md#4-binding-state-to-the-ui).

### Why does my state go back to its initial value?

You created it inside `Body()`. `UseState`, `UseScroll`, `UseTextEdit`… return a **new object on every call**, and
`Body()` runs again on each `Rebuild()`. Declare them as **fields**:

```csharp
private readonly State<int> _count = UseState(0);   // ✓ field
```

### When do I use `Rebuild()` and when a `State`?

A `State` when a **value** changes (text, colour, progress). `Rebuild()` when the **structure** changes (an item is added,
a branch flips). When unsure, start with a `State`. See [Thinking in XenoUI §3](thinking-in-xenoui.md#3-the-two-ways-to-update-the-screen).

### Does XenoUI diff the tree like React or Compose?

No. `Rebuild()` runs the page's `Body()` again and replaces its scene. Fine-grained updates come from `State` bindings.
See [Thinking in XenoUI §4](thinking-in-xenoui.md#4-how-it-differs-from-jetpack-compose).

### Can I keep a `XenoComponent` in a field?

No. A `XenoComponent` is only valid within the composition that created it. Keep your **data** and recompose.

### What is a `State` versus a plain field?

A plain field holds a value but does not notify the UI. A `State<T>` notifies bound elements when its value changes. Use a
plain field plus `Rebuild()` for structural data (a list), a `State` for values bound to the display.

## Layout and styling

### How do I make something fill the width?

On the parent: `.CrossAxis(CrossAxisAlignment.Stretch)`; on the child: `.AlignSelf(CrossAxisAlignment.Stretch)`. To share
space along a `Row`, use `.FlexGrow(1)`. See [Layouts §3](layouts.md#3-sizes).

### Why is my text not centred?

The text's box is as narrow as the text. Give it `.AlignSelf(Stretch)` or put it in a container with `.CenterContent()`.

### Why can't I put an item in a corner of a `ZStack`?

A `ZStack` takes the horizontal position of **all** children from its own `MainAxis`. Place the item in a `Row` with a
`Spacer()` that fills the stack. See [Layouts §6](layouts.md#6-overlapping-zstack).

### Where is `Modifier`? Where is the `weight`?

Modifiers are chained on the result (`Box().Padding(8)`), and `weight` is `FlexGrow`. See
[From Jetpack Compose to XenoUI](compose-comparison.md#3-layout-and-modifiers).

### How do I change the colours of a built-in button's text?

Through its **parameter**, not a modifier: `Button("OK", tap, textColor: 0xFF000000)`. Modifiers act on the element they are
applied to, not on its children. See [Customising §3](customization.md#3-what-modifiers-cannot-do).

### Is there dark mode?

There is no built-in theme system: a theme is a class of yours that you apply explicitly, and switching it means
rebuilding. See [Customising §6](customization.md#6-colours-theme-and-dark-mode).

### Which units and colour format?

Sizes are **dp** (logical units); colours are `0xAARRGGBB` (`0xFF6D28D9` is opaque).

## Lists, navigation and input

### My list shows nothing

A lazy list needs a **bounded size** on its axis: `.FlexGrow(1)` inside a `Column`, or an explicit `.Height(…)`. See
[Layouts §8](layouts.md#8-virtualised-lists).

### Why do I need a `key` on lists?

It identifies an item as the visible window moves. Without one, identity is the index. Use a stable id.

### Navigation does nothing

The page was not registered: call `Page<MyPage>()` in `App.OnStart`. See [Lifecycle §2](lifecycle.md#2-application-lifecycle).

### How do I pass a value to another page?

Use `XenoPage<TArgs>` and `Navigate<TPage, TArgs>(args)`; read it in `OnNavigatedTo`. See the
[tutorial, step 5](tutorial-first-app.md#step-5-a-second-page).

### The back button closes the app

You are on the root of the stack, which is the intended behaviour. See [Troubleshooting](troubleshooting.md).

### How do I read what the user typed?

`controller.Value.Text` on the `TextEditController`, or subscribe to `controller.Changed`. See
[State §6](state.md#6-derived-values).

## Async work and data

### How do I call an API?

Not in `Body()`. Put the service call in a model or in `OnActivated`, run it off the UI thread, and come back with
`IUiDispatcherCapability.Post`. See [Side effects](side-effects.md) and [Application architecture](app-architecture.md).

### I get a crash or glitch when updating from a background thread

`State` assignments must happen on the **UI thread**. Use `Native.Require<IUiDispatcherCapability>().Post(() => …)`.
`Rebuild()` is the one call that can be made from any thread.

### My page updates after I left it

Guard late callbacks with `IsActive` and cancel work in `OnDeactivated`. See [Side effects §6](side-effects.md#6-late-results-isactive-versions-and-serial-numbers).

## Platforms and tooling

### Does it run on iOS?

The projects target iOS, but building and running need macOS and Xcode, and iOS is not yet validated end to end.
Android is the reference path. See [Why XenoUI?](why-xenoui.md#where-it-is-not-yet-the-right-choice).

### Can I preview without a device?

Yes: `[Preview]` in Visual Studio or VS Code, or `PreviewRenderer.Render` in a test ([Testing](testing.md)).

### Why .NET 11 and CoreCLR?

This branch targets .NET 11 and runs Android apps on CoreCLR; Mono is one property away. See
[Release notes](release-notes.md) and [Migration](migration.md).

### Is there a widget for Switch, Checkbox or Slider?

Not yet. Compose them from `Box`, `Row` and `Clickable`: see the [recipes](components.md#15-complete-recipes).

### Is it accessible?

Partly. See [Accessibility](accessibility.md) for what exists and what to do today.

### Can I use it in production?

It is a **pre-V1 preview**. Evaluate with your own risk in mind and read the [V1 trajectory](architecture/decisions-v1.md).
