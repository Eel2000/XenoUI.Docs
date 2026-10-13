# XenoUI text input — implementation brief

> **Historical plan.** The implementation has evolved since this proposal. See [Paragraphs, TextInput, IME and NativeTunnel](architecture/text-input-and-native.md) for the current architecture and [the developer guide](developer-guide.md#11-text-input-and-ime) for the API.

> Status when this document was started: the editing model (`TextFieldState`, `Graphemes`,
> `WordBoundary`, `TextEditHistory`) is written and tested (30 regression tests), not compiled
> for lack of a .NET SDK available to the tooling. Nothing exists yet on the rendering or platform side.

---

## 1. Goal and scope

Give XenoUI a text field usable in production on Android and iOS: hardware/software keyboard,
IME (CJK composition), copy/paste, touch selection, undo/redo.

**Outside the scope of this brief**: list recycling, accessibility (TalkBack/VoiceOver),
animations. These are separate work items that will build on this work (an accessible text
field needs a semantic node exposing `TextFieldState`, see §7).

**Guiding principle**: the keyboard and the IME are OS services. XenoUI does not draw them —
it exposes an editing model that the OS can read and modify through a platform bridge. The
rest (caret, selection, composition underline) is ordinary SkiaSharp drawing.

---

## 2. Architecture overview

```
┌──────────────────────────────────────────────────────────┐
│  XenoUI.Core/Text/  (done, tested, OS-independent)        │
│  TextFieldState ── Graphemes / WordBoundary / TextEditHistory │
└───────────────────────┬────────────────────────────────────┘
                        │ owned by the page (survives Rebuild, like ScrollState)
┌───────────────────────▼────────────────────────────────────┐
│  XenoUI.Core/Compositor/Widgets/TextInput  (to do, §4)     │
│  Compose component + caret/selection/composing rendering    │
│  + FocusManager (to do, §4.3)                               │
└───────────────────────┬────────────────────────────────────┘
                        │ IPlatformTextInput (to do, §3)
        ┌───────────────┴────────────────┐
┌───────▼────────┐              ┌────────▼────────┐
│ XenoUI.Android  │              │  XenoUI.Ios      │
│ InputConnection │              │  UITextInput     │
│ (§5)            │              │  (§6)            │
└─────────────────┘              └──────────────────┘
```

---

## 3. Platform contract (`XenoUI.Core`)

File to create: `XenoUI.Core/Text/IPlatformTextInput.cs`

```csharp
public readonly record struct TextInputConfig(
    KeyboardType KeyboardType,   // Text, Email, Number, Phone, Password, Multiline...
    ImeAction ImeAction,         // Done, Next, Search, Send, Go, None
    bool AutoCorrect,
    bool SecureEntry);

public interface IPlatformTextInput
{
    void Show(TextInputConfig config, TextFieldState state);
    void Hide();
    void OnStateChanged(TextFieldState state);     // the app changed text/selection → resync the OS
    void OnCaretRectChanged(RectF caretInWindow);   // to position the IME candidate window
}
```

This contract is **the only thing** `XenoUI.Android` and `XenoUI.Ios` implement. Everything
else (`TextFieldState`, composition commands) is already shared.

**Task 3.1** — Write `IPlatformTextInput.cs`, `TextInputConfig`, `KeyboardType`, `ImeAction`.
**Acceptance criterion**: compiles, no Android/iOS dependency in `XenoUI.Core`.

---

## 4. Rendering and component (`XenoUI.Core`)

### 4.1 Compose component

```csharp
TextField(_state)                 // _state: TextFieldState owned by the page
    .Placeholder("Name")
    .KeyboardType(KeyboardType.Email)
    .ImeAction(ImeAction.Next)
    .MaxLength(50)                // delegates to _state.MaxLength
    .SecureEntry(false)
```

**Task 4.1** — Add `TextField(TextFieldState)` to `Compose.cs` and the modifiers above.
The component creates an entity with a new `TextInputComponent` (ECS) referencing the
`TextFieldState` instance (no copy — the handle survives the rebuild, the entity does not).

### 4.2 Rendering (in `RenderSystem.cs`, a new branch for `TextInputComponent`)

| Element | Implementation |
|---|---|
| Text | `SKFont.MeasureText` / ordinary drawing, already in place for `Text()` |
| Caret | Vertical line at the `x` position of `state.Snapshot().Extent`, measured on the prefix |
| Blinking | Timer that only requests frames **if the field has focus** — see §4.3 |
| Selection | Background rectangles between `SelectionStart` and `SelectionEnd` |
| Composition | Underline (no background) between `ComposingStart` and `ComposingEnd` |
| Horizontal scroll | Offset to keep the caret visible; clip to the field's frame |
| Tap → index | Binary search over the cumulative widths, then `state.PlaceCaret(index)` |
| Double tap | `state.SelectWordAt(index)` |
| Password | Draw bullets instead of glyphs if `SecureEntry` |

**Task 4.2** — Implement the rendering above for the **single-line, LTR only** case. No
BiDi, no HarfBuzz at this stage (see §8 for what comes next).
**Acceptance criterion**: an unfocused field shows text + placeholder; a tap visually places
the caret in the right spot (checked by eye on the emulator, no automated test possible for
pixel rendering).

### 4.3 Focus

`TextFieldState` does not handle focus itself (it is not editing data). Add a minimal
`FocusManager` at the page/app level:

```csharp
public sealed class FocusManager
{
    public TextFieldState? Focused { get; private set; }
    public void RequestFocus(TextFieldState state, IPlatformTextInput bridge, TextInputConfig cfg);
    public void ClearFocus(IPlatformTextInput bridge);
}
```

Tracked **by reference to `TextFieldState`**, never by entity ID (IDs change on every
rebuild). `RequestFocus` calls `bridge.Show(...)`, `ClearFocus` calls `bridge.Hide()`.

**Task 4.3** — Write `FocusManager`. Wire an implicit `.Clickable` on `TextField` to
call `RequestFocus` on tap, and a tap outside any field to call `ClearFocus`.

---

## 5. Android bridge (`XenoUI.Android`)

`XenoView` is already an `SKGLSurfaceView` (hence a `View`). New file
`XenoUI.Android/Xeno/XenoInputConnection.cs` + changes to `XenoView.cs`.

| Step | Detail |
|---|---|
| 5.1 | `XenoView.OnCheckIsTextEditor()` → `true` |
| 5.2 | `OnCreateInputConnection(EditorInfo outAttrs)`: fills `InputType` (text/email/numeric/password/multiline according to `TextInputConfig.KeyboardType`) and `ImeOptions` (`ImeAction`), returns `new XenoInputConnection(this, state)` |
| 5.3 | `XenoInputConnection : BaseInputConnection` — delegates each method to `TextFieldState`: `CommitText → state.CommitText`, `SetComposingText → state.SetComposingText`, `SetComposingRegion → state.SetComposingRegion`, `FinishComposingText → state.FinishComposing`, `DeleteSurroundingText → state.DeleteSurrounding`, `GetTextBeforeCursor/GetTextAfterCursor/GetSelectedText → state.GetText...`, `SetSelection → state.SetSelection`, `PerformEditorAction → triggers the component's ImeAction`, `BeginBatchEdit/EndBatchEdit → state.BeginBatchEdit/EndBatchEdit` |
| 5.4 | `SendKeyEvent`: some hardware/virtual keyboards still send `KEYCODE_DEL` through this path rather than `deleteSurroundingText` → map to `state.DeleteBackward()` |
| 5.5 | `state.Changed += OnStateChanged` on the `XenoView` side: if `source == Program`, call `InputMethodManager.RestartInput(this)` (resyncs the whole IME); if `source == Input`, call `imm.UpdateSelection(...)` with the current offsets — **without this the keyboard keeps a stale state and autocorrection goes off the rails** |
| 5.6 | `RequestCursorUpdates` + `CursorAnchorInfo` for the candidate window (important for Gboard in Japanese/Chinese) |
| 5.7 | `InputMethodManager.ShowSoftInput` / `HideSoftInputFromWindow` called from `FocusManager` |
| 5.8 | IME insets: listen to `WindowInsets` (IME type, not just `adjustResize`), expose `Keyboard.Height` to the engine |

**Thread safety to respect**: `XenoView` renders on a GL thread, `InputConnection` receives its
calls on the UI thread. `TextFieldState` already manages its own lock — do not add extra
synchronisation on the Android side, just call the methods normally.

**Acceptance criteria for the Android phase**:
1. Latin Gboard: typing, deleting, selecting, copy/paste and autocorrection work.
2. Japanese Gboard: composition, conversion and committing a kanji work (manual test
   mandatory — none of this can be simulated in an automated test).
3. Physical keyboard (emulator with hardware keyboard enabled): arrows, Ctrl+arrow, Delete/Backspace
   work.

---

## 6. iOS bridge (`XenoUI.Ios`)

New file `XenoUI.Ios/Xeno/XenoTextInputView.cs`: a small invisible `UIView` added to the
hierarchy above `SKGLView` (do not try to have `SKGLView` carry `IUITextInput` directly).

| Step | Detail |
|---|---|
| 6.1 | `CanBecomeFirstResponder => true`; `BecomeFirstResponder()` / `ResignFirstResponder()` driven by `FocusManager` |
| 6.2 | Implement `IUITextInput` (not just `IUIKeyInput` — otherwise no autocorrection, no dictation, degraded CJK): `MarkedTextRange`, `SetMarkedText`/`UnmarkText` → `state.SetComposingText`/`FinishComposing`; `SelectedTextRange` (get/set) → `state.Snapshot()`/`state.SetSelection`; `TextInRange`/`ReplaceRange` → `state.GetText...`/`state.CommitText` |
| 6.3 | `UITextPosition`/`UITextRange`: minimal subclasses wrapping an `int` (UTF-16 offset) — no real complexity, just plumbing |
| 6.4 | `GetCaretRectForPosition`, `GetFirstRectForRange`, `GetClosestPositionToPoint`: need the `TextInput` component's text measurement (§4.2) — a dependency to set up properly |
| 6.5 | `InputDelegate.TextWillChange`/`TextDidChange`/`SelectionWillChange`/`SelectionDidChange`: called around any programmatic mutation (`source == Program` in `state.Changed`) |
| 6.6 | `UIKeyboardWillShowNotification`/`WillHide` → `Keyboard.Height`, same role as 5.8 on Android |

**Acceptance criterion**: the same three points as §5, on an iOS simulator/device.

---

## 7. What this brief does NOT cover but must anticipate

- **List recycling**: `LazyColumn` stays independent of this work, no dependency to
  manage.
- **Accessibility**: a future semantic node for `TextField` will need to read `TextFieldState`
  (role "text field", value = `Text`, selection = `SelectionStart/End`). Do nothing
  now, but do not wrap `TextFieldState` in an abstraction that would make this
  information inaccessible from outside the component.
- **BiDi / complex scripts** (Arabic, Indic): will need HarfBuzz (`SkiaSharp.HarfBuzz`).
  §4.2 measures text by simple LTR prefix — do not generalise this API to a point where
  replacing it later would break everything; keep caret measurement behind a single,
  easily substitutable internal function.

---

## 8. Recommended work order

1. **§3 — `IPlatformTextInput` contract** (isolated, quick, unblocks everything else)
2. **§4.2 — Single-line rendering** + **§4.3 — Focus** (visually testable without a real keyboard:
   calling `state.PlaceCaret()` by hand is enough to check the rendering)
3. **§5 — Full Android**, validated with Latin then Japanese/Chinese Gboard
4. **§6 — Full iOS**, validated with the system keyboard
5. **§5.8 / §6.6 — Keyboard insets**, `ImeAction`, `SecureEntry`
6. **Touch selection handles, clipboard, context menu** (native cut/copy/paste)
7. **Multi-line** (depends on the notion of a logical line already present in `TextFieldState`,
   §4.2 will need to add visual wrapping), then **BiDi**

## 9. Exit checklist (Definition of Done for the whole work item)

- [ ] `TextFieldStateTests.Run() == 0` (already in place, 30 tests)
- [ ] `IPlatformTextInput` compiles without an Android/iOS reference in `XenoUI.Core`
- [ ] Single-line rendering: text, placeholder, caret, selection, composition underline
- [ ] Android: Latin + Japanese Gboard validated manually, hardware keyboard validated
- [ ] iOS: system keyboard validated manually, dictation not broken
- [ ] `UpdateSelection`/`textWillChange` called correctly (no IME desync observed after
      a programmatic `SetText` while the field has focus)
- [ ] Keyboard insets: the focused field stays visible when the keyboard appears
- [ ] Copy/paste works both ways (to and from another app)
