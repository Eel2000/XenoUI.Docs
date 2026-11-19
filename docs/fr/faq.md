# FAQ

[Documentation](index.md) · [Tutoriel](tutorial-first-app.md) · [Glossaire](glossary.md) · [Dépannage](troubleshooting.md) · [Déboguer l’interface](debugging.md)

Réponses courtes aux questions les plus fréquentes des débutants. Pour les problèmes de build et d’appareil, voir
[Dépannage](troubleshooting.md) ; pour traquer un bug d’interface, [Déboguer l’interface](debugging.md).

## Concepts

### Pourquoi mon écran ne se met-il pas à jour quand je change une valeur ?

Soit la valeur n’est pas **liée**, soit la structure a changé sans reconstruction.

- Une valeur qui change souvent doit être un `State<T>` passé à une liaison : `Text(state)`, `Text($"{state}")`,
  `.Background(state)`. Lire `state.Value` dans `Body()` ne s’abonne à **rien**.
- Si un élément apparaît ou disparaît, appelez `Rebuild()`.

Voir [État](state.md).

### Pourquoi mon état revient-il à sa valeur initiale ?

Vous l’avez créé dans `Body()`. `UseState`, `UseScroll`, `UseTextEdit`… retournent un **objet neuf à chaque appel**, et
`Body()` se rappelle à chaque `Rebuild()`. Déclarez-les comme **champs** :

```csharp
private readonly State<int> _count = UseState(0);   // ✓ champ
```

### Quand utiliser `Rebuild()` et quand un `State` ?

Un `State` quand une **valeur** change (texte, couleur, progression). `Rebuild()` quand la **structure** change (un élément est
ajouté, une branche bascule). En cas de doute, commencez par un `State`. Voir
[Penser en XenoUI](thinking-in-xenoui.md).

### XenoUI compare-t-il les arbres comme React ou Compose ?

Non. `Rebuild()` rappelle le `Body()` de la page et remplace sa scène. Les mises à jour fines viennent des liaisons `State`.

### Puis-je garder un `XenoComponent` dans un champ ?

Non. Un `XenoComponent` n’est valable que dans la composition qui l’a créé. Gardez vos **données** et recomposez.

## Mise en page et style

### Comment remplir la largeur ?

Sur le parent : `.CrossAxis(CrossAxisAlignment.Stretch)` ; sur l’enfant : `.AlignSelf(CrossAxisAlignment.Stretch)`. Pour se
partager l’espace dans une `Row`, `.FlexGrow(1)`. Voir [Mises en page](layouts.md).

### Pourquoi mon texte n’est-il pas centré ?

La boîte du texte est aussi étroite que le texte. Donnez-lui `.AlignSelf(Stretch)` ou placez-le dans un conteneur
`.CenterContent()`.

### Pourquoi ne puis-je pas mettre un élément dans un coin d’un `ZStack` ?

Un `ZStack` prend la position horizontale de **tous** ses enfants depuis son propre `MainAxis`. Placez l’élément dans une
`Row` avec un `Spacer()` qui remplit la pile.

### Où est `Modifier` ? Où est `weight` ?

Les modificateurs s’enchaînent sur le résultat (`Box().Padding(8)`), et `weight` est `FlexGrow`. Voir
[De Jetpack Compose à XenoUI](compose-comparison.md).

### Comment changer la couleur du texte d’un bouton fourni ?

Par son **paramètre**, pas par un modificateur : `Button("OK", tap, textColor: 0xFF000000)`. Les modificateurs agissent sur
l’élément auquel on les applique, pas sur ses enfants. Voir [Personnaliser](customization.md).

### Y a-t-il un mode sombre ?

Il n’y a pas de système de thème intégré : un thème est une classe à vous, appliquée explicitement, et le changer demande
une reconstruction. Voir [Personnaliser](customization.md).

### Quelles unités et quel format de couleur ?

Les tailles sont en **dp** ; les couleurs en `0xAARRGGBB` (`0xFF6D28D9` est opaque).

## Listes, navigation et saisie

### Ma liste n’affiche rien

Une liste paresseuse a besoin d’une **taille bornée** sur son axe : `.FlexGrow(1)` dans une `Column`, ou une `.Height(…)`
explicite. Voir [Mises en page](layouts.md).

### Pourquoi une `key` sur les listes ?

Elle identifie un élément quand la fenêtre visible se déplace. Sans clé, l’identité est l’index. Utilisez un identifiant stable.

### La navigation ne fait rien

La page n’est pas enregistrée : appelez `Page<MaPage>()` dans `App.OnStart`. Voir [Cycle de vie](lifecycle.md).

### Comment passer une valeur à une autre page ?

`XenoPage<TArgs>` et `Navigate<TPage, TArgs>(args)` ; lisez-la dans `OnNavigatedTo`. Voir
l’[étape 5 du tutoriel](tutorial-first-app.md).

### Comment lire ce que l’utilisateur a tapé ?

`controller.Value.Text` sur le `TextEditController`, ou abonnez-vous à `controller.Changed`. Voir [État](state.md).

## Travail asynchrone et données

### Comment appeler une API ?

Pas dans `Body()`. Placez l’appel dans un modèle ou dans `OnActivated`, exécutez-le hors du thread UI et revenez avec
`IUiDispatcherCapability.Post`. Voir [Effets de bord](side-effects.md) et [Architecture d’une application](app-architecture.md).

### Plantage ou anomalie en mettant à jour depuis un thread de fond

Les affectations de `State` doivent se faire sur le **thread UI**. Utilisez `Native.Require<IUiDispatcherCapability>().Post(() => …)`.
`Rebuild()` est le seul appel utilisable depuis n’importe quel thread.

### Ma page se met à jour après que je l’ai quittée

Protégez les rappels tardifs avec `IsActive` et annulez le travail dans `OnDeactivated`. Voir [Effets de bord](side-effects.md).

## Plateformes et outils

### Est-ce que ça tourne sur iOS ?

Les projets ciblent iOS, mais compiler et lancer exigent macOS et Xcode, et iOS n’est pas encore validé de bout en bout.
Android est le chemin de référence. Voir [Pourquoi XenoUI ?](why-xenoui.md).

### Puis-je prévisualiser sans appareil ?

Oui : `[Preview]` dans Visual Studio ou VS Code, ou `PreviewRenderer.Render` dans un test ([Tester](testing.md)).

### Pourquoi .NET 11 et CoreCLR ?

Cette branche cible .NET 11 et exécute les applications Android sur CoreCLR ; Mono n’est qu’une propriété plus loin. Voir les
[notes de version](release-notes.md) et la [migration](migration.md).

### Y a-t-il un widget Switch, Checkbox ou Slider ?

Pas encore. Composez-les avec `Box`, `Row` et `Clickable` : voir les [recettes](components.md).

### Est-ce accessible ?

En partie. Voir [Accessibilité](accessibility.md).

### Puis-je l’utiliser en production ?

C’est une **préversion pré-V1**. Évaluez avec votre propre tolérance au risque et lisez la
[trajectoire V1](architecture/decisions-v1.md).
