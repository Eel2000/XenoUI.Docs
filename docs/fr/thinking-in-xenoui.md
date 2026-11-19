# Penser en XenoUI

[Documentation](index.md) · [État](state.md) · [Cycle de vie](lifecycle.md) · [Effets de bord](side-effects.md) · [Mises en page](layouts.md) · [Comparaison avec Jetpack Compose](compose-comparison.md)

XenoUI est un framework d’interface **déclaratif** : vous décrivez *à quoi ressemble* l’écran pour un état
donné, pas *comment* le modifier étape par étape. Cette page pose le modèle mental sur lequel reposent tous les
autres guides. Si vous venez de Jetpack Compose, la plupart des idées vous seront familières ; les différences
importantes sont signalées dans [chaque section](#4-ce-qui-diffère-de-jetpack-compose) et résumées dans
[Comparaison avec Jetpack Compose](compose-comparison.md).

## Sommaire

1. [Le paradigme déclaratif](#1-le-paradigme-déclaratif)
2. [Une interface est une fonction de l’état](#2-une-interface-est-une-fonction-de-létat)
3. [Les deux façons de mettre à jour l’écran](#3-les-deux-façons-de-mettre-à-jour-lécran)
4. [Ce qui diffère de Jetpack Compose](#4-ce-qui-diffère-de-jetpack-compose)
5. [Flux de données unidirectionnel](#5-flux-de-données-unidirectionnel)
6. [Sous le capot : entités et dessin](#6-sous-le-capot--entités-et-dessin)
7. [Règles à retenir](#7-règles-à-retenir)

---

## 1. Le paradigme déclaratif

Avec les vues impératives classiques, on construit un arbre d’objets puis on le **mute** : `label.Text = "…"`,
`button.Visible = false`. Chaque mutation est une occasion de désynchroniser l’écran et les données.

XenoUI inverse la logique. Une **fonction** décrit l’écran à partir des données ; quand les données changent,
la description est refaite ou une valeur liée est mise à jour :

```csharp
public override XenoComponent Body() =>
    Column(
        Text($"Total : {_count}", 24),
        Button("+1", () => _count.Value++)
    ).Gap(12).Padding(16);
```

Vous ne retrouvez jamais « le label du total » pour le modifier. Le texte est **lié** à `_count` : quand la
valeur change, il se met à jour. Le bouton ne change que l’état ; l’affichage suit.

## 2. Une interface est une fonction de l’état

Une page ou un composant est une fonction : mêmes entrées, même sortie.

```csharp
public static XenoComponent Greeting(string name, bool online) =>
    Row(
        Box(10, 10).Background(online ? 0xFF22C55E : 0xFF9CA3AF).CornerRadius(5),
        Text($"Bonjour, {name}", 16)
    ).Gap(8).CrossAxis(CrossAxisAlignment.Center);
```

Conséquences :

- **Pas d’état caché** dans la fonction : tout ce dont elle dépend vient de ses paramètres ou de champs
  persistants.
- **Pas d’effet de bord** : `Body()` et les composants ne lisent ni disque ni réseau, ne démarrent aucune tâche
  et ne modifient aucun état. Ils peuvent être rappelés à tout moment (reconstruction, aperçu).
- **Prévisibilité** : pour comprendre un écran, il suffit de lire sa fonction et l’état qu’elle reçoit.

### Composer, c’est appeler des fonctions

Il n’y a pas de classe `Button` à instancier puis à ajouter à un parent. `Column(a, b, c)` appelle les fonctions
`a`, `b`, `c`, qui retournent des `XenoComponent` ; `Column` les adopte comme enfants. L’arbre est
simplement la trace de ces appels. C’est pourquoi un composant est une fonction ordinaire, que vous pouvez
extraire, nommer, paramétrer et tester.

## 3. Les deux façons de mettre à jour l’écran

C’est la différence la plus importante avec ce que beaucoup de développeurs attendent. XenoUI **ne compare pas**
l’ancien et le nouvel arbre pour ne redessiner que ce qui diffère. Deux mécanismes distincts existent :

### 3.1 Liaison fine (`State<T>`)

Une valeur liée à un `State` met à jour **une seule entité** quand elle change, sans rappeler `Body()` :

```csharp
private readonly State<int> _count = UseState(0);

Text(_count, 28)                                // texte
Text($"Compteur : {_count}")                    // chaîne interpolée réactive
Box(10, 10).Background(_dotColor)               // State<uint>
LinearProgress(_progress, 4)                    // State<float>
```

C’est le chemin **rapide** : à privilégier pour tout ce qui change souvent (compteurs, progression, couleurs,
messages).

### 3.2 Reconstruction (`Rebuild()`)

Quand la **structure** change (un élément apparaît, une liste change de contenu, une branche conditionnelle
bascule), la page appelle `Rebuild()` : `Body()` est rappelée et l’arbre transitoire de la page est remplacé.

```csharp
private bool _expanded;

Button(_expanded ? "Réduire" : "Développer", () => { _expanded = !_expanded; Rebuild(); }),
_expanded ? Text("Détails…") : XenoComponent.None
```

`Rebuild()` ne conserve rien de l’arbre précédent : les **objets persistants** (états, scroll, contrôleurs de
texte) doivent donc vivre **hors** de `Body()`, dans des champs de page.

### 3.3 Lequel choisir ?

| Ce qui change | Outil |
|---|---|
| Un texte, une couleur, une progression | Liaison fine : `State<T>` |
| Plusieurs valeurs indépendantes à la fois | Plusieurs `State<T>` |
| Un élément apparaît / disparaît | `Rebuild()` |
| Le contenu d’une liste change | `Rebuild()` (puis la liste virtualisée ne recompose que la fenêtre visible) |
| Un thème change | `Rebuild()` + `RefreshChrome()` du scaffold |

Règle pratique : **commencez par un `State` ; passez à `Rebuild()` seulement si la forme change.** Détails :
[État et reconstruction](state.md).

## 4. Ce qui diffère de Jetpack Compose

| Idée Compose | En XenoUI |
|---|---|
| Recomposition intelligente : seules les fonctions dont les entrées changent sont réexécutées | **Pas de diff** : `Rebuild()` rappelle `Body()` de la page entière ; les mises à jour fines passent par `State<T>` |
| `remember { }` à l’intérieur d’un composable | Les objets persistants sont des **champs** de la page (ou de la classe du composant) ; une fabrique `UseState` appelée dans `Body()` crée un objet neuf à chaque appel |
| `mutableStateOf` observé automatiquement par tout composable qui le lit | Une liaison est **explicite** : `Text(state)`, `.Background(state)` ; lire `state.Value` dans `Body()` ne s’abonne à rien |
| Effets (`LaunchedEffect`, `DisposableEffect`) liés à la composition | Le travail asynchrone se rattache au **cycle de vie de la page** ([Effets de bord](side-effects.md)) |
| `CompositionLocal` | Pas d’équivalent : passez des paramètres ou lisez `Application` / `Native` dans la page |
| `Modifier` comme valeur composable (`Modifier.padding(8).clickable {}`) | Modificateurs **chaînés sur le résultat** (`Box().Padding(8).Clickable(…)`), qui mutent l’entité |
| Thème `MaterialTheme` ambiant | Thème = **classe à vous**, appliquée explicitement ([Personnaliser](customization.md)) |
| `rememberSaveable` / restauration après mort du processus | **Non fourni** pour l’instant ; l’état est en mémoire |

Ces différences ne sont pas des défauts d’un côté ou de l’autre : XenoUI choisit une exécution simple et
prévisible, sans allocation par frame, plutôt qu’un système de suivi des lectures d’état.

## 5. Flux de données unidirectionnel

Comme dans les architectures modernes d’UI, les données descendent et les événements remontent :

```text
      état (State<T>, paramètres)            événements (Action, callbacks)
  Modèle / Page  ─────────────────▶  Composant  ─────────────────▶  Modèle / Page
        ▲                                                               │
        └───────────────  met à jour l’état  ◀──────────────────────────┘
```

- Un composant **reçoit** ce qu’il affiche (valeurs, `State<T>`) et **déclare** ce qui se passe (`Action`).
- Il ne décide pas de la navigation, de la suppression ou de la sauvegarde : la page, ou un modèle qu’elle
  possède, le fait.
- L’état a **une seule source de vérité**. Si deux endroits stockent la même donnée, un des deux sera faux.

Voir [Architecture d’une application](app-architecture.md) pour la découpe en couches (UI, modèles, services).

## 6. Sous le capot : entités et dessin

Vous n’avez pas besoin de ceci pour utiliser XenoUI, mais cela explique ses garanties :

- Chaque appel de fabrique (`Text`, `Column`…) crée une **entité** dans un moteur à composants (ECS) ; un
  `XenoComponent` n’en est que l’identifiant (8 octets, aucune allocation).
- Un moteur de **layout** maison mesure puis place les entités (flex horizontal/vertical, piles).
- Un moteur de rendu **Skia** les dessine ; le texte et les champs sont dessinés par XenoUI, le clavier natif
  passe par un pont typé (`NativeTunnel`).
- Les changements visuels (couleur, ombre) marquent seulement l’entité « à redessiner » ; les changements de
  taille marquent « à remesurer ». C’est ce qui rend une liaison fine peu coûteuse.

Détails : [Architecture](architecture/index.md) ; le rebuild atomique et les drapeaux « sales » sont décrits dans
[Runtime §3–4](architecture/runtime.md#3-reconstruction-atomique), et la trajectoire d’une recomposition plus fine
dans [Décisions §5](architecture/decisions-v1.md#5-recomposition-future--contraintes-de-design).

## 7. Règles à retenir

1. **`Body()` et les composants sont purs** : mêmes entrées, même sortie, aucun effet de bord.
2. **Les objets persistants sont des champs** (`State`, `ScrollState`, `LazyListState`, `TextEditController`).
3. **Un `State` d’abord, `Rebuild()` ensuite.**
4. **Les données descendent, les événements remontent** ; une seule source de vérité.
5. **Le travail asynchrone vit dans le cycle de vie de la page**, et ses résultats reviennent sur le thread UI.
6. **Ne conservez jamais un `XenoComponent`** au-delà de la composition qui l’a créé.
7. **Tout est en dp** et les couleurs sont `0xAARRGGBB`.

## Pour aller plus loin

- [État et reconstruction](state.md) : où mettre l’état, remonter l’état, dériver, collections.
- [Cycle de vie](lifecycle.md) : application, pages, composants.
- [Effets de bord et travail asynchrone](side-effects.md) : équivalents de `LaunchedEffect` et `DisposableEffect`.
- [Mises en page](layouts.md) : conteneurs, tailles, alignements, listes.
- [Créer un composant](components.md), [Personnaliser](customization.md), [Animations](animations.md).
- [Apprendre par le SampleApp](sample-app.md) : tout cela dans une application complète.
