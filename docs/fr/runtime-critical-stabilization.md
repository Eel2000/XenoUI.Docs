# Runtime critical stabilization

Branch: `codex/runtime-critical-stabilization`.

## Implemented checkpoint

- Measure caches include constraints; arrange caches compare the incoming rectangle.
- Intrinsic measurement no longer stores stretch imposed by the parent.
- Clickable hit testing traverses children before parents in reverse painter order.
- Hit geometry accounts for local translation/scale, rounded ancestor clips, scroll and overscroll, and incoming navigation/chrome transforms.
- Tap and developer IME-action callbacks execute outside the engine lock; post-callback word selection validates the entity generation.
- Reactive text updates its existing string slot and requests redraw; replacing Clickable releases the previous callback.
- Reactive and editor subscriptions validate the captured entity generation; stale callbacks are rejected after slot recycling and disposal.
- Fling samples use monotone timestamps and position regression in dp/s; physics integrates elapsed time, capped at 50 ms.
- Renderer resource caches are bounded (32 fonts, 512 text blobs, 64 shadows); shadow keys preserve full ARGB and quantize geometry.
- Animated elevation is quantized to 0.5 dp; low-memory notification clears recreatable renderer resources under the frame lock.
- Lazy rewindowing retains overlapping items by key/index and assigns newly created items to the owning scene.
- Failed lazy builders roll back every created entity using generation-checked handles, including partial items whose root was never returned; the published window is preserved.
- Lazy build journals retain their capacity between window mutations and are returned even after a failed builder. Text measurement reuses an engine-owned font, preserving exact sizes and explicitly resetting the typeface between calls.
- Scene ownership uses dense swap-removal, and entity metadata stores a direct scene-record index for input/layout lookup.
- Native text geometry is posted only when changed; Android/iOS follow the primary touch.
- Drawer openness and the configured primary-destination root Back policy contribute to CanGoBack. Android returns to the initial destination before platform fallback; iOS retains independent tabs. An optional bounded MRU history does not create detail entries.

## Remaining work before declaring the full plan complete

- Unify clickable, editor, scroll, selection-handle and toolbar traversal into a single inverse-transform descent; remove the remaining global scans and ancestor walks.
- Dispatch all focus, clipboard and redraw notifications outside the engine lock through a bounded value-type queue. Tap and developer IME actions are already outside it.
- Purge unobserved implicit lazy states transactionally with ownership isolated across retained scenes.
- Restrict dirty cleanup to visited branches, preserving visual-only invalidations.
- Complete resize/IME/overlay/recycling/cancellation and cache eviction golden tests, allocation benchmarks and physical 60/120 Hz checks.
- Validate Drawer transitions on a connected tablet and input/lifecycle on physical iOS.

The checkpoint is not a statement that every frame of arbitrary developer content is allocation-free. Cache misses and lazy structural composition can still allocate; repeated warmed transitions are covered by the existing zero-allocation regression tests.

## Verification evidence

Physical phone reference: [Samsung A15 CPU/GL/startup benchmark](runtime-benchmark-a15.md). The [desktop benchmark](runtime-benchmark.md) is comparative only.

- Core executable regression suite: 106/106 passed, including rollback, dense ownership after 30 long jumps, stale callbacks, LRU disposal, shadow alpha keys, warmed zero-allocation navigation frames, repeated text measurement, IME chrome visibility, Android/iOS root Back policies, bounded primary history, configured initial destination and drawer/detail Back priority.
- Android tooling regression suite: passed.
- SampleApp Android build: passed; standalone APK installed on SM-A155F (A15).
- SampleApp iOS simulator compile: passed, with existing OpenGL deprecation warnings; this is not a physical iOS runtime test.
- A15 smoke test: Home → Runtime Lab, scroll the 10k list, switch Home → Lab with the scroll position retained; Gallery → Input Lab, focus the native field, type, dismiss the keyboard and Back to Gallery. The tested process remained alive with no AndroidRuntime error in its PID-filtered log.
- Latest standalone APK: Gallery → Input Lab → native Back was rechecked with a screenshot at each step; Runtime Lab scrolling and PID-filtered AndroidRuntime log were also checked successfully.
- An unattended repeated-Back loop ended on the Android launcher without a fatal error. It is inconclusive, not a passed stress gate; replace it with a state-verified journey before accepting repeated-navigation stress.
- Screenshots are local ignored artifacts, not benchmark evidence. Refresh-rate, tablet and physical iOS gates remain open.
- Android deployment caveat: an incremental package built after a full build produced `UnsatisfiedLinkError` for `MainActivity.n_onCreate`, before engine startup. Use a full `Rebuild` for the physical validation package; do not interpret build success or deployment activation alone as a successful runtime launch. The incremental-binding issue remains separate tooling debt.

## Next implementation checkpoints

1. **Event dispatch boundary**: enqueue typed focus/dismiss/clipboard/redraw records under the lock, drain after the outer public operation releases it, coalesce redraws, and validate generation/session before dispatch. Define overflow explicitly; do not silently drop focus or clipboard commands. Test reentrant navigation, disposal and cross-thread entry from each public notification.
2. **Unified input descent**: map the point once per node, traverse reverse painter order from chrome/active roots, and collect clickable/editor/scroll candidates through the same clip/transform path. Selection handles and toolbar use the same scene mapping. Test scaled/translated/clipped editors, overlays, nested axis arbitration and recycled gesture targets.
3. **Implicit lazy-state ownership**: use composition-owner identity rather than scene IDs (which change during rebuild). Track observed identities in a structural transaction, preserve retained owners, and purge only after a successful publication. Test shared helper call sites across two tabs, conditional removal, rebuild and failed composition.
4. **Layout invalidation cleanup**: clear only the passes actually processed, independently of visual invalidations; avoid walking clean subtrees. Test mixed visual/measure updates, resized clean descendants and keyboard viewport changes.
5. **Final gates**: full device stress journeys, visual goldens and measured allocation/frame pacing on 60/120 Hz hardware, tablet drawer transitions, physical iOS input/lifecycle. Keep cold-path cache misses and item construction distinct from warmed transition frames in the report.
