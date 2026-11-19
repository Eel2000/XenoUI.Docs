# Mises en page

[Documentation](index.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [Créer un composant](components.md) · [Personnaliser](customization.md) · [Navigation](navigation-scaffold.md)

Ce guide explique comment XenoUI place les éléments à l’écran : les conteneurs, comment les tailles sont
décidées, l’alignement, les marges, le défilement, les listes, le texte et le droite-à-gauche. Il complète
[Créer un composant §7](components.md#7-mise-en-page-à-lintérieur-dun-composant) avec le **modèle** de mise en page
et une série de motifs prêts à l’emploi.

> Pipeline de layout interne : [Runtime §5](architecture/runtime.md#5-pipeline-de-layout).

## Sommaire

1. [Le modèle de mise en page](#1-le-modèle-de-mise-en-page)
2. [Les conteneurs](#2-les-conteneurs)
3. [Les tailles](#3-les-tailles)
4. [Répartition et alignement](#4-répartition-et-alignement)
5. [Marges et espacement](#5-marges-et-espacement)
6. [Superposer : `ZStack`](#6-superposer--zstack)
7. [Défilement](#7-défilement)
8. [Listes virtualisées](#8-listes-virtualisées)
9. [Texte et images dans la mise en page](#9-texte-et-images-dans-la-mise-en-page)
10. [Droite à gauche](#10-droite-à-gauche)
11. [Écrans variés : tailles, zones sûres, clavier](#11-écrans-variés--tailles-zones-sûres-clavier)
12. [Motifs courants](#12-motifs-courants)
13. [Dépannage de mise en page](#13-dépannage-de-mise-en-page)
14. [Équivalences Compose](#14-équivalences-compose)

---

## 1. Le modèle de mise en page

La mise en page se fait en deux passes sur l’arbre d’entités :

1. **Mesure (*measure*)** : de haut en bas, chaque entité reçoit l’espace disponible et calcule la taille qu’elle
   *voudrait* (texte mesuré, enfants cumulés, taille explicite).
2. **Placement (*arrange*)** : le parent distribue l’espace réel (répartition, flex, alignement) et donne à chaque
   enfant sa position et sa taille finales.

Règles qui en découlent :

- Une taille **explicite** (`Width`, `Height`, `Size`) gagne sur la taille naturelle ; `0` signifie *automatique*.
- Un conteneur sans taille explicite prend la taille de son contenu plus son padding.
- Le **flex** (`FlexGrow`) se répartit sur l’axe principal du parent **après** que les enfants non flexibles ont
  pris leur place.
- Tout changement qui touche la taille ou la position (`Width`, `Padding`, `Gap`, alignements, texte) marque
  l’entité « à remesurer / replacer » ; un changement purement visuel (couleur, ombre) ne relance pas la
  mise en page ([Personnaliser §2](customization.md#2-niveau-1--les-modificateurs)).
- Les **unités** sont logiques (dp Android, points iOS) ; les couleurs sont ARGB.

## 2. Les conteneurs

| Conteneur | Axe principal | Usage |
|---|---|---|
| `Column(…)` | Vertical | Empiler de haut en bas |
| `Row(…)` | Horizontal | Aligner de gauche à droite (de droite à gauche en RTL) |
| `ZStack(…)` | — (pile) | Superposer ; le dernier enfant est dessus |
| `Box(w, h)` / `Box(w, h, enfants…)` | — | Rectangle de taille donnée ; avec enfants, c’est une pile |
| `LazyColumn` / `LazyRow` | Vertical / horizontal | Listes virtualisées |
| `VerticalPager` | Vertical | Pages plein écran |
| `Spacer()` | — | Espace flexible (`FlexGrow(1)`) |

Les enfants sont passés comme arguments (`Column(a, b, c)`), comme tableau (`Column(items)`) ou construits par
une boucle. `XenoComponent.None` est ignoré :

```csharp
Column(
    Header(),
    _loading ? Spinner() : XenoComponent.None,
    Content())
```

## 3. Les tailles

| Je veux… | J’écris |
|---|---|
| Taille naturelle | Rien |
| Taille fixe | `Width(120)`, `Height(48)`, `Size(120, 48)`, `Box(120, 48)` |
| Remplir l’espace restant sur l’axe principal | `FlexGrow(1)` |
| Se partager l’espace (2 : 1) | `FlexGrow(2)` et `FlexGrow(1)` |
| Remplir l’axe transversal | `CrossAxis(Stretch)` sur le parent, ou `AlignSelf(Stretch)` sur l’enfant |
| Rétrécir si l’espace manque (horizontal) | `FlexShrink(1)` (par défaut pour les largeurs automatiques) |

```csharp
// Barre de recherche : l’icône garde sa taille, le texte prend le reste
Row(Icon(search, 16), Text("Rechercher", 13).FlexGrow(1))
    .Gap(10).Padding(14, 12).CrossAxis(CrossAxisAlignment.Center);
```

> **Évitez de figer la largeur** d’un composant réutilisable : laissez l’appelant l’étirer avec `FlexGrow` ou
> `Stretch`. Fixez seulement ce qui fait l’identité (diamètre d’un avatar, hauteur d’un champ).

## 4. Répartition et alignement

Dans une `Row` ou une `Column`, deux axes :

- **axe principal** (horizontal pour `Row`, vertical pour `Column`) : `MainAxis(…)`
  — `Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly` ;
- **axe transversal** : `CrossAxis(…)` — `Start`, `Center`, `End`, `Stretch` ; surchargeable **par enfant** avec
  `AlignSelf(…)`.

```csharp
Row(Text("Gauche"), Text("Droite")).MainAxis(MainAxisAlignment.SpaceBetween);   // aux extrémités
Column(Icon(x, 40), Text("Titre")).CenterContent();                              // centré partout
Column(Text("Titre"), Text("Détail").AlignSelf(CrossAxisAlignment.End));         // un enfant à droite
```

`CenterContent()` est le raccourci de `MainAxis(Center)` + `CrossAxis(Center)`.

## 5. Marges et espacement

| Outil | Effet |
|---|---|
| `Padding(…)` | Espace **intérieur** du conteneur |
| `Margin(…)` | Espace **extérieur**, autour de l’élément |
| `Gap(g)` | Espace **entre** enfants d’un `Row`/`Column` |

`Spacing` : `All(v)`, `Symmetric(horizontal, vertical)`, `Only(left, top, right, bottom)`,
`Directional(start, top, end, bottom)`. `Padding(a, b)` fixe horizontal/vertical ; `Padding(Spacing)` remplace
les quatre côtés.

Préférez `Gap` aux marges individuelles : un `Column(...).Gap(12)` s’adapte quand on ajoute ou retire un enfant.

## 6. Superposer : `ZStack`

Les enfants d’un `ZStack` partagent le même repère. Règle d’alignement (différente d’une `Row`) :

- horizontal : `MainAxis` **du `ZStack`** (même position pour tous) ;
- vertical : `AlignSelf` de chaque enfant, sinon `CrossAxis` du `ZStack` ;
- `Stretch` remplit tout l’intérieur ; `Margin` décale.

```csharp
// Coin supérieur droit : une Row qui remplit la pile
ZStack(
    Icon(bell, 24),
    Row(Spacer(), Text("3", 10).Background(0xFFDC2626).CornerRadius(8).Padding(5, 2))
        .AlignSelf(CrossAxisAlignment.Stretch));

// Image avec dégradé / texte par-dessus
ZStack(
    Image("cover").ScaleMode(ImageScaleMode.Cover).AlignSelf(CrossAxisAlignment.Stretch),
    Column(Spacer(), Text("Titre", 20).TextColor(0xFFFFFFFF)).Padding(16)
        .AlignSelf(CrossAxisAlignment.Stretch));
```

C’est la structure de `TrendingCard` (`HomePage`) et des pages du feed.

## 7. Défilement

`.Scrollable(ScrollState)` rend un conteneur défilant (l’axe suit le conteneur : `Row` horizontal, sinon vertical).
Le `ScrollState` vient de `UseScroll()` dans un **champ** pour garder la position entre reconstructions.

```csharp
private readonly ScrollState _scroll = UseScroll();

public override XenoComponent Body() =>
    Column(/* contenu long */).Gap(16).Padding(16)
        .Background(Theme.Canvas).CrossAxis(CrossAxisAlignment.Stretch)
        .Scrollable(_scroll);
```

- Le conteneur défilant est la **racine** de la page dans le sample, sous la barre fixe du scaffold.
- Un conteneur défilant dans un conteneur défilant de même axe n’est pas conseillé ; un `LazyRow` dans une
  page verticale est normal (`HomePage`).
- Pour un en-tête fixe + corps défilant : `Column(Header(), Body().FlexGrow(1).Scrollable(_scroll))`.

## 8. Listes virtualisées

`LazyColumn`/`LazyRow` ne composent que la fenêtre visible (plus une marge) ; les tailles des éléments sont
**mesurées**, il n’y a pas de hauteur à déclarer.

```csharp
LazyColumn(People, PersonRow, key: index => index)       // 10 000 éléments
    .FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)
    .Padding(Spacing.Only(left: 16, right: 16, bottom: 12))
```

| Besoin | Option |
|---|---|
| Identité stable | `key: item => item.Id` |
| Observer / piloter le scroll | `state: _list` (`UseLazyList()`) |
| Page plein écran qui se cale | `VerticalPager(items, (item, index) => …, key, state)` ; `PageChanged`, `ScrollToPage` |
| Écart entre éléments | `Margin` en bas de chaque élément, ou un `Gap` sur le conteneur |
| Padding de la liste | `Padding(...)` sur la liste elle-même |

La liste a besoin d’une **taille bornée** sur son axe : donnez-lui `FlexGrow(1)` dans une `Column`, ou une
`Height`/`Width` explicite pour une liste dans une page défilante (`LazyRow(...).Height(210)`).

## 9. Texte et images dans la mise en page

**Texte** : se mesure lui-même ; la largeur maximale est l’espace disponible et le retour à la ligne est actif.

| Besoin | Outil |
|---|---|
| Limiter les lignes | `MaxLines(2, ellipsis: true)` |
| Une seule ligne | `TextWrap(false)` |
| Réduire la police plutôt que couper | `AutoSize(minimumFontSize)` |
| Texte à côté d’une icône sans pousser | `FlexGrow(1)` + `FlexShrink(1)` + `MaxLines` |
| Centrer le texte | `TextAlign(Center)` (dans sa boîte) ; la boîte doit avoir de la largeur |

**Images** : `Image("nom")` prend sa taille naturelle (en dp). `.Size(w, h)` la remplace ; `ScaleMode` règle
l’ajustement (`Cover` rogne, `Fit` inclut, `Fill` déforme, `None` taille naturelle centrée).

Un texte centré qui ne se centre pas : sa boîte est étroite ; donnez-lui `AlignSelf(Stretch)` ou placez-le dans
un conteneur `CenterContent()`.

## 10. Droite à gauche

Le sous-arbre se met en **miroir** quand sa direction est `RightToLeft` (`FlowDirection(...)`, ou la direction
de l’application) : les rangées démarrent à droite, le côté gauche du padding/marge est le côté « début »,
`TextAlign(Start)` aligne à droite. Les modificateurs `Spacing.Directional(start, …, end, …)` et `Start/End`
sont donc **toujours préférables** aux notions de gauche/droite absolues. Le hit-testing suit les limites
miroir. Ajoutez une `[Preview("…", RightToLeft = true)]` à vos composants.

## 11. Écrans variés : tailles, zones sûres, clavier

- **Largeurs** : concevez avec `FlexGrow`/`Stretch` ; vérifiez avec `[Preview(Device = "tablet")]`, `"iphone-se"`,
  `"foldable"` (`Width`/`Height` forcent une taille).
- **Navigation adaptative** : le scaffold choisit barre du bas, rail ou tiroir permanent (`NavigationMode.Automatic`).
- **Overlays** : `FillWidth` + `MaxWidth` pour rester centrés et bornés ([Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md)).
- **Zone sûre** : `NativeWindowMetrics.SafeAreaTop` (encoche iOS) et `NavigationPresentationContext.SafeAreaTop` ;
  sur Android la fenêtre démarre sous la barre d’état (valeur 0).
- **Clavier** : le viewport tient compte du recouvrement de l’IME ; prévoyez un conteneur défilant pour les formulaires longs ([Saisie de texte](developer-guide.md#11-saisie-de-texte-et-ime)).

## 12. Motifs courants

**Ligne de liste**

```csharp
Row(
    Image(a.Cover).Size(56, 56).CornerRadius(12).ScaleMode(ImageScaleMode.Cover),
    Column(Text(a.Title, 15).MaxLines(2, ellipsis: true), Text(a.Author, 12).TextColor(Theme.Muted))
        .Gap(3).FlexGrow(1),
    Icon(chevron, 11).TextColor(Theme.Faint))
.Gap(12).Padding(12).CrossAxis(CrossAxisAlignment.Center)
.Background(Theme.Surface).CornerRadius(16).Clickable(() => open(a));
```

**Grille à deux colonnes**

```csharp
Column(
    Row(Tile(a).FlexGrow(1), Tile(b).FlexGrow(1)).Gap(10),
    Row(Tile(c).FlexGrow(1), Tile(d).FlexGrow(1)).Gap(10)).Gap(10);
```

**En-tête, corps, pied**

```csharp
Column(Header(), Body().FlexGrow(1), Footer()).CrossAxis(CrossAxisAlignment.Stretch);
```

**Centré dans l’écran**

```csharp
Column(Icon(emptyIcon, 48), Text("Rien ici"), Button("Réessayer", Retry))
    .Gap(12).CenterContent().FlexGrow(1);
```

**Bouton flottant sur du contenu** : `ZStack(Content(), Column(Spacer(), Row(Spacer(), Fab())).AlignSelf(Stretch).Padding(16))`
— ou le FAB du scaffold ([Navigation](navigation-scaffold.md#slots-de-présentation-et-fab)).

## 13. Dépannage de mise en page

| Symptôme | Cause probable | Piste |
|---|---|---|
| Rien ne s’affiche | Taille 0 (liste sans hauteur, `Box(0, 0)` sans parent qui étire) | `FlexGrow(1)`, `Height`, `Stretch` |
| Un enfant déborde | Largeur fixe trop grande | `FlexGrow`/`FlexShrink`, `MaxLines` |
| Le texte n’est pas centré | Boîte du texte étroite | `Stretch`/`CenterContent` sur le parent |
| `Gap` sans effet dans une pile | `ZStack` n’a pas de gap | `Margin` ou autre structure |
| Position horizontale identique pour tous les enfants d’un `ZStack` | Alignement horizontal vient du conteneur | Envelopper dans une `Row` avec `Spacer` |
| Liste qui ne défile pas | Liste dans un conteneur défilant sans taille bornée | `Height`/`FlexGrow` |
| Élément mal placé en RTL | Gauche/droite absolus | `Spacing.Directional`, `Start/End` |

## 14. Équivalences Compose

| Compose | XenoUI |
|---|---|
| `Column` / `Row` | `Column` / `Row` |
| `Box` (pile) | `ZStack` ; `Box(w, h, …)` est un rectangle-conteneur |
| `Modifier.weight(1f)` | `FlexGrow(1)` |
| `Modifier.fillMaxWidth()` | `CrossAxis(Stretch)` du parent ou `AlignSelf(Stretch)` |
| `Modifier.fillMaxSize()` | `FlexGrow(1)` + `Stretch` |
| `Modifier.padding(8.dp)` | `Padding(8)` |
| `Arrangement.spacedBy(8.dp)` | `Gap(8)` |
| `Arrangement.SpaceBetween` | `MainAxis(MainAxisAlignment.SpaceBetween)` |
| `Alignment.CenterHorizontally` | `CrossAxis(Center)` / `AlignSelf(Center)` |
| `Spacer(Modifier.weight(1f))` | `Spacer()` |
| `LazyColumn { items(list, key = { }) }` | `LazyColumn(list, item => …, key: item => …)` |
| `Modifier.verticalScroll(state)` | `Scrollable(scrollState)` |
| `Modifier.size(48.dp)` | `Size(48, 48)` |

Tableau complet : [Comparaison avec Jetpack Compose](compose-comparison.md).
