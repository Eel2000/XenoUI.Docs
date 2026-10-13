# XenoUI documentation

[Repository home](https://github.com/Eel2000/XenoUI/blob/master/README.md) · [Developer guide](developer-guide.md) · [Troubleshooting](troubleshooting.md) · [Architecture](architecture/index.md)

This index is the official entry point to the repository documentation. It separates the current reference documents from historical notes, so that an old plan is not mistaken for the behaviour of the code.

## Recommended paths

### Application developer

1. [Installation](installation.md): packages, SDK, templates, the `xeno` tool and the Visual Studio previewer, from nuget.org or a local feed.
2. [Developer guide](developer-guide.md): creating a project, API, state, navigation, assets, text, devices and IDEs.
3. [Troubleshooting by symptom](troubleshooting.md): missing device, build, blank screen, scrolling, keyboard, assets and navigation.
4. [Navigation and NavigationScaffold](navigation-scaffold.md): stacks, transitions, adaptive navigation, FAB, center action, shapes and RTL.
5. [`SampleApp`](https://github.com/Eel2000/XenoUI/tree/master/samples/SampleApp): runnable demonstration of the current capabilities.
6. [Xeno.SDK](xeno-sdk.md): NuGet SDK, local feed, out-of-repository project and build customisation.
7. [API demo](sample-api.md): portable HTTP, remote list, cancellation and tests, without SQLite.
8. [Constrained text and typed parameters](text-layout-navigation.md): wrap, ellipsis, AutoSize, navigation payloads and the space reserved for the BottomBar.
9. [Visual Studio](visual-studio.md): project and item templates, `[Preview]` and the XenoUI Preview window.

### Framework contributor

1. [Architecture overview](architecture/index.md).
2. [Runtime, composition and rendering](architecture/runtime.md).
3. [Navigation and ECS scenes](architecture/navigation-and-scenes.md).
4. [Text, IME and NativeTunnel](architecture/text-input-and-native.md).
5. [SDK, platforms and tooling](architecture/tooling-and-platforms.md).
6. [Decisions, debt and the V1 trajectory](architecture/decisions-v1.md).
7. [A15 benchmark](runtime-benchmark-a15.md): Release mobile reference, CPU/OpenGL thread, allocations and startup; [comparative PC baseline](runtime-benchmark.md).
8. [Stabilisation tracking](runtime-critical-stabilization.md): verified fixes and gates still open.
9. [Video and audio players plan](media-playback-implementation.md): portable foundation, Android foreground audio, GPU interop and the gates before the Shorts feed. [Media controller](media-controller.md): API, SDK, threading and current limits.
10. [Android graphics recovery](android-graphics-lifecycle.md): EGL context loss, GPU ownership and keeping scenes after the screen is locked.

## Document status

| Document | Status | Role |
|---|---|---|
| [Installation](installation.md) | **Canonical, local preview** | Install and update packages, SDK, templates, `xeno` and the previewer |
| [Developer guide](developer-guide.md) | **Canonical** | Public usage contract as found in the current code |
| [Xeno.SDK](xeno-sdk.md) | **Canonical, local preview** | Configuration and distribution of the MSBuild SDK |
| [Visual Studio](visual-studio.md) | **Canonical, local preview** | Templates, `[Preview]` and the XenoUI Preview extension |
| [Navigation and NavigationScaffold](navigation-scaffold.md) | **Canonical** | Navigation API, transitions, Scaffold and lifecycle |
| [Troubleshooting](troubleshooting.md) | **Canonical** | Operational diagnosis |
| [Architecture module](architecture/index.md) | **Canonical** | Internal reference and contribution guide |
| [Runtime benchmark](runtime-benchmark.md) | Dated local measurements | CPU/allocation baseline of the tested checkpoint; not a mobile guarantee |
| [A15 benchmark](runtime-benchmark-a15.md) | Measured mobile reference | CPU, OpenGL thread, startup and thermal conditions on SM-A155F |
| [Runtime stabilisation](runtime-critical-stabilization.md) | Checkpoint tracking | Tests, fixes and remaining work before full validation |
| [Video and audio players](media-playback-implementation.md) | Android audio validated; reference native video | Architecture, phases and media validation before the Shorts demo |
| [SDK and project setup](sdk-and-project-setup.md) | Historical | Old local imports; use the Xeno.SDK guide |
| [Flutter-style text input](flutter-style-text-input.md) | Design note | Research that guided the IME tunnel; not a public contract |
| [Text input plan](text-input-implementation.md) | Historical | Proposal that predates the current implementation |
| [Historical architecture](architecture.md) | Superseded | Kept for traceability; use the architecture module |
| [Historical lifecycle](app-lifecycle.md) | Historical | May contain outdated details |
| [Historical V1 roadmap](v1-roadmap-and-architecture.md) | Historical | Initial intentions, not the official progress status |

## Source of truth

When documents contradict each other, apply this order:

1. code and tests of the current branch;
2. canonical documentation;
3. design notes;
4. historical plans and roadmaps.

The repository is pre-V1: an observed behaviour is only stable if a test covers it and it is explicitly presented as a contract. Known limitations are gathered in [Decisions and the V1 trajectory](architecture/decisions-v1.md#3-technical-debt-and-risks).

## Conventions

- Public dimensions are in logical units: Android dp, iOS points.
- Colours are ARGB integers `0xAARRGGBB`.
- Text positions use UTF-16 offsets, like the native Android/iOS APIs.
- Commands are shown from the repository root.
- PowerShell and Bash blocks point out syntax differences where they matter.
