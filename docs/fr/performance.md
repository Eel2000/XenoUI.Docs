# Performance

[Documentation](index.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [État](state.md) · [Mises en page](layouts.md) · [Benchmark A15](runtime-benchmark-a15.md)

Ce guide rassemble les pratiques qui gardent une application XenoUI fluide : **où le moteur est déjà
économe**, ce qui coûte réellement, comment structurer l’état et les listes, et comment mesurer sans se
tromper. Il se lit comme la page « Performance » d’un guide d’interface déclarative : d’abord les règles
simples, ensuite les détails.

> Mécanique des drapeaux « sales » : [Runtime §4](architecture/runtime.md#4-dirty-flags-et-mises-à-jour-fines). Trajectoire de recomposition : [Décisions §5](architecture/decisions-v1.md#5-recomposition-future--contraintes-de-design).

## Sommaire

1. [Le budget d’une frame](#1-le-budget-dune-frame)
2. [Ce que le moteur fait pour vous](#2-ce-que-le-moteur-fait-pour-vous)
3. [Les dix règles](#3-les-dix-règles)
4. [Liaison fine ou `Rebuild()`](#4-liaison-fine-ou-rebuild)
5. [Listes](#5-listes)
6. [Texte, images et ombres](#6-texte-images-et-ombres)
7. [Animations](#7-animations)
8. [Travail hors du thread UI](#8-travail-hors-du-thread-ui)
9. [Mesurer](#9-mesurer)
10. [Runtime : CoreCLR et Mono](#10-runtime--coreclr-et-mono)
11. [Liste de contrôle](#11-liste-de-contrôle)

---

## 1. Le budget d’une frame

À 60 Hz, une frame dure ~16,7 ms ; à 120 Hz, ~8,3 ms. Dans ce temps, le thread UI doit : traiter les entrées,
exécuter les callbacks, remesurer/replacer ce qui est « sale », puis dessiner. Dépasser le budget produit des
saccades. La règle générale : **faire peu de choses par frame, et jamais de travail lourd sur le thread UI**.

## 2. Ce que le moteur fait pour vous

- **Pas d’allocation par frame** sur les frames stables : le rendu, le layout et le texte réutilisent leurs
  tampons (c’est un contrat testé par la suite du framework).
- **Dirty flags** : un changement de couleur ou d’ombre ne relance pas la mise en page ; un changement de taille
  ne remesure que ce qui en dépend.
- **Virtualisation** : `LazyColumn`/`LazyRow` ne composent que la fenêtre visible plus une marge.
- **Textes mis en cache** : paragraphes mis en forme, polices et images décodées sont mises en cache.
- **Transitions précompilées** : `NavTransition`, `NavigationSelectionTransition` et `OverlayAnimation` sont
  des tableaux de pistes évalués sans appel à votre code.
- **Pas de diff d’arbre** : il n’y a pas de coût de comparaison ; il y a en revanche un coût de **recomposition
  complète de la page** à chaque `Rebuild()` (voir §4).

Votre rôle : ne pas annuler ces garanties par du travail dans `Body()`, des listes non virtualisées ou des
reconstructions en rafale.

## 3. Les dix règles

1. **Un `State` d’abord, `Rebuild()` seulement si la structure change.**
2. **Ne reconstruisez pas pendant un geste** (défilement, glissement, frappe).
3. **Utilisez `LazyColumn`/`LazyRow`** dès qu’une liste dépasse quelques dizaines d’éléments.
4. **Donnez une clé stable** à chaque élément de liste.
5. **Gardez `Body()` et les fonctions d’élément rapides** : pas de réseau, de disque, de LINQ lourd, de formatage
   coûteux.
6. **Ne créez pas d’objets persistants dans `Body()`** (`UseState`, `UseScroll`…) : perte d’état et allocations.
7. **Construisez les transitions et animations une fois** (`static readonly`).
8. **Déplacez le travail lourd hors du thread UI** et revenez par le dispatcher.
9. **Évitez d’animer des tailles** (largeur, `FlexGrow`) sur de grands conteneurs ; préférez couleurs et alpha,
   ou les scènes animées par le framework.
10. **Mesurez en Release, sans débogueur, sur un appareil réel.**

## 4. Liaison fine ou `Rebuild()`

| | Liaison fine (`State<T>`) | `Rebuild()` |
|---|---|---|
| Ce qui est refait | Une entité (texte, couleur) | `Body()` de la page entière et sa scène |
| Coût | Très faible | Proportionnel à la taille de la page |
| Perte d’état | Non | Les objets créés dans `Body()` |
| À utiliser pour | Valeurs qui changent souvent | Changements de structure rares |

Un exemple concret, `PeoplePage` : appuyer sur « Follow » met à jour **deux `State` de la ligne** et **un
`State` du compteur du haut** ; il n’y a aucun `Rebuild()` alors que la liste a 10 000 éléments.

À l’inverse, un `Rebuild()` à chaque caractère tapé recompose toute la page : utilisez
`Text(_preview)` lié à un `State<string>` mis à jour par `controller.Changed`.

**Divisez les pages lourdes** : une page très grande reconstruite entièrement pour un détail coûte plus que
plusieurs pages ou une liste virtualisée. Isolez les parties qui changent souvent dans des `State`.

## 5. Listes

```csharp
LazyColumn(items, ItemRow, key: item => item.Id)    // fenêtre visible seulement, tailles mesurées
```

- **Clé stable** : sans clé, l’identité est l’index ; une insertion en tête redonne tous les éléments.
- **Fonction d’élément légère** : elle est rappelée quand la fenêtre bouge. Pré-calculez (formatage de dates,
  libellés) dans le modèle, pas dans la fonction.
- **Pas de `Rebuild()` à chaque défilement** : la liste gère sa fenêtre seule.
- **Hauteur de liste bornée** (`FlexGrow(1)` ou `Height`) pour éviter de matérialiser trop d’éléments.
- **États par ligne** : créez-les dans la fonction d’élément (vivants tant que la ligne est matérialisée) et gardez
  la vérité dans un champ de page ([État §8](state.md#8-état-par-élément-de-liste)).
- **Images dans les listes** : tailles explicites (`Size`) pour éviter des remesures ; décodage une fois par asset.
- Pour un fil plein écran (une page = un écran), `VerticalPager` plutôt qu’une `LazyColumn` : une seule page
  active porte la ressource coûteuse (vidéo), les autres affichent un poster.

## 6. Texte, images et ombres

| Élément | Conseil |
|---|---|
| Texte long | `MaxLines(n, ellipsis: true)` ; évitez `AutoSize` sur de grands blocs |
| Texte qui change souvent | `Text(State<T>)` : seul ce texte est remesuré |
| Images | `Image("nom")` décode une fois ; `ImageFile(State<string?>)` met en cache par chemin ; dimensionnez explicitement |
| Ombres | `Elevation(0)` supprime l’ombre ; un flou nul est sauté ; évitez de très grosses ombres sur des dizaines d’éléments |
| Opacité de scène | Une scène avec `Opacity < 1` passe par une couche composée : limitez-la aux transitions courtes |
| Clip / `CornerRadius` | Peu coûteux ; évitez de les cumuler avec de grosses ombres |
| Polices d’icônes | Une police pour toutes les icônes ; glyphes plutôt qu’images pour les icônes simples |

## 7. Animations

- Les animations de **scène** (pages, sélection de navigation, overlays) sont les moins chères : pas
  d’allocation, pas de re-layout.
- Une animation d’application (timer + `State`) coûte **un `Post` et une invalidation par pas** : bornez-la en
  durée et en fréquence ; animez des couleurs plutôt que des tailles ([Animations §5](animations.md#5-animer-soi-même-pas-à-pas)).
- **Mouvement réduit** : honorez-le (durée 0) ; cela réduit aussi le travail.
- Annulez un minuteur avant d’en lancer un nouveau sur la même propriété.

## 8. Travail hors du thread UI

Tout ce qui dépasse quelques millisecondes quitte le thread UI : réseau, JSON, disque, tri, calculs. Revenez
avec `IUiDispatcherCapability.Post`, publiez un **snapshot immuable**, et ne touchez aux `State` que sur le thread UI
([Effets de bord](side-effects.md)). Le sample parse même le JSON hors UI (`Task.Run` dans `RemoteFeed<T>`).

Attention aux callbacks `Clickable` : un `Task.Run(...).Wait()` ou un accès disque synchrone fige l’interface.

## 9. Mesurer

1. **Release**, sans débogueur attaché, sur un **appareil réel** (un émulateur ne reflète pas le GPU).
2. **Mêmes conditions** à chaque mesure : luminosité, mode économie d’énergie, état thermique (laissez refroidir
   entre deux séries).
3. **Séparez** la mesure CPU du thread de rendu (Refresh) des temps GPU ; le dépôt mesure le temps CPU de
   `Refresh`.
4. **Scénarios répétables** : défilement continu, navigation répétée, frappe, basculement d’onglet.
5. **Allocation** : sur des frames stables, l’objectif du framework est zéro octet ; surveillez les collectes
   Gen0 pendant un scénario.
6. **Fiabilité** : plusieurs séries, médiane et p95, pas une seule exécution.

Le dépôt fournit des outils : `tests/XenoUI.Core.RegressionTests --benchmark` (CPU PC), l’APK
`tests/XenoUI.Android.Benchmarks` (appareil) et la méthode détaillée dans [Benchmark A15](runtime-benchmark-a15.md) et
[Benchmark runtime](runtime-benchmark.md). Ces chiffres sont **datés** et mesurés sur un runtime précis : ils
servent de repère, pas de promesse.

## 10. Runtime : CoreCLR et Mono

Les applications Android de cette branche s’exécutent sur **CoreCLR** (`UseMonoRuntime=false`) avec .NET 11 ; le
retour à Mono se fait par `UseMonoRuntime=true` ([Guide développeur §2](developer-guide.md#2-prérequis)). Les
mesures publiées dans les benchmarks datent de .NET 10 sur Mono : **remesurez sur votre cible** avant de
conclure, notamment le démarrage à froid et la taille de l’application. Les options de publication (AOT,
trimming) changent fortement le profil : indiquez-les avec vos chiffres.

## 11. Liste de contrôle

- [ ] Aucun `UseState`/`UseScroll`/`UseTextEdit` dans `Body()`.
- [ ] Toute liste de plus de ~30 éléments est une `LazyColumn`/`LazyRow` avec une clé stable.
- [ ] Les valeurs qui changent souvent sont des `State` liés, pas des `Rebuild()`.
- [ ] Aucun `Rebuild()` dans un geste, une frappe ou un rappel de défilement.
- [ ] Aucun travail bloquant dans `Body()`, `Clickable` ou les fonctions d’élément.
- [ ] Transitions et animations créées une seule fois.
- [ ] Images dimensionnées ; textes longs bornés (`MaxLines`).
- [ ] Mouvement réduit respecté.
- [ ] Mesuré en Release sur appareil, plusieurs séries.
