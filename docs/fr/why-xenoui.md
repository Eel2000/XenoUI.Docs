# Pourquoi XenoUI ?

[Documentation](index.md) · [Tutoriel](tutorial-first-app.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [De Jetpack Compose](compose-comparison.md) · [FAQ](faq.md)

XenoUI est un **framework d’interface mobile déclaratif pour .NET**. Vous décrivez l’interface en C# en appelant des
fonctions ; XenoUI exécute son propre moteur de mise en page et dessine tout avec **SkiaSharp** sur Android et iOS. Cette
page explique ce que cela signifie en pratique, à qui il convient et — tout aussi important — ce qu’il ne fait **pas encore**.

## En une minute

```csharp
public override XenoComponent Body() =>
    Column(
        Text($"Total : {_count}", 24),
        Button("+1", () => _count.Value++)
    ).Gap(12).Padding(16);
```

- **Un seul code C#, sans XAML ni balisage.** L’interface est du code : fonctions, modificateurs, état.
- **Dessiné, pas enveloppé.** Boutons, listes, champs de texte et animations sont peints par XenoUI : ils ont le même aspect et
  le même comportement sur Android et iOS. Seul ce que l’OS fait mieux (clavier et IME, presse-papiers, décodage vidéo)
  reste natif, via un pont typé appelé `NativeTunnel`.
- **Pensé pour les listes et les gestes fluides.** Listes virtualisées mesurées à la demande, aucune allocation par frame sur
  les frames stables, scènes conservées entre onglets.

## Comparaison

| | XenoUI | .NET MAUI | Jetpack Compose | Flutter |
|---|---|---|---|---|
| Langage | C# | C# + XAML | Kotlin | Dart |
| Plateformes | Android, iOS | Android, iOS, Windows, macOS | Android (+ Multiplatform) | Android, iOS, web, bureau |
| Modèle d’UI | Fonctions déclaratives | Contrôles natifs, XAML ou C# | Fonctions déclaratives | Widgets déclaratifs |
| Rendu | Moteur propre sur Skia | Contrôles natifs | Skia / canvas Android | Moteur propre (Impeller/Skia) |
| Aspect | Identique sur les deux plateformes | Natif à la plateforme | Material / personnalisé | Material / personnalisé |
| Mises à jour | `State<T>` liés explicitement + `Rebuild()` de page | Liaison de données | Recomposition intelligente | Reconstruction de widgets |
| Maturité | **Préversion pré-V1** | Production | Production | Production |

Si vous connaissez l’un d’eux, la [comparaison avec Jetpack Compose](compose-comparison.md) associe les concepts un à un.

## Quand XenoUI convient

- Vous êtes une **équipe .NET** et voulez de l’UI déclarative sans XAML.
- Vous voulez une interface **identique au pixel près** sur Android et iOS, avec des visuels et animations libres.
- Votre application est **riche en listes et en gestes** (fils, galeries, écrans type vidéo courte, messagerie).
- Vous aimez les **petits blocs testables** : les composants sont de simples fonctions et l’on peut produire des captures dans
  un test unitaire ([Tester](testing.md)).

## Quand ce n’est pas (encore) le bon choix

Soyez honnête avec vous-même avant de commencer :

- **C’est pré-V1.** Les API peuvent encore changer ; un comportement n’est un contrat que si un test le couvre et que la doc le
  dit ([Décisions et trajectoire V1](architecture/decisions-v1.md)).
- **Android est le chemin validé.** iOS exige macOS et Xcode et n’est pas encore validé de bout en bout.
- **Pas de cible bureau ni web**, pas d’application Windows/macOS.
- **Pas d’arbre d’accessibilité intégré** pour les éléments dessinés : le support de TalkBack/VoiceOver est partiel
  ([Accessibilité](accessibility.md)).
- **Pas de widgets Material prêts** pour interrupteurs, cases à cocher ou curseurs ; on les compose
  ([Catalogue des composants](component-catalog.md)).
- **Pas de restauration d’état après la mort du processus**, pas de liens profonds, pas de résultats de navigation typés
  ([Cycle de vie](lifecycle.md), [État](state.md)).
- **Pas de concepteur visuel.** Il y a des aperçus dans Visual Studio et VS Code, pas d’éditeur par glisser-déposer.

## Ce que vous avez aujourd’hui

| Domaine | Inclus |
|---|---|
| Composition | `Column`, `Row`, `ZStack`, `Box`, `Text`, `Image`, `Icon`, listes, pager — [catalogue](component-catalog.md) |
| État | `State<T>`, état de défilement et de liste, contrôleurs de texte — [État](state.md) |
| Navigation | Piles, scaffold adaptatif (barre / rail / tiroir), transitions — [Navigation](navigation-scaffold.md) |
| Overlays | Snackbar, alerte, dialogue, popup, feuille du bas, barre du haut |
| Saisie | Champs dessinés avec l’IME natif, RTL, emoji, CJK |
| Mouvement | Animations de pages, de sélection et d’overlays — [Animations](animations.md) |
| Média | Lecteurs audio et vidéo derrière un contrôleur portable — [Média](media-controller.md) |
| Outils | `Xeno.SDK`, templates, CLI `xeno`, aperçus Visual Studio et VS Code — [Outils](xeno-sdk.md) |

## Ensuite

1. [Tutoriel : votre première application](tutorial-first-app.md) — 30 minutes, en pratique.
2. [Installation](installation.md) si vous n’avez pas encore les outils.
3. [Penser en XenoUI](thinking-in-xenoui.md) pour le modèle mental.
