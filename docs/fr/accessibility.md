# Accessibilité

[Documentation](index.md) · [Personnaliser](customization.md) · [Animations](animations.md) · [Localisation](localization.md)

Parce que XenoUI **dessine ses propres pixels**, l’accessibilité n’est pas gratuite comme avec des vues natives. Cette page dit
ce que le framework fait aujourd’hui, ce qu’il ne fait **pas encore**, et les pratiques qui rendent une application utilisable par
plus de monde dès maintenant. C’est un document vivant : le framework est pré-V1.

## Ce que le framework fournit aujourd’hui

| Domaine | État |
|---|---|
| **Champs de texte** | Adossés à un contrôle de texte natif caché : clavier, IME, dictée et **libellé** d’accessibilité fonctionnent. Le libellé vaut par défaut le placeholder (ou « Text input ») et se fixe avec `InputOptions(label: "Nom complet")`. |
| **Champs sécurisés** | `InputOptions(secure: true)` / `obscureText` masquent le texte ; le mode sécurisé ne le révèle pas. |
| **Mouvement réduit** | Le réglage système est lu (`IMotionPreferencesCapability`) et appliqué : les overlays n’animent pas, la sélection de navigation applique son état final immédiatement, le tiroir passe à une durée nulle. |
| **Droite à gauche** | Miroir complet de la mise en page, du hit-testing et de l’alignement du texte ([Mises en page](layouts.md)). |
| **Texte agrandi** | Les tailles sont en dp ; le texte passe à la ligne par défaut. Les mises en page avec `FlexGrow` et `MaxLines` tiennent quand les polices grandissent. |

## Ce qu’il ne fournit pas encore

- **Pas d’arbre sémantique pour les éléments dessinés.** TalkBack et VoiceOver ne peuvent pas découvrir les boutons, listes ou
  titres dessinés par XenoUI, et il n’y a pas de modificateur `contentDescription` / `accessibilityLabel`. Une conception
  prévue couvre des nœuds sémantiques (rôle, libellé, valeur, état, actions, bornes), la correspondance avec
  `AccessibilityNodeProvider` (Android) et `UIAccessibility` (iOS), l’ordre de focus, le contenu défilant et les régions
  dynamiques ([Texte, IME et NativeTunnel](architecture/text-input-and-native.md)).
- Pas d’ordre de focus ni de navigation clavier pour les contrôles personnalisés, pas de régions dynamiques, pas de tests
  automatisés de lecteur d’écran.

Si le support des lecteurs d’écran est une exigence ferme de votre produit, ce framework n’est pas prêt pour vous
([Pourquoi XenoUI ?](why-xenoui.md)).

## Ce que vous pouvez faire dès aujourd’hui

**Rendre les visuels robustes**

- **Contraste** : au moins 4,5:1 pour le texte courant et 3:1 pour le grand texte et les icônes. Testez chaque thème et surface translucide.
- **Cibles tactiles** : au moins 44–48 dp de côté (`Box(44, 44, icon).CenterContent().Clickable(tap)`).
- **Ne vous fiez pas à la couleur seule** : associez une erreur rouge à une icône ou un texte.
- **Ne figez pas les hauteurs autour du texte** : préférez padding, `MaxLines` et `FlexGrow` ; testez avec un texte long.
- **Respectez le mouvement réduit** dans vos animations : durée 0 ([Animations](animations.md)).

**Étiqueter vos champs de texte**

```csharp
TextField(_email, "name@example.com",
    InputOptions(NativeKeyboardType.Email, autofill: NativeAutofillHint.Email, label: "Adresse e-mail"))
```

**Offrir des alternatives aux gestes.** Le glisser pour fermer existe sur les snackbars et feuilles, mais chacun se ferme aussi
par un bouton, le voile ou le Retour système ; gardez un contrôle visible équivalent pour toute action réservée à un geste.

**Fournir des équivalents textuels** aux boutons d’icône seule : affichez un libellé, ou un popup à l’appui long, en attendant
la sémantique du framework.

**Tester avec les réglages système** : plus grande taille de police, taille d’affichage, mouvement réduit, contraste élevé,
mode sombre, langue RTL, clavier matériel. Utilisez `[Preview(RightToLeft = true)]` et `[Preview(Device = …)]`.

## Liste de contrôle

- [ ] Chaque champ de texte a un libellé (`InputOptions(label: …)`).
- [ ] Contrastes et tailles de cibles vérifiés.
- [ ] Information jamais portée par la couleur seule.
- [ ] La mise en page survit aux grandes polices et aux textes longs (traduits).
- [ ] Les animations respectent le mouvement réduit.
- [ ] Chaque geste a une alternative visible.
- [ ] RTL vérifié.
