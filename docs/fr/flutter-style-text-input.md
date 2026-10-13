# XenoUI text input architecture

> **Note de conception.** Ce document explique la recherche ayant inspiré l’implémentation ; il ne constitue pas le contrat public. Référence actuelle : [architecture texte et NativeTunnel](architecture/text-input-and-native.md).

Status: first interactive implementation on `codex/native-text-input`.
Android has been exercised on a Samsung SM-A155F; iOS compiles but has not
been exercised on a device. This is not yet a release-ready text control.

## Boundary

XenoUI paints the field, glyphs, caret, composition underline, selection and
decoration. Android and iOS provide the system keyboard and text services. No
visible `EditText`, `UITextField` or `UITextView` is the primary renderer.

The four layers are:

1. Composable UI (`TextField`, `TextArea`, optional leading/trailing content).
2. Editing and paragraph geometry (text, selection, composition, history,
   shaping, wrapping, hit-testing and selection rectangles).
3. Rendering and gesture handling (Skia, caret, scroll, handles and menus).
4. `NativeTunnel` (typed platform capabilities, with a hidden Android
   `EditText`/`InputConnection` or iOS `UITextView`/`UITextInput` serving the
   keyboard without painting the visible editor).

Text editing state belongs to the page/controller, never to an ECS entity ID.
Rebuilds may replace all entity IDs without replacing an active editing session.

## Current checkpoint

`TextEditController` models directed selection, provisional composition and
bounded undo/redo. `ITextParagraphEngine` makes the layout backend replaceable;
the default `TextParagraph` wraps RichTextKit/HarfBuzz and translates
between UTF-16 editor offsets and code-point paragraph offsets. It is the
single source of geometry for glyph painting, caret and tap hit-testing.
`TextField` and `TextArea` render in XenoUI; the sample app has a dedicated
test page, including password masking/reveal, email keyboard, long single-line
value and long multiline note. A retained `TextViewportState` clips and scrolls
editor contents independently of the surrounding page, reveals the caret after
edits and survives ECS tree replacement. A focused multiline field captures
vertical drags only when its paragraph overflows; otherwise the page scrolls.
Composing underlines are split by wrapped paragraph line. Taps are hit-tested
against the viewport currently on screen before any caret reveal occurs. The
custom-painted caret blinks on a 500 ms timer without keeping the render loop
continuously active; typing and collapsed-selection changes restart it, while a
range selection or blur suppresses it. Drag selection keeps a fixed logical
anchor and scrolls the editor viewport near its edges. Double-tap and long-press
select shaped word boundaries. The renderer paints both selection handles and
each endpoint can be dragged independently with an enlarged touch target.
The XenoUI-painted contextual toolbar dispatches Cut, Copy, Paste and Select
All through a typed clipboard capability in `NativeTunnel`; secure fields hide
Cut and Copy.

Pages build it with `InputOptions(keyboard, action, autoCorrect, secure, autofill, label)`.
`TextInputConfiguration` also carries portable accessibility labels and
autofill semantics (name, email, username, password, new password, telephone
and one-time code). Android exposes them on the hidden `EditText`, overrides its
accessibility bounds with the painted editor rectangle and gates Autofill to API
26+. iOS maps the same contract to `UITextView` accessibility geometry and
`UITextContentType`.

`TextEditController.BeginBatchEdit()` now coalesces nested synchronous edits
into one notification and one undo transaction. Application-owned text updates
during an active IME composition have an explicit policy: cancel composition,
defer the latest update until composition ends, or reject the conflicting
update. This removes the previous implicit last-writer-wins behavior.

Native keyboard actions are routed back through the focused editor. Developers
can handle `Done`, `Next`, `Search`, `Send` and `Go` with the composable's
`onAction` callback; `Next` advances to the following text editor in composition
order and `Done` dismisses the session. Password keyboard configuration also
normalizes to secure native input and masked XenoUI painting unless the caller
explicitly enables reveal mode.

Android uses an invisible `EditText` so its own `Editable` and `InputConnection`
retain composing spans, batch edits and native keyboard queries. Its complete
text/selection/composition snapshot is imported into `TextEditController` after
the IME operation. iOS uses an invisible `UITextView` and its marked-text range
for the same purpose. Its `UITextInput` caret and first-range rectangles are
overridden with shaped XenoUI geometry so candidate windows follow the
custom-painted caret rather than UIKit's hidden layout. Programmatic controller
changes sync back to the hidden control; IME-origin changes do not echo back.
`TextInputCoordinator` preserves sessions over tree rebuilds and discards
actions from retired sessions.

On a Samsung SM-A155F, the sample opened the system keyboard, accepted typing,
revealed the end of a long single-line value, scrolled a long note internally,
and kept the note and caret through a page rebuild. The Android entry-point
template requests `adjustResize`; the view also feeds IME insets to the core
viewport because the Skia surface can remain full-height on edge-to-edge
devices. A long multiline field was visually verified above the keyboard while
typing. iOS observes the keyboard frame and feeds the same core viewport, but
this path is compile-checked only. These checks do not cover the full language,
selection and lifecycle matrix.

## Required implementation order

1. Finish gesture selection: horizontal drag in an already focused field,
   tap-to-caret, double-tap/long-press word selection, painted handles,
   edge-driven autoscroll, programmatic selection, draggable handles and the
   clipboard-backed contextual toolbar are implemented.
2. Finish editor-internal scrolling and painting: bounded multiline drag,
   caret reveal, docked-keyboard inset avoidance and wrapped composition
   underlines, cursor blink and edge-driven selection autoscroll are
   implemented. Composition geometry now intersects the logical composing
   range with each shaped bidi run, preserving disjoint visual spans instead
   of bridging RTL/LTR boundaries. Verify keyboard avoidance on iOS and older
   Android versions.
3. Accessibility labels, editable semantics, autofill hints and painted-field
   geometry are bridged through the hidden native editor. Android and iOS both
   compile; inspect with TalkBack/Accessibility Scanner and VoiceOver on real
   devices before considering the accessibility contract release-ready.
4. Test IME composition, clipboard, undo/redo, selection, secure input and
   lifecycle on real Android and iOS devices with Latin, CJK, RTL and emoji.
   The iOS bridge is compile-checked only.
5. Batch editing and external-update policy are implemented and covered for
   nested edits, undo coalescing, deferred IME completion, rejection and the
   default cancel-composition behavior. Extend device testing to rapid remote
   state updates during real CJK composition.

## Sources

- Flutter `EditableText`: https://api.flutter.dev/flutter/widgets/EditableText-class.html
- Flutter `RenderEditable`: https://api.flutter.dev/flutter/rendering/RenderEditable-class.html
- Flutter `TextInput`: https://api.flutter.dev/flutter/services/TextInput-class.html
- Android `InputConnection`: https://developer.android.com/reference/android/view/inputmethod/InputConnection
- Apple `UITextInput`: https://developer.apple.com/documentation/uikit/uitextinput
- RichTextKit: https://www.toptensoftware.com/richtextkit/
