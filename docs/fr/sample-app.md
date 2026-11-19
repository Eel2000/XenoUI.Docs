# Apprendre par le SampleApp

[Documentation](index.md) · [Guide développeur](developer-guide.md) · [Créer un composant](components.md) · [Personnaliser](customization.md) · [Animations](animations.md) · [Démonstration API](sample-api.md)

Le [`SampleApp`](https://github.com/Eel2000/XenoUI/tree/master/samples/SampleApp) est l’application de référence du dépôt : environ 2 900 lignes de C#
qui exercent presque toute l’API publique. Ce guide en fait un **parcours de développeur** : comment
l’application est construite, quel fichier illustre quel concept, quels motifs copier, et lesquels
manipuler avec précaution. Tous les extraits viennent du code du dépôt ; les noms de fichiers et de membres
sont ceux du projet.

## Sommaire

1. [Lancer l’application](#1-lancer-lapplication)
2. [Carte du projet](#2-carte-du-projet)
3. [Le projet : `SampleApp.csproj`](#3-le-projet--sampleappcsproj)
4. [`App.cs` : le point d’entrée](#4-appcs--le-point-dentrée)
5. [Le design maison : `Theme`, `Ui`, `AppChrome`](#5-le-design-maison--theme-ui-appchrome)
6. [Page par page](#6-page-par-page)
7. [Catalogue de motifs](#7-catalogue-de-motifs)
8. [Précautions : ce que le sample fait pour la démonstration](#8-précautions--ce-que-le-sample-fait-pour-la-démonstration)
9. [Pas à pas : ajouter une page « Notes »](#9-pas-à-pas--ajouter-une-page--notes-)
10. [Tests liés au sample](#10-tests-liés-au-sample)

---

## 1. Lancer l’application

```powershell
dotnet build samples/SampleApp/SampleApp.csproj -f net11.0-android
xeno devices
xeno run --project samples/SampleApp/SampleApp.csproj --device <id> --framework net11.0-android
```

Prérequis (SDK .NET 11, workload Android, plateforme API 37, runtime CoreCLR) : [Guide développeur §2](developer-guide.md#2-prérequis).
Le sample est aussi prévisualisable sans appareil grâce aux `[Preview]` de ses pages
([Visual Studio](visual-studio.md), [VS Code](vscode.md)).

L’application s’ouvre sur cinq destinations : **Home**, **Explore**, **Feed** (bouton central), **People** et
**Studio**. Sur téléphone c’est une barre du bas ; sur tablette un rail ; un tiroir modal est disponible
partout (bouton hamburger de la barre du haut). **Studio** pousse les labs : vidéo, audio, API en direct, saisie
de texte, laboratoire de performances, overlays et préférences.

## 2. Carte du projet

```text
samples/SampleApp/
├─ SampleApp.csproj        Sdk="Xeno.SDK", TFM, identité de l’app
├─ App.cs                  XenoApp : scaffold de navigation, pages, cycle de vie
├─ AndroidManifest.xml     permissions (INTERNET…) ; Info.plist pour iOS
├─ Assets/                 Fonts (Font Awesome 5), Images (avatar, logo)
├─ Design/
│  ├─ Theme.cs             palette, polices, IconDef
│  ├─ Ui.cs                briques réutilisables (boutons, cartes, lignes, barres)
│  └─ AppChrome.cs         barre du bas, rail, tiroir (slots de présentation)
├─ Pages/                  une XenoPage par écran (13)
└─ Services/               code portable, sans type XenoUI (HTTP, flux distants, catalogue)
```

La séparation à retenir : **`Services/` ne connaît ni XenoUI ni la plateforme** ; **`Design/` définit
l’apparence** ; **`Pages/` assemble l’interface et orchestre**.

## 3. Le projet : `SampleApp.csproj`

```xml
<Project Sdk="Xeno.SDK">
  <PropertyGroup>
    <TargetFrameworks>net11.0-android;net11.0-ios</TargetFrameworks>
    <XenoEnableMedia>true</XenoEnableMedia>

    <XenoAppId>com.xenoui.sample</XenoAppId>
    <XenoAppName>XenoUI Sample</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>
</Project>
```

Tout le reste vient de `Xeno.SDK` : références `XenoUI.*`, `MainActivity` générée, manifeste par défaut, et
**imports implicites**. C’est pourquoi les pages n’ont presque aucun `using` XenoUI : `Compose` (statique),
`Hooks` (statique), `Widgets` (statique : `Button`, `TextButton`), `XenoUI.Core.Compositor`,
`Components.Enums`, `StandardLibrary.Controls/Overlays/Shell`. Sur Android, `Dialog` est aliasé vers
`XenoUI.StandardLibrary.Overlays.Dialog`. `XenoEnableMedia` ajoute `XenoUI.Media` (lecteurs audio/vidéo).
Voir [Xeno.SDK](xeno-sdk.md). Sur Android, le SDK exécute l’application sur CoreCLR (`UseMonoRuntime=false`).

## 4. `App.cs` : le point d’entrée

`App : XenoApp` fait quatre choses.

### 4.1 Il déclare le scaffold de navigation

`NavigationScaffoldState` reçoit les destinations (`NavigationDestination.For<HomePage>("Home",
Theme.Glyph(""))`, avec un badge facultatif) et un initialiseur d’objet qui règle tout le chrome :

| Propriété | Rôle dans le sample |
|---|---|
| `Mode = NavigationMode.Automatic` | Barre du bas, rail ou tiroir selon la taille de fenêtre |
| `ContentOverlaysNavigationBar = false` | Le contenu (et la vidéo du feed) s’arrête au-dessus de la barre |
| `EnableModalDrawer`, `DrawerGesturesEnabled`, `DrawerDirection`, `DrawerNavigationDelay` | Tiroir modal, geste, côté d’ouverture, délai avant navigation |
| `DestinationTransition = NavTransitions.FadeThrough(220)` | Changement d’onglet |
| `ChromeTransition` | `NavTransition` personnalisée pour l’apparition du chrome |
| `BottomBarSelectionTransition`, `RailSelectionTransition`, `DrawerSelectionTransition` | Animation de sélection par mode ([Animations §7](animations.md#7-animer-la-sélection-dans-la-navigation)) |
| `DrawerOpenTransition`, `DrawerCloseTransition` | `DrawerTransitions.Open/Close(direction, ms)` |
| `ReselectionBehavior = PopToRoot` | Retoucher l’onglet courant revient à sa racine |
| `BottomBar`, `NavigationRail`, `Drawer` | Slots : fonctions de `AppChrome` ([§5](#5-le-design-maison--theme-ui-appchrome)) |
| `TopBar = TopBars.ForActivePage` | Barre d’application construite depuis `ITopBarPage` |

Extrait réel (sélection de la barre du bas, avec un easing à ressort sans appel par frame) :

```csharp
BottomBarSelectionTransition = NavigationSelectionTransition.Create()
    .Easing(t => 1 - MathF.Pow(1 - t, 3) + MathF.Sin(t * MathF.PI) * 0.08f)
    .Surface(x => x.Scale(0.92f, 1))
    .Icon(x => x.ForegroundColor(0xFF8A8A9C, Theme.White).Scale(0.86f, 1))
    .Label(x => x.ForegroundColor(0xFF8A8A9C, Theme.White))
    .Badge(x => x.Scale(0.8f, 1))
    .Duration(240).Build(),
```

### 4.2 Il enregistre les pages

```csharp
public override void OnStart()
{
    AppChrome.Destinations = _navigation.Destinations.Select(x => x.PageType).ToArray();
    ArticleDetails = new ArticleDetailsModel(new ArticleService(ApiClient),
        Native.Require<IUiDispatcherCapability>().Post);
    Page<HomePage>();
    Page<ExplorePage>();
    /* … les 13 pages … */
    UseNavigationScaffold(_navigation);
}
```

`Page<T>()` instancie la page **une seule fois** : ses champs (`State`, `ScrollState`, listes) conservent donc
leur valeur entre navigations. Une page qui n’est pas enregistrée ne peut pas être ciblée par `Navigate<T>()`.
`UseNavigationScaffold` active le scaffold.

### 4.3 Il porte l’état et les services de l’application

`ApiClient` (un seul `HttpClient`), `ArticleDetails` (modèle partagé), `Feed` (référence à la page du feed),
`GetDemoVideo()` (lecteur vidéo paresseux). Les pages y accèdent par le cast `(App)Application`, et l’`App`
expose des **méthodes d’intention** plutôt que de laisser les pages manipuler le scaffold :

```csharp
internal void SelectTab(int index) => _navigation.SelectDestination(index);
internal void OpenApi() => Navigate<ApiPage>(NavTransitions.SlideHorizontal(280));
internal void OpenArticle(Article article) =>
    Navigate<ArticleDetailsPage, ArticleDetailsArgs>(new(article), NavTransitions.SlideHorizontal(280));
```

`OpenFeedClip(int)` illustre la navigation inter-onglets : sélectionner la destination du feed, puis appeler
`Feed?.ShowClip(clip)`.

### 4.4 Il relaie le cycle de vie de l’application

```csharp
public override void OnBackground()
{
    if (ActivePage is AudioLabPage audio) audio.Suspend();
    if (ActivePage is VideoLabPage video) video.Suspend();
    if (ActivePage is FeedPage feed) feed.Suspend();
    if (ActivePage is ApiPage api) api.Suspend();
    else if (ActivePage is ArticleDetailsPage) ArticleDetails.Deactivate();
}
```

`OnForeground` fait l’inverse (`Resume`/`Activate`), `OnDispose` libère le lecteur, le modèle et le client.
Règle à retenir : **tout ce qui tourne en arrière-plan (lecteur, requête) doit s’arrêter à `OnBackground` et
reprendre à `OnForeground`**.

`OnPreview()` est appelée à la place de `OnStart()` quand le previewer construit l’application sans la
démarrer : n’y mettez que ce que les pages lisent, jamais de réseau.

## 5. Le design maison : `Theme`, `Ui`, `AppChrome`

### 5.1 `Theme.cs`

Une classe statique de constantes (`Canvas`, `Surface`, `SurfaceHigh`, `Ink`, `Muted`, `Accent`, `Cyan`…,
chacune avec une variante « Soft » translucide pour les fonds d’icône), les noms de polices (`Fa`,
`FaRegular`, `FaBrands`) et `Theme.Glyph(string)` qui retourne un `IconDef` Font Awesome. Voir
[Personnaliser §6](customization.md#6-couleurs-thème-et-mode-sombre).

### 5.2 `Ui.cs` : le niveau « enveloppe »

Des fonctions statiques qui appliquent le thème aux composants standard : `Primary`, `Secondary`, `Pill`,
`Card`, `Stat`, `ReactiveStat(State<string>, …)`, `NavRow`, `IconButton`, `SectionTitle`, `Eyebrow`,
`Divider`, plus des fabriques d’options de barre (`DrawerBar`, `BackBar`, `BarAction`). Exemples :

```csharp
internal static XenoComponent Primary(string label, Action tap, float size = 14) =>
    Button(label, tap, size).Background(Theme.Accent).CornerRadius(14).Padding(16, 13);

internal static XenoComponent IconButton(string glyph, Action tap, uint background = Theme.SurfaceHigh,
    uint foreground = Theme.Ink) =>
    Box(42, 42, Icon(Theme.Glyph(glyph), 16).TextColor(foreground))
        .Background(background).CornerRadius(14).CenterContent().Clickable(tap);
```

`Ui.cs` contient aussi deux méthodes `[Preview]` (boutons, cartes de statistiques, avec une variante RTL) : le
modèle à suivre pour prévisualiser vos briques ([Créer un composant §12](components.md#12-prévisualisation--preview)).

### 5.3 `AppChrome.cs` : les slots de présentation

Trois fonctions reçoivent un `NavigationPresentationContext` et retournent l’arbre d’un chrome :
`BottomBar`, `Rail`, `Drawer`. Elles lisent `context.Destinations`, `context.SelectedIndex`, appellent
`context.Select(i)` au toucher, puis **enregistrent chaque élément** pour que les transitions de sélection
sachent quoi animer :

```csharp
var surface = Column(iconSlot, label).Gap(4).CenterContent().FlexGrow(1)
    .Clickable(() => context.Select(index));
return context.RegisterItem(index, surface, icon, label, badge: badge);
```

Détails remarquables : l’élément du feed est un bouton central distinct (`FeedItem`) ; le rail réserve
`context.SafeAreaTop` ; en mode « démo arabe », `NavigationBars.Docked(context, style, action)` produit une
barre à bouton central avec `DockedNavigationBarStyle.Default with { … }`, et `SetArabicNavigation` bascule
`FlowDirection` de l’application en RTL puis appelle `_navigation.RefreshChrome()`.

## 6. Page par page

| Page | Concepts illustrés | À copier pour… |
|---|---|---|
| `HomePage` | `ITopBarPage`, `UseScroll`, `LazyRow` à clés, état réactif, `OpenNavigationDrawer` | Un écran d’accueil défilant |
| `ExplorePage` | Filtre par `Rebuild()`, sections conditionnelles, `Navigate` avec transitions, catalogue de contrôles | Un catalogue / une page de composants |
| `PeoplePage` | 10 000 lignes `LazyColumn`, états **par ligne**, compteur réactif | Une grande liste interactive |
| `SettingsPage` | Interrupteur contrôlé, groupes, `[Preview]` avec `Device`, bascule RTL | Un écran de préférences |
| `StudioPage` | Hub de navigation, `Navigate<T>` générique, tuiles | Un menu de fonctionnalités |
| `TextInputPage` | `UseTextEdit`, `TextField`/`TextArea`, `InputOptions`, `Changed`, actions clavier | Un formulaire |
| `ApiPage` | Flux distant, états idle/loading/ready/error, `LazyListState`, `RebuildTopBar` | Une liste réseau |
| `ArticleDetailsPage` | `XenoPage<TArguments>`, `OnNavigatedTo`, modèle partagé, défilement par sélection | Un détail avec paramètres typés |
| `FeedPage` | `VerticalPager`, vidéo native, états par page, minuteur `After` | Un fil vertical type vidéo courte |
| `OverlaysPage` | Tous les overlays, `OverlayAnimation` personnalisée, `TopBarOptions` complet | Snackbar, dialogue, feuille |
| `LabPage` | Pression sur le runtime : 10 000 lignes + 120 puces | Tester la perf |
| `VideoLabPage`, `AudioLabPage` | `MediaController`, cycle de vie `Suspend`/`Resume` | Lecteurs média |

### 6.1 `HomePage` : un écran qui défile

Le squelette commun à presque toutes les pages du sample :

```csharp
public sealed class HomePage : XenoPage, ITopBarPage
{
    private readonly ScrollState _scroll = UseScroll();          // persistant : champ

    public string TopBarTitle => "XenoUI";
    public TopBarOptions? TopBarOptions => Ui.DrawerBar("Native UI · one C# codebase", OpenNavigationDrawer,
        Ui.BarAction("", () => Navigate<SettingsPage>(NavTransitions.SlideHorizontal())));

    public override XenoComponent Body() =>
        Column(Column(/* contenu */).Gap(16).Padding(...).CrossAxis(CrossAxisAlignment.Stretch))
            .Background(Theme.Canvas).CrossAxis(CrossAxisAlignment.Stretch).Scrollable(_scroll);
}
```

- `ITopBarPage` (`TopBarTitle`, `TopBarOptions`) fournit la barre fixe du scaffold.
- La page défile par `.Scrollable(_scroll)` sur le conteneur racine.
- Un rail horizontal de clips : `LazyRow(FeedCatalog.Clips, clip => TrendingCard(app, clip), key: clip => clip.Url)
  .Height(210).Gap(10)` ; chaque carte appelle `app.OpenFeedClip(index)`.
- Les sous-composants (`Hero`, `Shortcut`, `Feature`) sont des méthodes statiques privées retournant
  `XenoComponent`, c’est-à-dire des composants locaux ([Créer un composant](components.md)).

### 6.2 `ExplorePage` : filtrer par reconstruction

```csharp
private int _category;                                         // état de page, champ
private bool Show(int category) => _category == 0 || _category == category;

Show(2) ? Section("TYPOGRAPHY", "…", Typography()) : XenoComponent.None,

private XenoComponent CategoryChip(string name) { /* … */
    return Text(name, 12)…Clickable(() => { _category = index; Rebuild(); });
}
```

Changer de catégorie modifie la **structure** : la page appelle `Rebuild()`. Les sections sont de petites
fonctions : `Surfaces()` montre `Elevation(0 | 4 | 10)`, `Media()` montre `ScaleMode(Cover)` et les icônes,
`Layout()` montre `Spacer()`, `Transitions()` déclenche `NavTransitions.SlideHorizontal/ScaleFade/FadeThrough`.

### 6.3 `PeoplePage` : 10 000 lignes sans reconstruire la liste

```csharp
LazyColumn(People, PersonRow, key: index => index)
    .FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)

private XenoComponent PersonRow(int index)
{
    var label = UseState(following ? "Following" : "Follow");           // états liés à la ligne
    var background = UseState(following ? Theme.SurfaceHigh : Theme.Accent);
    return Row(/* … */,
        Text(label, 12).Background(background)…Clickable(() =>
        {
            bool now = _following.Add(index);
            if (!now) _following.Remove(index);
            label.Value = now ? "Following" : "Follow";                  // repeint seulement ce bouton
            background.Value = now ? Theme.SurfaceHigh : Theme.Accent;
            _followingCount.Value = _following.Count.ToString();         // repeint le compteur du haut
        }));
}
```

Leçons : (1) la **source de vérité** (`HashSet<int> _following`) est un champ de page ; (2) les `State`
créés dans `PersonRow` vivent **tant que la ligne est matérialisée** ; une ligne recréée relit
`_following` pour retrouver son état ; (3) un `State<string>` de page (`_followingCount`) met à jour une
statistique ailleurs dans l’écran via `Ui.ReactiveStat`.

### 6.4 `SettingsPage` : un interrupteur contrôlé

```csharp
Row(Box(22, 22).Background(Theme.White).CornerRadius(11))
    .MainAxis(toggle.Value ? MainAxisAlignment.End : MainAxisAlignment.Start)
    .Width(48).Height(28).Padding(3)
    .Background(toggle.Value ? Theme.Accent : 0xFF3A3A48).CornerRadius(14)
```

La bascule du pouce se fait par `MainAxis(End/Start)` de la rangée (plus simple qu’un `Spacer`), et la ligne
entière est `.Clickable(() => { toggle.Value = !toggle.Value; changed?.Invoke(...); Rebuild(); })` : l’état est
un `State<bool>` de page, le visuel dépend de sa valeur, un `Rebuild()` le rafraîchit. `Group(title, rows…)`
intercale des `Ui.Divider()` en construisant un `XenoComponent[]`. La page porte deux `[Preview]`, dont
`Device = "iphone"`.

### 6.5 `StudioPage` et `TextInputPage`

`StudioPage` factorise la navigation avec une méthode générique :

```csharp
private void Push<TPage>() where TPage : XenoPage =>
    Navigate<TPage>(NavTransitions.SlideHorizontal(280));
```

`TextInputPage` montre le cycle d’un formulaire :

```csharp
private readonly TextEditController _name = UseTextEdit();
private readonly State<string> _namePreview = UseState("Nothing typed yet");

public TextInputPage()
{
    _name.Changed += (_, change) => _namePreview.Value = /* dérivé de change.Value.Text */;
}

TextField(_name, "Your name", InputOptions(action: NativeInputAction.Next,
    autofill: NativeAutofillHint.Name, label: "Full name"),
    onAction: action => _actionPreview.Value = $"Name: {action}").Height(54),
Text(_namePreview, 13)                                    // réactif
```

Sont aussi démontrés : clavier e-mail, mot de passe avec `obscureText: !_revealSecret` (révéler = `Rebuild()`),
`TextArea`, `SetText`, `Undo`/`Redo`/`SelectAll`, `CommitText`. Le texte et la sélection survivent aux
reconstructions car les contrôleurs sont des champs.

### 6.6 Réseau : `ApiPage`, `ArticleDetailsPage` et `Services/`

Trois couches, du bas vers le haut :

1. **`ArticleService`** : HTTP + JSON *source-generated*, validation, délai de 20 s. Aucun type XenoUI.
2. **`RemoteFeed<T>`** : états `Idle/Loading/Ready/Error`, annulation, numéro de requête qui rejette les
   résultats périmés, publication via un `Action<Action> post` injecté (le **dispatcher UI**).
3. **La page** : s’abonne, reconstruit, affiche.

Le cycle de vie de la page est le modèle à suivre :

```csharp
public override void OnCreate()
{
    var dispatcher = Native.Require<IUiDispatcherCapability>();
    _feed = new ArticleFeed(new ArticleService(((App)Application).ApiClient), dispatcher.Post, () =>
    {
        if (_destroyed || !IsActive) return;       // jamais d’écriture sur une page quittée
        Rebuild();
        RebuildTopBar();                           // l’icône Refresh suit l’état du flux
    });
}
public override void OnActivated()   => _feed.Activate();     // charge au premier affichage
public override void OnDeactivated() => _feed.Deactivate();   // annule la requête en cours
public override void OnDestroy()     { _destroyed = true; _feed.Dispose(); }
```

La liste est `LazyColumn(state.Items, ArticleCard, key: article => article.Id, state: _listState)`, et le
toucher appelle `((App)Application).OpenArticle(article)`.

`ArticleDetailsPage : XenoPage<ArticleDetailsArgs>` reçoit un **payload typé** :

```csharp
public sealed record ArticleDetailsArgs(Article Article);
public override void OnNavigatedTo(ArticleDetailsArgs arguments) => _model.Select(arguments.Article);
```

Le défilement est remis en haut quand l’article change (`_scroll = UseScroll()` sur changement de sélection,
explicitement un chemin « à froid »). Détails du réseau : [Démonstration API](sample-api.md) ; paramètres
typés : [Texte contraint et navigation typée](text-layout-navigation.md).

### 6.7 `FeedPage` : un fil vertical

```csharp
return ZStack(
    Items.Length == 0 ? FollowingEmpty() :
        VerticalPager(Items, BuildPage, key: clip => clip.Url, state: ActiveState),
    TopBar(), Toast(),
    _commentsOpen ? CommentsSheet() : XenoComponent.None
).Background(Theme.Black).CrossAxis(CrossAxisAlignment.Stretch);
```

- **Un seul `State` par propriété et par page** (`_likeColor[i]`, `_progress[i]`, `_videoHandle[i]`…) : se poser
  sur un clip bascule quelques valeurs, sans reconstruire l’écran en plein geste. Les tableaux sont créés par
  `UseStates(n, valeur)`.
- `LazyListState.PageChanged` annonce le clip arrivé ; `ScrollToPage(index)` pilote le fil depuis le code ;
  `UserScrollEnabled = false` rend la feuille de commentaires maîtresse des gestes verticaux.
- La vidéo est une sortie native rattachée à la page courante (`.NativeVideo(_videoHandle[i])`) ; un **pool de
  deux lecteurs** garde le clip précédent.
- Un double toucher « like » avec un cœur qui disparaît utilise le minuteur `After(ms, action)` :
  `Task.Delay(ms).ContinueWith(_ => post(action))`, `post` étant `Native.Require<IUiDispatcherCapability>().Post`,
  avec un **numéro de série** (`_heartSerial`) pour ignorer les minuteurs périmés
  ([Animations §5](animations.md#5-animer-soi-même-pas-à-pas)).
- `OnActivated`/`OnDeactivated` reprennent et suspendent les lecteurs ; `OnDestroy` les libère.

### 6.8 `OverlaysPage`

Chaque bouton appelle un widget : `Snackbar.Show(Application, "Saved to your library", options)`, alertes,
dialogues, popups ancrés, feuilles. La page ajoute un sélecteur de mouvement dont une entrée est une animation
**personnalisée** construite une fois :

```csharp
private static readonly OverlayAnimation Bounce = OverlayAnimation.Custom(NavTransition.Create()
    .Enter(x => x.Opacity(0, 1)
        .Animate(NavAnimatedProperty.ScaleX, 0.6f, 1, OvershootEase)
        .Animate(NavAnimatedProperty.ScaleY, 0.6f, 1, OvershootEase))
    .Exit(x => x.Opacity(1, 0).Scale(1, 0.85f))
    .Duration(380).Build());
```

Sa propriété `TopBarOptions` renvoie un objet d’options **complet** (icône de navigation, actions, couleurs,
élévation) : la référence pour personnaliser la barre. Voir [Overlays](https://github.com/Eel2000/XenoUI/blob/master/docs/overlays.md).

## 7. Catalogue de motifs

| Je veux… | Regarde |
|---|---|
| Un écran défilant avec barre d’application | `HomePage` (`ITopBarPage` + `UseScroll`) |
| Un état qui repeint un seul élément | `HomePage.Counter`, `PeoplePage` (`State<string>`, `State<uint>`) |
| Changer la structure (filtre, onglet interne) | `ExplorePage` (`_category` + `Rebuild()`) |
| Une grande liste | `PeoplePage`, `LabPage` (`LazyColumn(..., key: …)`) |
| Une liste horizontale | `HomePage`, `ExplorePage` (`LazyRow`) |
| Des états par élément de liste | `PeoplePage.PersonRow` |
| Un interrupteur / préférences | `SettingsPage.Setting` |
| Un formulaire et le clavier | `TextInputPage` |
| Appeler une API sans bloquer l’UI | `Services/`, `ApiPage` |
| Un détail avec un paramètre typé | `ArticleDetailsPage`, `App.OpenArticle` |
| Naviguer entre onglets depuis une page | `App.SelectTab`, `HomePage` (`app.SelectTab(…)`) |
| Une transition de page personnalisée | `ExplorePage.Transitions`, `App.ChromeTransition` |
| Personnaliser la barre du bas / le rail / le tiroir | `AppChrome` |
| RTL | `App.SetArabicNavigation`, `SettingsPage` |
| Un overlay animé | `OverlaysPage` |
| Vidéo native | `FeedPage`, `VideoLabPage` |
| Reprendre/suspendre en arrière-plan | `App.OnBackground/OnForeground`, `Suspend()/Resume()` des pages |
| Un thème et des briques réutilisables | `Design/Theme.cs`, `Design/Ui.cs` |
| Prévisualiser sans appareil | `[Preview]` sur pages et sur `Ui` |

## 8. Précautions : ce que le sample fait pour la démonstration

Le sample privilégie la lisibilité ; quatre choses ne sont **pas** à reproduire telles quelles en production :

1. **`UseState` dans `Body()`.** `HomePage.Body` (`var taps = UseState(24)`) et `ExplorePage.Body`
   (`var count = UseState(7)`) créent leur état dans la composition : il repart à sa valeur initiale à chaque
   `Rebuild()` (par exemple quand on change de catégorie dans Explore). Dans votre code, faites-en un champ
   (`private readonly State<int> _taps = UseState(24);`). Le [guide développeur](developer-guide.md#7-état-mises-à-jour-et-reconstruction) le rappelle.
2. **Cast `(App)Application`.** Pratique ici ; dans une grande application, exposez plutôt une interface de
   services ou passez des `Action` aux composants.
3. **`UseScroll()` réassigné** dans `ArticleDetailsPage` : c’est un choix délibéré (nouveau scroll quand
   l’article change), pas un motif général.
4. **Chaînes et glyphes en dur** (`""`) : en production, nommez-les (`Icons.Home`).

À l’inverse, plusieurs choix valent d’être imités tels quels : séparer `Services/` du reste, injecter le
dispatcher UI, garder `IsActive` en garde des rappels tardifs, valider les données à la frontière, et garder
les objets persistants (`UseScroll`, `UseLazyList`, `UseTextEdit`, `State`) en champs de page.

## 9. Pas à pas : ajouter une page « Notes »

Un exercice qui réutilise les idiomes du sample. *Cet exemple n’a pas été compilé dans cette documentation
(le workload Android API 37 n’était pas disponible sur la machine de rédaction) : adaptez-le si le compilateur
signale un écart.*

**1. Créer la page** `Pages/NotesPage.cs` :

```csharp
using SampleApp.Design;
using XenoUI.Core.Hooks;

namespace SampleApp.Pages;

public sealed class NotesPage : XenoPage, ITopBarPage
{
    private sealed record Note(int Id, string Text);

    private readonly TextEditController _input = UseTextEdit();
    private readonly List<Note> _notes = new();
    private readonly LazyListState _list = UseLazyList();
    private int _nextId;

    public string TopBarTitle => "Notes";
    public TopBarOptions? TopBarOptions => Ui.BackBar($"{_notes.Count} notes", GoBack);

    public override XenoComponent Body() =>
        Column(
            Row(
                TextField(_input, "New note").Height(48).FlexGrow(1),
                Ui.Primary("Add", Add, 13))
            .Gap(8).CrossAxis(CrossAxisAlignment.Center).Padding(16),
            _notes.Count == 0
                ? Text("Nothing yet. Write something above.", 13).TextColor(Theme.Muted).Padding(16)
                : LazyColumn(_notes, NoteRow, key: note => note.Id, state: _list)
                    .FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)
                    .Padding(Spacing.Only(left: 16, right: 16, bottom: 12)))
        .Background(Theme.Canvas).CrossAxis(CrossAxisAlignment.Stretch);

    private static XenoComponent NoteRow(Note note) =>
        Text(note.Text, 14).TextColor(Theme.Ink).MaxLines(3, ellipsis: true)
            .Padding(14).Background(Theme.Surface).CornerRadius(14)
            .Margin(Spacing.Only(bottom: 8));

    private void Add()
    {
        string text = _input.Value.Text.Trim();
        if (text.Length == 0) return;
        _notes.Insert(0, new Note(_nextId++, text));
        _input.SetText("");
        Rebuild();
        RebuildTopBar();                               // le sous-titre affiche le nombre de notes
        Snackbar.Show(Application, "Note added");
    }
}
```

**2. L’enregistrer** dans `App.OnStart()` : `Page<NotesPage>();`.

**3. Y accéder** depuis `StudioPage`, avec une ligne de plus après les autres `Ui.NavRow` :

```csharp
Ui.NavRow("", "Notes", "A tiny list with a text field",
    Theme.Amber, Theme.AmberSoft, () => Push<NotesPage>()),
```

**4. Ce que l’exercice met en jeu :** un objet persistant en champ (`TextEditController`, `LazyListState`),
une liste à clés stables, une barre dont le sous-titre dépend de l’état (`RebuildTopBar()`), un overlay
(`Snackbar`), et la navigation par `Push<T>()`. Pour une version avec vue détail, calquez
`ArticleDetailsPage : XenoPage<TArguments>`. Pour la prévisualiser, ajoutez `[Preview]` sur la classe.

## 10. Tests liés au sample

```powershell
dotnet run --project tests/SampleApp.Api.RegressionTests -c Release
dotnet run --project tests/SampleApp.Overlays.RegressionTests -c Release
dotnet run --project tests/XenoUI.Core.RegressionTests -c Release
```

Les tests de l’API couvrent HTTP, JSON, annulation, publication différée et résultats hors ordre sans projet
de plateforme ; ceux des overlays couvrent la couche overlay. Une validation du sample sur appareil
(navigation, lecteurs, clavier, arrière-plan) reste manuelle : [Dépannage](troubleshooting.md).
