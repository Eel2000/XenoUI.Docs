# Android: recovery after locking and GPU context loss

[Index](index.md) · [Runtime](architecture/runtime.md) · [Troubleshooting](troubleshooting.md)

## Contract

The lifetime of ECS scenes and pages is independent of the EGL context's. Locking, going to the background or recreating the surface must neither call `Body()` again, nor change the navigation stack, nor clear text controllers or scroll. This contract concerns an application whose process stays alive; restoration after process death is a separate feature.

`XenoView` uses `XenoGLSurfaceView`, a `GLSurfaceView` host with a renderer owned by XenoUI. It no longer depends on the internal resource handling of `SKGLSurfaceView` 3.119.4, whose `OnSurfaceCreated()` is empty.

## Resources and threads

- Main thread: application foreground/background, the GL view's `OnResume()`/`OnPause()`, NativeTunnel and media.
- GL thread, in `OnSurfaceCreated()`: abandon the old `GRContext` before releasing the surface/target wrappers, invalidate render caches, create a new GPU generation. No structural ECS mutation.
- GL thread, in `OnSurfaceChanged()`: abandon the old render target and re-read the framebuffer, even with identical dimensions. The context may stay valid if only the surface changed.
- First frame of the generation: creation of the Skia context, render target and surface. Allocations sit on this cold-path boundary.
- Following frames: reuse of the canvas and resources; no framebuffer array, event args or `SKAutoCanvasRestore` created by the renderer.
- Shutdown: wait for the GL thread to be paused before releasing graphics resources and the engine. Idempotent release; late renderer callbacks are ignored.

The EGL context is not preserved during pause by default: the recovery path must work without this optimisation. Enabling preservation would not remove the need to handle a real context loss.

`XenoGL` logs show the EGL generation and the creation of its surface. They are only produced at context/surface boundaries, never per stable frame. Native text and video geometries are republished after recovery. The foreground playback policy and the limits of the video underlay do not change.

## Validation

Core test `GraphicsRecoveryRetainsUi`: ten background/foreground cycles with cache invalidation, no recomposition or entity renewal, navigation, text and scroll preserved, then back to the previous page. This test uses a raster surface: on its own, it does not prove EGL recovery.

Physical Android validation:

1. Install a fully rebuilt APK, without fast deployment.
2. Open a page, scroll it, navigate to a detail and type some text.
3. Lock then unlock the phone normally. If a code is required, its owner must type it; never disable the protection to test.
4. Visually check the UI and the preserved state, as well as interactions after resuming.
5. Repeat the cycle and do Home/return. Check the new `XenoGL` logs, the same process/Activity and the absence of a crash.
6. Also check the video feed, its overlaid controls and the drawer.

### Physical results — 8 October 2026

Samsung Galaxy A15 (SM-A155F), fully rebuilt Release APK: after several lock/unlock cycles and Home/return, the interface comes back in the same process (PID 7725). The logs confirm five successive EGL generations, i.e. four recoveries after the initial creation, with no crash in the process log. The counter stays at 25 and Home's scroll position is preserved, with no interaction needed to trigger rendering. Navigating to Studio then Text input and opening the keyboard work after these resumes. The typed text `ResumeTest` is kept after a manual unlock; Back then works. The feed's overlaid controls and opening/closing the drawer work after Home/return.

Automated suites: 145/145 Core tests and 24/24 media tests pass. Android build and Release SampleApp: no errors. These tests are not an on-device allocation measurement.

Rotation and process death must be qualified separately; a locking test does not validate full session restoration. Actual network video playback and resumption are not validated by these captures: the feed showed its controls but no video frame before going to the background. No iOS test was done for this Android fix.
