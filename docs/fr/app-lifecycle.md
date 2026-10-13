# Application Lifecycle & Navigation

> **Document historique.** Utilisez le [guide développeur](developer-guide.md#8-navigation-et-retour-natif) et la [référence runtime](architecture/runtime.md#10-navigation) pour le contrat actuel. Ce fichier peut contenir des hypothèses antérieures.

This document traces the complete lifecycle of a XenoUI application — from OS launch to pixels on screen.

---

## Boot Sequence

```mermaid
sequenceDiagram
    participant OS as Operating System
    participant Entry as Entry Point
    participant Platform as Platform XenoView
    participant Discovery as XenoAppDiscovery
    participant App as Your App Class
    participant Engine as XenoEngine
    participant Compose as Compose

    OS->>Entry: Launch app
    Note over Entry: Android: MainActivity.OnCreate()\niOS: SceneDelegate.WillConnectTo()

    Entry->>Platform: new XenoView()

    Platform->>Engine: new XenoEngine()
    Platform->>Engine: SetDensity(screenDensity)

    Platform->>Discovery: XenoAppDiscovery.Find()
    Note over Discovery: Scans all loaded assemblies\nfor concrete XenoApp subclass
    Discovery-->>Platform: returns App instance

    Platform->>App: app.Engine = engine
    Platform->>App: app.OnRebuildRequested = Invalidate

    Platform->>App: InitialBuild()
    App->>App: OnStart()
    Note over App: Page of HomePage\nPage of SettingsPage\nNavigate to HomePage

    App->>Compose: Mount(engine)
    App->>App: RootEntityId = Body()
    Note over App: Body() calls ActivePage.Body()\nwhich creates entity tree

    Platform->>Platform: First Refresh() on vsync
```

### Step-by-Step

1. **OS launches the app** → platform entry point runs (`MainActivity.OnCreate` on Android, `SceneDelegate` on iOS)
2. **XenoView is created** → this is a SkiaSharp surface view (`SKGLSurfaceView` on Android, `UIViewController` + `SKGLView` on iOS)
3. **XenoEngine is created** → allocates all 7 slabs in `XenoUICacheMemory` (default capacity: 1024 entities)
4. **Screen density is set** → `DisplayMetrics.Density` (Android) or `UIScreen.MainScreen.Scale` (iOS)
5. **XenoAppDiscovery.Find()** → one-time assembly reflection scan. Finds the single concrete `XenoApp` subclass. Throws if 0 or >1 found.
6. **Engine is wired to App** → `app.Engine = engine`, `app.OnRebuildRequested = Invalidate` (platform redraw callback)
7. **InitialBuild()** → calls `OnStart()` (page registration + initial navigation), then `Compose.Mount(engine)`, then `Body()` to create the entity tree
8. **First vsync** → platform calls `Refresh()` → Layout → Render → pixels on screen

---

## Page Registration & Navigation

### Registration

Pages are registered once in `OnStart()` and **reused** across navigations:

```csharp
public class App : XenoApp
{
    public override void OnStart()
    {
        // Instantiate once, store in registry
        Page<HomePage>();       // new HomePage() → _pages[typeof(HomePage)] = page
        Page<SettingsPage>();   // new SettingsPage() → _pages[typeof(SettingsPage)] = page

        // Push initial page and build
        Navigate<HomePage>();   // _navStack.Add(page) → Rebuild()
    }
}
```

### Navigation Stack

```mermaid
stateDiagram-v2
    [*] --> HomePage: Navigate of HomePage

    HomePage --> SettingsPage: Navigate of SettingsPage
    SettingsPage --> HomePage: GoBack()

    state HomePage {
        [*] --> HomeBody: Body()
        HomeBody --> HomeBody: Rebuild()\n(counter++, etc.)
    }

    state SettingsPage {
        [*] --> SettingsBody: Body()
        SettingsBody --> SettingsBody: Rebuild()\n(toggle, etc.)
    }
```

**Key behaviors:**
- `Navigate<T>()` pushes page onto `_navStack`, triggers `Rebuild()`
- `GoBack()` pops the stack, triggers `Rebuild()`
- Page instances are **reused** — state (counters, toggles, etc.) survives across navigations
- `ActivePage` is always the last item in `_navStack`

### Navigation Internals

```csharp
// Inside XenoApp:
public void Navigate<TPage>() where TPage : XenoPage
{
    if (!_pages.TryGetValue(typeof(TPage), out var page))
        throw new InvalidOperationException($"Page {typeof(TPage).Name} not registered.");

    _navStack.Add(page);   // push
    Rebuild();              // full ECS reset + re-render
}

public void GoBack()
{
    if (_navStack.Count > 1)
    {
        _navStack.RemoveAt(_navStack.Count - 1);  // pop
        Rebuild();
    }
}
```

---

## State Management (Current)

State is currently stored as fields on `XenoPage` subclasses:

```csharp
public class HomePage : XenoPage
{
    private int _counter = 0;      // ← state lives here
    private bool _darkMode = false; // ← survives across navigations

    public override XenoComponent Body() =>
        Column(
            Text($"Count: {_counter}").FontSize(28),
            Box(120, 50)
                .Background(0xFF4CAF50)
                .CornerRadius(8)
                .Clickable(() => { _counter++; Rebuild(); })
        ).Padding(20);
}
```

### State Change Flow

```mermaid
flowchart LR
    A["User taps button"] --> B["Clickable callback fires"]
    B --> C["_counter++"]
    C --> D["Rebuild()"]
    D --> E["Engine.Reset()\nClear all 7 slabs\n(buffers reused)"]
    E --> F["Compose.Mount(engine)"]
    F --> G["Body() re-creates\nentire entity tree"]
    G --> H["OnRebuildRequested()\nInvalidate/SetNeedsDisplay"]
    H --> I["Next vsync:\nLayout → Render"]
```

**Pattern**: Mutate state → call `Rebuild()` → entire entity tree reconstructed → platform redraws.

**Why this works**: `Slab.Clear()` just resets `_count = 0` without freeing memory. Rebuilding ~100 entities costs microseconds because it's just a series of `Push()` calls into pre-allocated native arrays.

---

## Platform Integration

### Android

```mermaid
graph TB
    subgraph Android["Android Platform"]
        MA["MainActivity\n(injected by SDK)"]
        XV["XenoView\nextends SKGLSurfaceView"]
        GL["OpenGL ES Surface"]
        Touch["OnTouchEvent()"]

        MA -->|"SetContentView"| XV
        XV -->|"OnPaintSurface"| GL
        XV -->|"OnTouchEvent"| Touch
    end

    subgraph Core["XenoUI Core"]
        Engine["XenoEngine"]
        Refresh["Refresh(canvas, w, h, rootId)"]
        Pointer["HandlePointerDown/Up"]
    end

    GL -->|"SKCanvas"| Refresh
    Touch -->|"logical coords"| Pointer
    Refresh --> Engine
    Pointer --> Engine
```

| Aspect | Implementation |
|---|---|
| **View** | `SKGLSurfaceView` (GPU-accelerated SkiaSharp) |
| **Density** | `context.Resources.DisplayMetrics.Density` |
| **Invalidation** | `Invalidate()` triggers next `OnPaintSurface` |
| **Render Loop** | `OnPaintSurface` calls `Invalidate()` at end (continuous vsync) |
| **Touch** | `OnTouchEvent` → convert to logical coords (÷density) → `HandlePointerDown/Up` |

### iOS

```mermaid
graph TB
    subgraph iOS["iOS Platform"]
        SD["SceneDelegate\n(standard UIKit)"]
        XV["XenoView\nextends UIViewController"]
        SKGL["SKGLView\n(GPU-accelerated SkiaSharp)"]
        Touch["TouchesBegan/Ended/Cancelled"]

        SD -->|"RootViewController"| XV
        XV -->|"LoadView"| SKGL
        XV -->|"Touch events"| Touch
    end

    subgraph Core["XenoUI Core"]
        Engine["XenoEngine"]
        Refresh["Refresh(canvas, w, h, rootId)"]
        Pointer["HandlePointerDown/Up"]
    end

    SKGL -->|"PaintSurface event"| Refresh
    Touch -->|"logical coords"| Pointer
    Refresh --> Engine
    Pointer --> Engine
```

| Aspect | Implementation |
|---|---|
| **View** | `UIViewController` with `SKGLView` child (Metal-backed) |
| **Density** | `UIScreen.MainScreen.Scale` |
| **Invalidation** | `_skglView.SetNeedsDisplay()` triggers next paint |
| **Scene** | Standard `UIWindowSceneSessionRoleApplication` configuration |
| **Touch** | `TouchesBegan/Ended/Cancelled` → logical coords → `HandlePointerDown/Up` |
| **App Delegate** | Standard `UIApplicationDelegate` with scene configuration |

### Platform Abstraction

The core engine (`XenoEngine`, `LayoutSystem`) never references SkiaSharp or any platform API. The abstraction boundary:

```
                    Core (platform-agnostic)
                    ┌──────────────────────────┐
                    │ LayoutSystem              │
                    │   TextMeasurer = callback │  ← injected
                    │ RenderSystem              │
                    │   uses SKCanvas           │  ← SkiaSharp (cross-platform)
                    │ InputSystem               │
                    │   logical coordinates     │  ← platform converts
                    └──────────────────────────┘
                              ▲
                    ┌─────────┴─────────┐
             ┌─────┴─────┐       ┌─────┴─────┐
             │  Android   │       │    iOS     │
             │ XenoView   │       │ XenoView   │
             │ (density,  │       │ (density,  │
             │  touch,    │       │  touch,    │
             │  vsync)    │       │  vsync)    │
             └───────────┘       └───────────┘
```

---

## Reusable Components

Components are **plain classes** with a `Body()` method — no base class required:

```csharp
public class HeaderBar
{
    private readonly string _title;

    public HeaderBar(string title) => _title = title;

    public XenoComponent Body() =>
        Row(
            Text(_title).FontSize(20).TextColor(0xFFFFFFFF)
        ).Padding(16, 12).Background(0xFF1976D2);
}
```

Used inside pages:

```csharp
public override XenoComponent Body() =>
    Column(
        new HeaderBar("Home").Body(),    // ← reusable component
        Text($"Count: {_counter}"),
        // ...
    );
```

---

## File Organization (Swift-Inspired)

```
MyApp/
├── MyApp.csproj          ← imports Sdk.props + Sdk.targets
├── App.cs                ← XenoApp subclass (one per app)
├── Pages/
│   ├── HomePage.cs       ← one file per page
│   ├── SettingsPage.cs
│   └── ProfilePage.cs
└── Components/
    ├── HeaderBar.cs      ← reusable UI components
    ├── TabBar.cs
    └── Card.cs
```

Each page is self-contained with its own state and `Body()`. Components are composable building blocks. This pattern follows the Swift/SwiftUI convention of one file per screen.

---

## Lifecycle Summary

```mermaid
graph TD
    A["App Launch"] --> B["XenoView Created"]
    B --> C["XenoEngine Created\n(7 slabs allocated)"]
    C --> D["XenoAppDiscovery.Find()\n(assembly scan)"]
    D --> E["InitialBuild()\nOnStart → Page → Navigate → Body"]
    E --> F["Vsync Loop"]

    F --> G{"Dirty flags?"}
    G -->|"Measure/Children"| H["LayoutSystem.Measure()"]
    G -->|"Arrange/Children"| I["LayoutSystem.Arrange()"]
    G -->|"Visual only"| J["Skip layout"]
    G -->|"None"| K["Skip layout"]

    H --> I --> L["RenderSystem.Render()"]
    J --> L
    K --> L

    L --> F

    M["User Touch"] --> N["InputSystem\nHit test → callback"]
    N --> O["State change + Rebuild()"]
    O --> P["Engine.Reset()\nClear slabs"]
    P --> Q["Body() rebuilds tree"]
    Q --> F
```
