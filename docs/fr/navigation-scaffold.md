# Navigation et NavigationScaffold

[Accueil](https://github.com/Eel2000/XenoUI/blob/master/README.md) · [Index docs](index.md) · [Guide développeur](developer-guide.md) · [Architecture des scènes](architecture/navigation-and-scenes.md)

`NavigationScaffold` ajoute une navigation principale adaptative sans remplacer la pile historique. Une destination possède sa propre pile ; `Navigate<TPage>()` pousse toujours dans la pile sélectionnée. Les instances de `XenoPage`, `ScrollState`, `LazyListState` et `TextEditController` restent persistantes.

Les pages `XenoPage<TArguments>` acceptent des [paramètres typés par entrée de pile](text-layout-navigation.md#paramètres-typés-de-navigation) via `Navigate<TPage, TArguments>(arguments, options)`. La BottomBar réserve par défaut sa hauteur réelle, marges comprises, afin que le dernier contenu reste accessible ; `ContentOverlaysNavigationBar = true` permet un overlay explicite.

## Configuration minimale

```csharp
public sealed class App : XenoApp
{
    private const string Font = "Font Awesome 5 Free-Solid-900";

    private readonly NavigationScaffoldState _navigation = new(
        NavigationDestination.For<HomePage>("Home", Glyph("\uf015", Font)),
        NavigationDestination.For<SearchPage>("Search", Glyph("\uf002", Font)),
        NavigationDestination.For<ProfilePage>("Profile", Glyph("\uf007", Font)))
    {
        Mode = NavigationMode.Automatic,
        DestinationTransition = NavTransitions.FadeThrough(),
        ReselectionBehavior = NavigationReselectionBehavior.PreserveStack
    };

    public override void OnStart()
    {
        Page<HomePage>();
        Page<SearchPage>();
        Page<ProfilePage>();
        Page<DetailsPage>();
        UseNavigationScaffold(_navigation);
    }
}
```

Il n’est pas nécessaire d’appeler `Navigate<HomePage>()` : `InitialDestinationIndex` (0 par défaut) désigne la destination initiale. `Automatic` choisit BottomBar sous 600 dp, Rail de 600 à 839 dp et Drawer modal à partir de 840 dp. Changer de classe de fenêtre recompose uniquement le chrome et relayout les roots si leur viewport utile change ; aucune destination n’est recomposée.

Une destination est composée paresseusement lors de sa première sélection. Une fois montée, elle conserve exactement le même root ECS, ses handles, son scroll, sa fenêtre lazy et ses contrôleurs texte. Revenir sur une destination déjà visitée ne rappelle pas `Body()`.

## Transitions entre destinations

Les transitions principales sont indépendantes des transitions push/pop :

```csharp
private readonly NavigationScaffoldState _navigation = new(...)
{
    DestinationTransition = NavTransitions.FadeThrough()
};

// Cold path : les deux transitions sont déjà compilées avant la première frame.
_navigation.TransitionFromTo<HomePage, SearchPage>(
    forward: NavTransitions.SlideHorizontal(),
    backward: NavTransitions.SlideHorizontalBack());
```

Sans paire explicite, l’ordre des destinations détermine le sens : la transition configurée est utilisée vers l’avant et son inverse compilé vers l’arrière. Une sélection n’exécute aucun slot dans la boucle de frame.

## Navigation secondaire et Back

Les API existantes restent valides :

```csharp
Navigate<DetailsPage>();
GoBack();
bool consumed = TryGoBack();
```

Depuis une page, `Navigate<DetailsPage>()` pousse le détail dans la pile de la destination active. Android délègue au système uniquement lorsque `TryGoBack()` retourne `false`.

### Retour à la racine : conventions de plateforme

`RootBackBehavior` vaut `PlatformDefault`. Le host fournit la convention via `INavigationPlatformCapability` dans NativeTunnel, sans types Android/UIKit dans Core :

- Android : fermer le drawer modal, dépiler les détails, puis revenir à la destination initiale avant de déléguer la sortie au système.
- iOS : fermer le drawer ou dépiler les détails ; une racine ne déclenche pas de changement d'onglet ni de sortie simulée.
- Host sans capacité : comportement `Exit` (le framework retourne `false`, il ne ferme pas lui-même l'application).

Le clavier reste géré par l'IME natif avant le callback de navigation Android. Le retour vers la destination initiale restaure sa pile existante, y compris un détail encore ouvert, sans la vider ni recomposer sa page.

```csharp
private readonly NavigationScaffoldState _navigation = new(...)
{
    InitialDestinationIndex = 0,
    RootBackBehavior = NavigationRootBackBehavior.PlatformDefault
};
```

Les alternatives explicites sont `Exit`, `ReturnToInitialDestination` et `PreviousDestination`. Cette dernière conserve un historique MRU sans doublons, borné au nombre de destinations : Retour consomme cet historique sans y enregistrer son propre changement. Un changement d'onglet n'ajoute jamais une page dans une pile de détails. `CanGoBack` inclut la politique de racine, afin que le host Android enregistre son callback au bon moment.

Une sélection via le chrome **ou** `SelectDestination()` ferme le drawer modal avant d'activer la destination, y compris lors de la resélection. Les gestes horizontaux de bord et de fermeture sont disponibles (voir « Drawer : gestes et sélection différée »). Le predictive Back complet, le drawer permanent et le pont d'accessibilité de navigation ne sont pas encore implémentés. Le bouton hamburger ☰ est le déclencheur du drawer, pas une destination de navigation.

### Hamburger avec navigation par onglets

`EnableModalDrawer = true` permet d'ouvrir le drawer explicitement en mode BottomBar ou Rail. Depuis une `XenoPage`, relier le bouton ☰ à `OpenNavigationDrawer()` ; depuis l'application, la même méthode publique est disponible. Le panneau modal remplace temporairement le chrome interactif, recouvre le contenu et masque le FAB. La fermeture restaure la présentation adaptative ; la page et sa pile ne sont pas reconstruites. Les slots du chrome peuvent être recomposés à l'ouverture/fermeture, sur le cold path uniquement.

```csharp
private readonly NavigationScaffoldState _navigation = new(...)
{
    EnableModalDrawer = true,
    DrawerOpenTransition = DrawerTransitions.Open(DrawerSlideDirection.FromLeft),
    DrawerCloseTransition = DrawerTransitions.Close(DrawerSlideDirection.FromLeft)
};

// Dans Body() d'une page :
Box(44, 44, Icon(Glyph("\uf0c9", iconFont), 19))
    .CenterContent().Clickable(OpenNavigationDrawer);
```

Le SampleApp montre ce parcours dans les en-têtes Home, Gallery, People et Lab : ☰ → panneau « Xeno Studio » → sélection d'une destination ou fermeture par croix, tap extérieur ou Retour Android. Le drawer conserve les mêmes destinations que la BottomBar.

Validation du 5 octobre 2026 : 108/108 tests Core passent, dont un test pixel → hit-testing sur une destination du drawer. Le SampleApp Release compile sans avertissement ni erreur et a été déployé sur le Galaxy A15 : ouverture depuis Home, sélection de Gallery, réouverture depuis Gallery et fermeture par Retour confirmées. Le paquet final corrige le dégagement de l'en-tête sous la barre système ; ouverture et fermeture par tap extérieur ont également été vérifiées visuellement. Ce parcours n'est pas une validation physique iOS/tablette ni des gestes de bord.

Une opération peut remplacer la transition :

```csharp
Navigate<DetailsPage>(NavTransitions.SlideHorizontal());

// Aller, puis retour :
Navigate<DetailsPage>(NavTransitions.SlideVertical(320), NavTransitions.Fade(220));
```

La priorité est : option de l’opération, paire source/cible, page cible, `PlatformDefault`. `DestinationTransition` ne s’applique qu’aux changements BottomBar/Rail/Drawer. Une application peut définir les deux niveaux intermédiaires dans `OnStart()` :

```csharp
TransitionFor<DetailsPage>(NavTransitions.ScaleFade());
TransitionFromTo<HomePage, DetailsPage>(NavTransitions.SlideHorizontal());
```

## Transition personnalisée

Le builder s’exécute une fois sur le cold path et produit des tracks contigus. Aucune lambda n’est rappelée pendant les frames :

```csharp
var transition = NavTransition.Create()
    .Enter(scene => scene
        .TranslateX(1, 0, NavEasing.Emphasized)
        .Opacity(0.4f, 1))
    .Exit(scene => scene
        .TranslateX(0, -0.18f)
        .Opacity(1, 0.8f))
    .Duration(300)
    .Build();
```

Les propriétés animables V1 incluent translation, échelle, opacité, rotation, pivot, clip normalisé, rayon et élévation. Les presets utilisent le même pipeline public.

Une easing développeur est échantillonnée en LUT pendant `Build()` et n’est jamais rappelée par le hot path :

```csharp
.Enter(scene => scene.Animate(
    NavAnimatedProperty.TranslationX, 1, 0,
    easing: t => 1 - MathF.Pow(1 - t, 4),
    samples: 129))
```

## Slots de présentation et FAB

`BottomBar`, `NavigationRail` et `PermanentDrawer` remplacent entièrement la présentation correspondante. Le contexte fournit les destinations, la sélection, le mode, la classe de fenêtre, la page active et `Select(index)`. Le framework garde la propriété du comportement et des piles.

```csharp
BottomBar = context =>
{
    var icon = Icon(context.Destinations[0].Icon, 18);
    var label = Text(context.Destinations[0].Label, 11);
    var surface = Column(icon, label).Clickable(() => context.Select(0));

    // Enregistre une seule fois les entités que le runtime animera ensuite.
    return context.RegisterItem(0, surface, icon, label);
},
FloatingActionButton = context =>
{
    var fab = NavigationFab.Standard(Glyph("\uf067", Font),
        () => context.Select(1));
    return context.RegisterFab(fab, 0); // visible uniquement sur Home
}
```

`NavigationFab.Standard` et `NavigationFab.Extended` fournissent les deux présentations V1. `FabPlacement` choisit `BottomEnd` ou `BottomCenter`; un slot peut toujours remplacer entièrement le rendu.

`FabBottomBarGap` configure l'espace vertical entre la rangée du FAB et le sommet du slot BottomBar (16 dp par défaut, valeur finie et positive ou nulle). Le positionnement suit la hauteur réelle du slot, sans offset absolu ni calcul par frame. Les marges et paddings supérieurs d'un slot personnalisé s'ajoutent à cet espace ; pour une pilule flottante, garder son wrapper transparent et sans padding supérieur. Cette option ne modifie pas les modes Rail/Drawer.

```csharp
FabPlacement = FloatingActionButtonPlacement.BottomEnd,
FabBottomBarGap = 16
```

La transition de destination et celle de sélection sont indépendantes :

```csharp
DestinationTransition = NavTransitions.SlideHorizontal(),
BottomBarSelectionTransition = NavigationSelectionTransition.Create()
    .Surface(x => x
        .BackgroundColor(0x00000000, 0xFF29274D)
        .Scale(0.94f, 1)
        .CornerRadius(14, 22))
    .Icon(x => x
        .Scale(0.88f, 1)
        .ForegroundColor(0xFF98A2BD, 0xFFFFFFFF))
    .Label(x => x.Opacity(0.65f, 1))
    .Fab(x => x.Opacity(0, 1).Scale(0.8f, 1))
    .Duration(260)
    .Easing(NavEasing.Emphasized)
    .Build(),
RailSelectionTransition = NavigationSelectionTransitions.FadeScale(),
DrawerSelectionTransition = NavigationSelectionTransitions.SlideHighlight()
```

Les rôles enregistrables sont `surface`, `indicator`, `icon`, `label` et `badge`. Un indicateur mobile unique peut être enregistré avec `context.RegisterSharedIndicator(indicator)`, puis configuré avec `.SharedIndicator(x => x.MatchSelectedBounds())` : sa position et sa taille suivent les bounds arrangés des items, y compris en RTL et avec des items de tailles différentes. Les propriétés disponibles sont translation, échelle, opacité, rayon, élévation, couleur de fond et couleur de premier plan. Un easing développeur est compilé en LUT pendant `Build()`.

La résolution suit l’ordre paire source/destination, destination cible, mode BottomBar/Rail/Drawer, puis `SelectionTransition`. `ReducedMotion = true` applique immédiatement l’état final sans supprimer les changements de couleur ou d’accessibilité. Android (`animator_duration_scale`) et iOS (`UIAccessibility.IsReduceMotionEnabled`) alimentent automatiquement cette préférence via `NativeTunnel`.

Les slots sont appelés uniquement sur le cold path initial ou lors d’un changement structurel. Une modification de `SelectedIndex` conserve le même root, les mêmes générations ECS et les mêmes callbacks. Le hot path parcourt seulement des tracks contigus et écrit les valeurs dans les slabs. Une interruption repart des valeurs visuelles courantes. Les transforms de présentation ne modifient jamais les bounds de hit-testing ; un FAB masqué est aussi désactivé logiquement afin de ne créer aucune zone tactile invisible.

`ChromeTransition` reste disponible pour un véritable remplacement structurel, par exemple BottomBar → Rail → Drawer. Il n’est jamais utilisé lors d’une simple sélection.

## Action centrale, barre à encoche et RTL

### Action centrale (hors destination)

`CenterAction` ajoute à la BottomBar un bouton qui exécute une action (« créer », « scanner »…) au lieu de sélectionner une destination. Il occupe un emplacement de la rangée mais ne devient jamais `SelectedIndex`. Quand il est défini et qu’aucun slot `BottomBar` n’est fourni, la barre par défaut devient `NavigationBars.Docked` : le bouton dépasse au-dessus de la barre et se loge dans une encoche découpée dans celle-ci.

```csharp
private readonly NavigationScaffoldState _navigation = new(
    NavigationDestination.For<HomePage>("الرئيسية", home),
    NavigationDestination.For<CategoriesPage>("الأقسام", categories),
    NavigationDestination.For<FavoritesPage>("المفضلة", favorites),
    NavigationDestination.For<AccountPage>("حسابي", account))
{
    CenterAction = NavigationAction.For(Glyph("", Font), OpenComposer) with
    {
        Background = 0xFFF5B301, Size = 60
    },
    CenterActionPosition = -1, // milieu des onglets visibles (défaut)
    DockedBarStyle = DockedNavigationBarStyle.Default with
    {
        Background = 0xFFFFFFFF,
        SelectedColor = 0xFFF5B301,
        UnselectedColor = 0xFF9A9AA8,
        LabelFontFamily = "Cairo-Regular", // police arabe enregistrée (optionnel)
        TopCornerRadius = 22
    }
};
```

Le bouton est un frère peint après la barre : il gagne le tap, y compris sur sa partie au-dessus du bord. L’encoche autour de lui n’est pas une zone tactile. Le contenu est réservé de la hauteur de la barre seulement ; le bouton qui dépasse recouvre le contenu.

Un slot `BottomBar` personnalisé peut réutiliser le même rendu, par exemple pour masquer une destination qui n’est atteinte que par l’action :

```csharp
BottomBar = context => NavigationBars.Docked(context, style with
{
    ShowDestination = index => index != FeedIndex,
    LabelFor = destination => Localize(destination.Label)
}, NavigationAction.For(feedIcon, () => context.Select(FeedIndex)))
```

Le contexte expose aussi `CenterAction`, `CenterActionPosition`, `SafeAreaBottom`, `IsRightToLeft`, `ReserveBottomBarBounds(surface)` (réserve la hauteur de ce seul élément) et `UseSelectionTransition(transition)` (la transition des items de ce slot ; les transitions par paire ou par destination restent prioritaires). `RefreshChrome()` reconstruit le slot quand l’état qu’il lit change (langue, thème), sans toucher aux pages.

### Zone sûre

`SafeAreaBottom` est la partie de la surface sous la barre d’accueil (iOS) ou sous la barre de navigation (Android, uniquement si l’app dessine edge-to-edge). La barre dockée et la barre par défaut ajoutent cette hauteur sous leur contenu et se reconstruisent quand elle change (rotation).

### Formes personnalisées

`.Shape(XenoShape)` remplace le rectangle arrondi d’une entité pour le fond, l’ombre, le clip des enfants et la zone tactile. `XenoShapes.TopNotch(...)` est l’encoche intégrée ; `DockedNavigationBarStyle.Shape` remplace entièrement le contour de la barre :

```csharp
var wave = XenoShape.Create((path, width, height) =>
{
    path.MoveTo(0, 12);
    path.CubicTo(width * 0.3f, -8, width * 0.7f, 28, width, 12);
    path.LineTo(width, height);
    path.LineTo(0, height);
    path.Close();
});

DockedBarStyle = DockedNavigationBarStyle.Default with { Shape = wave }
// ou sur n'importe quel élément :
Box(0, 80).Background(0xFF111827).Shape(wave)
```

Le chemin est construit en unités logiques, origine en haut à gauche de l’entité, uniquement quand la taille change : les frames stables n’allouent rien. Une forme n’est pas mise en miroir automatiquement en RTL ; une forme asymétrique lit `context.IsRightToLeft` pour se refléter.

### Droite à gauche

`XenoApp.FlowDirection` vaut `Inherit` : la direction suit la langue de l’appareil (`ILayoutDirectionCapability`, mise à jour à chaud sur Android). `LeftToRight` ou `RightToLeft` la forcent ; `.FlowDirection(...)` la change pour un sous-arbre.

Une scène RTL est le reflet exact de sa mise en page LTR : les `Row` commencent au bord droit, le padding et la marge gauches deviennent le côté « début » (`Spacing.Directional(start:, end:)` les nomme ainsi), `CrossAxisAlignment.Start` d’une colonne est à droite, et le hit-testing suit puisqu’il lit les positions arrangées. `TextAlignment.Start` suit la direction du paragraphe ; l’hébreu et l’arabe sont toujours façonnés, même avec une police enregistrée, pour que les lettres se lient. Changer de direction relaie les scènes sans recomposer les pages.

Limites : les conteneurs à défilement horizontal gardent une étendue gauche → droite ; le Rail et le drawer ne changent pas de côté automatiquement (`DrawerDirection = FromRight` reste explicite) ; une police enregistrée sans un glyphe ne bascule pas encore sur une police de secours.

## Quelle opération reconstruit quoi ?

1. **Recomposition du chrome** : concerne uniquement un changement adaptatif BottomBar/Rail/Drawer ou une modification structurelle explicite. Les roots de contenu restent intacts.
2. **Activation d’une destination existante** : sélectionne son root retained, met à jour l’interactivité et lance les tracks. Aucun `Body()` ni layout si son viewport est inchangé.
3. **Première composition lazy** : le premier accès à une destination appelle une seule fois le `Body()` de sa page racine et monte sa scène.
4. **Push/pop de la pile active** : un push monte une nouvelle scène de détail au-dessus de la précédente ; un pop réactive la scène précédente puis libère uniquement le détail retiré.
5. **Reconstruction explicite de page** : seule une demande structurelle de cette page remplace sa scène active. Les autres destinations et le chrome standard ne changent pas.

## Cycle de vie

`XenoApp` expose `OnStart`, `OnForeground`, `OnBackground`, `OnLowMemory` et `OnDispose`. `XenoPage` expose `OnCreate`, `OnActivated`, `OnDeactivated` et `OnDestroy`. Les callbacks de page ne changent qu’au commit de la navigation ; une scène simplement en cours d’entrée/sortie n’est pas présentée publiquement comme un nouvel état de lifecycle. Quitter un onglet produit `OnDeactivated`, pas `OnDestroy` : sa scène reste montée.

## Limites V1

Le Drawer est un overlay modal, y compris avec BottomBar/Rail lorsque `EnableModalDrawer = true`. Ne sont pas encore fournis : predictive back complet, deep links, restauration après terminaison du processus, shared elements, résultats typés, multi-pane, Snackbar, dialogs et bottom sheets. L’animation chorégraphiée des éléments d’une page est une dette séparée ; elle réutilisera l’horloge et les tracks sans entrer dans le moteur de navigation V1.

## Drawer : gestes et sélection différée

```csharp
EnableModalDrawer = true,
DrawerGesturesEnabled = true,
DrawerDirection = DrawerSlideDirection.FromLeft,
DrawerEdgeWidth = 24, // dp
DrawerNavigationDelay = TimeSpan.FromMilliseconds(90),
DrawerOpenTransition = DrawerTransitions.Open(DrawerSlideDirection.FromLeft, 340),
DrawerCloseTransition = DrawerTransitions.Close(DrawerSlideDirection.FromLeft, 280),
```

Une sélection depuis le drawer ferme d'abord le menu, attend la fin réelle de la transition du chrome, puis le délai configuré (80 ms par défaut), et active enfin la destination. La page et son lifecycle ne changent pas pendant la fermeture. Les taps supplémentaires sont ignorés pendant cette transaction ; Retour l'annule. Un délai nul supprime seulement la pause, pas l'attente de fermeture. Le passage en arrière-plan, le reset et la destruction annulent une sélection encore en attente.

L'ouverture gestuelle commence dans la bande du bord configuré ; la fermeture commence sur le panneau. Après un seuil de 8 dp, seul un mouvement horizontal dominant capture le geste et annule le tap/scroll précédent. Le panneau suit le doigt, le scrim reste fixe et son opacité suit la progression. Au relâchement, une vitesse supérieure à 400 dp/s choisit l'ancrage ; sinon le seuil est 50 %. Une annulation système revient à l'ancrage de départ. Les durées de stabilisation proviennent des transitions configurées et respectent `ReducedMotion`.

Les gestes sont disponibles pour `FromLeft` et `FromRight` ; régler la direction et les deux transitions de façon cohérente. Les transitions programmatiques personnalisées restent disponibles. Le settlement gestuel utilise un easing numérique ease-out, sans rappeler les delegates des transitions. Les gestes verticaux de drawer restent à traiter.

Sur Android 10+, le contrat `NativeDrawerGesturePolicy` est traduit en une exclusion du geste Retour limitée à une bande centrale de 200 dp de hauteur sur le bord du drawer. Le système conserve Retour ailleurs, et Home n'est pas exclu. La région se met à jour sur le thread UI lors des changements structurels, du viewport ou du clavier, jamais dans les frames de drag. Elle disparaît lorsque les gestes sont désactivés, le clavier masque le chrome ou une sélection est en attente. Cette limite suit les [règles Android de coexistence des gestes](https://developer.android.com/develop/ui/views/touch-and-input/gestures/gesturenav).

La composition du chrome se produit une seule fois lors de l'acquisition/fermeture, jamais pendant le drag. Le hot path modifie seulement les valeurs ECS du panneau/scrim et l'horloge de stabilisation. La publication d'une navigation différée est exécutée à la frontière cold path après le rendu, hors du verrou du moteur. Ce modèle reprend les interactions du [drawer modal Android](https://developer.android.com/develop/ui/compose/components/drawer), sans utiliser de vue native ni prétendre implémenter predictive back.

Validation du 2026-10-05 : 111 tests Core Release, dont le délai après fermeture, l'annulation par Retour, les gestes gauche/droit, l'annulation de pointer, l'arbitrage vertical et les gestes désactivés. Après acquisition/warm-up : 1 000 mises à jour de drag et 200 frames de rendu testées à 0 octet managé alloué. Ce résultat ne concerne pas les allocations de composition du chrome.

Parcours physique vérifié sur Galaxy Note20 SM-N981B (`RFCR91ELK6Y`, navigation système gestuelle) : ouverture depuis le bord dans la région réservée, fermeture en tirant le panneau, ouverture hamburger puis sélection Gallery, et Retour système hors région réservée ramenant à Home. Le conflit initial avec Retour système a été reproduit puis corrigé par l'exclusion bornée. Aucun crash AndroidRuntime observé sur ce parcours. Aucun nouveau test physique iOS, tablette ou A15 n'est revendiqué.
