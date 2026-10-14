# Media controller — portable foundation

[Documentation](index.md) · [Plan and gates](media-playback-implementation.md)

Checkpoint of 6 October 2026: portable foundation and **Android Media3 1.11.1 foreground audio/video** adapter. The basic flow of the rectangular native video output is validated on the A15; it is not the Skia GPU output of the future Shorts feed. Background services are not implemented. Creating a controller without `IMediaPlaybackCapability` fails explicitly with `NotSupportedException`; opening a video session without a video host also fails.

## Optional module

In a Xeno.SDK application, `XenoEnableMedia=true` adds the portable module and, on Android only, `XenoUI.Media.Android`. The generated Activity registers the capability before `OnStart`. A custom Activity must provide this registration through the `XenoView(context, configureNative)` overload and forward `HandleForeground`/`HandleBackground`, then call `Shutdown()` on destruction. This method stops the GL thread and releases the runtime once, without prematurely disposing the Java peer still held by the window; do not call `view.Dispose()` from `Activity.OnDestroy`. No Android service or background permission is added.

**iOS (merged on 14 October 2026, shipped with the next preview after `{{ xeno_version }}`)**: the iOS host in `XenoUI.Platforms` always registers `IosMediaPlaybackCapability`, an AVPlayer backend that follows the Android contract (session budgets and revisions, a 100 ms main-thread pump, one audible session at a time, foreground/background policy, embedded assets, first-frame thumbnails). AVFoundation ships with iOS: no extra package. Video is a native `AVPlayerLayer` placed under the transparent Skia view; the renderer clears the video area, so overlays stay on top. For servers that ignore HTTP Range requests (which AVFoundation cannot stream), the file is downloaded once into a 256 MB LRU disk cache and served to AVFoundation as it arrives. Validated on the iPhone simulator (Debug) only: real devices and Release builds still have to be validated.

Both modules, [`XenoUI.Media`](https://www.nuget.org/packages/XenoUI.Media) and [`XenoUI.Media.Android`](https://www.nuget.org/packages/XenoUI.Media.Android), are published on nuget.org with the same version as `Xeno.SDK`; the SDK references them automatically when the property is enabled.

```xml
<PropertyGroup>
  <XenoEnableMedia>true</XenoEnableMedia>
</PropertyGroup>
```

## Ownership and commands

Create a `MediaController` on the host's control thread, during a creation lifecycle callback, not in `Body()`. Keep it with the page or the application; a recomposition must not reopen a session. Call `Dispose()` in its owner's destruction lifecycle callback. The controller is not thread-safe: all its reads and commands belong to the same thread.

Once a conforming backend is registered, the sequence is:

```csharp
using XenoUI.Media;
using XenoUI.Core.NativeTunnel;
using MediaController = XenoUI.Media.MediaController; // avoids Android.Widget.MediaController

var player = nativeTunnel.CreatePlayer(); // MediaKind.Audio by default
player.Configure(volume: 0.35f);
player.Prepare(MediaSource.Https("https://example.org/track.mp3"));
player.Play(); // intent to play, not a guarantee of immediate sound
player.Suspend(); // keeps the intent for a possible return
player.Pause(); // cancels the intent even during a suspension
player.Resume(); // does not restart after this Pause
player.Stop(); // a new Prepare is needed before Play
player.Dispose();
```

`Prepare` replaces the source and cancels the previous Play intent. `Seek` uses a known native range and refuses a non-seekable source; a duration of `-1` means unknown. Volume: 0..1; requested speed: 0.25..4, subject to the capabilities of the future adapter. An unsupported command must fail explicitly.

`MediaSource.Asset` accepts a relative path without traversal. Android resolves the `XenoAsset/<path>` resource, normalises Windows separators, then extracts it into a private cache with host lifetime: at most eight assets, 16 MiB per file and 32 MiB in total. This synchronous extraction is an audio cold path; it is not the streaming/cache strategy for large videos. `File` requires an absolute path, but grants no access right to the file. `Https` refuses HTTP and credentials in the URL. Advanced HTTP options and playlists are not implemented yet.

Media3 alone handles Android focus through `SetAudioAttributes(..., true)` and the audio-becoming-noisy event. The host caps sessions at two and removes the native intent of the other session when audible playback is requested. `RequiresUserPlay` cancels the controller's intent after a permanent focus loss, unplugged headphones or replacement by another player. Transient interruptions keep the intent. These physical routes still need validating on devices with the user's agreement.

## Reference native video output

The optional SDK Activity uses a `FrameLayout` containing the Xeno view and a non-interactive `TextureView`. The adapter is registered as `IMediaPlaybackCapability` and `INativeVideoOutputCapability`. A custom Activity provides this host as the third argument of the Android constructor and registers both contracts. The budget is six media sessions in total, of which at most four video; only the session named by the current video placement is attached to the `TextureView`. Each player has a bounded buffer (start after 1 s, at most 15 s) so that several preloaded players stay frugal in memory and network.

```csharp
// Page activation/creation, control thread:
var player = Native.CreatePlayer(MediaKind.Video);
player.Prepare(MediaSource.Https(videoUrl));
// Body(), under a CrossAxis(Stretch) parent, outside any scroll:
var viewport = MediaView.Video(player, height: 210);
// Play/Pause/Seek/Configure are the same commands as for audio.
```

Only the generational handle enters the visual slab. The renderer publishes a rectangle in physical pixels; a bounded mailbox coalesces the values before they are applied on the Android main thread. No Media3 call, View positioning or application callback runs in the renderer. No video pixel is copied into a bitmap or a C# array: Android composes the native surface, **not Skia**. This does not demonstrate zero-copy GPU rendering.

On Android, the media host now uses a **native underlay**: the RGBA Skia surface is above the TextureView. The renderer clears the accepted video rectangle with a reused `Clear` paint, then draws the following children/siblings normally. Controls placed after `MediaView.Video()` in a `ZStack` are therefore genuinely overlaid and still hit-tested by XenoUI.

Aspect-fit, scroll translation and the viewport's rectangular clipping are supported. Placement carries numeric rectangle and clip values to the control thread; Android uses the View translation and its ClipBounds. No crop/fill, rounded clipping, video alpha/rotation, blur or secure DRM Surface. Rounded or transparent ancestors and non-translational matrices (including bounce stretch) hide the video. During a transition or with the drawer open, the surface stays hidden then is reattached after settling; no animated snapshot of the video. A temporary black frame is expected on reattachment: a poster until the first frame and versioned leases remain open gates. `Playing` is **not proof that the first frame is displayed**. The native underlay does not make the video composable like a Skia GPU image.

SampleApp's Video page keeps its diagnostic viewport. The Feed page uses the `Page → LazyColumn → ZStack(Video, Column(controls))` structure per item. Four [Samplelib clips](https://samplelib.com/sample-videos.html) of 5 to 15 seconds are played directly over HTTPS, one of them vertical; their [terms of use](https://samplelib.com/license.html) allow demo assets. A remote asset that the backend reports as longer than 60 seconds is paused. The links may change or become unavailable; Retry and connection errors are visible.

Feed relies on `IMediaPreloadCapability`: on each page change it passes its ordered list and the current index. The Android backend (Media3 `DefaultPreloadManager`) preloads the next clip in memory (~2 s), the two after it into a shared 256 MB disk cache (~3 s), the previous one (~1 s) and nothing else; reranking cancels the work for pages skipped by a fast swipe. A session that prepares a preloaded clip starts from that data. Two controllers are enough: the current page plays, the other keeps the previous clip (immediate going back). The pager publishes the chosen page as soon as the finger lifts; the page receives its player through `NativeVideo(State<MediaHandle>)`, without rebuilding the tree. The poster stays painted above the surface until the new clip plays (the module notifies as soon as the video size is known). This poster is the clip's first frame: `IMediaThumbnailCapability` extracts it in the background (Android `MediaMetadataRetriever`, two extractions at a time), caches it on disk (`cache/xeno-thumbs`) and `ImageFile(State<string?>)` displays it, framed like the video; during a swipe the next page therefore already shows its image, and the video replaces it without a visible jump. Clips must be encoded for short video (~1–1.5 Mb/s, MP4 index at the start): at 6–13 Mb/s, even preloaded, they start slowly on a mobile network. Video Lab keeps the controller owned/disposed by App. Likes/favourites are local page state, comments are simulated.

For physical validations of this increment, rebuild the full APK (`-t:Rebuild`) and install it without fast deployment. An incremental package failed before the Activity was created with `UnsatisfiedLinkError` on `MainActivity.n_onCreate`; this JNI consistency problem must not be confused with a Media3 playback error.

## Snapshot, threads and hot path

`Synchronize()` is a control-path operation. On Android, a single host pump at 250 ms reads the native values outside rendering and caches the snapshots. `Changed` notifications are delivered on the main thread on a state change or a change of the displayed second. Stale handles and revisions are rejected. `Snapshot` then reads the controller's cache without calling the adapter back. No per-card timer and no Skia frame to keep the sound going.

The controller does not publish an ECS slab yet. Do not read it directly from a concurrent GL thread: the future control → ECS transport will have to publish the structs consistently. No timer, global subscription, UI frame or `Body()` is created automatically by this module.

The native contract requires consistent snapshots, commands on the appropriate thread, revisions captured at the origin of the asynchronous operation and a destruction that cancels callbacks. The Android adapter does not route any asynchronous result from an old item: it stops/removes the previous source, then reads the player's current state on its owner thread. `Stop` and `Dispose` are direct and do not go through a queue that might lose them. Leases and video frame transports are still to be implemented.

## Validation

Checkpoint results: 23/23 media, 122/122 Core, eight SDK evaluations and the Android SampleApp build without warnings or errors. After warm-up, the loop of 100,000 synchronisations/snapshot reads with a simulated backend allocates 0 managed bytes on its thread. This test does not demonstrate zero allocation for the native players, the pump, reactive labels or the native publication of a navigation commit. Physical interruptions, the background service, iOS and the full benchmark remain to be done.

When the Core suite was re-run during this validation, the pre-existing cache test once reported 12,336 bytes on its measured loop (121/122), then the suite passed 122/122 again without changes. This intermittent result still needs isolating in a dedicated campaign; the following success does not turn it into proof of allocation stability.
