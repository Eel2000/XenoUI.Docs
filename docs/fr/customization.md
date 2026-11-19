# Personnaliser un composant

[Documentation](index.md) · [Guide développeur](developer-guide.md) · [Créer un composant](components.md) · [Animations](animations.md)

Ce guide couvre tout ce qui permet d’adapter l’apparence et le comportement d’un composant XenoUI :
modificateurs, paramètres, objets d’options, thèmes, formes, ombres, polices, icônes, variantes, droite à
gauche, et la manière de rendre **vos** composants personnalisables par les autres. Il suppose la lecture de
[Créer un composant](components.md) pour le vocabulaire (`XenoComponent`, `State<T>`, `Body()`).

## Sommaire

1. [Les six niveaux de personnalisation](#1-les-six-niveaux-de-personnalisation)
2. [Niveau 1 : les modificateurs](#2-niveau-1--les-modificateurs)
3. [Ce que les modificateurs ne peuvent pas faire](#3-ce-que-les-modificateurs-ne-peuvent-pas-faire)
4. [Niveau 2 et 3 : paramètres et objets d’options](#4-niveau-2-et-3--paramètres-et-objets-doptions)
5. [Formes, ombres et élévation](#5-formes-ombres-et-élévation)
6. [Couleurs, thème et mode sombre](#6-couleurs-thème-et-mode-sombre)
7. [Typographie, polices et icônes](#7-typographie-polices-et-icônes)
8. [Variantes et tailles](#8-variantes-et-tailles)
9. [Niveau 4 : envelopper un composant standard](#9-niveau-4--envelopper-un-composant-standard)
10. [Niveau 5 : composition complète (slots et overlays)](#10-niveau-5--composition-complète-slots-et-overlays)
11. [Niveau 6 : écrire son propre composant](#11-niveau-6--écrire-son-propre-composant)
12. [Personnaliser la navigation et la barre du haut](#12-personnaliser-la-navigation-et-la-barre-du-haut)
13. [États visuels : pressé, désactivé, sélectionné](#13-états-visuels--pressé-désactivé-sélectionné)
14. [Droite à gauche et adaptation à l’écran](#14-droite-à-gauche-et-adaptation-à-lécran)
15. [Concevoir un composant facile à personnaliser](#15-concevoir-un-composant-facile-à-personnaliser)
16. [Référence rapide des modificateurs](#16-référence-rapide-des-modificateurs)

---

## 1. Les six niveaux de personnalisation

Du plus simple au plus profond ; choisissez le premier niveau qui suffit.

| Niveau | Outil | Exemple | Quand |
|---|---|---|---|
| 1 | Modificateurs chaînés | `Button("OK", ok).Background(0xFF16A34A).CornerRadius(16)` | Changer couleur, taille, marge d’une instance |
| 2 | Paramètres du composant | `Button("OK", ok, fontSize: 18, textColor: 0xFF000000)` | Ce qui touche le texte interne |
| 3 | Objet d’options | `new SnackbarOptions { Background = …, Icon = … }` | Beaucoup de réglages, valeurs par défaut |
| 4 | Enveloppe | `Primary("OK", ok)` qui appelle `Button(...)` | Un style d’application réutilisé partout |
| 5 | Slots / overlay sur mesure | `Snackbar.Show(app, h => MonContenu(h), options)` | Remplacer le contenu, garder le comportement |
| 6 | Composant à vous | `static class Chip { View(...) }` | Aucun composant existant ne convient |

## 2. Niveau 1 : les modificateurs

Un composant standard retourne un `XenoComponent` « normal » : chaque modificateur de l’API s’y applique et
**remplace** la valeur par défaut posée par le composant.

```csharp
// Defaults de Button : fond 0xFF6200EE, rayon 8, élévation 2, padding 20/12, texte blanc 16.
Button("Enregistrer", Save)
    .Background(0xFF16A34A)      // remplace le violet
    .CornerRadius(16)            // remplace 8
    .Padding(28, 16)             // remplace 20/12
    .Elevation(6)                // remplace 2
    .Margin(Spacing.Only(top: 12))
    .FlexGrow(1);                // remplit la largeur d’une Row
```

Ce que chaque famille de modificateurs recompose :

| Famille | Modificateurs | Invalide |
|---|---|---|
| Mise en page | `Width`, `Height`, `Size`, `Padding`, `Margin`, `Gap`, `FlexGrow`, `FlexShrink`, `MainAxis`, `CrossAxis`, `AlignSelf`, `CenterContent`, `FlowDirection` | Mesure + placement |
| Visuel | `Background`, `CornerRadius`, `Shape`, `Elevation`, `Shadow` | Dessin seul |
| Texte | `TextColor`, `FontSize`, `FontFamily`, `TextAlign`, `TextWrap`, `MaxLines`, `AutoSize` | Mesure (taille, police) ou dessin (couleur) |
| Image | `ScaleMode` | Dessin |
| Interaction | `Clickable`, `Scrollable` | Aucun re-layout |

Un changement purement visuel (couleur, ombre, forme) ne relance pas le layout : il est peu coûteux, y compris
lié à un `State` (voir [§13](#13-états-visuels--pressé-désactivé-sélectionné)).

### Ordre et écrasement

Les modificateurs écrivent dans l’entité à l’instant de l’appel :

```csharp
Box(100, 40).Background(0xFFFF0000).Background(0xFF0000FF);   // bleu : le dernier gagne
Box(100, 40).CornerRadius(12).Shape(myShape);                  // CornerRadius ignoré : le shape prime
Box(100, 40).Elevation(8).Shadow(0, 2, 4);                     // l’ombre manuelle remplace l’élévation
Box(100, 40).Clickable(a).Clickable(b);                        // b remplace a (a est libéré)
```

`Padding(horizontal, vertical)` et `Padding(Spacing)` remplacent **les quatre côtés** : pour modifier un seul
côté, passez par `Spacing.Only(...)` en rappelant les autres valeurs.

## 3. Ce que les modificateurs ne peuvent pas faire

### 3.1 Ce qu’un modificateur ne peut pas atteindre

Un modificateur agit sur **l’entité à laquelle il est appliqué**, pas sur ses enfants. Il n’y a pas
d’héritage de style :

```csharp
Button("OK", ok).TextColor(0xFFFF0000)   // ne colore PAS le libellé : TextColor vise le ZStack,
                                          // le Text est un enfant
Button("OK", ok, textColor: 0xFFFF0000)  // correct : paramètre du composant
```

Conséquences pour qui personnalise :

- le **texte interne** d’un composant se règle par ses **paramètres** (`fontSize`, `textColor`, options) ;
- si un composant n’expose pas ce paramètre, utilisez le niveau 4 ou 5 ou 6 (reconstruire le contenu).

Conséquences pour qui écrit un composant : exposez tout ce qui touche un texte ou une icône interne
(couleur, taille, police). Voir [§15](#15-concevoir-un-composant-facile-à-personnaliser).

### 3.2 Pas de sélecteurs ni de feuille de style

Il n’existe ni CSS, ni classes, ni thème global implicite : la personnalisation est **explicite** et vit dans
votre code C#. C’est volontaire (pas d’allocation, pas de résolution à l’exécution) ; le thème est une classe
à vous (voir [§6](#6-couleurs-thème-et-mode-sombre)).

## 4. Niveau 2 et 3 : paramètres et objets d’options

### 4.1 Paramètres

Les paramètres facultatifs à valeur par défaut sont le moyen le plus lisible :

```csharp
TextButton("Annuler", Cancel, color: 0xFFDC2626, fontSize: 15)
Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), size: 32).TextColor(0xFF111827)
```

### 4.2 Objets d’options

Les overlays et la barre du haut se règlent par un objet dont chaque propriété a une valeur par défaut :

```csharp
Snackbar.Show(Application, "Message supprimé", new SnackbarOptions
{
    ActionLabel = "Annuler",
    OnAction = Restore,
    Icon = Glyph("", "Font Awesome 5 Free-Solid-900"),
    Background = 0xFF1F2937,
    TextColor = 0xFFF9FAFB,
    ActionColor = 0xFF93C5FD,
    CornerRadius = 12,
    Elevation = 8,
    Margin = 20,
    Placement = OverlayPlacement.Top,
    TopInset = 24,                       // sous la barre d’état
    Duration = TimeSpan.FromSeconds(6),  // null : reste jusqu’au retrait
    Animation = OverlayAnimation.Scale(200),
    Closed = () => { }
});
```

Propriétés de `SnackbarOptions` : `Duration`, `ActionLabel`, `OnAction`, `DismissOnAction`, `Icon`, `IconColor`,
`Background`, `TextColor`, `ActionColor`, `FontSize`, `CornerRadius`, `Elevation`, `Margin`, `MaxWidth`,
`TopInset`, `PaddingX`, `PaddingY`, `Placement`, `AvoidNavigationBar`, `Animation`, `SwipeToDismiss`,
`Closed`. Les autres overlays suivent le même patron : voir [Overlays et TopBar](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md).

`TopBarOptions` (barre d’application) : `Subtitle`, `NavigationIcon`, `OnNavigation`, `Actions`,
`CenterTitle`, `Background`, `Foreground`, `SubtitleColor`, `Height`, `TopInset`, `Elevation`, `TitleSize`,
`SubtitleSize`, `IconSize`, `CornerRadius`, `TitleContent` (remplace titre et sous-titre par n’importe quel
contenu).

### 4.3 Dériver des options (`with`)

Quand l’objet est un `record` (ex. `NavigationAction`, `DockedNavigationBarStyle`), dérivez-le sans muter
l’original :

```csharp
DockedNavigationBarStyle.Default with { /* propriétés à changer */ }
```

Pour vos propres classes d’options, exposez des instances nommées partagées (`ChipOptions.Success`,
`ChipOptions.Danger`) construites une fois (immuables en pratique) et passez-les aux composants.

## 5. Formes, ombres et élévation

### 5.1 Rayon

`CornerRadius(r)` arrondit le fond, l’ombre, le clip des enfants et la zone tactile. Un rayon égal à la moitié
du plus petit côté donne une pastille ; pour un cercle, `Size(d, d).CornerRadius(d / 2)`.

### 5.2 Élévation

`Elevation(dp)` pose une ombre Material calculée depuis `dp` (plage conseillée 0–24) :

| Paramètre d’ombre | Valeur |
|---|---|
| Décalage X | 0 |
| Décalage Y | `dp × 0,5` |
| Flou (sigma) | `dp × 0,8` |
| Couleur | Noir, alpha = `0,10 + 0,012 × dp`, plafonné à 0,5 |

`Elevation(0)` ou l’absence d’appel : pas d’ombre (le moteur saute l’ombre quand le flou est nul).

### 5.3 Ombre manuelle

```csharp
Box(120, 80).Background(0xFFFFFFFF).CornerRadius(16)
    .Shadow(offsetX: 0, offsetY: 8, blur: 16, color: 0x60000000);
```

`Shadow` met l’élévation à 0 et remplace tout réglage d’`Elevation`. La couleur par défaut est un noir à 25 %.
L’ombre peut être colorée (`0x556D28D9` pour une lueur violette). Elle suit la forme (rayon ou `Shape`).

### 5.4 Formes libres : `XenoShape`

`Shape(XenoShape?)` remplace le rectangle arrondi par un contour quelconque pour **le fond, l’ombre, le clip
des enfants et la zone tactile**. Le contour est construit en unités logiques, origine en haut à gauche de
l’entité, et **seulement quand la taille change** ; une instance peut être partagée par des entités de tailles
différentes.

```csharp
using SkiaSharp;

// Étiquette : coin supérieur droit coupé
static readonly XenoShape Tag = XenoShape.Create((path, w, h) =>
{
    float cut = Math.Min(16, Math.Min(w, h) / 2);
    path.MoveTo(0, 0);
    path.LineTo(w - cut, 0);
    path.LineTo(w, cut);
    path.LineTo(w, h);
    path.LineTo(0, h);
    path.Close();
});

Text("NOUVEAU", 11).TextColor(0xFFFFFFFF).Padding(14, 8)
    .Background(0xFFDC2626).Shape(Tag).Elevation(2);
```

Sous-classe pour une forme paramétrée et testable :

```csharp
public sealed class RoundedTriangle : XenoShape
{
    protected override void BuildPath(SKPath path, float width, float height)
    {
        path.MoveTo(width / 2, 0);
        path.LineTo(width, height);
        path.LineTo(0, height);
        path.Close();
    }
}
```

Intégré : `XenoShapes.TopNotch(notchRadius, notchCenterY, notchCenterX, shoulderRadius, topCornerRadius,
bottomCornerRadius)` — barre avec une encoche circulaire pour un bouton flottant, utilisée par la
navigation à action centrale ([détails](navigation-scaffold.md#formes-personnalisées)).

Règles : `CornerRadius` est ignoré sur une entité qui a un shape ; `Shape(null)` restaure le rectangle
arrondi ; le contour sert aussi de clip aux enfants et de zone tactile.

## 6. Couleurs, thème et mode sombre

### 6.1 Format

`uint` ARGB : `0xAARRGGBB`. `0xFF6D28D9` opaque ; `0x336D28D9` ≈ 20 % ; `0x00000000` transparent. Il n’y a
pas de modificateur d’opacité d’entité : on fait varier l’alpha de la couleur (voir
[Animations](animations.md#4-opacité-et-fondu-dun-composant)).

### 6.2 Un thème est une classe à vous

L’application d’exemple centralise sa palette (`Design/Theme.cs`) :

```csharp
internal static class Theme
{
    internal const uint Canvas  = 0xFF0A0A0F;
    internal const uint Surface = 0xFF15151D;
    internal const uint Ink     = 0xFFF4F4F8;
    internal const uint Muted   = 0xFF8D8DA1;
    internal const uint Accent  = 0xFFFE2C55;
    internal const string Fa    = "Font Awesome 5 Free-Solid-900";
    internal static IconDef Glyph(string glyph) => Compose.Glyph(glyph, Fa);
}
```

et des **briques d’application** (`Design/Ui.cs`) qui appliquent ce thème aux composants standard :

```csharp
internal static XenoComponent Primary(string label, Action tap, float size = 14) =>
    Button(label, tap, size).Background(Theme.Accent).CornerRadius(14).Padding(16, 13);

internal static XenoComponent Card(params XenoComponent[] children) =>
    Column(children).Gap(10).Padding(16).Background(Theme.Surface).CornerRadius(20)
        .CrossAxis(CrossAxisAlignment.Stretch);
```

Les pages n’appellent plus `Button(...)` directement mais `Ui.Primary(...)` : changer le style d’un bouton dans
toute l’application = modifier une ligne. C’est le niveau 4.

### 6.3 Clair / sombre, ou plusieurs thèmes à l’exécution

Des `const` ne changent pas à l’exécution. Pour un thème dynamique, faites de la palette un **objet** :

```csharp
public sealed record Palette(uint Canvas, uint Surface, uint Ink, uint Muted, uint Accent)
{
    public static readonly Palette Dark  = new(0xFF0A0A0F, 0xFF15151D, 0xFFF4F4F8, 0xFF8D8DA1, 0xFFFE2C55);
    public static readonly Palette Light = new(0xFFFAFAFB, 0xFFFFFFFF, 0xFF111827, 0xFF6B7280, 0xFFFE2C55);
}

public static class Theme
{
    public static Palette Current { get; private set; } = Palette.Dark;
    public static void Use(Palette palette) => Current = palette;
}
```

Les composants lisent `Theme.Current.Surface` **à la composition** ; changer de thème exige donc de
recomposer :

```csharp
Theme.Use(Palette.Light);
Rebuild();                 // la page courante
// Les destinations déjà montées du scaffold et ses slots (barre, rail, tiroir) :
// _navigation.RefreshChrome();  (voir Navigation et NavigationScaffold)
```

Les destinations d’un scaffold conservent leur arbre une fois montées : pour qu’elles prennent le nouveau
thème, reconstruisez-les (le `RefreshChrome()` du contexte de slot reconstruit le chrome ; les pages se
reconstruisent par leur `Rebuild()`).

Pour un changement de couleur **sans reconstruction**, liez la couleur à un `State<uint>` :

```csharp
private readonly State<uint> _surface = UseState(Palette.Dark.Surface);
Column(...).Background(_surface);
// plus tard :
_surface.Value = Palette.Light.Surface;
```

C’est adapté à quelques surfaces clés ; au-delà, la reconstruction est plus simple.

### 6.4 Contraste

Vérifiez les paires texte/fond (≥ 4,5:1 en corps de texte, ≥ 3:1 pour les gros textes et les icônes). Une
couleur translucide sur un fond variable change de contraste : testez sur les deux thèmes.

## 7. Typographie, polices et icônes

### 7.1 Taille et mise en forme

`Text(valeur, taille)` ; puis `FontSize`, `TextAlign`, `TextWrap(false)` pour une ligne unique, `MaxLines(n,
ellipsis: true)`, `AutoSize(minimumFontSize)` pour réduire la police jusqu’à un minimum plutôt que de couper.
`AutoSize` exige `minimumFontSize ≤ taille` (sinon `ArgumentOutOfRangeException`) et ne descend jamais sous ce
minimum.

### 7.2 Polices personnalisées

Déposez le fichier dans `Assets/Fonts/` (dans le projet de l’application) ; il est chargé au démarrage et se
désigne par son nom :

```csharp
Text("Titre", 24).FontFamily("Roboto-Bold")
```

Le nom est celui du fichier sans extension (l’exemple utilise `Font Awesome 5 Free-Solid-900`). Un nom inconnu
retombe sur la police système. Une même `FontFamily` s’applique aux icônes (qui sont du texte).

### 7.3 Icônes

```csharp
Icon(Glyph("", "Font Awesome 5 Free-Solid-900"), 20).TextColor(0xFFE11D48)
```

- Le glyphe est un caractère Unicode de la police d’icônes (`""`).
- Changer la couleur : `TextColor`. Changer la taille : deuxième argument de `Icon` ou `FontSize`.
- Regroupez glyphe et police dans un `IconDef` (`Glyph(...)`) : les overlays, la barre du haut et la
  navigation attendent un `IconDef`.
- Plusieurs polices d’icônes cohabitent (`Fa`, `FaRegular`, `FaBrands` dans l’exemple) : chaque `IconDef`
  porte la sienne.

## 8. Variantes et tailles

Modélisez les variantes par une **énumération** plutôt que par des booléens multiples :

```csharp
public enum ButtonKind { Primary, Secondary, Danger, Ghost }
public enum ButtonSize { Small, Medium, Large }

public static class Btn
{
    public static XenoComponent View(string label, Action tap,
        ButtonKind kind = ButtonKind.Primary, ButtonSize size = ButtonSize.Medium)
    {
        (uint bg, uint fg) = kind switch
        {
            ButtonKind.Primary   => (Theme.Accent, 0xFFFFFFFF),
            ButtonKind.Secondary => (Theme.SurfaceHigh, Theme.Ink),
            ButtonKind.Danger    => (0xFFDC2626, 0xFFFFFFFF),
            _                    => (0x00000000, Theme.Accent)       // Ghost : fond transparent
        };
        (float font, float px, float py) = size switch
        {
            ButtonSize.Small => (13f, 14f, 8f),
            ButtonSize.Large => (17f, 28f, 16f),
            _                => (15f, 20f, 12f)
        };
        return Button(label, tap, font, fg).Background(bg).CornerRadius(py + 4).Padding(px, py);
    }
}
```

Note sur `Ghost` : un fond **transparent** n’a pas de teinte au toucher. Donnez-lui un fond translucide
(`0x1AFE2C55`) si vous voulez un retour visuel.

## 9. Niveau 4 : envelopper un composant standard

Envelopper garde le comportement du composant (tap, teinte au toucher, centrage) et impose votre style :

```csharp
public static XenoComponent Cta(string label, Action tap) =>
    Button(label, tap, fontSize: 16, textColor: 0xFFFFFFFF)
        .Background(Theme.Current.Accent)
        .CornerRadius(28)
        .Padding(32, 16)
        .Elevation(6);
```

Le résultat est un `XenoComponent` : l’appelant peut encore chaîner `.FlexGrow(1)`, `.Margin(…)`. **Ne figez
pas** ce que l’appelant voudra ajuster (largeur, marge) : laissez-le aux modificateurs d’appel.

`Widgets` est une classe `partial` de la bibliothèque standard : vous ne pouvez pas y *redéfinir* `Button`
(conflit de nom). Donnez à vos enveloppes un autre nom (`Primary`, `Cta`) ou placez-les dans votre classe
(`Ui`).

## 10. Niveau 5 : composition complète (slots et overlays)

Les overlays offrent deux surcharges : le contenu par défaut, et le **contenu fourni par vous** qui garde
placement, timing, animation, voile et gestes :

```csharp
// Contenu par défaut
Snackbar.Show(app, "Enregistré");

// Contenu à vous, comportement du snackbar
Snackbar.Show(app, handle =>
    Row(
        Icon(Glyph("", Theme.Fa), 18).TextColor(0xFF22C55E),
        Text("Synchronisé", 14).TextColor(0xFFFFFFFF).FlexGrow(1),
        TextButton("Fermer", handle.Dismiss, 0xFF93C5FD)
    ).Gap(10).CrossAxis(CrossAxisAlignment.Center)
     .Padding(16, 12).Background(0xFF111827).CornerRadius(14).Elevation(8),
    new SnackbarOptions { Duration = TimeSpan.FromSeconds(3) });

// Overlay entièrement libre
Application.Overlays.Show(h => MaCarte(h.Dismiss), new OverlayOptions
{
    Placement = OverlayPlacement.Center, Modal = true, ScrimColor = 0x99000000,
    Animation = OverlayAnimation.Scale(220).WithFade(),
    SwipeToDismiss = OverlaySwipe.Down, Closed = () => { }
});
```

Chaque overlay expose aussi son visuel seul (`Snackbar.Body`, `Dialog.Surface`, `Popup.Surface`,
`BottomSheet.Surface`, `Alert.Content`, `Popup.Menu`) pour l’aperçu ou la composition ailleurs.

## 11. Niveau 6 : écrire son propre composant

Quand rien ne convient, [écrivez un composant](components.md). Le gabarit de personnalisabilité :

```csharp
public static class Banner
{
    public static XenoComponent View(string message, BannerOptions? options = null)
    {
        options ??= new BannerOptions();
        return Row(
                options.Icon is { } icon ? Icon(icon, 20).TextColor(options.Foreground) : XenoComponent.None,
                Text(message, options.FontSize).TextColor(options.Foreground).FlexGrow(1).FlexShrink(1))
            .Gap(10).CrossAxis(CrossAxisAlignment.Center)
            .Padding(options.PaddingX, options.PaddingY)
            .Background(options.Background).CornerRadius(options.CornerRadius);
    }
}
```

La racine porte des valeurs par défaut ; l’appelant ajuste le reste par modificateurs (`Banner.View(...)
.Margin(16)`), le texte par `BannerOptions`.

## 12. Personnaliser la navigation et la barre du haut

- **Barre du haut** : `TopBarOptions` (voir [§4.2](#42-objets-doptions)) fournie par la page qui implémente
  `ITopBarPage` ; le scaffold la monte via `TopBars.ForActivePage`. `TitleContent` remplace titre et
  sous-titre. `RebuildTopBar()` la reconstruit quand l’état qu’elle lit change.
- **Barre d’onglets, rail, tiroir** : slots de présentation du scaffold (`NavigationPresentationContext`),
  `DockedNavigationBarStyle`, formes (`XenoShapes.TopNotch`), action centrale et FAB. Voir
  [Navigation et NavigationScaffold](navigation-scaffold.md#slots-de-présentation-et-fab).
- **Changement de sélection d’onglet** : `NavigationSelectionTransition` (icône, libellé, indicateur, surface,
  badge) — voir [Animations §7](animations.md#7-animer-la-sélection-dans-la-navigation).
- **Transitions de pages** : `NavTransition` — voir [Animations §6](animations.md#6-transitions-de-pages--navtransition).

## 13. États visuels : pressé, désactivé, sélectionné

**Pressé.** Automatique pour toute entité cliquable à fond non transparent : teinte noire de 15 % (calcul
dans le moteur, sans layout). Pour changer l’apparence au toucher au-delà de la teinte, pilotez la couleur par
un `State` dans le callback.

**Sélectionné / actif.** Passez l’état en paramètre et choisissez les couleurs à la composition (voir la barre
de segments dans [Créer un composant §15.4](components.md#154-barre-de-segments)), ou liez les couleurs à un
`State<uint>` pour éviter la reconstruction :

```csharp
public static XenoComponent Tab(string label, State<bool> selected, Action tap) { … }
```

**Désactivé.** Il n’y a pas de modificateur `Disabled` : ne posez simplement pas `Clickable` et estompez les
couleurs (alpha réduit) :

```csharp
public static XenoComponent ActionButton(string label, bool enabled, Action tap)
{
    var surface = ZStack(
            Text(label, 16).TextColor(enabled ? 0xFFFFFFFF : 0x99FFFFFF)
                .AlignSelf(CrossAxisAlignment.Center))
        .Background(enabled ? 0xFF6D28D9 : 0x666D28D9)
        .CornerRadius(8).Padding(20, 12).CenterContent();
    return enabled ? surface.Clickable(tap) : surface;   // désactivé : aucun callback, aucune teinte
}
```

`Button` pose toujours son `Clickable` : pour une version réellement inerte, recomposez le même arbre comme
ci-dessus, ou ignorez l’action dans le callback quand `enabled` est faux.

**Erreur / succès.** Variez la couleur de bordure/fond/texte et le message ; ne reposez pas sur la couleur
seule (icône ou texte en plus).

## 14. Droite à gauche et adaptation à l’écran

- **RTL** : `FlowDirection(RightToLeft)` sur un sous-arbre, ou direction globale de l’application. Les `Row`
  se mettent en miroir, le côté gauche du padding/margin est le côté « début ». Utilisez
  `Spacing.Directional(start, top, end, bottom)`.
- **Taille d’écran** : tout est logique (dp). Utilisez `FlexGrow` et `Stretch` ; des `Width` fixes en dp
  conviennent aux éléments d’identité (avatar, icône), pas aux colonnes de contenu.
- **Tablette / grand écran** : les overlays acceptent `FillWidth` + `MaxWidth` pour rester centrés et bornés ;
  le scaffold bascule bas / rail / tiroir permanent ([Navigation](navigation-scaffold.md)).
- **Zone sûre** : `NativeWindowMetrics.SafeAreaTop` (encoche iOS) et `TopInset` des options ; sur Android la
  fenêtre démarre sous la barre d’état.

## 15. Concevoir un composant facile à personnaliser

1. **Valeurs par défaut sur la racine, surchargeables.** Posez `Background`, `CornerRadius`, `Padding` sur la
   racine retournée ; l’appelant les remplace par modificateur.
2. **Exposez le texte interne.** Couleur, taille, police, nombre de lignes : paramètres ou options.
3. **Un `IconDef` plutôt que deux chaînes** pour les icônes ; `XenoComponent.None` pour « pas d’icône ».
4. **Ne fixez pas ce que l’appelant dimensionne** (largeur, marge, `FlexGrow`).
5. **Ne mutez rien d’extérieur** : tout passe par paramètres et callbacks.
6. **Offrez un slot** (`XenoComponent` ou `Func`) quand le contenu varie vraiment (en-tête, pied, action).
7. **Séparez visuel et comportement** : une méthode qui ne fait que composer (`Body`, `Surface`), une autre qui
   l’affiche ou l’active (`Show`).
8. **Prévisualisez les variantes** : défaut, personnalisé, RTL, long texte, thème clair et sombre.
9. **Documentez** les valeurs par défaut dans le commentaire XML (le code de la bibliothèque le fait).

## 16. Référence rapide des modificateurs

| Modificateur | Paramètres | Notes |
|---|---|---|
| `Width(w)` / `Height(h)` / `Size(w, h)` | dp | 0 = auto |
| `Padding(all)` / `(h, v)` / `(Spacing)` | dp | Remplace les quatre côtés |
| `Margin(all)` / `(Spacing)` | dp | |
| `Gap(g)` | dp | Entre enfants (non appliqué dans un `ZStack`) |
| `MainAxis(a)` | `Start/Center/End/SpaceBetween/SpaceAround/SpaceEvenly` | Dans un `ZStack` : position horizontale |
| `CrossAxis(a)` | `Start/Center/End/Stretch` | |
| `AlignSelf(a)` | idem | Un enfant ; dans un `ZStack` : position verticale |
| `CenterContent()` | | Centre sur les deux axes |
| `FlexGrow(f)` / `FlexShrink(f)` | ≥ 0 | `FlexShrink` rejette les valeurs négatives ou non finies |
| `FlowDirection(d)` | `Inherit/LeftToRight/RightToLeft` | Sous-arbre |
| `Background(uint)` / `(State<uint>)` | ARGB | |
| `CornerRadius(r)` | dp | Ignoré si `Shape` |
| `Shape(XenoShape?)` | | `null` restaure le rayon |
| `Elevation(dp)` | 0–24 | Ombre calculée |
| `Shadow(x, y, blur, color)` | dp, dp, sigma, ARGB | Remplace l’élévation |
| `TextColor(uint)` / `(State<uint>)` | ARGB | Entité porteuse du texte seulement |
| `FontSize(s)` | dp | |
| `FontFamily(nom)` | fichier de `Assets/Fonts` | |
| `TextAlign(a)` | `Start/Center/End` | |
| `TextWrap(bool)` | | Actif par défaut |
| `MaxLines(n, ellipsis)` | `n ≥ 0` | |
| `AutoSize(min)` | `0 < min ≤ taille` | |
| `ScaleMode(m)` | `Cover/Fit/Fill/None` | Images |
| `Clickable(Action)` | | Remplace le callback précédent |
| `Scrollable(ScrollState, horizontal?)` | | Axe par défaut selon le conteneur |
| `NativeVideo(...)` | `MediaHandle` ou `State` | Sortie vidéo native (backend de référence) |

`XenoComponent.None` accepte tous ces modificateurs et ne fait rien : vous pouvez chaîner sans tester.
