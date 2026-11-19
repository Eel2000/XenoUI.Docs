# Animations et mouvement

[Documentation](index.md) · [Guide développeur](developer-guide.md) · [Créer un composant](components.md) · [Personnaliser](customization.md) · [Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md) · [Navigation](navigation-scaffold.md)

Ce guide décrit **tout ce que XenoUI sait animer aujourd’hui**, comment le configurer, ses unités, ses
limites, et comment obtenir un mouvement sur vos propres composants là où le framework n’offre pas (encore)
d’API dédiée. Il est écrit d’après le code de la branche courante ; quand un comportement n’existe pas, ce
guide le dit.

## Sommaire

1. [Panorama : qui anime quoi](#1-panorama--qui-anime-quoi)
2. [Principes communs](#2-principes-communs)
3. [Retour au toucher et changements d’état](#3-retour-au-toucher-et-changements-détat)
4. [Opacité et fondu d’un composant](#4-opacité-et-fondu-dun-composant)
5. [Animer soi-même, pas à pas](#5-animer-soi-même-pas-à-pas)
6. [Transitions de pages : `NavTransition`](#6-transitions-de-pages--navtransition)
7. [Animer la sélection dans la navigation](#7-animer-la-sélection-dans-la-navigation)
8. [Animer un overlay : `OverlayAnimation`](#8-animer-un-overlay--overlayanimation)
9. [Mouvement réduit](#9-mouvement-réduit)
10. [Courbes d’accélération (easings)](#10-courbes-daccélération-easings)
11. [Recettes](#11-recettes)
12. [Performance et diagnostic](#12-performance-et-diagnostic)
13. [Limites actuelles](#13-limites-actuelles)

---

## 1. Panorama : qui anime quoi

| Besoin | Mécanisme | Animé par le moteur ? | Section |
|---|---|---|---|
| Teinte d’un élément pressé | Automatique sur `Clickable` | Oui (instantané : pressé / relâché) | [§3](#3-retour-au-toucher-et-changements-détat) |
| Changer texte, couleur de texte, couleur de fond, barre de progression | `State<T>` lié (`Text(state)`, `.Background(state)`…) | Mise à jour **immédiate**, sans interpolation | [§3](#3-retour-au-toucher-et-changements-détat) |
| Fondu / interpolation d’un composant | État piloté par votre minuterie | Non : c’est du code à vous (recette fournie) | [§4](#4-opacité-et-fondu-dun-composant), [§5](#5-animer-soi-même-pas-à-pas) |
| Passage d’une page à l’autre (push/pop) | `NavTransition` | **Oui**, compilée à froid, sans allocation par frame | [§6](#6-transitions-de-pages--navtransition) |
| Changement de destination principale (onglets, rail, tiroir) | `DestinationTransition`, `TransitionFromTo` | Oui | [§6](#6-transitions-de-pages--navtransition) |
| Item de navigation sélectionné (icône, libellé, indicateur, fond, badge, FAB) | `NavigationSelectionTransition` | Oui | [§7](#7-animer-la-sélection-dans-la-navigation) |
| Apparition / disparition d’un snackbar, dialog, popup, bottom sheet | `OverlayAnimation` | Oui | [§8](#8-animer-un-overlay--overlayanimation) |
| Glisser pour fermer un overlay, tiroir, défilement | Gestes + physique internes | Oui | [Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md#glisser-pour-fermer) |

Il n’existe **pas** à ce jour de modificateur générique `Animate(...)`, `Opacity(...)`, `TranslateX(...)` ou
`Scale(...)` sur un `XenoComponent` quelconque, ni de moteur de ressorts (springs) public. Le mouvement
« natif » concerne des *scènes* (pages, chrome de navigation, overlays) ; le mouvement d’un composant
individuel se fait par états pilotés ([§5](#5-animer-soi-même-pas-à-pas)).

## 2. Principes communs

**Valeurs de présentation, après la mise en page.** Les animations de navigation et d’overlay modifient la
*présentation* (translation, échelle, opacité, rotation, rognage) d’une scène ou d’un élément **après** le
layout. Elles ne remesurent ni ne replacent rien ; pour les éléments d’un chrome de navigation, elles ne déplacent pas
la cible tactile d’une destination.

**Compilation à froid.** Une `NavTransition`, une `NavigationSelectionTransition` et une `OverlayAnimation`
sont construites **une fois** par un builder ; pendant les frames, le moteur ne parcourt que des tableaux de
pistes et ne rappelle jamais votre code. En particulier, une fonction d’easing que vous fournissez est
**échantillonnée dans une table (LUT) à la construction** : elle n’est pas appelée par frame.

**Immuabilité.** Ces objets sont immuables et partageables ; créez-les une fois (`static readonly`) et
réutilisez-les.

**Piste (*track*).** Une animation est un ensemble de pistes. Une piste = une propriété, une valeur de départ
(`From`), d’arrivée (`To`), un intervalle de progression (`StartFraction`…`EndFraction`, de 0 à 1) et un
easing. Plusieurs pistes avec des intervalles décalés produisent des mouvements échelonnés.

**Durée en millisecondes**, **fractions normalisées** pour la progression, **easing** parmi `Linear`,
`Standard`, `Emphasized`, `EaseIn`, `EaseOut` ou une fonction à vous.

## 3. Retour au toucher et changements d’état

### 3.1 Teinte au toucher

Toute entité `Clickable` dont le fond n’est pas transparent est assombrie de 15 % tant qu’elle est pressée.
C’est un simple changement de couleur au dessin (aucun layout) : pas d’interpolation, la teinte apparaît à
l’appui et disparaît au relâchement. Elle ne s’applique pas aux champs de texte.

### 3.2 Changements d’état liés

Ces liaisons mettent à jour l’entité **immédiatement** quand le `State` change :

| Liaison | Effet |
|---|---|
| `Text(State<T>)`, `Text($"…{state}")` | Remplace le texte et le remesure |
| `.TextColor(State<uint>)` | Change la couleur du texte |
| `.Background(State<uint>)` | Change la couleur de fond |
| `LinearProgress(State<float>)` | Redimensionne la part remplie (0–1) |
| `ImageFile(State<string?>)` | Change l’image affichée |
| `.NativeVideo(State<…>)` | Active ou change la sortie vidéo |

Elles ne **transitionnent pas** : c’est un saut. Pour un passage progressif, faites évoluer le `State`
vous-même ([§5](#5-animer-soi-même-pas-à-pas)). Chaque affectation d’un `State` à une valeur *identique* est
ignorée.

## 4. Opacité et fondu d’un composant

XenoUI n’a pas de modificateur d’opacité sur une entité arbitraire. Deux techniques s’appliquent selon la
cible.

### 4.1 Fondu par le canal alpha de la couleur (un élément)

Le texte et le fond sont des couleurs ARGB : faire varier l’octet alpha donne un fondu.

```csharp
private readonly State<uint> _titleColor = UseState(0x00111827u);   // texte invisible
private readonly State<uint> _cardColor  = UseState(0x00FFFFFFu);   // fond invisible

Column(Text("Bienvenue", 20).TextColor(_titleColor))
    .Padding(16).Background(_cardColor).CornerRadius(16);
```

Passer de `0x00…` à `0xFF…` par pas successifs (voir [§5](#5-animer-soi-même-pas-à-pas)) fait apparaître le
texte et le fond. Limite : la liaison existe pour `Background` et `TextColor`, pas pour les couleurs d’icône
(`Icon(...).TextColor(State<uint>)` fonctionne : une icône est du texte), ni pour les images.

### 4.2 Fondu d’un sous-arbre complet : passer par une scène

L’opacité d’une **scène entière** (page, overlay, chrome) est rendue par une couche compositée : tout le
sous-arbre s’estompe d’un bloc, sans artefact de recouvrement. Pour estomper un groupe de composants qui se
chevauchent, affichez-le comme overlay non modal avec `OverlayAnimation.Fade(...)`
([§8](#8-animer-un-overlay--overlayanimation)).

## 5. Animer soi-même, pas à pas

Le mécanisme est : **un `State` + un minuteur + `IUiDispatcherCapability.Post`**. L’exemple `FeedPage` du
`SampleApp` l’emploie déjà (`After(ms, action)` pour un cœur qui disparaît, un toast qui se retire). Ce qui suit
généralise ce motif en interpolation.

> Ceci est du **code d’application**, pas une API du framework. Le minuteur n’est pas synchronisé sur le
> rafraîchissement d’écran : les pas sont discrets (≈ 60 par seconde au mieux). Réservez ce mécanisme à des
> animations courtes et peu coûteuses (couleur, alpha, progression) ; pour déplacer ou redimensionner des
> panneaux, préférez les scènes ([§6](#6-transitions-de-pages--navtransition), [§8](#8-animer-un-overlay--overlayanimation)).

### 5.1 Boîte à outils

```csharp
using System.Diagnostics;
using XenoUI.Core.NativeTunnel;

namespace MyApp.Design;

/// <summary>Fonctions d’easing et interpolation de couleurs (code d’application).</summary>
public static class Motion
{
    public static float Linear(float t) => t;
    public static float EaseOutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
    public static float EaseInOut(float t) => t * t * (3 - 2 * t);

    private static int Channel(uint color, int shift) => (int)((color >> shift) & 0xFF);

    private static uint Mix(uint from, uint to, float t, int shift)
    {
        int a = Channel(from, shift), b = Channel(to, shift);
        return (uint)Math.Clamp((int)MathF.Round(a + (b - a) * t), 0, 255);
    }

    /// <summary>Interpolation canal par canal de deux couleurs ARGB.</summary>
    public static uint Lerp(uint from, uint to, float t) =>
        (Mix(from, to, t, 24) << 24) | (Mix(from, to, t, 16) << 16) |
        (Mix(from, to, t, 8) << 8) | Mix(from, to, t, 0);

    /// <summary>Même couleur avec un alpha de 0 à 1.</summary>
    public static uint WithAlpha(uint color, float alpha) =>
        ((uint)Math.Clamp((int)MathF.Round(alpha * 255), 0, 255) << 24) | (color & 0x00FFFFFF);
}

/// <summary>Exécute une interpolation 0→1 et applique chaque pas sur le thread UI.</summary>
public static class Tween
{
    /// <returns>Source d’annulation : appelez <c>Cancel()</c> pour interrompre.</returns>
    public static CancellationTokenSource Run(IUiDispatcherCapability ui, int milliseconds,
        Func<float, float> ease, Action<float> apply, Action? done = null)
    {
        var source = new CancellationTokenSource();
        var token = source.Token;
        int pending = 0;                                  // au plus un pas en attente côté UI

        _ = Task.Run(async () =>
        {
            var clock = Stopwatch.StartNew();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    float t = milliseconds <= 0 ? 1f
                        : Math.Min(1f, clock.ElapsedMilliseconds / (float)milliseconds);
                    float eased = ease(t);
                    bool last = t >= 1f;
                    if (last || Interlocked.Exchange(ref pending, 1) == 0)
                    {
                        ui.Post(() =>
                        {
                            Volatile.Write(ref pending, 0);
                            if (token.IsCancellationRequested) return;
                            apply(eased);
                            if (last) done?.Invoke();
                        });
                    }
                    if (last) break;
                    await Task.Delay(16, token);
                }
            }
            catch (OperationCanceledException) { /* annulée : rien à faire */ }
        });
        return source;
    }
}
```

Points de conception :

- `apply` s’exécute **sur le thread UI** (via `Post`) : c’est là qu’on affecte les `State`.
- `pending` évite d’empiler des pas si le thread UI est en retard ; le **dernier** pas est toujours posté.
- `ease` est appelée sur le fil d’arrière-plan : gardez-la pure et rapide.
- Annulez l’animation précédente avant d’en lancer une nouvelle sur les mêmes états.

### 5.2 Exemple : apparition en fondu d’une carte

```csharp
public sealed class WelcomePage : XenoPage
{
    private readonly State<uint> _surface = UseState(0x00FFFFFFu);
    private readonly State<uint> _title   = UseState(0x00111827u);
    private CancellationTokenSource? _anim;

    public override void OnActivated() => FadeIn();
    public override void OnDeactivated() => _anim?.Cancel();
    public override void OnDestroy() => _anim?.Cancel();

    private void FadeIn()
    {
        _anim?.Cancel();
        var ui = Native.Require<IUiDispatcherCapability>();
        bool reduced = Native.Get<IMotionPreferencesCapability>()?.ReducedMotion == true;
        _anim = Tween.Run(ui, reduced ? 0 : 320, Motion.EaseOutCubic, t =>
        {
            _surface.Value = Motion.WithAlpha(0xFFFFFFFF, t);
            _title.Value = Motion.WithAlpha(0xFF111827, t);
        });
    }

    public override XenoComponent Body() =>
        Column(Text("Bienvenue", 22).TextColor(_title))
            .Padding(24).Background(_surface).CornerRadius(20).Margin(24);
}
```

### 5.3 Exemple : couleur qui passe d’un état à l’autre

```csharp
private readonly State<uint> _chip = UseState(0xFFE5E7EB);
private CancellationTokenSource? _chipAnim;

private void SetSelected(bool selected)
{
    uint from = _chip.Value, to = selected ? 0xFF6D28D9 : 0xFFE5E7EB;
    _chipAnim?.Cancel();
    _chipAnim = Tween.Run(Native.Require<IUiDispatcherCapability>(), 180, Motion.EaseInOut,
        t => _chip.Value = Motion.Lerp(from, to, t));
}

// Body(): Text("Swift", 13).Padding(12, 6).CornerRadius(16).Background(_chip).Clickable(() => SetSelected(true))
```

### 5.4 Exemple : progression animée

```csharp
private readonly State<float> _progress = UseState(0f);

private void ProgressTo(float target)
{
    float from = _progress.Value;
    Tween.Run(Native.Require<IUiDispatcherCapability>(), 400, Motion.EaseOutCubic,
        t => _progress.Value = from + (target - from) * t);
}

// Body(): LinearProgress(_progress, height: 4, trackColor: 0x33000000, fillColor: 0xFF6D28D9)
```

Chaque pas de `LinearProgress` relance un layout de la barre (deux `FlexGrow`) : c’est acceptable pour une
barre mince, évitez l’équivalent sur de grands conteneurs.

### 5.5 Garde-fous obligatoires

| Règle | Pourquoi |
|---|---|
| Modifier les `State` sur le thread UI uniquement | Les callbacks marquent des entités sales ; ce n’est pas thread-safe depuis un fil de fond |
| Annuler dans `OnDeactivated`/`OnDestroy` | Éviter des écritures tardives sur une page quittée |
| Tester `IsActive` si vous reprenez une animation asynchrone | Une page en arrière-plan n’a pas à animer |
| Respecter le mouvement réduit ([§9](#9-mouvement-réduit)) | Accessibilité |
| Ne pas animer un `Rebuild()` | Une reconstruction recompose la page ; animez des `State` |
| Ne pas lancer de minuteur par élément d’une liste | Piloter un `State` partagé, ou animer à la sélection seulement |

## 6. Transitions de pages : `NavTransition`

C’est l’animation « native » la plus complète : elle s’exécute dans la boucle de rendu, sur des scènes
complètes.

### 6.1 Vocabulaire

Une `NavTransition` a :

- des pistes **`Enter`** (la scène qui arrive, « incoming ») ;
- des pistes **`Exit`** (la scène qui part, « outgoing ») ;
- une **durée** (280 ms par défaut du builder) ;
- sa version inverse compilée, `Reversed()`, utilisée pour le retour.

Une scène démarre à l’état d’identité : échelle 1, opacité 1, translation 0, pivot au centre, pas de
rognage. Chaque piste fait évoluer une propriété de cet état.

### 6.2 Propriétés animables (`NavAnimatedProperty`)

| Propriété | Unité / sens pour une **page** | Valeur neutre |
|---|---|---|
| `TranslationX` | Fraction de la **largeur** de l’écran (1 = une largeur entière) | 0 |
| `TranslationY` | Fraction de la **hauteur** de l’écran | 0 |
| `ScaleX`, `ScaleY` | Facteur autour du pivot | 1 |
| `Opacity` | 0–1, rendu par une couche sur la scène entière | 1 |
| `Rotation` | Degrés autour du pivot | 0 |
| `PivotX`, `PivotY` | Fraction de la largeur/hauteur (0,5 = centre) | 0,5 |
| `ClipLeft`, `ClipTop` | Bord gauche/haut du rognage, fraction (0 = bord) | 0 |
| `ClipRight`, `ClipBottom` | Bord droit/bas du rognage, fraction (1 = bord) | 1 |
| `CornerRadius` | dp, arrondit le rognage de la scène | 0 |
| `Elevation` | Définie et évaluée, mais **non utilisée par le rendu des scènes** dans cette version | 0 |

> Dans un **overlay** en `OverlayAnimation.Custom(...)`, les translations sont en **dp** relatives à la
> position finale du panneau (voir [§8](#8-animer-un-overlay--overlayanimation)). Pour une page, ce sont des
> fractions de l’écran.

### 6.3 Construire une transition

```csharp
public static class AppTransitions
{
    public static readonly NavTransition SlideAndFade = NavTransition.Create()
        .Enter(scene => scene
            .TranslateX(1, 0, NavEasing.Emphasized)     // arrive de la droite
            .Opacity(0.4f, 1))
        .Exit(scene => scene
            .TranslateX(0, -0.18f)                      // part légèrement vers la gauche
            .Opacity(1, 0.8f))
        .Duration(300)
        .Build();
}
```

Raccourcis du builder de scène (`NavSceneTransitionBuilder`) :

| Méthode | Équivaut à |
|---|---|
| `TranslateX(from, to, easing)` | `Animate(TranslationX, …)` |
| `TranslateY(from, to, easing)` | `Animate(TranslationY, …)` |
| `Opacity(from, to, easing)` | `Animate(Opacity, …)` |
| `Scale(from, to, easing)` | Deux pistes : `ScaleX` et `ScaleY` |
| `Animate(property, from, to, easing, startFraction, endFraction)` | Piste complète |
| `Animate(property, from, to, Func<float,float> easing, startFraction, endFraction, samples)` | Easing à vous (échantillonné en table) |

Contraintes validées par le builder : `from` et `to` finis ; `0 ≤ startFraction ≤ endFraction ≤ 1` ;
`samples` entre 2 et 4096 ; un easing à vous doit renvoyer des valeurs finies. Une piste dont
`startFraction == endFraction` saute à sa valeur finale à cet instant.

### 6.4 Échelonner : fractions de progression

Chaque piste vit dans sa fenêtre `[StartFraction, EndFraction]` de la durée totale :

```csharp
NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.TranslationY, 0.08f, 0,
                 NavEasing.EaseOut, startFraction: 0.00f, endFraction: 0.70f)
        .Animate(NavAnimatedProperty.Opacity, 0, 1,
                 NavEasing.Linear, startFraction: 0.20f, endFraction: 1.00f))
    .Exit(scene => scene
        .Animate(NavAnimatedProperty.Opacity, 1, 0,
                 NavEasing.Linear, startFraction: 0.00f, endFraction: 0.25f))
    .Duration(320)
    .Build();
```

C’est ainsi que `FadeThrough` est construite : la scène sortante s’efface pendant les 35 premiers pour cent, la
scène entrante apparaît ensuite (et grandit de 0,96 à 1).

### 6.5 Prédéfinies : `NavTransitions`

| Prédéfinie | Entrée | Sortie | Durée par défaut |
|---|---|---|---|
| `None` | — | — | 0 |
| `Fade(d)` | Opacité 0→1 | Opacité 1→0 | 220 |
| `FadeThrough(d)` | Opacité 0→1 sur les derniers 65 %, échelle 0,96→1 | Opacité 1→0 sur les premiers 35 % | 260 |
| `SlideHorizontal(d)` | `TranslateX` 1→0 | `TranslateX` 0→−0,25, opacité 1→0,85 | 280 |
| `SlideHorizontalBack(d)` | `TranslateX` −0,25→0, opacité 0,85→1 | `TranslateX` 0→1 | 260 |
| `SlideVertical(d)` | `TranslateY` 1→0 | `TranslateY` 0→−0,15, opacité 1→0,85 | 280 |
| `ScaleFade(d)` | Échelle 0,92→1, opacité 0→1 | Échelle 1→0,96, opacité 1→0 | 260 |
| `PlatformDefault` | = `SlideHorizontal()` | | |
| `PlatformBack` | = `SlideHorizontalBack()` | | |

### 6.6 Appliquer une transition

**Navigation push/pop** (depuis une page) :

```csharp
// Aller et retour distincts
Navigate<DetailsPage>(NavTransitions.SlideVertical(320), NavTransitions.Fade(220));

// Une seule transition (le retour utilise la transition inverse compilée)
Navigate<DetailsPage>(AppTransitions.SlideAndFade);

// Par options
Navigate<DetailsPage>(new NavigationOptions
{
    Transition = AppTransitions.SlideAndFade,
    PopTransition = NavTransitions.Fade()
});
```

**Destinations principales du scaffold** (onglets, rail, tiroir) :

```csharp
private readonly NavigationScaffoldState _navigation = new(/* … */)
{
    DestinationTransition = NavTransitions.FadeThrough()
};

// Cold path : transition spécifique entre deux destinations
_navigation.TransitionFromTo<HomePage, SearchPage>(
    forward: NavTransitions.SlideHorizontal(),
    backward: NavTransitions.SlideHorizontalBack());
```

Sans paire explicite, l’ordre des destinations donne le sens : la transition configurée est utilisée vers
l’avant et son inverse compilé vers l’arrière.

### 6.7 `Reversed()`

`transition.Reversed()` renvoie l’inverse temporel : les pistes d’entrée et de sortie échangent leurs rôles,
`From`/`To` sont permutés, les fenêtres de progression sont retournées (`1 − fin` … `1 − début`), `EaseIn` et
`EaseOut` sont échangées, et les LUT d’easing sont inversées. Le résultat est mis en cache. Le retour de
navigation est donc **le miroir exact** de l’aller (la page qui part reste au-dessus de celle qu’elle révèle).

### 6.8 Exemples de transitions

**Révélation depuis le centre (rognage + rayon)** :

```csharp
public static readonly NavTransition RevealFromCenter = NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.ClipLeft,   0.5f, 0, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.ClipTop,    0.5f, 0, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.ClipRight,  0.5f, 1, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.ClipBottom, 0.5f, 1, NavEasing.Emphasized)
        .Animate(NavAnimatedProperty.CornerRadius, 48, 0, NavEasing.EaseOut))
    .Exit(scene => scene.Opacity(1, 0.7f))
    .Duration(360)
    .Build();
```

**Rebond (easing à vous)** :

```csharp
public static readonly NavTransition Bounce = NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.TranslationY, 1, 0,
                 easing: t => 1 - MathF.Exp(-6 * t) * MathF.Cos(10 * t))
        .Animate(NavAnimatedProperty.Opacity, 0, 1, NavEasing.EaseOut,
                 startFraction: 0, endFraction: 0.4f))
    .Exit(scene => scene.Opacity(1, 0))
    .Duration(480)
    .Build();
```

**Rotation + échelle (carte qui se retourne à moitié)** :

```csharp
NavTransition.Create()
    .Enter(scene => scene
        .Animate(NavAnimatedProperty.Rotation, -8, 0, NavEasing.EaseOut)
        .Scale(0.85f, 1, NavEasing.EaseOut)
        .Opacity(0, 1))
    .Exit(scene => scene.Scale(1, 1.05f).Opacity(1, 0))
    .Duration(280)
    .Build();
```

## 7. Animer la sélection dans la navigation

`NavigationSelectionTransition` décrit comment **chaque élément d’un chrome de navigation** (barre d’onglets,
rail, tiroir) change quand sa destination est sélectionnée ou désélectionnée. Elle ne possède aucune scène et
n’invoque jamais votre code pendant un frame.

### 7.1 Éléments et propriétés

Éléments (`NavigationSelectionElement`) : `Surface`, `Indicator`, `Icon`, `Label`, `Badge`, `Fab`,
`SharedIndicator`.

Propriétés (`NavigationSelectionProperty`) : `TranslationX/Y` (dp), `ScaleX/Y`, `Opacity`, `CornerRadius` (dp),
`Elevation` (dp), `BackgroundColor`, `ForegroundColor` (ARGB, interpolées), et `MatchBounds*` (suivre les
limites de l’item sélectionné, pour un indicateur partagé).

Chaque piste donne une valeur **inactive** (item non sélectionné) et une valeur **active** (sélectionné), et
peut avoir sa fenêtre de progression.

### 7.2 Construire

```csharp
var transition = NavigationSelectionTransition.Create()
    .Surface(x => x
        .BackgroundColor(0x00000000, 0xFF29274D)
        .Scale(0.94f, 1)
        .CornerRadius(14, 22))
    .Icon(x => x
        .Scale(0.88f, 1)
        .ForegroundColor(0xFF98A2BD, 0xFFFFFFFF))
    .Label(x => x.Opacity(0.65f, 1, startFraction: 0.2f))
    .Fab(x => x.Opacity(0, 1).Scale(0.8f, 1))
    .Duration(260)
    .Delay(40)
    .Easing(NavEasing.Emphasized)
    .Build();
```

Méthodes du builder d’élément (`NavigationSelectionElementBuilder`) : `TranslateX`, `TranslateY`, `Scale`,
`Opacity`, `CornerRadius`, `Elevation`, `BackgroundColor`, `ForegroundColor`, `MatchSelectedBounds`, chacune
avec `startFraction`/`endFraction` facultatifs.

Méthodes du builder principal : `Surface`, `Indicator`, `Icon`, `Label`, `Badge`, `Fab`, `SharedIndicator`,
`Duration(ms)` (240 par défaut), `Delay(ms)`, `Easing(NavEasing)`, `Easing(Func<float,float>, samples)`,
`Enabled(bool)`.

### 7.3 Prédéfinies : `NavigationSelectionTransitions`

| Prédéfinie | Effet |
|---|---|
| `None` | Désactivée (changement immédiat) |
| `FadeScale(d)` | Icône : échelle 0,86→1 et opacité ; libellé : opacité ; fond de surface teinté |
| `SpringIndicator(d, overshoot)` | Easing avec dépassement ; surface qui grossit et dont le rayon passe de 14 à 22 |
| `SlideHighlight(d)` | Surface et icône glissent de quelques dp, libellé qui apparaît |

### 7.4 Indicateur partagé

Un indicateur unique qui glisse d’un item à l’autre :

```csharp
// Dans un slot de présentation : context.RegisterSharedIndicator(indicator);
NavigationSelectionTransition.Create()
    .SharedIndicator(x => x.MatchSelectedBounds())
    .Duration(280)
    .Build();
```

Sa position et sa taille suivent les limites arrangées des items, y compris en RTL et avec des items de tailles
différentes.

### 7.5 Où l’appliquer

Dans `NavigationScaffoldState` : `SelectionTransition` (défaut), `BottomBarSelectionTransition`,
`RailSelectionTransition`, `DrawerSelectionTransition`, `SelectionTransitionFromTo<TFrom, TTo>(transition)`,
et par destination à la création. Dans un slot : `context.UseSelectionTransition(transition)`.

Ordre de résolution : paire source/destination → destination cible → slot de présentation → mode
(BottomBar / Rail / Drawer) → `SelectionTransition`.

## 8. Animer un overlay : `OverlayAnimation`

`OverlayAnimation` règle l’entrée et la sortie du **panneau** d’un overlay ; le voile (scrim) s’estompe avec
la même progression. Instances immuables : les `With…` renvoient une copie.

### 8.1 Mouvements (`OverlayMotion`)

| Fabrique | Mouvement | Durée par défaut |
|---|---|---|
| `OverlayAnimation.None` | Apparition instantanée | 0 |
| `Fade(ms)` | Opacité | 180 |
| `Scale(ms, from)` | Grossit de `from` (0,88) à 1 en s’estompant | 220 |
| `SlideUp(ms)` | Monte depuis le bas de l’écran (feuilles, snackbars) | 300 |
| `SlideDown(ms)` | Descend depuis le haut (bannières) | 300 |
| `SlideStart(ms)` / `SlideEnd(ms)` | Entre par le bord de début / de fin (miroir en RTL) | 300 |
| `Custom(NavTransition)` | Vos pistes (translations en **dp**) | Celle de la transition |

Valeurs par défaut des fabriques standard : sortie à **75 %** de la durée d’entrée ; easing d’entrée
`EaseOut`, de sortie `EaseIn`.

### 8.2 Ajustements

```csharp
OverlayAnimation.SlideUp(320)
    .WithFade()                                       // glisse en s’estompant
    .WithDuration(enterMilliseconds: 320, exitMilliseconds: 200)
    .WithEasing(NavEasing.Emphasized, NavEasing.EaseIn);
```

### 8.3 Overlay entièrement animé à votre façon

```csharp
var pop = OverlayAnimation.Custom(NavTransition.Create()
    .Enter(x => x.Opacity(0, 1).Scale(0.6f, 1, NavEasing.EaseOut).TranslateY(24, 0))
    .Exit(x => x.Opacity(1, 0).TranslateY(0, 16))
    .Duration(250)
    .Build());

Snackbar.Show(Application, "Copié", new SnackbarOptions { Animation = pop });
Application.Overlays.Show(h => MaCarte(h.Dismiss), new OverlayOptions
{
    Placement = OverlayPlacement.Center,
    Animation = pop
});
```

Dans un overlay `Custom`, `Enter` joue à l’ouverture et `Exit` à la fermeture ; l’échelle et l’opacité sont
celles du panneau, les translations en dp **relatives à la position placée**. Les mouvements `Custom` ont un
easing `Linear` côté overlay : mettez l’accélération **dans les pistes**.

Défaut du snackbar : `SlideUp(260).WithFade()` en bas, `SlideDown(260).WithFade()` en haut. Le glisser pour
fermer ([Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md#glisser-pour-fermer)) fait suivre le doigt au panneau, estompe le voile, puis
termine la sortie ou revient en place.

## 9. Mouvement réduit

Quand l’utilisateur demande moins de mouvement :

- **Android** : l’échelle de durée des animateurs du système (`animator_duration_scale`) ;
- **iOS** : `UIAccessibility.IsReduceMotionEnabled`.

Le framework relaie ce réglage par `IMotionPreferencesCapability` (`NativeTunnel`) :

- **Overlays** : toute animation est remplacée par `None` ;
- **Navigation** : `ReducedMotion = true` applique immédiatement l’état final de la sélection sans supprimer les
  changements de couleur ni d’accessibilité (le scaffold l’alimente automatiquement) ; le tiroir passe à une
  durée nulle ;
- **Vos animations** : lisez-le vous-même et mettez la durée à 0 :

```csharp
bool reduced = Native.Get<IMotionPreferencesCapability>()?.ReducedMotion == true;
Tween.Run(ui, reduced ? 0 : 320, Motion.EaseOutCubic, apply);
```

Avec une durée de 0, `Tween.Run` applique directement l’état final.

## 10. Courbes d’accélération (easings)

### 10.1 Intégrées (`NavEasing`)

| Valeur | Formule (`t` de 0 à 1) | Usage |
|---|---|---|
| `Linear` | `t` | Fondus d’opacité, rotation constante |
| `Standard` | `t²(3 − 2t)` (*smoothstep*) | Défaut des pistes de navigation |
| `Emphasized` | `4t³` pour `t < ½`, sinon `1 − (−2t + 2)³ / 2` | Grands déplacements |
| `EaseIn` | `t²` | Sorties (accélère) |
| `EaseOut` | `1 − (1 − t)²` | Entrées (ralentit) |

### 10.2 À vous (table échantillonnée)

```csharp
// Dépassement léger (overshoot)
static float Back(float t) { const float s = 1.70158f; t -= 1; return t * t * ((s + 1) * t + s) + 1; }

NavTransition.Create()
    .Enter(x => x.Animate(NavAnimatedProperty.ScaleX, 0.6f, 1, Back)
                 .Animate(NavAnimatedProperty.ScaleY, 0.6f, 1, Back))
    .Duration(320).Build();
```

La fonction est évaluée en 129 points par défaut (`samples`, jusqu’à 4096) **lors de `Build()`** ; elle doit
renvoyer des valeurs finies. À l’exécution, une interpolation linéaire entre échantillons est lue, sans appel
à votre code. Une valeur hors de [0, 1] (dépassement) est acceptée.

## 11. Recettes

### 11.1 Pile de pages qui glisse avec parallaxe

```csharp
public static readonly NavTransition Parallax = NavTransition.Create()
    .Enter(x => x.TranslateX(1, 0, NavEasing.Emphasized))
    .Exit(x => x.TranslateX(0, -0.3f, NavEasing.Emphasized).Opacity(1, 0.7f))
    .Duration(320).Build();
```

### 11.2 Feuille plus lente, avec fondu

Le défaut d’un `BottomSheet` est `SlideUp(320)` avec l’easing `Emphasized` à l’entrée et `EaseIn` à la sortie.
Pour une entrée plus longue qui s’estompe :

```csharp
BottomSheet.Show(Application, h => MonContenu(h.Dismiss), new BottomSheetOptions
{
    Animation = OverlayAnimation.SlideUp(420).WithFade()
        .WithEasing(NavEasing.Emphasized, NavEasing.EaseIn)
});
```

### 11.3 Toast qui se contracte

```csharp
Snackbar.Show(Application, "Envoyé", new SnackbarOptions
{
    Animation = OverlayAnimation.Scale(220, from: 0.7f),
    Duration = TimeSpan.FromSeconds(2)
});
```

### 11.4 Onglet qui « gonfle »

```csharp
BottomBarSelectionTransition = NavigationSelectionTransition.Create()
    .Icon(x => x.Scale(0.9f, 1.1f).ForegroundColor(0xFF98A2BD, 0xFF6D28D9))
    .Label(x => x.Opacity(0.5f, 1))
    .Duration(220)
    .Easing(t => 1 - MathF.Exp(-7 * t) * MathF.Cos(9 * t))
    .Build()
```

### 11.5 Pulsation d’un point d’état

```csharp
private readonly State<uint> _dot = UseState(0xFF22C55E);
private CancellationTokenSource? _pulse;

private void Pulse()
{
    _pulse?.Cancel();
    var ui = Native.Require<IUiDispatcherCapability>();
    _pulse = Tween.Run(ui, 600, t => MathF.Sin(t * MathF.PI), s =>
        _dot.Value = Motion.Lerp(0xFF22C55E, 0xFFBBF7D0, s));
}
```

La fonction d’easing sert ici de forme d’onde (0 → 1 → 0) ; l’animation s’arrête d’elle-même.

## 12. Performance et diagnostic

- **Construisez une fois** : placez les `NavTransition`, `NavigationSelectionTransition` et
  `OverlayAnimation` dans des champs `static readonly` ; ne les reconstruisez pas à chaque navigation (le
  coût de compilation est « à froid », mais il alloue).
- **L’opacité de scène** est rendue par une couche (`SaveLayer`) tant qu’elle est inférieure à 1 : sur de
  grandes scènes, préférez `Opacity` sur la seule scène entrante, ou des durées courtes.
- **Rognage et rayon** (`Clip*`, `CornerRadius`) découpent à chaque frame : peu coûteux, mais évitez de les
  cumuler avec de très grosses ombres.
- **Minuteurs d’application** : un pas = un `Post` + une invalidation. Les couleurs (`Background`,
  `TextColor`) ne relancent pas de layout ; les largeurs (`LinearProgress`, `FlexGrow`) si.
- **Un seul minuteur par propriété** : annulez l’ancien avant d’en créer un nouveau.
- **Mesurer** : sur appareil, Release, sans débogueur ; la baseline de rendu est dans
  [Benchmark A15](runtime-benchmark-a15.md) (mesures Mono de .NET 10, non rejouées sous CoreCLR).
- **Interaction pendant le mouvement** : n’attendez pas qu’un élément en cours d’animation soit un point
  d’appui fiable ; pour les éléments de navigation, les animations de sélection ne déplacent pas la cible
  tactile d’une destination.

## 13. Limites actuelles

- Pas de modificateur d’animation générique, pas d’opacité/translation/échelle par entité, pas de ressorts
  physiques, pas de *shared element transition* entre pages.
- `NavAnimatedProperty.Elevation` n’a pas d’effet visuel sur les scènes de page dans cette version ; la
  propriété `Elevation` des transitions de **sélection** fonctionne.
- Les animations de minuteur d’application ([§5](#5-animer-soi-même-pas-à-pas)) ne sont pas calées sur le
  rafraîchissement d’écran ; leur régularité dépend du planificateur du système.
- Les translations des overlays `Custom` sont en dp, celles des pages en fraction d’écran : ne mélangez pas les
  deux unités en réutilisant une même `NavTransition` pour les deux usages sans la vérifier.
- Le framework est pré-V1 : ces API sont décrites d’après le code et leurs tests, pas encore comme contrat
  stable (voir [Décisions et trajectoire V1](architecture/decisions-v1.md#3-dette-technique-et-risques)).
