# État et reconstruction

[Documentation](index.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [Cycle de vie](lifecycle.md) · [Effets de bord](side-effects.md) · [Architecture](app-architecture.md)

L’**état** est toute valeur qui peut changer pendant la vie de l’application et dont l’interface dépend : un
compteur, le texte d’un champ, la liste des éléments chargés, l’onglet sélectionné. Ce guide explique où le
mettre, comment l’exposer à un composant, comment le dériver, et quand il déclenche une reconstruction.

## Sommaire

1. [Les objets d’état](#1-les-objets-détat)
2. [Où garder l’état](#2-où-garder-létat)
3. [Remonter l’état (state hoisting)](#3-remonter-létat-state-hoisting)
4. [Lier l’état à l’interface](#4-lier-létat-à-linterface)
5. [Quand appeler `Rebuild()`](#5-quand-appeler-rebuild)
6. [Valeurs dérivées](#6-valeurs-dérivées)
7. [Collections et objets](#7-collections-et-objets)
8. [État par élément de liste](#8-état-par-élément-de-liste)
9. [Conserver l’état](#9-conserver-létat)
10. [Pièges fréquents](#10-pièges-fréquents)

---

## 1. Les objets d’état

| Objet | Fabrique | Usage |
|---|---|---|
| `State<T>` | `UseState(initial)`, `UseStates(n, initial)` | Une valeur observable liée à l’interface |
| `ScrollState` | `UseScroll()` | Position de défilement d’un conteneur |
| `LazyListState` | `UseLazyList()` | Position, page et fenêtre d’une liste virtualisée ou d’un pager |
| `TextEditController` | `UseTextEdit("initial")` | Texte, sélection, historique d’un champ |

Ils partagent trois règles :

1. **Ils sont persistants** : on les crée une fois et on les garde.
2. **Ils ne sont jamais créés dans `Body()`** : les fabriques `Use…` retournent un objet **neuf** à chaque
   appel ; dans `Body()` vous en créeriez un nouveau à chaque reconstruction, et l’état reviendrait à sa valeur
   initiale.
3. **Ils sont modifiés sur le thread UI.**

### `State<T>`

```csharp
public class State<T>
{
    public event Action? OnChanged;     // levé quand la valeur change réellement
    public T Value { get; set; }        // l’affectation d’une valeur égale est ignorée
}
```

L’égalité utilise `EqualityComparer<T>.Default` : réaffecter la même valeur est gratuit et ne notifie personne.
Pour un type référence mutable (une `List<T>` modifiée sur place), **la valeur ne change pas** au sens de
l’égalité : voir [§7](#7-collections-et-objets).

## 2. Où garder l’état

| Portée | Où | Exemple | Durée de vie |
|---|---|---|---|
| Un écran | **Champ de la page** | `_scroll`, `_category`, un `TextEditController` | Celle de la page (toute l’application, car les pages sont instanciées une fois) |
| Un composant avec état interne | **Champ d’une classe de composant**, détenue par la page | `Expander._open` | Celle de la page qui détient l’instance |
| Un élément d’une liste virtualisée | **Dans la fonction de l’élément** | `PeoplePage.PersonRow` | Tant que la ligne est matérialisée |
| Plusieurs écrans | **Un modèle / service** créé dans `App` | `ArticleDetailsModel` | Celle de l’application |

```csharp
public sealed class SettingsPage : XenoPage
{
    private readonly ScrollState _scroll = UseScroll();         // écran
    private readonly State<bool> _autoplay = UseState(true);    // écran
    private readonly State<bool> _dataSaver = UseState(false);  // écran
    …
}
```

> Les pages sont créées **une seule fois** (`Page<T>()`) : leurs champs conservent leur valeur quand l’utilisateur
> navigue ailleurs puis revient. Une destination du scaffold déjà montée garde même sa scène (son arbre, son
> scroll, ses contrôleurs) sans rappeler `Body()`.

## 3. Remonter l’état (state hoisting)

*State hoisting* : retirer l’état d’un composant pour le confier à son appelant. Le composant devient une
fonction pure qui **reçoit** la valeur et **expose** des événements.

Avant (état interne, difficile à tester, impossible à partager) :

```csharp
public sealed class Counter
{
    private readonly State<int> _n = UseState(0);
    public XenoComponent Body() =>
        Row(Text(_n, 20), Button("+", () => _n.Value++));
}
```

Après (état remonté) :

```csharp
public static class Counter
{
    public static XenoComponent View(State<int> count, Action increment) =>
        Row(Text(count, 20), Button("+", increment)).Gap(8);
}

// Dans la page
private readonly State<int> _count = UseState(0);
… Counter.View(_count, () => _count.Value++)
```

Avantages : le composant est réutilisable (deux compteurs liés au même état), prévisualisable
(`View(UseState(42), () => { })`) et la logique est dans la page. **Remontez l’état au plus bas ancêtre commun**
de tous les composants qui le lisent ou le modifient — pas plus haut.

Deux variantes selon le besoin :

| Variante | Signature | Quand |
|---|---|---|
| **Lecture seule + événement** (contrôlé) | `View(bool on, Action<bool> onChange)` | Composants dont la forme change (interrupteur, onglet) ; la page appelle `Rebuild()` |
| **État observable** | `View(State<int> count, …)` | Valeurs qui changent souvent ou localement (texte, couleur, progression) |

## 4. Lier l’état à l’interface

La liaison est **explicite**. Seuls ces points d’entrée observent un `State` :

| Liaison | Met à jour |
|---|---|
| `Text(State<T>)`, `Text($"…{state}…")` | Le texte (et sa mesure) |
| `.TextColor(State<uint>)` | La couleur du texte |
| `.Background(State<uint>)` | La couleur de fond |
| `LinearProgress(State<float>)` | La part remplie (0–1) |
| `ImageFile(State<string?>)` | L’image locale affichée |
| `.NativeVideo(State<MediaHandle>)` ou `(handle, State<bool>)` | La sortie vidéo native |

```csharp
Text($"{_followers} abonnés")                  // se met à jour quand _followers change
Text($"{price:N2} €", 16)                      // `price` non réactif : formaté une fois
```

Dans une chaîne interpolée, seuls les `State<T>` incorporés sont suivis ; les autres valeurs sont formatées à
la composition (spécificateurs de format et alignement acceptés : `{state:N2}`, `{state,8}`).

Lire `_count.Value` dans `Body()` donne la valeur **à cet instant** ; cela ne crée aucun abonnement.
Pour que l’affichage suive, passez le `State` lui-même à la liaison.

## 5. Quand appeler `Rebuild()`

`Rebuild()` rappelle `Body()` de la page active et remplace son arbre transitoire. Appelez-le quand la
**structure** dépend de l’état :

- une branche conditionnelle bascule (`_expanded ? A : B`) ;
- des éléments sont ajoutés, retirés ou réordonnés ;
- le contenu d’une liste change (la liste, elle, ne recompose que sa fenêtre visible) ;
- une couleur ou une taille **calculée à la composition** dépend d’une valeur qui change.

```csharp
private int _category;

private XenoComponent CategoryChip(string name) { /* … */
    return Text(name, 12)…Clickable(() => { _category = index; Rebuild(); });
}
```

À savoir :

- `Rebuild()` peut être appelée depuis n’importe quel thread : elle se **replanifie elle-même** sur le thread de
  contrôle. Ce n’est pas le cas des affectations de `State` ([Effets de bord](side-effects.md)).
- Elle ne touche ni la pile de navigation, ni les overlays ouverts, ni les objets persistants.
- Elle est **plus chère** qu’une liaison fine : n’en abusez pas pendant un geste (défilement, glissement) ou à
  chaque frappe de clavier.
- `RebuildTopBar()` reconstruit seulement la barre d’application quand ce qu’elle affiche (titre, icône
  d’action) a changé.
- Pas de `Rebuild()` si la page n’est plus active : testez `IsActive` dans les rappels tardifs.

## 6. Valeurs dérivées

Il n’y a pas d’équivalent automatique à `derivedStateOf`. Trois façons de dériver une valeur d’un état :

**a) Calculer dans `Body()` (cas le plus fréquent).** Valable si la dérivée n’a pas besoin de se mettre à jour
sans reconstruction :

```csharp
bool canSubmit = _name.Value.Text.Length > 0 && _terms.Value;
return … Button("Valider", Submit).Background(canSubmit ? Theme.Accent : Theme.Faint) …
```

**b) Calculer à l’écriture** et publier dans un second `State` :

```csharp
_name.Changed += (_, change) =>
    _namePreview.Value = change.Value.Text.Length == 0 ? "Nothing typed yet"
                                                       : $"{change.Value.Text.Length} caractères";
```

`TextInputPage` fait exactement cela : le contrôleur lève `Changed`, le gestionnaire met à jour un `State<string>`
affiché par `Text(_namePreview, 13)`.

**c) S’abonner à `OnChanged`** d’un `State` pour en produire un autre :

```csharp
_a.OnChanged += () => _sum.Value = _a.Value + _b.Value;
_b.OnChanged += () => _sum.Value = _a.Value + _b.Value;
```

Désabonnez-vous quand l’objet qui s’abonne disparaît (page détruite) si les durées de vie diffèrent.

## 7. Collections et objets

`State<List<T>>` ne notifie pas quand on `Add` à la liste : la **référence** n’a pas changé. Trois options :

1. **Garder la liste dans un champ** et appeler `Rebuild()` après modification — c’est le choix des pages du
   sample (`PeoplePage._following`, `OverlaysPage._last`).
2. **Remplacer la valeur** : `_items.Value = _items.Value.Append(x).ToList();` (nouvelle référence → notification
   ; utile si une liaison dépend de la liste).
3. **Utiliser un snapshot immuable** publié par un modèle :
   `public sealed record RemoteSnapshot<T>(FeedStatus Status, T[] Items, string? Error = null)` ; le modèle
   remplace le snapshot entier, la page appelle `Rebuild()` sur `Changed`.

Préférez les **types immuables** (`record`, tableaux copiés) pour ce que l’interface lit : cela évite les
courses entre le thread qui charge et le thread UI.

## 8. État par élément de liste

Dans une liste virtualisée, la fonction d’un élément est appelée quand l’élément entre dans la fenêtre :

```csharp
private XenoComponent PersonRow(int index)
{
    bool following = _following.Contains(index);            // source de vérité : champ de page
    var label = UseState(following ? "Following" : "Follow");     // états de la ligne
    var background = UseState(following ? Theme.SurfaceHigh : Theme.Accent);
    return Row(…, Text(label, 12).Background(background)…Clickable(() =>
    {
        bool now = _following.Add(index);
        if (!now) _following.Remove(index);
        label.Value = now ? "Following" : "Follow";
        background.Value = now ? Theme.SurfaceHigh : Theme.Accent;
    }));
}
```

- La **vérité** est dans un champ de page (`_following`) ; les `State` de la ligne ne sont qu’un **reflet**
  vivant tant que la ligne existe.
- Donnez toujours une **clé stable** (`key: item => item.Id`) : elle identifie l’élément quand la fenêtre
  bouge.
- N’y mettez pas d’état que vous voulez garder quand la ligne sort de l’écran.

## 9. Conserver l’état

| Événement | L’état est-il conservé ? |
|---|---|
| `Rebuild()` de la page | Oui, pour les champs ; les objets créés dans `Body()` sont perdus |
| Navigation vers une autre page puis retour | Oui (la page est un singleton) |
| Changement d’onglet du scaffold | Oui (la scène reste montée) |
| Rotation / changement de taille | Oui (mise en page recalculée, pas de recomposition d’état) |
| Application en arrière-plan puis retour | Oui tant que le processus vit |
| Processus tué par le système, redémarrage à froid | **Non** : pas de restauration automatique d’état fournie |

Pour survivre à la mort du processus, persistez vous-même ce qui compte (préférences, brouillon) avec les
API de la plateforme et rechargez-le dans `App.OnStart()` ou `OnCreate()` d’une page. Le framework ne fournit pas
encore d’équivalent à `rememberSaveable` ([Limites V1](navigation-scaffold.md#limites-v1)).

## 10. Pièges fréquents

| Piège | Symptôme | Correction |
|---|---|---|
| `UseState` dans `Body()` | La valeur repart à l’initiale à chaque `Rebuild()` | Champ de page |
| Lire `state.Value` dans `Body()` en espérant un suivi | L’écran ne suit pas | Passer le `State` à `Text(state)` / `.Background(state)` |
| Modifier une `List` sur place dans un `State<List<T>>` | Aucune notification | Champ + `Rebuild()`, ou remplacer la référence |
| Affecter un `State` depuis un thread de fond | Corruption visuelle | `IUiDispatcherCapability.Post` |
| `Rebuild()` à chaque frappe | Perte de focus, saccades | Liaison fine (`Text(state)`) |
| Deux copies de la même donnée | Écrans incohérents | Une source de vérité, l’autre dérivée |
| Garder un `XenoComponent` dans un champ | Entité morte après `Rebuild()` | Garder les données, recomposer |
