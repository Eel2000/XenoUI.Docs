# Saisie de texte XenoUI — Fiche technique d'implémentation

> **Plan historique.** L’implémentation a évolué depuis cette proposition. Consultez [Paragraphes, TextInput, IME et NativeTunnel](architecture/text-input-and-native.md) pour l’architecture actuelle et [le guide développeur](developer-guide.md#11-saisie-de-texte-et-ime) pour l’API.

> État au démarrage de ce document : le modèle d'édition (`TextFieldState`, `Graphemes`,
> `WordBoundary`, `TextEditHistory`) est écrit et testé (30 tests de régression), non compilé
> faute de SDK .NET disponible côté outillage. Rien côté rendu ni plateforme n'existe encore.

---

## 1. Objectif et périmètre

Donner à XenoUI un champ de texte utilisable en production sur Android et iOS : clavier
matériel/logiciel, IME (composition CJK), copier/coller, sélection tactile, undo/redo.

**Hors périmètre de cette fiche** : recyclage de listes, accessibilité (TalkBack/VoiceOver),
animations. Ce sont des chantiers séparés qui consommeront ce travail (un champ de texte
accessible a besoin d'un nœud sémantique exposant `TextFieldState`, voir §7).

**Principe directeur** : le clavier et l'IME sont des services de l'OS. XenoUI ne les dessine
pas — il expose un modèle d'édition que l'OS peut lire et modifier via un pont plateforme. Le
reste (caret, sélection, soulignement de composition) est du dessin SkiaSharp classique.

---

## 2. Vue d'ensemble de l'architecture

```
┌──────────────────────────────────────────────────────────┐
│  XenoUI.Core/Text/  (fait, testé, indépendant de l'OS)    │
│  TextFieldState ── Graphemes / WordBoundary / TextEditHistory │
└───────────────────────┬────────────────────────────────────┘
                        │ possédé par la page (survit au Rebuild, comme ScrollState)
┌───────────────────────▼────────────────────────────────────┐
│  XenoUI.Core/Compositor/Widgets/TextInput  (à faire, §4)   │
│  Composant Compose + rendu caret/sélection/composing        │
│  + FocusManager (à faire, §4.3)                              │
└───────────────────────┬────────────────────────────────────┘
                        │ IPlatformTextInput (à faire, §3)
        ┌───────────────┴────────────────┐
┌───────▼────────┐              ┌────────▼────────┐
│ XenoUI.Android  │              │  XenoUI.Ios      │
│ InputConnection │              │  UITextInput     │
│ (§5)            │              │  (§6)            │
└─────────────────┘              └──────────────────┘
```

---

## 3. Contrat plateforme (`XenoUI.Core`)

Fichier à créer : `XenoUI.Core/Text/IPlatformTextInput.cs`

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
    void OnStateChanged(TextFieldState state);     // l'app a modifié texte/sélection → resynchroniser l'OS
    void OnCaretRectChanged(RectF caretInWindow);   // pour positionner la fenêtre de candidats IME
}
```

Ce contrat est **la seule chose** que `XenoUI.Android` et `XenoUI.Ios` implémentent. Tout le
reste (`TextFieldState`, commandes de composition) est déjà partagé.

**Tâche 3.1** — Écrire `IPlatformTextInput.cs`, `TextInputConfig`, `KeyboardType`, `ImeAction`.
**Critère d'acceptation** : compile, aucune dépendance Android/iOS dans `XenoUI.Core`.

---

## 4. Rendu et composant (`XenoUI.Core`)

### 4.1 Composant Compose

```csharp
TextField(_state)                 // _state : TextFieldState possédé par la page
    .Placeholder("Nom")
    .KeyboardType(KeyboardType.Email)
    .ImeAction(ImeAction.Next)
    .MaxLength(50)                // délègue à _state.MaxLength
    .SecureEntry(false)
```

**Tâche 4.1** — Ajouter `TextField(TextFieldState)` à `Compose.cs` et les modifiers ci-dessus.
Le composant crée une entité avec un nouveau `TextInputComponent` (ECS) référençant l'instance
de `TextFieldState` (pas de copie — le handle survit au rebuild, l'entité non).

### 4.2 Rendu (dans `RenderSystem.cs`, nouvelle branche pour `TextInputComponent`)

| Élément | Implémentation |
|---|---|
| Texte | `SKFont.MeasureText` / dessin classique, déjà en place pour `Text()` |
| Caret | Ligne verticale à la position `x` de `state.Snapshot().Extent`, mesurée sur le préfixe |
| Clignotement | Timer qui ne redemande des frames que **si le champ a le focus** — voir §4.3 |
| Sélection | Rectangles de fond entre `SelectionStart` et `SelectionEnd` |
| Composition | Soulignement (pas de fond) entre `ComposingStart` et `ComposingEnd` |
| Scroll horizontal | Décalage pour garder le caret visible ; clip au cadre du champ |
| Tap → index | Recherche binaire sur les largeurs cumulées, puis `state.PlaceCaret(index)` |
| Double-tap | `state.SelectWordAt(index)` |
| Mot de passe | Dessiner des puces à la place des glyphes si `SecureEntry` |

**Tâche 4.2** — Implémenter le rendu ci-dessus pour le cas **mono-ligne, LTR uniquement**. Pas
de BiDi, pas de HarfBuzz à ce stade (voir §8 pour la suite).
**Critère d'acceptation** : un champ non focus affiche texte + placeholder ; un tap positionne
visuellement le caret au bon endroit (vérifié à l'œil sur l'émulateur, pas de test automatisé
possible pour du rendu pixel).

### 4.3 Focus

`TextFieldState` ne gère pas le focus lui-même (ce n'est pas une donnée d'édition). Ajouter un
`FocusManager` minimal au niveau de la page/l'app :

```csharp
public sealed class FocusManager
{
    public TextFieldState? Focused { get; private set; }
    public void RequestFocus(TextFieldState state, IPlatformTextInput bridge, TextInputConfig cfg);
    public void ClearFocus(IPlatformTextInput bridge);
}
```

Suivi **par référence à `TextFieldState`**, jamais par entity ID (les IDs changent à chaque
rebuild). `RequestFocus` appelle `bridge.Show(...)`, `ClearFocus` appelle `bridge.Hide()`.

**Tâche 4.3** — Écrire `FocusManager`. Brancher `.Clickable` implicite sur `TextField` pour
appeler `RequestFocus` au tap, et un tap en dehors de tout champ pour appeler `ClearFocus`.

---

## 5. Pont Android (`XenoUI.Android`)

`XenoView` est déjà un `SKGLSurfaceView` (donc une `View`). Nouveau fichier
`XenoUI.Android/Xeno/XenoInputConnection.cs` + modifications de `XenoView.cs`.

| Étape | Détail |
|---|---|
| 5.1 | `XenoView.OnCheckIsTextEditor()` → `true` |
| 5.2 | `OnCreateInputConnection(EditorInfo outAttrs)` : renseigne `InputType` (texte/email/numérique/mot de passe/multiligne selon `TextInputConfig.KeyboardType`) et `ImeOptions` (`ImeAction`), retourne `new XenoInputConnection(this, state)` |
| 5.3 | `XenoInputConnection : BaseInputConnection` — délègue chaque méthode à `TextFieldState` : `CommitText → state.CommitText`, `SetComposingText → state.SetComposingText`, `SetComposingRegion → state.SetComposingRegion`, `FinishComposingText → state.FinishComposing`, `DeleteSurroundingText → state.DeleteSurrounding`, `GetTextBeforeCursor/GetTextAfterCursor/GetSelectedText → state.GetText...`, `SetSelection → state.SetSelection`, `PerformEditorAction → déclenche ImeAction du composant`, `BeginBatchEdit/EndBatchEdit → state.BeginBatchEdit/EndBatchEdit` |
| 5.4 | `SendKeyEvent` : certains claviers matériels/virtuels envoient encore `KEYCODE_DEL` par ce chemin plutôt que `deleteSurroundingText` → mapper vers `state.DeleteBackward()` |
| 5.5 | `state.Changed += OnStateChanged` côté `XenoView` : si `source == Program`, appeler `InputMethodManager.RestartInput(this)` (resynchronise tout l'IME) ; si `source == Input`, appeler `imm.UpdateSelection(...)` avec les offsets courants — **sans ça le clavier garde un état périmé et l'autocorrection déraille** |
| 5.6 | `RequestCursorUpdates` + `CursorAnchorInfo` pour la fenêtre de candidats (important pour Gboard en japonais/chinois) |
| 5.7 | `InputMethodManager.ShowSoftInput` / `HideSoftInputFromWindow` appelés depuis `FocusManager` |
| 5.8 | Insets IME : écouter `WindowInsets` (type IME, pas seulement `adjustResize`), exposer `Keyboard.Height` à l'engine |

**Thread-safety à respecter** : `XenoView` rend sur un thread GL, `InputConnection` reçoit ses
appels sur le thread UI. `TextFieldState` gère déjà son propre lock — ne pas ajouter de
synchronisation supplémentaire côté Android, juste appeler les méthodes normalement.

**Critère d'acceptation de la phase Android** :
1. Gboard latin : taper, effacer, sélectionner, copier/coller, autocorrection fonctionnent.
2. Gboard japonais : composition, conversion, validation d'un kanji fonctionnent (test manuel
   obligatoire — rien de tout ça n'est simulable en test automatisé).
3. Clavier physique (émulateur avec clavier matériel activé) : flèches, Ctrl+flèche, Suppr/Retour
   arrière fonctionnent.

---

## 6. Pont iOS (`XenoUI.Ios`)

Nouveau fichier `XenoUI.Ios/Xeno/XenoTextInputView.cs` : petite `UIView` invisible ajoutée à la
hiérarchie au-dessus de `SKGLView` (ne pas essayer de faire porter `IUITextInput` par
`SKGLView` directement).

| Étape | Détail |
|---|---|
| 6.1 | `CanBecomeFirstResponder => true` ; `BecomeFirstResponder()` / `ResignFirstResponder()` pilotés par `FocusManager` |
| 6.2 | Implémenter `IUITextInput` (pas seulement `IUIKeyInput` — sinon pas d'autocorrection, pas de dictée, CJK dégradé) : `MarkedTextRange`, `SetMarkedText`/`UnmarkText` → `state.SetComposingText`/`FinishComposing` ; `SelectedTextRange` (get/set) → `state.Snapshot()`/`state.SetSelection` ; `TextInRange`/`ReplaceRange` → `state.GetText...`/`state.CommitText` |
| 6.3 | `UITextPosition`/`UITextRange` : sous-classes minimales encapsulant un `int` (offset UTF-16) — pas de vraie complexité, juste de la plomberie |
| 6.4 | `GetCaretRectForPosition`, `GetFirstRectForRange`, `GetClosestPositionToPoint` : nécessitent la mesure de texte du composant `TextInput` (§4.2) — dépendance à poser correctement |
| 6.5 | `InputDelegate.TextWillChange`/`TextDidChange`/`SelectionWillChange`/`SelectionDidChange` : appelés autour de toute mutation programmatique (`source == Program` dans `state.Changed`) |
| 6.6 | `UIKeyboardWillShowNotification`/`WillHide` → `Keyboard.Height`, même rôle que 5.8 côté Android |

**Critère d'acceptation** : mêmes trois points que §5, sur simulateur/appareil iOS.

---

## 7. Ce que cette fiche NE couvre PAS mais doit anticiper

- **Recyclage de listes** : `LazyColumn` reste indépendant de ce chantier, aucune dépendance à
  gérer.
- **Accessibilité** : un futur nœud sémantique pour `TextField` devra lire `TextFieldState`
  (rôle "champ de texte", valeur = `Text`, sélection = `SelectionStart/End`). Ne rien faire
  maintenant, mais ne pas encapsuler `TextFieldState` dans une abstraction qui rendrait ces
  informations inaccessibles depuis l'extérieur du composant.
- **BiDi / scripts complexes** (arabe, indien) : nécessitera HarfBuzz (`SkiaSharp.HarfBuzz`).
  Le §4.2 mesure le texte par simple préfixe LTR — ne pas généraliser cette API à un point où
  la remplacer plus tard casserait tout ; garder la mesure de caret derrière une seule fonction
  interne facilement substituable.

---

## 8. Ordre de travail recommandé

1. **§3 — Contrat `IPlatformTextInput`** (isolé, rapide, débloque tout le reste)
2. **§4.2 — Rendu mono-ligne** + **§4.3 — Focus** (testable visuellement sans clavier réel :
   `state.PlaceCaret()` appelé à la main suffit à vérifier le rendu)
3. **§5 — Android complet**, validé avec Gboard latin puis japonais/chinois
4. **§6 — iOS complet**, validé avec le clavier système
5. **§5.8 / §6.6 — Insets clavier**, `ImeAction`, `SecureEntry`
6. **Poignées de sélection tactile, presse-papiers, menu contextuel** (couper/copier/coller natif)
7. **Multiligne** (dépend de la notion de ligne logique déjà présente dans `TextFieldState`,
   §4.2 devra ajouter le wrap visuel), puis **BiDi**

## 9. Checklist de sortie (Definition of Done pour l'ensemble du chantier)

- [ ] `TextFieldStateTests.Run() == 0` (déjà en place, 30 tests)
- [ ] `IPlatformTextInput` compile sans référence Android/iOS dans `XenoUI.Core`
- [ ] Rendu mono-ligne : texte, placeholder, caret, sélection, soulignement de composition
- [ ] Android : Gboard latin + japonais validés manuellement, clavier matériel validé
- [ ] iOS : clavier système validé manuellement, dictée non cassée
- [ ] `UpdateSelection`/`textWillChange` appelés correctement (pas de désync IME observée après
      un `SetText` programmatique pendant que le champ a le focus)
- [ ] Insets clavier : le champ focus reste visible quand le clavier apparaît
- [ ] Copier/coller fonctionne dans les deux sens (vers et depuis une autre app)
