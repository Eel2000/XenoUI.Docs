# Accessibility

[Documentation](index.md) · [Customising](customization.md) · [Animations §9](animations.md) · [Localization](localization.md)

Because XenoUI **draws its own pixels**, accessibility is not free the way it is with native views. This page states what
the framework does today, what it does **not** do yet, and the practices that make an app usable by more people right now.
Treat it as a living document: the framework is pre-V1.

## What the framework provides today

| Area | Status |
|---|---|
| **Text fields** | Backed by a hidden native text control, so the keyboard, IME, dictation and an accessibility **label** work. The label defaults to the placeholder (or "Text input") and can be set with `InputOptions(label: "Full name")`. |
| **Secure fields** | `InputOptions(secure: true)` / `obscureText` hide the text; the secure mode does not reveal it. |
| **Reduced motion** | The system setting is read (`IMotionPreferencesCapability`) and applied: overlays use no animation, navigation selection applies its final state immediately, the drawer uses zero duration. |
| **Right-to-left** | Full mirroring of layout, hit-testing and text alignment ([Layouts §10](layouts.md)). |
| **Large text** | Sizes are in dp; text wraps by default. Layouts that use `FlexGrow` and `MaxLines` hold up when fonts grow. |

## What it does not provide yet

- **No semantic tree for drawn elements.** TalkBack and VoiceOver cannot discover buttons, lists or headings drawn by XenoUI,
  and there is no `contentDescription` / `accessibilityLabel` modifier. A planned design covers semantic nodes (role, label,
  value, state, actions, bounds), Android `AccessibilityNodeProvider` and iOS `UIAccessibility` mapping, focus order,
  scrollable content and live regions ([Text, IME and NativeTunnel §10](architecture/text-input-and-native.md)).
- No focus order or keyboard navigation for custom controls, no live regions, no automated screen-reader tests.

If screen-reader support is a hard requirement for your product, this framework is not ready for you
([Why XenoUI?](why-xenoui.md)).

## What you can do today

**Make the visuals robust**

- **Contrast**: at least 4.5:1 for body text and 3:1 for large text and icons. Test on every theme and translucent surface.
- **Touch targets**: at least 44–48 dp per side (`Box(44, 44, icon).CenterContent().Clickable(tap)`).
- **Do not rely on colour alone**: pair a red error with an icon or text.
- **Do not freeze heights around text**: prefer padding, `MaxLines` and `FlexGrow`; check the layout with long text.
- **Respect reduced motion** in your own animations: set the duration to 0 ([Animations §9](animations.md)).

**Label your text fields**

```csharp
TextField(_email, "name@example.com",
    InputOptions(NativeKeyboardType.Email, autofill: NativeAutofillHint.Email, label: "Email address"))
```

**Give users alternatives to gestures.** Swipe-to-dismiss exists on snackbars and sheets, but each also closes by a
button, the scrim or the system Back; keep an equivalent visible control for any gesture-only action.

**Provide text equivalents** for icon-only buttons: show a label, or a tooltip-like popup on long-press, until the
framework offers semantics.

**Test with the system settings**: largest font size, display size, reduced motion, high contrast, dark mode, RTL language,
hardware keyboard. Use `[Preview(RightToLeft = true)]` and `[Preview(Device = …)]` to check layout variants quickly.

## Checklist

- [ ] Every text field has a label (`InputOptions(label: …)`).
- [ ] Contrast and touch-target sizes checked.
- [ ] Information never carried by colour alone.
- [ ] Layout survives large fonts and long (translated) text.
- [ ] Animations honour reduced motion.
- [ ] Every gesture has a visible alternative.
- [ ] RTL checked.
