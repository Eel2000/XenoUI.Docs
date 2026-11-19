# De Jetpack Compose à XenoUI

[Documentation](index.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [État](state.md) · [Effets de bord](side-effects.md) · [Mises en page](layouts.md)

Vous connaissez Jetpack Compose (ou SwiftUI, Flutter) ? Cette page est un **dictionnaire** : pour chaque concept
de Compose, son équivalent en XenoUI, et les écarts à connaître. Elle ne remplace pas les guides détaillés :
chaque ligne renvoie à la page qui l’explique.

## Sommaire

1. [Ce qui est pareil, ce qui diffère](#1-ce-qui-est-pareil-ce-qui-diffère)
2. [Dictionnaire des concepts](#2-dictionnaire-des-concepts)
3. [Mise en page et modificateurs](#3-mise-en-page-et-modificateurs)
4. [Composants Material et widgets](#4-composants-material-et-widgets)
5. [Navigation](#5-navigation)
6. [Un même écran, écrit dans les deux](#6-un-même-écran-écrit-dans-les-deux)
7. [Habitudes à désapprendre](#7-habitudes-à-désapprendre)
8. [Ce qui n’existe pas (encore)](#8-ce-qui-nexiste-pas-encore)

---

## 1. Ce qui est pareil, ce qui diffère

**Pareil** : interface déclarative, composants = fonctions, état remonté, flux de données unidirectionnel,
listes paresseuses à clés, aperçus (`@Preview`), un seul code C#/Kotlin pour décrire l’écran.

**Différent** :

| | Jetpack Compose | XenoUI |
|---|---|---|
| Langage | Kotlin | C# (.NET 11) |
| Rendu | Canvas d’Android (Compose Multiplatform : Skia) | Skia, sur **Android et iOS** |
| Mise à jour | Recomposition intelligente, suivie par lecture d’état | `State<T>` **lié explicitement** + `Rebuild()` de page |
| Composable | Fonction annotée `@Composable` | Fonction retournant `XenoComponent` |
| Modificateurs | Chaîne de valeurs `Modifier` passée en paramètre | Chaîne **sur le résultat** : `Box().Padding(8)` |
| Ancrage à l’OS | Vues natives sous le capot pour certaines API | Tout est dessiné ; clavier/IME natifs via un pont typé |

## 2. Dictionnaire des concepts

| Jetpack Compose | XenoUI | Détails |
|---|---|---|
| `@Composable fun Foo()` | `static XenoComponent Foo(…)` | [Créer un composant](components.md) |
| Recomposition | `State<T>` lié (fin) ou `Rebuild()` (page) | [Penser en XenoUI §3](thinking-in-xenoui.md#3-les-deux-façons-de-mettre-à-jour-lécran) |
| `remember { mutableStateOf(0) }` | `private readonly State<int> _n = UseState(0);` (champ) | [État §1](state.md#1-les-objets-détat) |
| `by remember` (délégué) | `state.Value` | |
| `rememberSaveable` | Non fourni | [État §9](state.md#9-conserver-létat) |
| State hoisting | Passer `State<T>` + `Action` en paramètres | [État §3](state.md#3-remonter-létat-state-hoisting) |
| `derivedStateOf` | Calcul dans `Body()`, ou second `State` mis à jour dans un handler | [État §6](state.md#6-valeurs-dérivées) |
| `LaunchedEffect(key)` | `OnActivated` / `OnNavigatedTo` + annulation en `OnDeactivated` | [Effets §1](side-effects.md#1-correspondance-avec-les-effets-de-compose) |
| `DisposableEffect` | Abonnement `OnCreate`, désabonnement `OnDestroy` | [Effets §5](side-effects.md#5-sabonner-et-se-désabonner) |
| `rememberCoroutineScope` | `CancellationTokenSource` en champ | [Effets §4](side-effects.md#4-démarrer-et-annuler-du-travail-avec-la-page) |
| `collectAsState()` | Callback `changed` d’un modèle → `Rebuild()` / `State.Value` | [Architecture §4](app-architecture.md#4-couche-modèles-de-présentation) |
| `snapshotFlow` | `State<T>.OnChanged` | |
| `CompositionLocal` | Pas d’équivalent : paramètres, ou `Application`/`Native` depuis la page | |
| `ViewModel` | Modèle de présentation (`RemoteFeed<T>`) | [Architecture](app-architecture.md) |
| `Dispatchers.Main` | `IUiDispatcherCapability.Post` | [Effets §3](side-effects.md#3-threads--qui-peut-faire-quoi) |
| `@Preview` | `[Preview]` | [Créer un composant §12](components.md#12-prévisualisation--preview) |
| Hot reload / Live Edit | Aperçus VS / VS Code | [Visual Studio](visual-studio.md), [VS Code](vscode.md) |
| `MaterialTheme` | Classe `Theme` à vous, appliquée explicitement | [Personnaliser §6](customization.md#6-couleurs-thème-et-mode-sombre) |
| `key(id) { }` | `key: item => item.Id` sur les listes | [Mises en page §8](layouts.md#8-listes-virtualisées) |
| `Modifier.testTag` + `createComposeRule` | `PreviewRenderer` (rendu headless) | [Tester](testing.md) |
| `Modifier.semantics` | Libellé d’accessibilité sur les champs ; reste à venir | [Tester §11](testing.md#11-limites-actuelles) |

## 3. Mise en page et modificateurs

| Compose | XenoUI |
|---|---|
| `Column { }` / `Row { }` | `Column(…)` / `Row(…)` |
| `Box { }` (pile) | `ZStack(…)` (et `Box(w, h, …)` pour un rectangle-conteneur) |
| `Spacer(Modifier.weight(1f))` | `Spacer()` |
| `Modifier.weight(1f)` | `.FlexGrow(1)` |
| `Modifier.fillMaxWidth()` | `.CrossAxis(Stretch)` (parent) ou `.AlignSelf(Stretch)` |
| `Modifier.fillMaxSize()` | `.FlexGrow(1)` + `Stretch` |
| `Modifier.size(48.dp)` / `width` / `height` | `.Size(48, 48)` / `.Width` / `.Height` |
| `Modifier.padding(8.dp)` | `.Padding(8)` / `.Padding(h, v)` / `.Padding(Spacing…)` |
| Marge extérieure (padding avant `background`) | `.Margin(…)` |
| `Arrangement.spacedBy(8.dp)` | `.Gap(8)` |
| `Arrangement.SpaceBetween` | `.MainAxis(MainAxisAlignment.SpaceBetween)` |
| `Alignment.CenterVertically` | `.CrossAxis(CrossAxisAlignment.Center)` |
| `Modifier.align(Alignment.End)` | `.AlignSelf(CrossAxisAlignment.End)` |
| `contentAlignment = Center` | `.CenterContent()` |
| `Modifier.background(color)` | `.Background(0xAARRGGBB)` |
| `Modifier.clip(RoundedCornerShape(12.dp))` | `.CornerRadius(12)` |
| `Modifier.clip(CustomShape)` | `.Shape(XenoShape…)` |
| `Modifier.shadow(8.dp)` / `Card(elevation)` | `.Elevation(8)` / `.Shadow(x, y, blur, color)` |
| `Modifier.clickable { }` | `.Clickable(() => …)` |
| `Modifier.verticalScroll(state)` | `.Scrollable(scroll)` |
| `Modifier.alpha(0.5f)` | Alpha de la couleur ; sinon scène ([Animations §4](animations.md#4-opacité-et-fondu-dun-composant)) |
| `Modifier.rotate / scale / offset` | Pistes de transition de scène ; pas de modificateur par entité |
| `Text(fontSize, color, maxLines, overflow)` | `Text(s, size).TextColor(c).MaxLines(n, ellipsis: true)` |
| `Image(painter)` / `contentScale` | `Image("asset")` / `.ScaleMode(Cover/Fit/Fill/None)` |
| `Icon(imageVector)` | `Icon(IconDef, size)` (glyphe de police) |
| `LazyColumn { items(list, key) }` | `LazyColumn(list, item => …, key: …)` |
| `rememberLazyListState()` | `UseLazyList()` |
| `VerticalPager` | `VerticalPager(items, (item, i) => …)` |
| `rememberScrollState()` | `UseScroll()` |
| `Modifier.fillMaxHeight().width(1.dp).background(c)` (diviseur) | `Box(1, 0).Background(c)` ou `Ui.Divider()` |

## 4. Composants Material et widgets

| Compose / Material | XenoUI |
|---|---|
| `Button`, `TextButton` | `Button(label, tap)`, `TextButton(label, tap)` |
| `FloatingActionButton` | `NavigationFab.Standard/Extended` (scaffold) |
| `Scaffold`, `TopAppBar` | `NavigationScaffoldState` + `ITopBarPage` / `TopBarOptions` |
| `NavigationBar`, `NavigationRail`, `ModalNavigationDrawer` | Slots `BottomBar`, `NavigationRail`, `Drawer` du scaffold |
| `SnackbarHost` | `Snackbar.Show(app, message, options)` |
| `AlertDialog` | `Alert.Show(…)` / `Dialog` |
| `ModalBottomSheet` | `BottomSheet.Show(…)` |
| `DropdownMenu` | `Popup` (ancré) |
| `TextField` / `OutlinedTextField` | `TextField(controller, placeholder, InputOptions(…))`, `TextArea` |
| `Switch`, `Checkbox`, `Slider`, `Card`, `Chip` | À composer : [recettes](components.md#15-recettes-complètes) (pas de widget prêt) |
| `CircularProgressIndicator` | À composer ; `LinearProgress(State<float>)` existe |
| `AnimatedVisibility`, `Crossfade` | Condition + `Rebuild()` ; `NavTransitions.FadeThrough` pour pages |
| `animate*AsState` | Interpolation d’application ([Animations §5](animations.md#5-animer-soi-même-pas-à-pas)) |
| `rememberInfiniteTransition` | Minuteur applicatif |

## 5. Navigation

| Jetpack Navigation / Compose | XenoUI |
|---|---|
| `NavHost` + routes | `Page<T>()` enregistrées dans `OnStart` |
| `navController.navigate("detail/42")` | `Navigate<DetailsPage, DetailsArgs>(new(42))` |
| Arguments de route | `XenoPage<TArguments>` + `OnNavigatedTo(args)` |
| `popBackStack()` | `GoBack()` |
| `popUpTo` | `ReselectionBehavior = PopToRoot` |
| `NavigationBar` + back stack par onglet | `NavigationScaffoldState` : une pile par destination |
| `BackHandler` | Retour système géré par le scaffold ; `CanGoBack` |
| Transitions `enterTransition` / `exitTransition` | `NavTransition` (`Enter` / `Exit`) |
| Deep links | Non fournis ([Limites V1](navigation-scaffold.md#limites-v1)) |

## 6. Un même écran, écrit dans les deux

**Jetpack Compose**

```kotlin
@Composable
fun CounterScreen(viewModel: CounterViewModel = viewModel()) {
    val count by viewModel.count.collectAsStateWithLifecycle()
    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Total : $count", fontSize = 24.sp)
        Button(onClick = viewModel::increment) { Text("+1") }
    }
}
```

**XenoUI**

```csharp
public sealed class CounterPage : XenoPage
{
    private readonly State<int> _count = UseState(0);                 // remember { mutableStateOf }

    public override XenoComponent Body() =>
        Column(
            Text($"Total : {_count}", 24),                            // lié : se met à jour seul
            Button("+1", () => _count.Value++)
        ).Gap(12).Padding(16);
}
```

Avec un modèle (équivalent du `ViewModel`) :

```csharp
public sealed class CounterPage : XenoPage
{
    private CounterModel _model = null!;
    public override void OnCreate()    { _model = ((App)Application).Counter; _model.Changed += OnChanged; }
    public override void OnDestroy()   => _model.Changed -= OnChanged;
    private void OnChanged()           { if (IsActive) Rebuild(); }

    public override XenoComponent Body() =>
        Column(Text($"Total : {_model.Count}", 24), Button("+1", _model.Increment)).Gap(12).Padding(16);
}
```

## 7. Habitudes à désapprendre

| Habitude Compose | En XenoUI |
|---|---|
| « L’état lu dans le composable se suit tout seul » | Lier explicitement (`Text(state)`) ou appeler `Rebuild()` |
| « J’appelle `remember` où je veux » | Créer les `State` dans des **champs** ; jamais dans `Body()` |
| « Les lambdas capturent l’état courant » | Idem, mais un `Rebuild()` recrée l’arbre : lisez des champs au moment de l’exécution |
| « La recomposition saute ce qui n’a pas changé » | `Rebuild()` recompose la page entière ; granularité = vos `State` |
| « Je passe un `Modifier` en paramètre » | Le composant retourne un `XenoComponent` : l’appelant chaîne ses modificateurs sur le résultat |
| « `MaterialTheme` fournit les couleurs » | Votre classe `Theme`, appliquée explicitement |
| « `LaunchedEffect` démarre quand le composable entre » | Démarrer dans `OnActivated` ; pas d’effet à la composition |
| « `dp`/`sp` sont des types » | Tout est en dp (floats) ; polices en dp |
| « Modificateur après modificateur = ordre significatif (`padding` avant `background`) » | Les modificateurs écrivent dans l’entité ; **l’ordre n’a pas d’effet** sauf écrasement de la même propriété |

## 8. Ce qui n’existe pas (encore)

Pour planifier honnêtement : pas de restauration d’état après mort du processus, pas de deep links, pas de
résultats typés de navigation, pas de shared element transitions, pas de modificateur d’animation générique ni
de ressorts publics, pas de `CompositionLocal`, pas de harnais de test d’interaction, pas d’arbre sémantique pour
les éléments dessinés, pas de widgets Material prêts pour `Switch`/`Checkbox`/`Slider`. Le framework est
**pré-V1** : voir [Décisions et trajectoire V1](architecture/decisions-v1.md#3-dette-technique-et-risques).
