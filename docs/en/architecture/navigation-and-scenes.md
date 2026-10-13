# Navigation, ECS scenes and the frame path

[Architecture](index.md) · [Runtime](runtime.md) · [NavigationScaffold API](../navigation-scaffold.md) · [V1 decisions](decisions-v1.md)

## Keyboard and retained chrome

In BottomBar mode, `HideBottomBarWhenKeyboardVisible` is `true` by default: the bar and its FAB are hidden and excluded from hit-testing while the IME is visible. Scenes stay mounted, without calling the slots or `Body()` per frame, then become visible again on close. `false` keeps the chrome above the keyboard. Rail and Drawer are not hidden by this option.

The Android host passes IME visibility and the viewport inset separately: a system bar inset alone does not mean the keyboard is open. Hosts can use `SetViewportBottomInset(inset, keyboardVisible)`; the old single-argument overload remains available for hosts whose inset only represents the keyboard (iOS).

On Android, visibility is read from the root window's insets, including when the parent consumes the insets before they reach the surface. The host observes native layout changes and only passes the remaining occlusion within the visible frame: with `adjustResize`, this value can be zero while the IME is visible. The BottomBar then stays hidden, without subtracting the keyboard height twice. This observation stays out of the render loop; it is detached with the view.

### Physical validation — Galaxy A15, 5 October 2026

Release SampleApp deployed on a Samsung SM-A155F (Android 16). The Gallery → TextInput flow confirmed the BottomBar is hidden while typing, `XenoIMEtest` is kept, then restored on the first native Back (keyboard closes). The second Back pops TextInput and returns to Gallery. No `AndroidRuntime:E` message seen for the tested process. The 102 Core tests passed before the root policy was added; this physical validation does not cover iOS.

### Physical validation — Galaxy Note20, 6 October 2026

Release SampleApp deployed on a Samsung SM-N981B (Android 13). The Home → TextInput flow confirms that the floating BottomBar and the FAB are hidden while typing, then restored on the native Back that closes the keyboard. The field background stays white while pressed, with no button darkening. Long-press selection and its menu keep working with the keyboard open. The 120 Core tests pass, including the case of a visible IME with zero residual occlusion after a native resize.

The behaviour initially observed (exiting from Lab after several selections) is replaced by `RootBackBehavior.PlatformDefault`: Android restores the initial destination before exiting, iOS does not walk through tabs on Back. Selections do not become detail entries. The policy and the optional history are handled on the cold path; presentation reuses the retained scenes and the compiled tracks.

A15 validation of the fully regenerated Release package confirms Gallery → People → Lab → Back → Home → Back → launcher, then Gallery → TextInput → IME open → Back (IME closed, detail kept) → Back (Gallery) → Back (Home). The tested process (PID 10308) has no `AndroidRuntime:E` message. The 106 Core tests pass, including drawer/detail/root priority and the Android/iOS conventions; the drawer was not tested physically on this phone in BottomBar mode, and iOS is only validated at the Core contract level.

## Central invariant

A transition does not recompose pages on every frame. For main navigation, the cold path builds a destination only when it is first opened, then later selections publish two roots that are already mounted. The hot path only evaluates values and draws the two scenes.

```text
ScaffoldSceneHost
├── ScaffoldChromeScene        stable identity for standard components
└── DestinationSceneRecord[]
    ├── retained Home stack
    ├── retained Search stack
    └── retained Profile stack
```

The chrome and the content have distinct ECS owners. The renderer draws the active content then the chrome. The InputSystem gives priority to the chrome and only accepts the incoming destination; hit-test coordinates are inverted with the same `SceneVisualState` as the renderer.

```text
NavTransitionBuilder → immutable NavTransition → AnimationTrack[]
                                              ↓ frame
                                 SceneVisualState (struct)
```

Entities carry `SceneId` and `Generation`. An `EntityHandle` pairs the slab index with its generation in order to reject a late callback after recycling. A contiguous table of `SceneRecord`s describes each root and a dense registry keeps the entities owned per scene. Destroying a scene empties neither the other scenes nor the engine-lifetime font/image caches.

## Navigation transaction

1. Resolve the stack and the transition on the cold path.
2. Find the retained root or compose the scene once in a thread-bound `Compose` scope.
3. Measure/arrange only if this root has never seen this viewport.
4. Publish the incoming root and make the outgoing one non-interactive.
5. Evaluate translation, scale, rotation, clip and opacity in the slabs/structs.
6. On commit, keep the outgoing destination inactive. On a pop, destroy only the removed detail entry.

A new selection during a transition cancels the intermediate presentation without a spurious lifecycle callback. Destination scenes stay mounted; an entry explicitly removed from a stack releases its callbacks along with its entities.

On the Android/iOS hosts, the end of a transition published from a render thread is dispatched to the host's control thread before the public `OnActivated`/`OnDeactivated` callbacks. The commit captures the stack entry's identity on the cold path: a Back, a new navigation or a dispose rejects the old commit even if two entries share the same page instance. The drawer scheduler only computes the deadline within the frame; structural closing and deferred selection are dispatched once to the control thread. Two dedicated tests cover rendering on a separate thread and invalidated late commits.

## Cold path and hot path

Allowed on the cold path: `Body()`, Scaffold/FAB slots, validation, transition compilation, creating stacks, entities, callbacks, paragraphs and loading assets.

Transition hot path: monotonic clock, progress, linear loop over the tracks, interpolation, updating `SceneVisualState`, Skia rendering, transformed hit-test and requesting the next frame. No `Body()`, LINQ, temporary collection, developer delegate, asset or transition compilation may enter it.

Virtualisation can trigger a structural mutation when its window changes: it is handled as an explicitly scoped cold-path sub-pass, with reused buffers. It must not contaminate transition evaluation while the window stays stable.

## Ownership and resources

- entities, callbacks, strings and subscriptions: owned by the scene;
- pages, scroll/lazy states and text controllers: owned by the application/page;
- decoded images and fonts: owned by the engine, shared between scenes;
- active IME session: a single native session; the controller and the logical selection stay owned by the retained page.

## Official debt: Element Animation & Choreography Engine

This future engine will cover appearance/disappearance, delay, stagger, order, rhythm, layout changes, add/remove/reorder, virtualised items, `AnimationKey` and reduced motion. It will reuse the clock, the scheduler, the easings and the navigation tracks, but will stay a separate module: a scene transition must never call element code arbitrarily.
