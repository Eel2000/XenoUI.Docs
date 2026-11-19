# Créer un composant

[Documentation](index.md) · [Guide développeur](developer-guide.md) · [Personnaliser](customization.md) · [Animations](animations.md)

Ce guide explique, de bout en bout, comment concevoir un composant XenoUI réutilisable : sa forme, ses
paramètres, son état, ses enfants, ses événements, sa mise en page, sa prévisualisation et ses pièges. Les
exemples n’emploient que l’API publique actuelle ; les noms de membres sont ceux du code (`XenoUI.Core`,
`XenoUI.StandardLibrary`). Pour adapter l’apparence d’un composant existant, voir
[Personnaliser](customization.md) ; pour le mouvement, voir [Animations](animations.md).

## Sommaire

1. [Le modèle mental](#1-le-modèle-mental)
2. [Anatomie d’un composant](#2-anatomie-dun-composant)
3. [Ce qu’est un `XenoComponent`](#3-ce-quest-un-xenocomponent)
4. [Paramètres : valeurs, états, callbacks, slots](#4-paramètres--valeurs-états-callbacks-slots)
5. [État : trois emplacements possibles](#5-état--trois-emplacements-possibles)
6. [Mise à jour fine ou reconstruction ?](#6-mise-à-jour-fine-ou-reconstruction-)
7. [Mise en page à l’intérieur d’un composant](#7-mise-en-page-à-lintérieur-dun-composant)
8. [Interaction : toucher, retour visuel, défilement](#8-interaction--toucher-retour-visuel-défilement)
9. [Texte, icônes et images](#9-texte-icônes-et-images)
10. [Listes et identité des éléments](#10-listes-et-identité-des-éléments)
11. [Composant avec options (le patron `Options`)](#11-composant-avec-options-le-patron-options)
12. [Prévisualisation : `[Preview]`](#12-prévisualisation--preview)
13. [Droite à gauche, densités et accessibilité](#13-droite-à-gauche-densités-et-accessibilité)
14. [Organisation, templates et distribution](#14-organisation-templates-et-distribution)
15. [Recettes complètes](#15-recettes-complètes)
16. [Pièges et liste de contrôle](#16-pièges-et-liste-de-contrôle)
17. [Limites actuelles](#17-limites-actuelles)

---

## 1. Le modèle mental

XenoUI est **déclaratif et sans widget instancié**. Une interface est le résultat d’appels de fonctions :

- les *factories* de `Compose` (`Column`, `Row`, `Box`, `ZStack`, `Text`, `Image`, `Icon`, `Spacer`,
  `LazyColumn`, `TextField`…) créent des **entités** dans le moteur (ECS) et retournent un handle ;
- les *modificateurs* (`.Padding(16)`, `.Background(0xFF…)`, `.Clickable(…)`) **modifient cette entité en
  place** et retournent le même handle, ce qui permet le chaînage ;
- un **composant** est simplement une fonction qui assemble ces appels et retourne un `XenoComponent`.

Il n’y a donc ni classe de base à hériter, ni arbre de widgets à diffuser, ni `new`. Un composant est une
fonction pure du point de vue de l’appelant : mêmes paramètres, même sous-arbre.

```csharp
public static class Badge
{
    public static XenoComponent View(string label, uint color) =>
        Text(label, 11).TextColor(0xFFFFFFFF)
            .Background(color).CornerRadius(10).Padding(9, 4);
}
```

Utilisation depuis n’importe quel `Body()` ou autre composant :

```csharp
Row(Text("Messages"), Badge.View("3", 0xFFDC2626)).Gap(8)
```

> `Compose` est importé statiquement par `Xeno.SDK` (`using static`), de même que `Widgets` (`Button`,
> `TextButton`) et les fabriques `Use…`. Dans un projet qui n’utilise pas le SDK, ajoutez
> `using static XenoUI.Core.Compositor.Compose;`.

## 2. Anatomie d’un composant

### 2.1 La forme recommandée : une classe statique avec `View`

C’est la forme des templates `xenoui-app` (`Components/Counter.cs`) et `xenoui-component` :

```csharp
using XenoUI.Core.Preview;

namespace MyApp.Components;

/// <summary>Carte de profil : avatar, nom, rôle.</summary>
public static class ProfileCard
{
    public static XenoComponent View(string name, string role) =>
        Row(
            Box(48, 48, Text(name[..1], 20).TextColor(0xFFFFFFFF))
                .Background(0xFF6D28D9).CornerRadius(24).CenterContent(),
            Column(
                Text(name, 16).TextColor(0xFF111827),
                Text(role, 12).TextColor(0xFF6B7280))
            .Gap(2).FlexGrow(1))
        .Gap(12).Padding(12).CrossAxis(CrossAxisAlignment.Center)
        .Background(0xFFF3F4F6).CornerRadius(16);

    [Preview("ProfileCard")]
    private static XenoComponent Preview() => View("Amina", "Designer");
}
```

Règles de forme :

| Règle | Raison |
|---|---|
| Une méthode `View(...)` (ou un nom parlant : `Chip(...)`, `Row(...)` si le composant est une variante) qui **retourne `XenoComponent`** | L’appelant peut enchaîner des modificateurs sur le résultat |
| Les paramètres de contenu d’abord, puis les options facultatives avec valeur par défaut | Lisibilité à l’appel |
| Aucun effet de bord dans `View` (pas de réseau, pas de lecture disque, pas d’allocation d’état) | `View` est rappelée à chaque `Rebuild()` |
| La racine du composant porte l’apparence « par défaut » ; l’appelant peut la surcharger | Voir [Personnaliser](customization.md) |

### 2.2 La forme classe avec `Body()` (comme `Button`)

La bibliothèque standard écrit certains composants comme une classe qui capture ses paramètres puis produit
l’arbre dans `Body()` ; une fabrique statique dans `Widgets` la masque :

```csharp
public class Button
{
    private readonly string _label;
    private readonly Action _onTap;
    private readonly float _fontSize;
    private readonly uint _textColor;

    public Button(string label, Action onTap, float fontSize = 16f, uint textColor = 0xFFFFFFFF)
    { /* … */ }

    public XenoComponent Body() =>
        ZStack(Text(_label, _fontSize).TextColor(_textColor).AlignSelf(CrossAxisAlignment.Center))
            .Background(0xFF6200EE).CornerRadius(8).Elevation(2).Padding(20, 12)
            .CenterContent().Clickable(_onTap);
}

public static partial class Widgets
{
    public static XenoComponent Button(string label, Action onTap, float fontSize = 16f, uint textColor = 0xFFFFFFFF)
        => new Button(label, onTap, fontSize, textColor).Body();
}
```

Choisissez cette forme quand le composant a **un état propre** à conserver (voir
[§5](#5-état--trois-emplacements-possibles)) ou quand l’arbre se découpe en plusieurs méthodes privées.
`Widgets` est une classe `partial` : vous pouvez y ajouter vos propres fabriques dans votre projet pour
qu’elles s’appellent comme les composants standard.

### 2.3 Composant ou page ?

| | Composant | Page (`XenoPage`) |
|---|---|---|
| Rôle | Morceau d’interface réutilisable | Écran navigable |
| Cycle de vie | Aucun : appelé par `Body()` | `OnCreate`, `OnActivated`, `OnDeactivated`, `OnDestroy` |
| Reconstruction | Dépend de son appelant | `Rebuild()`, `RebuildTopBar()` |
| Navigation | Reçoit un `Action` | `Navigate<T>()`, `GoBack()` |
| Où garder l’état | Paramètre ou instance détenue par la page | Champs de la page |

Si un morceau a besoin de `Navigate`, `Native`, `Application` ou `IsActive`, c’est une page ; passez à ses
composants une action (`Action onOpen`) plutôt que la page elle-même.

## 3. Ce qu’est un `XenoComponent`

`XenoComponent` est une `readonly struct` de 8 octets : l’identifiant de l’entité et un indicateur de
validité. Retenez cinq conséquences :

1. **Aucune allocation** pour composer : copier un `XenoComponent` ne copie pas l’arbre.
2. **Les modificateurs mutent l’entité** dans le moteur. `a.Padding(8)` et `var b = a; b.Padding(8)` agissent
   sur la même entité.
3. **L’ordre des modificateurs est libre**, sauf quand deux modificateurs écrivent la même propriété : le
   dernier gagne (`Padding(8).Padding(16)` → 16 ; `Elevation(4).Shadow(…)` → l’ombre manuelle remplace
   l’élévation ; `CornerRadius` est ignoré si `Shape(...)` est posé).
4. **`XenoComponent.None`** représente « rien » : tous les modificateurs sont des no-ops dessus et les
   conteneurs l’ignorent. C’est le mécanisme du rendu conditionnel.
5. Un `XenoComponent` **n’est valable que dans la composition qui l’a créé**. Ne le stockez pas dans un champ
   pour le réutiliser après un `Rebuild()` : l’arbre transitoire est détruit et recréé. Stockez des *données*
   ou des objets persistants (`State<T>`), pas des handles.

```csharp
// Rendu conditionnel : None est ignoré par Column/Row/ZStack/Box.
Column(
    Text(title),
    showSubtitle ? Text(subtitle, 12) : XenoComponent.None,
    items.Count == 0 ? EmptyState.View() : XenoComponent.None
)
```

`default(XenoComponent)` vaut aussi `None`. En revanche, convertir `None` en `int` (l’identifiant d’entité)
lève une `InvalidOperationException` : testez `IsNone` avant.

Les conteneurs prennent leurs enfants par `params ReadOnlySpan<XenoComponent>` : vous pouvez passer une
liste d’arguments, un tableau (`XenoComponent[]`) ou construire un tableau à partir de données :

```csharp
XenoComponent[] rows = tags.Select(t => Badge.View(t, 0xFF2563EB)).ToArray();
Row(rows).Gap(6)
```

## 4. Paramètres : valeurs, états, callbacks, slots

### 4.1 Valeurs simples

`string`, `float`, `uint` (couleur ARGB), énumérations. Elles sont lues **à la composition** : une valeur
changée plus tard n’a d’effet qu’au prochain `Rebuild()`.

```csharp
public static XenoComponent Price(decimal amount, string currency = "€") =>
    Text($"{amount:N2} {currency}", 16).TextColor(0xFF111827);
```

> Dans `Text($"…")`, une chaîne interpolée **sans** `State` est formatée une fois ; avec un `State<T>`
> embarqué, elle devient réactive (voir [§6](#6-mise-à-jour-fine-ou-reconstruction-)).

### 4.2 États réactifs : `State<T>`

Passez un `State<T>` quand la valeur doit pouvoir changer **sans reconstruire l’arbre** :

```csharp
public static XenoComponent Counter(State<int> count, Action increment) =>
    Column(
        Text($"Compteur : {count}", 20),
        Button("+1", increment)
    ).Gap(12);
```

Le composant lit `count` ; l’appelant le possède. Cette inversion (« état remonté ») est la manière la plus
simple de garder un composant sans état propre.

### 4.3 Callbacks

Utilisez `Action` (ou `Action<T>`) pour tout ce que le composant ne doit pas décider lui-même : navigation,
suppression, validation. Le composant déclare l’intention, la page décide.

```csharp
public static XenoComponent ArticleRow(Article a, Action<Article> open, Action<Article> delete) => …
```

Un callback est stocké une fois dans la table de callbacks du moteur (`Clickable`) ; il est remplacé, pas
empilé, si vous appelez `Clickable` deux fois sur la même entité.

### 4.4 Slots : du contenu fourni par l’appelant

Un *slot* est un paramètre qui porte du contenu. Deux formes sont idiomatiques.

**Par `XenoComponent` (contenu déjà composé)** — simple, à utiliser quand le contenu ne dépend pas d’un
paramètre calculé par le composant :

```csharp
public static XenoComponent Card(params XenoComponent[] content) =>
    Column(content).Gap(10).Padding(16)
        .Background(0xFF15151D).CornerRadius(20)
        .CrossAxis(CrossAxisAlignment.Stretch);
```

C’est exactement `Ui.Card` de l’application d’exemple. Appel :

```csharp
Card(Text("Titre", 18), Text("Détail", 13), Button("OK", Ok))
```

**Par `Func<…, XenoComponent>` (contenu construit à la demande)** — nécessaire quand le composant fournit une
donnée au slot (un handle de fermeture, un index, un état) ou construit le contenu plus tard. La bibliothèque
l’emploie pour les overlays :

```csharp
Snackbar.Show(app, handle => MyCustomBody(handle.Dismiss), options);
```

Règle : si le slot doit pouvoir recevoir une information du composant, utilisez une `Func` ; sinon passez un
`XenoComponent`. Passez toujours `XenoComponent.None` plutôt que `null` pour « pas de contenu ».

## 5. État : trois emplacements possibles

Les objets persistants (`State<T>`, `ScrollState`, `LazyListState`, `TextEditController`) doivent **survivre
aux reconstructions**. Ils ne vivent donc jamais dans `View()`/`Body()`.

> Les fabriques `UseState`, `UseStates`, `UseScroll`, `UseLazyList`, `UseTextEdit` créent un **objet neuf à
> chaque appel**. Appelez-les dans l’initialiseur d’un champ, jamais dans une méthode de composition.

### 5.1 L’état est possédé par l’appelant (recommandé)

Le composant reçoit `State<T>` en paramètre (voir [§4.2](#42-états-réactifs--statet)). Le composant est une
simple fonction, trivial à prévisualiser (`View(UseState(42), () => { })`).

### 5.2 L’état est possédé par une instance détenue par la page

Quand l’état est interne au composant (un menu replié ou non, un onglet courant), faites-en une classe dont
la **page détient une instance dans un champ** :

```csharp
public sealed class Expander
{
    private readonly State<bool> _open = UseState(false);
    private readonly string _title;
    private readonly Func<XenoComponent> _content;
    private readonly Action _rebuild;

    public Expander(string title, Func<XenoComponent> content, Action rebuild)
    { _title = title; _content = content; _rebuild = rebuild; }

    public XenoComponent Body() =>
        Column(
            Row(
                Text(_title, 16).FlexGrow(1),
                Icon(Glyph(_open.Value ? "" : "", "Font Awesome 5 Free-Solid-900"), 12)
            ).CrossAxis(CrossAxisAlignment.Center).Padding(12)
             .Clickable(() => { _open.Value = !_open.Value; _rebuild(); }),
            _open.Value ? _content() : XenoComponent.None
        ).Background(0xFFF3F4F6).CornerRadius(14);
}

// Dans la page :
private readonly Expander _faq;
public FaqPage() => _faq = new Expander("Comment ça marche ?", () => Text("…"), Rebuild);
public override XenoComponent Body() => Column(_faq.Body());
```

Ici l’ouverture/fermeture est **structurelle** (le contenu apparaît ou disparaît), donc elle appelle
`Rebuild()`, que la page transmet au composant. L’instance, elle, survit : l’état n’est pas perdu.

### 5.3 L’état est global (service d’application)

Un état partagé par plusieurs écrans (utilisateur connecté, panier) appartient à un service créé dans
`XenoApp` ; les pages le lisent et s’abonnent à ses changements (voir `ArticleDetailsPage` de l’exemple :
`OnChanged` → `Rebuild()` seulement si `IsActive`). Un composant ne devrait pas lire un service
directement : passez-lui les valeurs.

## 6. Mise à jour fine ou reconstruction ?

XenoUI ne diffuse pas deux arbres. Deux mécanismes existent, à choisir selon ce qui change :

| Ce qui change | Mécanisme | Coût |
|---|---|---|
| Le texte d’un `Text` | `Text(State<T>)` ou `Text($"…{state}")` | Re-mesure du seul texte |
| La couleur d’un texte | `.TextColor(State<uint>)` | Repaint de l’entité |
| La couleur de fond | `.Background(State<uint>)` | Repaint de l’entité |
| La part remplie d’une barre | `LinearProgress(State<float>)` | Re-layout de la barre |
| La source d’une image locale | `ImageFile(State<string?>)` | Repaint |
| La vidéo native d’une zone | `.NativeVideo(State<…>)` | Repaint |
| Ajouter, retirer, réordonner des éléments, changer de branche conditionnelle | `Rebuild()` de la page | Recomposition de la page |

Exemple de composant entièrement réactif, sans reconstruction :

```csharp
public static XenoComponent StatusDot(State<uint> color, State<string> label) =>
    Row(
        Box(10, 10).Background(color).CornerRadius(5),
        Text(label, 13)
    ).Gap(8).CrossAxis(CrossAxisAlignment.Center);
```

`StatusDot` ne se reconstruit jamais : changer `color.Value` ou `label.Value` repeint seulement les deux
entités concernées. Le setter de `State<T>` ne notifie que si la valeur **change réellement**
(`EqualityComparer<T>.Default`) ; réassigner la même valeur est gratuit.

Une reconstruction rappelle `Body()` sous verrou et remet à zéro l’arbre transitoire. Elle ne reconstruit
**pas** la pile de navigation, les overlays ouverts, ni les objets persistants. Voir
[Quelle opération reconstruit quoi ?](navigation-scaffold.md#quelle-opération-reconstruit-quoi-).

**Threading** : toute mutation d’un `State` qui a un effet visuel doit se faire sur le thread UI. Depuis une
tâche d’arrière-plan, passez par `Native.Require<IUiDispatcherCapability>().Post(...)` et, pour une réponse
tardive, vérifiez `IsActive` avant d’agir.

## 7. Mise en page à l’intérieur d’un composant

Toutes les dimensions sont en **unités logiques** (dp Android, points iOS). Les couleurs sont `0xAARRGGBB`.

### 7.1 Conteneurs

| Factory | Disposition | Notes |
|---|---|---|
| `Column(children)` | Verticale | Fond transparent par défaut |
| `Row(children)` | Horizontale | `FlowDirection` RTL : démarre à droite |
| `ZStack(children)` | Superposée | Premier enfant en bas, dernier au-dessus ; voir la règle d’alignement ci-dessous |
| `Box(w, h)` / `Box(w, h, children)` | Rectangle ou conteneur de taille donnée | `0` = taille automatique |
| `Spacer()` | Espace flexible | Équivaut à `FlexGrow(1)` |
| `LazyColumn` / `LazyRow` / `VerticalPager` | Listes virtualisées | Voir [§10](#10-listes-et-identité-des-éléments) |

**Alignement dans un `ZStack`.** Il n’obéit pas aux mêmes règles qu’une `Row`/`Column` :

- la position **horizontale** de *tous* les enfants vient du `MainAxis` du `ZStack` (`Start` par défaut,
  `Center`, `End` ; les variantes `Space…` valent `Start`) ;
- la position **verticale** de chaque enfant vient de son `AlignSelf`, à défaut du `CrossAxis` du `ZStack`
  (`Start`, `Center`, `End`) ;
- `Stretch` (par `AlignSelf` ou `CrossAxis`) fait remplir tout l’intérieur du `ZStack` à l’enfant ;
- `Margin` décale un enfant dans cet espace ; il n’y a pas d’alignement horizontal par enfant.

C’est pourquoi `Button` centre son texte avec `.CenterContent()` sur le `ZStack` et `AlignSelf(Center)` sur le
texte. Pour placer un élément dans un coin, faites-lui remplir le `ZStack` avec une `Row`/`Column` :
voir la recette « pastille en coin » ci-dessous.

### 7.2 Modificateurs de layout

| Modificateur | Effet |
|---|---|
| `Width(w)`, `Height(h)`, `Size(w, h)` | Taille explicite ; `0` = automatique |
| `Padding(all)` / `Padding(h, v)` / `Padding(Spacing)` | Marge intérieure |
| `Margin(all)` / `Margin(Spacing)` | Marge extérieure |
| `Gap(g)` | Espace entre enfants |
| `MainAxis(MainAxisAlignment)` | Répartition sur l’axe principal : `Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly` |
| `CrossAxis(CrossAxisAlignment)` | Placement sur l’axe transversal : `Start`, `Center`, `End`, `Stretch` |
| `AlignSelf(CrossAxisAlignment)` | Surcharge pour **cet** enfant |
| `CenterContent()` | `MainAxis(Center)` + `CrossAxis(Center)` |
| `FlexGrow(f)` | Part de l’espace restant (0 = ne grandit pas) |
| `FlexShrink(f)` | Compression horizontale (les enfants de largeur automatique rétrécissent par défaut) |
| `FlowDirection(dir)` | `Inherit`, `LeftToRight`, `RightToLeft` pour le sous-arbre |

`Spacing` : `Spacing.All(v)`, `Spacing.Symmetric(horizontal, vertical)`, `Spacing.Only(left, top, right,
bottom)` et `Spacing.Directional(start, top, end, bottom)` (noms lisibles en RTL ; `Left` est le côté début).

### 7.3 Recettes de disposition

```csharp
// Ligne : icône + texte qui prend la place + valeur à droite
Row(Icon(i, 18), Text(label).FlexGrow(1), Text(value))
    .Gap(10).CrossAxis(CrossAxisAlignment.Center);

// Contenu centré dans une surface (bouton, état vide, avatar)
Box(64, 64, Text("A", 24)).Background(0xFF6D28D9).CornerRadius(32).CenterContent();

// Pastille en coin supérieur droit : une Row qui remplit le ZStack, avec un Spacer
ZStack(
    Icon(bell, 24),
    Row(Spacer(), Text("3", 10).TextColor(0xFFFFFFFF)
            .Background(0xFFDC2626).CornerRadius(8).Padding(5, 2))
        .AlignSelf(CrossAxisAlignment.Stretch)
);

// Page en trois zones : en-tête fixe, corps qui prend le reste, pied fixe
Column(Header(), Body().FlexGrow(1), Footer()).CrossAxis(CrossAxisAlignment.Stretch);
```

Pour un texte qui doit rester dans une rangée sans pousser ses voisins, donnez-lui `.FlexGrow(1)`
**et** `.FlexShrink(1)`, et limitez-le (`.MaxLines(n, ellipsis: true)`) ; c’est le montage de
`Snackbar.Body`.

### 7.4 Dimensionnement : ce que le moteur décide

- Un `Text` se mesure lui-même (largeur max = largeur disponible, retour à la ligne actif par défaut).
- Un `Image(asset)` prend sa taille naturelle divisée par la densité d’écran ; `.Size`, `.Width` ou un
  parent `Stretch` la remplacent.
- Un conteneur sans taille explicite prend la taille de son contenu plus son padding.
- `Stretch` agrandit les enfants sur l’axe transversal ; `FlexGrow` répartit l’axe principal.

Un composant bien élevé **ne fixe pas de largeur** quand l’appelant peut vouloir l’étirer : laissez-le à
`FlexGrow`/`Stretch` et fixez seulement ce qui fait l’identité du composant (hauteur d’un champ, diamètre d’un
avatar).

## 8. Interaction : toucher, retour visuel, défilement

### 8.1 `Clickable`

`.Clickable(Action)` rend **n’importe quelle entité** tactile. Le callback part au relâchement, si le doigt
est encore dans les limites de l’entité. La zone tactile est celle de l’entité : sa forme si elle a un
`Shape`, sinon son rectangle (arrondi compris).

**Retour visuel** : une entité cliquable dont le fond n’est pas transparent s’assombrit de 15 % tant qu’elle est
pressée (sans coût de layout). Conséquence pratique : un `Text(...).Clickable(…)` à fond **entièrement transparent** (alpha 0) n’a
**pas** de retour visuel, puisque rien n’est dessiné à teinter. Donnez-lui un fond (même une couleur
translucide comme `0x1F6750A4`, c’est ce que fait la doc de `TextButton`) ou enveloppez-le dans une surface
colorée. Les champs de texte n’ont pas cette teinte.

Pour une cible tactile confortable, visez **au moins 44–48 dp** de côté (`Box(44, 44, icon).CenterContent()
.Clickable(tap)` est le motif d’`Ui.IconButton`).

### 8.2 Éléments imbriqués cliquables

Chaque entité a un seul callback de toucher. Le moteur résout le toucher dans l’ordre Z inverse (l’élément
dessiné au-dessus est testé en premier) et ne déclenche **qu’une** entité. Évitez d’imbriquer deux cibles
tactiles qui se recouvrent et de multiplier les cibles minuscules dans une même rangée ; si une ligne entière
est cliquable *et* contient un bouton, vérifiez le comportement sur appareil.

### 8.3 Défilement

`.Scrollable(ScrollState)` rend un conteneur défilant ; l’axe suit le conteneur (`Row` horizontal, les autres
vertical) sauf si vous le forcez. `ScrollState` vient de `UseScroll()` dans un **champ**, pour conserver la
position entre reconstructions.

```csharp
private readonly ScrollState _scroll = UseScroll();

Column(items).Gap(12).Padding(16).Scrollable(_scroll)
```

Pour de longues listes, préférez `LazyColumn` (virtualisée) à une `Column` scrollable qui matérialise tout.

### 8.4 Saisie de texte

`TextField(controller, placeholder, InputOptions(...))` et `TextArea(...)` sont des champs dessinés par le
moteur, pilotés par un `TextEditController` persistant (`UseTextEdit("")` dans un champ). Un composant de
formulaire reçoit le contrôleur en paramètre :

```csharp
public static XenoComponent LabeledField(string label, TextEditController text, string hint) =>
    Column(
        Text(label, 12).TextColor(0xFF6B7280),
        TextField(text, hint).Height(48)
    ).Gap(6);
```

Détails IME, clavier, mot de passe : [Guide développeur §11](developer-guide.md#11-saisie-de-texte-et-ime).

## 9. Texte, icônes et images

### 9.1 Texte

| Modificateur | Effet |
|---|---|
| `TextColor(uint)` / `TextColor(State<uint>)` | Couleur ARGB |
| `FontSize(float)` | Taille en dp (aussi un paramètre de `Text`) |
| `FontFamily("Nom")` | Police placée dans `Assets/Fonts/`, chargée au démarrage |
| `TextAlign(Start / Center / End)` | Alignement dans la boîte du texte |
| `TextWrap(bool)` | Retour à la ligne (actif par défaut) |
| `MaxLines(n, ellipsis)` | Limite de lignes, avec « … » si `ellipsis` |
| `AutoSize(minimumFontSize)` | Réduction bornée de la taille pour tenir |

Les modificateurs de texte s’appliquent **à l’entité qui porte le texte**. Ils ne se propagent pas à ses
enfants : `ZStack(Text("OK")).TextColor(0xFFFF0000)` ne colore pas le `Text` enfant. C’est pourquoi `Button`
reçoit `fontSize` et `textColor` en **paramètres**. Un composant qui contient du texte doit donc exposer les
paramètres de texte dont l’appelant aura besoin (voir [Personnaliser](customization.md#31-ce-quun-modificateur-ne-peut-pas-atteindre)).

### 9.2 Icônes

Une icône est un glyphe de police :

```csharp
Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), 20).TextColor(0xFF111827)
```

`IconDef` (`Glyph(glyph, fontName)`) associe glyphe et police pour qu’ils voyagent ensemble : prenez un
`IconDef` en paramètre plutôt que deux chaînes. Les polices vont dans `Assets/Fonts/` ; une police inconnue
retombe sur la police système.

### 9.3 Images

`Image("avatar")` charge `Assets/Images/avatar.*` (nom court, sans extension) et prend sa taille naturelle
(en dp). `ScaleMode(ImageScaleMode.Cover | Fit | Fill | None)` règle l’ajustement. `ImageFile(State<string?>)`
affiche un fichier local réactif (`null` masque l’image).

```csharp
public static XenoComponent Avatar(string asset, float size = 48) =>
    Image(asset).Size(size, size).CornerRadius(size / 2).ScaleMode(ImageScaleMode.Cover);
```

## 10. Listes et identité des éléments

```csharp
LazyColumn(
    items,                                   // IReadOnlyList<T>
    item => ArticleRow.View(item, open),     // construit un élément
    key: item => item.Id,                    // identité stable (recommandé)
    state: _list)                            // LazyListState persistant (facultatif)
```

- Seuls les éléments visibles (plus une marge) sont composés ; leurs tailles sont **mesurées**.
- `key` donne une identité stable : sans clé, l’identité est l’index. Une clé dupliquée dans la fenêtre
  matérialisée lève une `InvalidOperationException` pendant la composition.
- La clé identifie l’élément quand la fenêtre se déplace ; utilisez un identifiant métier (id), jamais un
  index ni un objet recréé à chaque composition.
- `LazyListState` (`UseLazyList()`) permet d’observer ou de piloter le défilement ; `VerticalPager` fournit
  un pager plein écran qui se cale sur une page (`PageChanged`, levé sur le thread UI).
- Les éléments sont reconstruits quand la fenêtre bouge : leur `itemContent` doit rester **rapide et sans
  effet de bord**.

Un composant de ligne est une fonction ordinaire :

```csharp
public static class ArticleRow
{
    public static XenoComponent View(Article a, Action<Article> open) =>
        Row(
            Image(a.Cover).Size(72, 72).CornerRadius(12).ScaleMode(ImageScaleMode.Cover),
            Column(
                Text(a.Title, 15).MaxLines(2, ellipsis: true),
                Text(a.Author, 12).TextColor(0xFF6B7280)
            ).Gap(4).FlexGrow(1))
        .Gap(12).Padding(12).CrossAxis(CrossAxisAlignment.Center)
        .Background(0xFFFFFFFF).CornerRadius(16)
        .Clickable(() => open(a));
}
```

## 11. Composant avec options (le patron `Options`)

Dès qu’un composant a plus de trois ou quatre paramètres facultatifs, regroupez-les dans un objet d’options
dont **chaque propriété a une valeur par défaut**. C’est le patron de `SnackbarOptions`, `TopBarOptions`,
`PopupOptions`, `DialogOptions`, `AlertOptions`, `BottomSheetOptions`.

```csharp
public sealed class ChipOptions
{
    public uint Background { get; set; } = 0xFFEDE9FE;
    public uint Foreground { get; set; } = 0xFF5B21B6;
    public float FontSize { get; set; } = 13;
    public float CornerRadius { get; set; } = 16;
    public float PaddingX { get; set; } = 12;
    public float PaddingY { get; set; } = 6;
    public IconDef? Icon { get; set; }
}

public static class Chip
{
    public static XenoComponent View(string label, Action? onTap = null, ChipOptions? options = null)
    {
        options ??= new ChipOptions();
        var chip = Row(
            options.Icon is { } icon
                ? Icon(icon, options.FontSize + 2).TextColor(options.Foreground)
                : XenoComponent.None,
            Text(label, options.FontSize).TextColor(options.Foreground)
        ).Gap(6).CrossAxis(CrossAxisAlignment.Center)
         .Padding(options.PaddingX, options.PaddingY)
         .Background(options.Background).CornerRadius(options.CornerRadius);
        return onTap is null ? chip : chip.Clickable(onTap);
    }
}
```

Appel : `Chip.View("Swift", onTap, new ChipOptions { Background = 0xFFDCFCE7, Foreground = 0xFF166534 })`.

Si l’objet doit être **immuable et dérivable**, utilisez un `record` : l’appelant écrit
`Chip.Default with { Background = … }` (c’est le style de `NavigationAction` et `DockedNavigationBarStyle`).

Fournissez aussi, quand c’est utile, une méthode qui ne produit **que le visuel** (`Snackbar.Body`,
`Dialog.Surface`) séparée de celle qui l’affiche (`Show`) : elle sert aux aperçus et à la composition
ailleurs.

## 12. Prévisualisation : `[Preview]`

Une méthode **statique, sans paramètre, qui retourne `XenoComponent`** et porte `[Preview]` apparaît dans la
fenêtre *XenoUI Preview* de Visual Studio et dans l’aperçu de VS Code.

```csharp
[Preview("Chip")]
[Preview("Chip (RTL)", RightToLeft = true)]
[Preview("Chip, tablette", Device = "tablet")]
private static XenoComponent ChipPreview() =>
    Row(Chip.View("Swift"), Chip.View("Rust")).Gap(8).Padding(20);
```

Propriétés : `Name` (légende), `Device` (`android`, `pixel`, `iphone`, `iphone-se`, `foldable`, `tablet`),
`Width`, `Height` (dp ; 0 = valeur de l’appareil, ou la hauteur du contenu pour un composant),
`RightToLeft`. Une méthode est un aperçu de **composant** (largeur de l’appareil, hauteur du contenu) ; une
page vaut par défaut `android`.

Bonnes habitudes :

- une preview par **variante** (vide, plein, long texte, RTL, erreur) ;
- des données d’exemple en dur : une preview ne reçoit aucun paramètre (`View(UseState(42), () => { })`) ;
- pas d’accès réseau, disque ni `Native` dans une preview ;
- le code des aperçus est compilé en mémoire par l’extension ; consultez [Visual Studio](visual-studio.md)
  pour ce qu’elle couvre et ses limites.

Détails : [Visual Studio](visual-studio.md), [Visual Studio Code](vscode.md).

## 13. Droite à gauche, densités et accessibilité

**RTL.** Utilisez `Row`, `Spacing.Directional(start, …)` et `TextAlign(Start/End)` plutôt que des positions
absolues : le sous-arbre se met en miroir quand `FlowDirection` vaut `RightToLeft` (ou que l’application est
en RTL). Ajoutez toujours une preview RTL à un composant qui contient une rangée ou une flèche directionnelle
(chevron : changez le glyphe selon `IsRightToLeft` si le composant a accès à l’application).

**Densités.** Tout est en dp. Ne convertissez jamais en pixels ; les tailles de police sont aussi en dp.

**Accessibilité.** Le moteur dessine lui-même ses pixels : la lisibilité repose sur vos choix. Contraste
suffisant (≥ 4,5:1 pour le texte courant), cibles ≥ 44 dp, informations jamais portées par la couleur seule,
texte qui peut grandir (évitez les hauteurs fixes autour d’un texte ; préférez le padding). Les champs de
texte reçoivent un libellé d’accessibilité dérivé de leur placeholder (`InputOptions(label: …)` pour le
fixer). Le mouvement réduit du système est respecté par les animations de la bibliothèque (voir
[Animations](animations.md#9-mouvement-réduit)).

## 14. Organisation, templates et distribution

Structure conseillée d’un projet :

```
MyApp/
  App.cs                 XenoApp : pages, scaffold
  Pages/                 une XenoPage par écran
  Components/            un fichier par composant (Chip.cs, ProfileCard.cs…)
  Design/Theme.cs        constantes de couleurs, tailles, polices
  Assets/Fonts, Images
```

Créer un composant avec le template :

```powershell
dotnet new xenoui-component -n Avatar -o Components
```

Visual Studio : *Ajouter › Nouvel élément › XenoUI Component*. VS Code : la commande de création de
composant de l’extension XenoUI.

Réutiliser entre applications : placez les composants dans une bibliothèque de classes qui référence
`XenoUI.Core` (et `XenoUI.StandardLibrary` si vous composez `Button`/`TextButton`) et ciblez le même TFM que
l’application (`net11.0-android` / `net11.0-ios`, ou `net11.0` si le code est portable et sans type de
plateforme). Pour un thème commun, exposez-le dans cette bibliothèque ; chaque application peut le
surcharger avec ses propres options.

## 15. Recettes complètes

### 15.1 Champ avec étiquette et message d’erreur

```csharp
public static class FormField
{
    public static XenoComponent View(string label, TextEditController text, string hint,
        State<string> error)
        => Column(
            Text(label, 12).TextColor(0xFF6B7280),
            TextField(text, hint).Height(48),
            Text(error, 12).TextColor(0xFFDC2626)       // vide = invisible, sans reconstruire
        ).Gap(6).CrossAxis(CrossAxisAlignment.Stretch);
}
```

`error` est un `State<string>` : valider un champ met à jour le message sans toucher à la structure.

### 15.2 Interrupteur contrôlé

```csharp
public static class Toggle
{
    public static XenoComponent View(bool on, Action<bool> onChange)
    {
        uint track = on ? 0xFF6D28D9 : 0xFFD1D5DB;
        var thumb = Box(24, 24).Background(0xFFFFFFFF).CornerRadius(12).Elevation(1);
        return Row(on ? Spacer() : XenoComponent.None, thumb, on ? XenoComponent.None : Spacer())
            .Width(52).Height(32).Padding(4).CrossAxis(CrossAxisAlignment.Center)
            .Background(track).CornerRadius(16)
            .Clickable(() => onChange(!on));
    }
}
```

Composant **contrôlé** : il ne retient rien, la page possède le booléen et `Rebuild()` après le changement.
C’est le choix le plus prévisible pour tout composant dont la forme change.

### 15.3 Évaluation en étoiles

```csharp
public static class Rating
{
    private const string Fa = "Font Awesome 5 Free-Solid-900";

    public static XenoComponent View(int value, Action<int> onRate, int max = 5)
    {
        var stars = new XenoComponent[max];
        for (int i = 0; i < max; i++)
        {
            int rating = i + 1;
            stars[i] = Icon(Glyph("", Fa), 24)
                .TextColor(rating <= value ? 0xFFF59E0B : 0xFFD1D5DB)
                .Padding(4)
                .Clickable(() => onRate(rating));
        }
        return Row(stars);
    }
}
```

### 15.4 Barre de segments

```csharp
public static XenoComponent Segmented(string[] labels, int selected, Action<int> onSelect)
{
    var items = new XenoComponent[labels.Length];
    for (int i = 0; i < labels.Length; i++)
    {
        int index = i;
        bool active = i == selected;
        items[i] = Text(labels[i], 14)
            .TextColor(active ? 0xFFFFFFFF : 0xFF374151)
            .TextAlign(TextAlignment.Center)
            .Padding(14, 10).FlexGrow(1)
            .Background(active ? 0xFF6D28D9 : 0x00000000)
            .CornerRadius(10)
            .Clickable(() => onSelect(index));
    }
    return Row(items).Gap(4).Padding(4).Background(0xFFE5E7EB).CornerRadius(14);
}
```

Un segment inactif a un fond transparent : il n’a donc pas de teinte au toucher (voir
[§8.1](#81-clickable)). Si vous voulez un retour visuel, donnez-lui un fond translucide non nul.

### 15.5 Carte avec état vide

```csharp
public static XenoComponent InboxList(IReadOnlyList<Message> messages, Action<Message> open) =>
    messages.Count == 0
        ? Column(
            Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), 40).TextColor(0xFF9CA3AF),
            Text("Aucun message", 16),
            Text("Les nouveaux messages apparaîtront ici.", 13).TextColor(0xFF6B7280)
              .TextAlign(TextAlignment.Center))
          .Gap(8).CenterContent().Padding(32)
        : LazyColumn(messages, m => MessageRow.View(m, open), key: m => m.Id);
```

## 16. Pièges et liste de contrôle

| Piège | Symptôme | Correction |
|---|---|---|
| `UseState(…)` appelé dans `View()`/`Body()` | L’état revient à sa valeur initiale à chaque `Rebuild()` | Initialiser dans un champ |
| Stocker un `XenoComponent` dans un champ | Entité morte après `Rebuild()` | Stocker des données, recomposer |
| Modifier un `State` depuis un thread de fond | Corruption visuelle ou exception | `IUiDispatcherCapability.Post` |
| Texte cliquable sans fond | Aucun retour visuel | Fond non transparent ou surface enveloppante |
| `.TextColor` sur un conteneur | Le texte enfant garde sa couleur | Passer la couleur au `Text`, via un paramètre |
| Clé dupliquée dans une liste lazy | `InvalidOperationException` à la composition | Clés uniques par élément |
| Largeur fixe partout | Débordement en grande police ou petite largeur | `FlexGrow`, `Stretch`, `MaxLines` |
| `Rebuild()` à chaque frappe | Perte de focus ou de défilement | État fin (`State`) ; reconstruire seulement si la structure change |
| Callback async tardif | Mise à jour d’une page quittée | Tester `IsActive` avant d’agir |
| `CornerRadius` ignoré | `Shape(...)` posé sur l’entité | Retirer le shape (`Shape(null)`) ou l’intégrer au shape |

Liste de contrôle avant de publier un composant :

- [ ] `View` retourne `XenoComponent` et ne fait aucun effet de bord ;
- [ ] aucun état créé dans `View` ; les états sont des paramètres ou des champs ;
- [ ] valeurs par défaut raisonnables ; apparence surchargeable par modificateurs ;
- [ ] paramètres de texte exposés (couleur, taille) si le composant contient du texte ;
- [ ] pas de largeur fixe superflue ; testé avec un long texte ;
- [ ] zone tactile ≥ 44 dp ;
- [ ] previews : défaut, variantes, RTL ;
- [ ] contraste vérifié sur fond clair et sombre.

## 17. Limites actuelles

- **Pas d’héritage de style** : la couleur ou la police d’un parent ne descend pas dans ses enfants.
- **Pas de dessin libre public** : en dehors des formes (`XenoShape`, [Personnaliser](customization.md#5-formes-ombres-et-élévation))
  le moteur n’expose pas de point d’extension pour peindre directement sur le canevas Skia.
- **Pas de diff d’arbre** : un `Rebuild()` recompose la page ; les mises à jour fines passent par `State`.
- **Composants sans cycle de vie** : pas d’équivalent de `OnCreate`/`OnDestroy` pour un composant ; libérez
  les ressources dans la page qui le possède.
- Le framework est **pré-V1** : une API n’est stable que si elle est couverte par un test et présentée
  comme contrat (voir [Décisions et trajectoire V1](architecture/decisions-v1.md#3-dette-technique-et-risques)).
