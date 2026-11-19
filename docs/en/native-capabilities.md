# Native capabilities (`NativeTunnel`)

[Documentation](index.md) · [Application architecture](app-architecture.md) · [Text, IME and NativeTunnel](architecture/text-input-and-native.md) · [Developer guide §12](developer-guide.md)

XenoUI draws its own pixels, but some things belong to the operating system: the keyboard, the clipboard, video
decoding, the device's language and window size. They are reached through **capabilities**: small typed interfaces held
by `NativeTunnel`. This guide shows how to **use** the built-in ones and how to **write your own**. (The internal design
is in [Text, IME and NativeTunnel](architecture/text-input-and-native.md).)

## Using a capability

Pages and the application reach it through `Native`:

```csharp
// Required: throws if the platform does not provide it
var ui = Native.Require<IUiDispatcherCapability>();

// Optional: null when absent (previews, unsupported platform)
bool reduced = Native.Get<IMotionPreferencesCapability>()?.ReducedMotion == true;
```

Use `Get<T>()` when your feature can degrade gracefully, `Require<T>()` when it cannot. Both are called **from the UI
layer**; hand the result to models and services as plain delegates ([Application architecture](app-architecture.md)).

## Built-in capabilities

| Interface | Provides | Typical use |
|---|---|---|
| `IUiDispatcherCapability` | `Post(Action)` onto the UI thread | Bringing background results back ([Side effects](side-effects.md)) |
| `IWindowMetricsCapability` | `Current` (`Width`, `Height`, `Density`, safe areas) and `Changed` | Responsive decisions, safe-area padding |
| `ILayoutDirectionCapability` | `IsRightToLeft`, `Changed` | RTL ([Localization](localization.md)) |
| `IMotionPreferencesCapability` | `ReducedMotion` | Honouring reduced motion ([Animations §9](animations.md)) |
| `ITextInputCapability` | Native text input sessions | Used by `TextField` / `TextArea` |
| `IClipboardCapability` | Read and write the clipboard | Copy / paste |
| `INavigationPlatformCapability` | Platform navigation conventions | Back handling |
| `IMediaPlaybackCapability`, `IMediaPreloadCapability`, `IMediaThumbnailCapability`, `INativeVideoOutputCapability` | Audio/video playback, preload, thumbnails, native video surface | `Native.CreatePlayer(MediaKind.Video)` ([Media](media-controller.md)) |

Example, reacting to window size:

```csharp
var metrics = Native.Get<IWindowMetricsCapability>();
bool wide = metrics is { Current.Width: >= 600 };
metrics!.Changed += m => { if (IsActive) Rebuild(); };
```

## Writing your own capability

Follow the recipe from the architecture notes, with an example: a *haptics* capability.

**1. Define the contract** in portable code, with no platform types:

```csharp
public interface IHapticsCapability : INativeCapability
{
    bool IsSupported { get; }
    void Tap();
}
```

**2. Decide whether absence is acceptable.** Haptics are optional: consumers call `Native.Get<IHapticsCapability>()`.

**3. Implement it per platform**, in the platform folders of your project (`Platforms/android`, `Platforms/ios`) or in a
platform library:

```csharp
// Platforms/android/AndroidHaptics.cs
public sealed class AndroidHaptics(Android.Views.View view) : IHapticsCapability
{
    public bool IsSupported => true;
    public void Tap() => view.PerformHapticFeedback(Android.Views.FeedbackConstants.VirtualKey);
}
```

**4. Register it before the first composition**, where the host creates the `NativeTunnel`:

```csharp
native.Register<IHapticsCapability>(new AndroidHaptics(hostView));
```

`Register<T>` holds one implementation per interface.

**5. Define the contract's rules**: which thread calls it (usually the UI thread), its lifetime, whether it needs a
**permission** (declaring it in the manifest is not asking for it: see [Publishing §3](publishing.md#3-permissions)), and
how it reports errors.

**6. Use it:**

```csharp
Native.Get<IHapticsCapability>()?.Tap();
```

**7. Test it** with a fake implementation registered in a test (the contract is just an interface).

### Design guidelines

- **Asynchronous work** takes a `CancellationToken` and returns a typed result, never an opaque command.
- **No platform type** in the interface (`Android.Views.View`, `UIView`): keep it portable.
- **Events** (`Changed`) are raised on the UI thread, or documented otherwise.
- **Permissions and user interaction** (camera, notifications) need an explicit, awaited request flow; a capability
  that "just works" without permission will fail on devices.
- Prefer a **small, specific** interface to a general one.
- Say precisely what each platform supports, as the framework's own contracts do.

### Limits

`NativeTunnel` does not yet have a general convention for permissions, asynchronous streams or activity/scene lifecycle
([Text, IME and NativeTunnel §11](architecture/text-input-and-native.md)). Model these
explicitly in your own contract.
