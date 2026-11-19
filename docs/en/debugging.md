# Debugging your UI

[Documentation](index.md) · [FAQ](faq.md) · [Troubleshooting](troubleshooting.md) · [Testing](testing.md) · [Developer guide §15](developer-guide.md)

[Troubleshooting](troubleshooting.md) is organised by **symptom** (device, build, blank screen…). This page is organised by
**method**: how to find out what your UI is doing when it is not doing what you expect. XenoUI has no visual inspector yet
(no layout inspector, no recomposition counter), so debugging relies on a few simple techniques that work well because
`Body()` is a plain function.

## Contents

1. [A method that works](#1-a-method-that-works)
2. [See what was composed: render it to a PNG](#2-see-what-was-composed-render-it-to-a-png)
3. [The screen is blank](#3-the-screen-is-blank)
4. [A value does not update](#4-a-value-does-not-update)
5. [The layout is wrong](#5-the-layout-is-wrong)
6. [State is lost or reset](#6-state-is-lost-or-reset)
7. [Crashes and exceptions](#7-crashes-and-exceptions)
8. [Logging](#8-logging)
9. [Threading problems](#9-threading-problems)
10. [Slow or janky](#10-slow-or-janky)
11. [Attaching the debugger](#11-attaching-the-debugger)
12. [What to include in a bug report](#12-what-to-include-in-a-bug-report)

---

## 1. A method that works

1. **Reproduce** in the smallest screen: put the faulty component in a `[Preview]` or a test with sample data.
2. **Isolate**: is the problem in the *data* (what you pass), the *structure* (what you compose) or the *layout* (sizes)?
3. **Look**: render it ([§2](#2-see-what-was-composed-render-it-to-a-png)) and log the inputs ([§8](#8-logging)).
4. **Change one thing** at a time.

Because components are functions of their inputs, a bug that appears with given inputs reproduces **every time** in a
preview. That is your main advantage.

## 2. See what was composed: render it to a PNG

The headless previewer works from a small test or console project, with no device:

```csharp
var image = PreviewRenderer.Render(() => MyComponent.View(sampleData), new PreviewAttribute { Device = "android" }, scale: 2f);
if (!image.Succeeded) Console.WriteLine($"{image.Error?.Message} {image.Hint}");
else File.WriteAllBytes("debug.png", image.Png!);
```

`Succeeded` is false when composition throws; `Error` and `Hint` tell you why. Open the PNG to check what is really drawn.
Try other devices (`Device = "iphone-se"`, `"tablet"`), `RightToLeft = true`, and a long text sample. See
[Testing §3](testing.md).

## 3. The screen is blank

Check in this order (the order of likelihood):

| # | Check | How |
|---|---|---|
| 1 | An **exception in `Body()`** | Run the page as a preview ([§2](#2-see-what-was-composed-render-it-to-a-png)): `Error` shows it; or read logcat ([§8](#8-logging)) |
| 2 | The page is **not registered** | `Page<MyPage>()` in `App.OnStart` ([Lifecycle](lifecycle.md)) |
| 3 | A **persistent object created in `Body()`** | `UseState`/`UseScroll`/`UseTextEdit` must be fields ([State](state.md)) |
| 4 | **Size zero** | A lazy list without bounded size, `Box(0, 0)` without a stretching parent; give `FlexGrow(1)`, `Height` or `Stretch` ([Layouts §13](layouts.md)) |
| 5 | **Invisible colours** | Alpha `00` (`0x00FFFFFF`) or text the same colour as its background |
| 6 | **Content under the bar** | `ContentOverlaysNavigationBar` or safe-area padding |
| 7 | A **late callback** wrote to a left page | Guard with `IsActive` ([Side effects §6](side-effects.md)) |

## 4. A value does not update

Ask: *is it bound?*

- A value that changes must be a `State<T>` **passed to a binding**: `Text(state)`, `Text($"{state}")`,
  `.Background(state)`, `.TextColor(state)`. Reading `state.Value` inside `Body()` subscribes to nothing.
- If the **structure** changed (an item appears, a branch flips), you must call `Rebuild()`.
- A `List` modified in place inside a `State<List<T>>` does not notify: keep the list in a field and `Rebuild()`.
- A `State` assigned **from a background thread** can fail to repaint: assign on the UI thread
  (`IUiDispatcherCapability.Post`).
- A **component you stored in a field** is dead after a rebuild: store data, recompose.

A quick test: log in the setter path (`_count.OnChanged += () => Console.WriteLine("changed")`). If it logs but the screen
does not change, the element is not bound; if it does not log, the assignment never happened.

## 5. The layout is wrong

Use **temporary backgrounds** to make boxes visible:

```csharp
Column(...).Background(0x33FF0000)     // see the real bounds of a container
```

Then check, in order: the **main** and **cross** axes of the parent (`MainAxis`, `CrossAxis`), whether the child has an
explicit `Width`/`Height`, `FlexGrow` versus `Stretch`, `Padding` versus `Margin`, and the special `ZStack` rule (horizontal
position comes from the stack). The [layout troubleshooting table](layouts.md) lists the usual
causes. Preview on a narrow device (`iphone-se`) and with long text to expose overflow.

## 6. State is lost or reset

| Symptom | Cause |
|---|---|
| Counter goes back to its initial value after an unrelated change | `UseState` inside `Body()` |
| Scroll position jumps to the top | `UseScroll()` created in `Body()` |
| Typed text disappears on rebuild | `UseTextEdit()` in `Body()` |
| List loses its position | `UseLazyList()` in `Body()`, or unstable keys |
| State vanishes when the process is killed | Not restored: persist what matters yourself ([State §9](state.md)) |

## 7. Crashes and exceptions

- **Composition throws** (null reference, bad index): the stack trace points into your `Body()` or a component. In a preview it
  becomes `image.Error`; on a device it is in logcat.
- **Duplicate list key** → `InvalidOperationException` during composition ("Lazy list key … is duplicated"): make keys unique.
- **`XenoComponent.None` converted to `int`** → `InvalidOperationException`: test `IsNone` first.
- **Crash during back/navigation**: usually a page not registered, a persistent object created in `Body()` or a stale entity.
  See [Troubleshooting](troubleshooting.md).
- **Unobserved task exceptions**: catch at the boundary of every `Task.Run` ([Side effects](side-effects.md)).

## 8. Logging

On Android, `Console.WriteLine` and `System.Diagnostics.Debug.WriteLine` appear in logcat:

```powershell
adb shell pidof com.example.myapp
adb logcat --pid=<pid>
```

Log **inputs and decisions**, not every frame:

```csharp
public override XenoComponent Body()
{
    System.Diagnostics.Debug.WriteLine($"NotesPage.Body: {_notes.Count} notes");   // runs on each rebuild
    …
}
```

Counting how often `Body()` runs is also a quick **rebuild counter**: if it logs more than expected, something calls
`Rebuild()` too often. Remove temporary logs before committing; never log secrets or user text.

## 9. Threading problems

Symptoms: occasional visual glitches, `InvalidOperationException` from the UI, behaviour that changes between runs.
Cause: a `State`, `TextEditController` or `ScrollState` touched from a background thread. Fix: marshal with
`Native.Require<IUiDispatcherCapability>().Post(...)`. `Rebuild()` is the only call safe from any thread
([Side effects §3](side-effects.md)).

## 10. Slow or janky

1. Measure in **Release**, without a debugger, on a **real device** ([Performance §9](performance.md)).
2. Count `Body()` calls ([§8](#8-logging)): too many rebuilds is the usual culprit; use `State` bindings.
3. Check lists: `LazyColumn` with stable keys, item functions free of heavy work.
4. Check the UI thread: no network, disk or parsing in callbacks.
5. Look at large shadows, scene opacity and animated sizes ([Performance §6–7](performance.md)).

## 11. Attaching the debugger

Run a **Debug** build on the device or emulator from Visual Studio, or `xeno run … --configuration Debug`
([Developer guide §5](developer-guide.md)), then set breakpoints in `Body()`, callbacks and models.
Remember that `Body()` runs on each rebuild and that previews run it too: a breakpoint in `Body()` hits often.

To inspect the build rather than the app, use `dotnet build -v:diagnostic` and `dotnet msbuild -getProperty:…`
([Developer guide §15](developer-guide.md)).

## 12. What to include in a bug report

- XenoUI package version, .NET SDK version, Android API level or iOS version, device and runtime (CoreCLR or Mono);
- a **minimal component or page** reproducing it, ideally as a `[Preview]`;
- expected versus actual behaviour, with the **PNG** from [§2](#2-see-what-was-composed-render-it-to-a-png);
- the relevant logcat lines and the stack trace;
- whether it happens in Debug and Release, and on a second device.

See also the list in [Troubleshooting](troubleshooting.md).
