# Video and audio players — implementation plan

[Documentation](index.md) · [Architecture](architecture/index.md) · [NativeTunnel](architecture/text-input-and-native.md)

**Status: portable foundation, audio and the reference Android native video flow validated on the A15.** Research and checkpoint of 6 October 2026, branch `codex/media-playback`. The iOS/GPU/Shorts gates remain open.

The Shorts page requested in SampleApp is postponed until the real players are validated. The keyboard/TextInput fixes are published separately in commit `175054c`. No video/audio decoder performance result has been measured yet.

### Portable foundation checkpoint

- Optional `XenoUI.Media`, independent of Android/iOS; `IMediaPlaybackCapability` and `IMediaPlaybackSession` contracts in NativeTunnel.
- Persistent controller with user intent distinct from suspension, cached struct snapshots, generational handle and source revision.
- Play/pause/stop/seek/configuration and idempotent dispose; commands restricted to the owner thread, with no developer callbacks or native polling when reading the snapshot.
- 23/23 media tests with a simulated backend; 122/122 Core tests. The test of 100,000 reads/synchronisations after warm-up measures **0 managed bytes on the tested thread**. This result covers neither JNI/ObjC, nor the decoder, nor video rendering.
- SDK opt-in `XenoEnableMedia`: portable module and `XenoUI.Media.Android` adapter; no Android adapter in the iOS references. Eight SDK evaluations source/package, enabled/disabled, Android/iOS pass.
- Media3 1.11.1: HTTPS sources and local files, bounded extraction of embedded assets, play/pause/stop/seek, volume/speed/loop, focus handled by Media3, noisy stop and arbitration of one audible session.
- A native control pump at 250 ms per host, pre-allocated runnable; SampleApp labels follow seconds and states through reactive bindings, without rebuilding the page. The pump stops when sessions no longer need tracking or when the host goes to the background.
- AudioLab added to the common BottomBar/Drawer/Rail destinations, controller created on first activation only. Local PCM track generated on click and an official demo HTTPS MP3; no autoplay.
- The Android test revealed a navigation/lifecycle commit on the GLThread. Hosts now dispatch deferred commits and structural drawer actions to the control thread; old entries/dispose invalidate pending commits.
- The test destroying a second Activity also revealed a premature release of `XenoView`'s Java peer. `Shutdown()` releases the engine after the GL thread stops, idempotently, without invalidating the view still held by Android while the window detaches. Late callbacks no longer touch released resources.

**Open gates:** AVPlayer adapter and iOS validation, physical/headphone/Bluetooth interruptions, full benchmark of the pump/JNI and the navigation handoff, video transport to the ECS slabs, video/GPU outputs and background service. Phases 0 and 1 are therefore not declared complete on both platforms. See the [contract and usage rules](media-controller.md).

### Reference native video increment

- `MediaView.Video(controller, height)` reserves a fixed rectangular ECS viewport. The `MediaKind.Video` controller stays persistent, with the commands and suspension policies of the common foundation.
- Android: a non-interactive `TextureView` above the GL host, XenoUI controls outside the viewport, Media3 wired directly to the texture. Pixels go neither through C# nor Skia. At most two media sessions, of which only one video.
- Placement in structs, bounded mailbox, coalescing; geometry changes applied on the main thread, no native placement call in the renderer. The rectangle follows the layout constraints and the video dimensions are used for aspect-fit.
- Output hidden during transitions/drawer gestures, and under scrollable/rounded ancestors or non-identity matrices. The settled frame republishing the rectangle is requested only once. This fallback does not promise the effects of the future GPU backend: no overlay in the clip, scrollable feed, shared elements or animated clipping.
- `VideoLabPage` and a Video destination shared by BottomBar/Drawer/Rail. Official Android MP4 demo, load without autoplay, Play/Pause/Stop/Seek, mute/repeat and explicit rebuild. Fixed viewport, separate scrollable controls.
- Tests added: placement, removal, unsupported constraints, coalescing and stable-frame allocations; video API rejecting an audio controller. Adapter compilation: zero warnings and errors.
- Video suites checkpoint: 123/123 Core, 24/24 media and eight SDK source/package Android/iOS evaluations. These tests do not measure decoding or JNI allocations.

The first frame actually presented, versioned posters, a replaced source without a flash of the old frame and surface/GPU context loss remain to be hardened. Phase 2 is not declared complete; phase 3 and the TikTok page are still open.

A15 video validation: fully rebuilt final APK, zero warnings/errors. MP4 frames genuinely visible, Playing state 1280×720, duration 10 s. Pause and seek +2 s, explicit rebuild then Home→Video keeping the pause at 2 s. Drawer opened without video above the menu, Video selected and surface reattached after closing. Stop returns to Idle with a black viewport and the native output hidden. No new crash for the process of this flow (`logcat -b crash`). The initial network load suffered buffering; no throughput/latency measurement or A/V guarantee is derived from these captures. The Mali chroma siting warnings observed are a separate colour validation topic.

### Physical Android validation — 6 October 2026

Device: Samsung Galaxy A15 SM-A155F, Android 16. Full Release APK, without fast deployment, final build: zero warnings and zero errors. Tests run with the `android-cli` skill, captures inspected visually and crash logs consulted.

- 20 s local PCM track: play, pause, seek +5 s; returning via tab and drawer keeps the position and the explicit pause.
- Going to Home during local playback: AudioTrack observed paused in `dumpsys audio`, then the Play intent resumed on return to the original activity.
- Google HTTPS MP3: Playing state and 59 s duration observed; mute and 1.5× speed commands accepted and displayed. This test is not an acoustic measurement nor a validation of every audio route.
- Two explicit UI rebuilds: HTTPS source, pause at 41 s and settings kept; Restart/Play then Stop commands still usable.
- Reproducing the creation then destruction of a second Activity: back to AudioLab, same process, no new crash in its buffer; old detach crash fixed.
- Return from Home through a MAIN/LAUNCHER intent: existing task brought back to the foreground, without creating an extra activity. Track stopped at the end of the tests.

The captures are ignored local artefacts, not a dependency of the GitHub documentation. This short check does not replace a soak test, real interruptions or native allocation benchmarks.

## 1. Structuring decisions

- Reuse the native engines, without rewriting codecs: AndroidX Media3/ExoPlayer on Android, AVFoundation/AVPlayer on iOS. The same playback foundation for audio and video sources; audio alone creates no video surface.
- Keep visual controls in XenoUI: buttons, progress, buffering, volume, subtitles and replaceable overlays. Do not impose `PlayerView` or `AVPlayerViewController` as the public interface.
- Separate the persistent controller, the native session and the ECS visual entity. Recycling a card or rebuilding `Body()` must not recreate its player or lose its position.
- Route commands/capabilities through NativeTunnel; do not introduce Android, UIKit, Media3 or AVFoundation types into Core or applications.
- Do not present "zero GC" as a property of codecs: measure .NET, Java/ART, native memory, GPU, network and decoding separately.
- Video output composable in Skia is a prerequisite gate for the animated feed, not a detail to postpone until after the page is built.

Media3 allows video to be connected to a surface independently of the controls and recommends not creating a surface for audio only. The SurfaceView/TextureView choice has real trade-offs in power consumption, synchronisation and protected output. [Media3 surfaces](https://developer.android.com/media/media3/ui/surface).

AVPlayer is the proposed foundation for audiovisual content; AVPlayerItemVideoOutput provides a way to retrieve frames for custom composition. [AVFoundation playback](https://developer.apple.com/documentation/avfoundation/media-playback).

## 2. V1 scope

### Common

- Embedded assets, permitted local files, HTTPS URLs.
- MP4 H.264/AAC for reference video; MP3 and AAC/M4A for reference audio.
- Play, pause, stop, seek, resume, loop, volume, mute, speed according to native capabilities.
- Position, known or unknown duration, seekable range, buffering, end and typed errors.
- HTTP headers, timeout, cancellation and bounded retries, without secrets in the logs.
- HLS streaming after progressive playback is validated; formats/capabilities queried, no "all codecs/all devices" promise.
- Sources with stable identity and versioned requests; changing source during prepare/seek/dispose supported.

Containers and codecs must be distinguished: an MP4 extension does not guarantee that all its tracks decode on every device. [Media3 formats](https://developer.android.com/media/media3/exoplayer/supported-formats).

### Video

- Poster kept until the first frame is actually presented, not just until `Ready`.
- Fit/fill/crop modes, orientation, ratio, clipping and overlaid controls.
- Full screen handled by the navigation architecture, without a second player.
- Surface change/recreation and GPU context loss.
- One active audible clip; preparing the next one under an explicit budget.
- Simple WebVTT subtitles, with an optional track and customisable rendering; typography/bidi through the paragraph engine. Do not decode/recompose cues within the frame.

### Audio

- Player without a video entity: local and remote music/podcasts.
- Playlist, previous/next, repeat none/one/all and relative seek.
- Arbitration between music, video and other applications; explicit pause, ducking or mix policy.
- Interruption by a call, focus loss, unplugged headphones, Bluetooth route change and locking.
- Optional background playback, lock screen controls and system metadata.
- The feed and the music player do not become two competing owners of audio focus.

Background requires a media session and service on Android, and a suitable audio/background configuration on iOS; it is not a simple boolean in the renderer. [MediaSessionService](https://developer.android.com/media/media3/session/background-playback), [iOS audio configuration](https://developer.apple.com/documentation/avfoundation/configuring-your-app-for-media-playback).

## 3. Separation of responsibilities

| Layer | Responsibility | Forbidden |
|---|---|---|
| Proposed portable `XenoUI.Media` module | Sources, controllers, playlists, policies, presentation model | Platform dependency, decoding |
| Core / ECS-DOD | State slabs, generational handles, geometry, composition and hit-testing | Native player, HTTP/decoding in the frame |
| NativeTunnel | Playback, output and capability contracts; command/snapshot transports | Native types in the public API |
| Android adapter | Media3, surfaces, audio focus, session/service | Imposed control UI |
| iOS adapter | AVPlayer, video output, AVAudioSession, Now Playing | Two competing global audio sessions |
| SampleApp | Composition of controls and pressure scenarios | Private slab access, ad hoc native orchestration |

`XenoUI.Media` must stay optional. Extend Xeno.SDK to include its Android/iOS dependencies and configurations only if the feature is enabled. The NativeTunnel registry can stay a cold-path dictionary; its capabilities are resolved once on open, never per frame.

Contracts to design before implementing them:

- `MediaSource`: asset/file/HTTPS, identity, metadata and a reference to network options.
- `MediaController`: persistent object controlling a session; `VideoController` and `AudioController` as specialised facades if that improves the API.
- `IMediaPlaybackCapability` / `IMediaPlaybackSession`: prepare/play/pause/seek/configure/release.
- `IVideoOutputCapability`: acquiring/attaching/detaching an output, with documented capabilities.
- `MediaHandle`: index + generation; `SourceRevision` for events from an old source on a reused session.
- `MediaSnapshot`: struct with playback, position, duration, buffer, dimensions, effective state and error identifier.
- `AudioPolicy`: music/video intent, background, interruption and arbitration.

The exact API names will be settled after the output prototype. Simple usage must remain: create a controller, provide a source, compose `Video(...)` or `AudioControls(...)`. Custom slots are evaluated at composition, not per frame.

## 4. Video and rendering: mandatory prototype

The current hosts use SKGLSurfaceView / SKGLView. Do not assume that a Media3 texture or an Apple pixel buffer can be imported directly by SkiaSharp.

### A. Native output — reference and explicit fallback

Android SurfaceView/TextureView and iOS AVPlayerLayer first make it possible to validate decoding, sound, seek and the first frame. Test in the real XenoUI hierarchy: transparent surface, z-order, overlays, scroll, IME, drawer and two scenes during a transition.

This path must expose its limits for clipping, transforms and effects. A fallback must not silently remove the requested effects. Protected content may require a secure output rather than an importable texture.

### B. Composable GPU output — target for Shorts

- Android: Surface/SurfaceTexture to an external texture, then GPU import or blit to a Skia-compatible 2D texture.
- iOS: AVPlayerItemVideoOutput to CVPixelBuffer, then interop with the actual backend; OpenGL for the existing host if viable, Metal only with a compatible backend, not by assumption.
- A minimal native prototype if the .NET bindings create wrappers per frame; transport of opaque handles, no new managed instances at 30/60 Hz.
- UV, rotation, YUV/RGB conversion, SDR gamut/colour, timestamps and A/V sync validated.
- No conversion to `byte[]`, SKBitmap or CPU copy of the pixels on each frame.
- "No CPU copy" is not "zero-copy": measure blits, synchronisations and GPU costs.
- Frames held by bounded leases, released after the fence/end of consumption; not on the immediate destruction of a scene.

Deliverable: a documented decision per platform on the chosen backend, a matrix of the effects actually supported, measurements and limitations. If GPU composition fails, deliver only the limited native output and explicitly block the promise of a fully animatable feed.

## 5. Cold path, hot path and threads

### Cold / control path

Resolving sources, creating the controller/player, configuration, metadata, network connections, loading the poster, binding commands, creating slots and structural changes. Allocations are allowed, bounded and measured.

Native commands run on the thread each backend requires, never directly from the OpenGL thread. Pre-registered listeners; no new closures/tasks per frame. Developer callbacks go through the application dispatcher outside rendering.

### Hot path

Read a published snapshot, interpolate a numeric progress, walk the slabs, apply the same matrices as the InputSystem and compose the latest available frame. No `Body()`, stable layout, HTTP access, capability resolution, text formatting or developer callback per frame.

- Bounded struct queues for commands/events, with explicit producer ownership. Do not choose SPSC if several native threads write directly.
- Coalesce position/buffering and stale frames; keep the order of critical commands. Stop/dispose/error must never be lost when the queue is full.
- The renderer handles neither textual errors nor structural changes; a control-path consumer publishes the states into the ECS.
- SourceRevision and generation reject late events after recycling, a source change or dispose.
- No new timer per card. Use the existing scheduler for the UI and new-frame signals.
- 30 Hz video on a 120 Hz screen: reuse the frame, do not import the same frame four times.
- Audio without UI/visible progress: request no UI frame to keep playback going.
- Time labels are refreshed outside rendering only at the displayed precision, not formatted at 60/120 Hz.

Gate: zero managed allocation attributable to media rendering/interpolation after warm-up. Also measure the control/event path over a full window; do not hide regular allocations under the "cold" label. Internal allocations of the native player are measured separately.

## 6. Lifecycle and ownership

States: Idle, Preparing, Ready, Buffering, Playing, Paused, Seeking, Ended, Failed, Disposed. Keep the user intent and the effective state separate: a buffer shortage or an interruption does not mean the user asked for Pause.

- `XenoPage.OnDeactivated`: feed video suspended and output detached; position kept.
- A rebuild/recomposition changes the entity, not the controller's identity.
- A virtualised card owns an output lease, not a permanent player of its own.
- `OnBackground`: feed stopped; audio continues only with opt-in and a valid system configuration.
- On return, resume only if the intent is still Play, the source valid, the page/policy eligible and focus available.
- End of interruption: no resume after an explicit pause that happened in the meantime.
- `OnLowMemory`: release preloading, unused posters and optional caches before the active player.
- Idempotent dispose, removal of observers, invalidation of handles, ordered native and GPU release.
- A music session can belong to the application/service and outlive a page; it does not outlive one by accident through a forgotten reference.

Native arbitration must not be duplicated: let Media3 handle audio focus when configured to do so, then reflect its decisions in the common model. On iOS, a coordinator configures the single AVAudioSession according to the active uses. [Android audio focus](https://developer.android.com/media/optimize/audio-focus), [iOS interruptions](https://developer.apple.com/documentation/avfaudio/handling-audio-interruptions).

## 7. Feed budget, cache and network

- 10,000 posts = data/identities, not 10,000 players, textures or downloads.
- One audible player; an initial maximum pool of two video sessions, reduced to one depending on resources and decoding capabilities. The second player is not a guarantee on every device.
- Prepare the next clip according to visibility and direction; do not decode the whole LazyColumn overscan.
- Priorities: visible > next > previous; cancel/coalesce requests during fast swipes.
- Preloading compressed data is distinct from pre-decoding; explicit budgets for RAM, GPU, buffers and disk.
- Bounded cache with eviction and identities that take authorisation into account; no leaks between accounts.
- Distinguish network stop, invalid source, unsupported format, refused HTTP and decoder unavailable.
- Media networking stays the playback backend's responsibility; do not download a whole stream in C# to simulate a local source.
- Snapping/pager and the orchestration of likes/stagger are separate UI work, not decoder features.

## 8. Phases and gates

| Phase | Deliverable | Exit condition |
|---|---|---|
| 0 — Contracts and dependencies | States, ownership, capabilities, module structure, compatible .NET/Media3 versions | Android build; iOS API availability validated with the target SDK/workload |
| 1 — Foreground audio | MP3/M4A asset and HTTPS, play/pause/seek/volume/loop, focus and interruptions | Core tests with a simulated backend + real playback on both platforms |
| 2 — Reference native video | MP4 asset and HTTPS, poster/first-frame, ratio and synchronised sound | Surface recreation, navigation/Back, lifecycle without crash or ghost sound |
| 3 — GPU interop | Skia-composable backend and leases; documented fallback | Reliable overlays/clip/transitions + no CPU copy and per-frame allocations measured |
| 4 — Streaming and UI control | HLS, buffering, errors/retry, seekable ranges, tracks and accessible slots | Deterministic server tests, degraded network and fast commands |
| 5 — Background audio | Android session/service; iOS audio session, background modes and remote commands | Locking, headphones, Bluetooth, interruptions and user stop validated |
| 6 — Shorts feed and AudioLab | Common tabbar/drawer/rail entries, a real clip and a real audio track | Virtualisation + bounded pool + no playback outside the visibility policy |
| 7 — Stabilisation | API docs, pressure scenario, traces and reproducible measurements | Runtime/memory gates validated; limitations explicitly published |

Implement the phases in reviewable increments on `codex/media-playback`. Do not take the absence of an iOS device/toolchain for an iOS validation: report the open gates separately.

### SDK configuration to plan

Official Media3 bindings from the .NET Android repository, consistent versions across the common/exoplayer/HLS/session modules, without including `media3-ui` if not needed. [.NET Android inventory](https://github.com/dotnet/android-libraries/blob/main/docs/artifact-list-with-versions.md).

Xeno.SDK handles, opt-in, media assets, network permissions, the mediaPlayback service/foreground service and its Android declaration, as well as iOS background modes. Artwork metadata stays bounded and outside rendering. iOS system controls through Now Playing and RemoteCommandCenter. [Apple remote commands](https://developer.apple.com/documentation/mediaplayer/remote-command-center-events).

## 9. Mandatory tests and measurements

### Core contracts / fake backend

- Double dispose, source replaced during prepare, seek success/error after a source change.
- Events from an old generation; fast pause/play; saturated queue without losing stop.
- Video/audio arbitration, focus loss, user pause during an interruption, conditional resume.
- Controller kept after rebuild, output released after recycling, resource fence respected.
- Live source/unknown duration without division by zero; out-of-range seek; nullable metadata/error.
- No `Body()`/layout/formatter/slot callback in a stable frame.

### Physical devices

- A15, Note20 and an Android tablet when connected; iPhone/iPad with a build environment available.
- Portrait/landscape, native back, drawer, cancelled transitions, tabs, IME on comments.
- Active feed → another tab → background → lock → foreground.
- Headphones/Bluetooth removed, focus taken by another app and a real interruption with the user's agreement.
- Corrupted file, 404, timeout, network cut/restored, adaptive HLS, surface/context loss.
- Background audio: notification/lock screen and consistent pause/next commands, no unsolicited restart.

### Pressure test and publishing the results

- 10,000 identities; 1,000 clip changes and 100 navigation/rebuild round trips.
- Measure the number of live sessions/decoders/surfaces, .NET/ART/native/GPU memory and stability after warm-up.
- Allocations/frame and number of GCs per heap; JNI/ObjC callback allocations included in the analysis.
- Frame time P50/P95/P99 and jank at 60/120 Hz for the UI; media FPS, dropped frames and A/V sync separately.
- Prepare → Ready and prepare → first frame actually displayed, local/cold/warm/network separately.
- Clip change latency, seek cost, buffering, CPU and thermal effect over a long test.
- A 30 FPS feed is not a 120 FPS video feed just because its screen runs at 120 Hz.
- No latency target before a physical baseline; set regression thresholds after measuring.

## 10. Debt explicitly outside V1

DRM/Widevine/FairPlay and advanced secure outputs; PiP; dedicated Cast/AirPlay UI; protected offline downloads; advanced HDR; guaranteed gapless/crossfade between sources; equaliser/mixer/DSP and low-latency sound effects; microphone/camera recording; editing/transcoding; real-time waveforms; multi-player synchronisation.

Element animation, stagger and choreography stay in the `Element Animation & Choreography Engine` debt. Video playback does not allow developer code to run in the hot path and does not replace that engine.

## Next action after validation

Start with the contracts and the foreground audio player, then a native and GPU video prototype on the current hosts. The Shorts page with real media comes afterwards, not as a substitute for the player gates.
