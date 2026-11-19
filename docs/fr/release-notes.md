# Notes de version

[Documentation](index.md) · [Guide de migration](migration.md) · [Installation](installation.md) · [Pourquoi XenoUI ?](why-xenoui.md)

Ce qui a changé dans chaque préversion de XenoUI. XenoUI est **pré-V1** : une préversion peut contenir des changements
cassants, et chaque entrée dit ce que vous devez faire. Le site documente l’état courant du code ; pour la version exacte des
paquets, voir [Installation](installation.md).

## Non publié (branche courante)

**Plateforme**

- **.NET 11.** Toutes les bibliothèques, le SDK, les templates, les outils et les tests ciblent `net11.0` / `net11.0-android` /
  `net11.0-ios`. Exige le SDK .NET 11. → [Guide de migration](migration.md)
- **CoreCLR sur Android.** `Xeno.SDK` fixe `UseMonoRuntime=false` pour les applications Android. Mettez `UseMonoRuntime=true`
  pour revenir à Mono.
- **Plateforme Android API 37** et workload requis par `net11.0-android`.

**Corrections**

- Les frames Android sont cadencées sur la vsync et les listes paresseuses ne refont plus leur fenêtre par élément (défilement plus
  fluide sur écrans 120 Hz).
- Le texte n’est plus rogné aux densités d’écran fractionnaires ; correctifs de mise en page sur tablette.

**Documentation**

- Nouveaux guides : [Tutoriel](tutorial-first-app.md), [Penser en XenoUI](thinking-in-xenoui.md), [État](state.md),
  [Cycle de vie](lifecycle.md), [Effets de bord](side-effects.md), [Mises en page](layouts.md),
  [Créer un composant](components.md), [Personnaliser](customization.md), [Animations](animations.md),
  [Architecture d’une application](app-architecture.md), [Tester](testing.md), [Performance](performance.md),
  [Publier](publishing.md), [Accessibilité](accessibility.md), [Localisation](localization.md),
  [Capacités natives](native-capabilities.md), [Déboguer](debugging.md), [Catalogue des composants](component-catalog.md),
  [Référence d’API](reference/index.md), [Glossaire](glossary.md), [FAQ](faq.md), [Pourquoi XenoUI ?](why-xenoui.md),
  [De Jetpack Compose](compose-comparison.md).
- Les captures sont générées par l’aperçu headless ; le code du tutoriel est compilé contre les sources.

## 0.1.0-preview.6 — 20 octobre 2026

**Nouveautés**

- **Overlays :** `Snackbar`, `Alert`, `Dialog`, `Popup`, `BottomSheet`, avec `OverlayAnimation` personnalisable, glisser pour
  fermer et popups ancrés qui suivent leur ancre.
- **Barre du haut du scaffold :** une barre d’application fixe au-dessus de la page (`ITopBarPage`, `TopBars.ForActivePage`),
  reconstruite par page et animée avec elle.
- **SDK :** imports implicites de `XenoUI.StandardLibrary.Overlays` et `Shell`, alias `Dialog` sur Android ;
  `NativeWindowMetrics.SafeAreaTop` pour l’encoche iOS.

**Corrections**

- Navigation : le retour est le miroir exact de l’aller (la page qui part reste au-dessus) ; les transitions démarrent à leur
  première image ; les barres du haut suivent leurs pages ; un tiroir modal garde la barre du bas sous son voile ; les
  fondus enchaînés gardent la page sortante comme fond opaque.
- iOS : les rappels des overlays s’exécutent sur le thread UI.

## Entre preview.4 et preview.6 (octobre 2026)

- **Extension VS Code** avec le panneau XenoUI Preview et la création de projets, pages et composants sur macOS, Linux et Windows.
- **Média iOS :** backend AVPlayer avec vidéo native sous l’interface ; fin de lecture gérée sur le thread principal.
- **SDK :** `OutputType` vaut `Exe` par défaut avant `Microsoft.NET.Sdk` (corrige un `CS8805` iOS).
- Améliorations des aperçus et templates Visual Studio et VS Code.

## 0.1.0-preview.4 — 12 octobre 2026

- **Aperçus de pages** : elles s’attachent à leur objet application sans le démarrer (`[Preview]` sur les pages).
- **DSL sans `new`** : `UseState`, `UseStates`, `UseScroll`, `UseLazyList`, `UseTextEdit`, `InputOptions`, `Selection`, `Caret`,
  `Glyph` — déclarer l’état sans `new`.
- `NativeTunnel.CreatePlayer(MediaKind)` et `Configure(volume, speed, loop)` pour le média.
- Les aperçus compilent et rendent avec le runtime XenoUI du projet.

## 0.1.0-preview.3 — 12 octobre 2026

- **Templates :** `xenoui` (application), `xenoui-page`, `xenoui-component`.
- **Extension Visual Studio** avec la fenêtre XenoUI Preview et un serveur de rendu isolé.
- Packaging NuGet (`packages/`, `Pack-NuGet.ps1`), documentation de l’installation et de l’aperçu.

## Versions antérieures

Les hôtes Android et iOS ont été fusionnés dans `XenoUI.Platforms` ; l’attribut `[Preview]` et le moteur d’aperçu headless ont été
introduits ; l’ordre du texte bidirectionnel et une démo arabe droite à gauche ont été ajoutés. Voir l’historique du dépôt.

## Lire une entrée

| Repère | Signification |
|---|---|
| **Nouveautés** | Une capacité que vous pouvez commencer à utiliser |
| **Corrections** | Un comportement modifié pour correspondre à celui documenté |
| **→ Guide de migration** | Vous devez changer quelque chose |

Un comportement non couvert par un test et non décrit dans la documentation n’est **pas** un contrat, même s’il marche
aujourd’hui ([Décisions et trajectoire V1](architecture/decisions-v1.md)).
