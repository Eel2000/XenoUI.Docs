# Architecture d’une application

[Documentation](index.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [État](state.md) · [Effets de bord](side-effects.md) · [Apprendre par le SampleApp](sample-app.md)

Ce guide propose une **découpe en couches** pour une application XenoUI, celle que suit le `SampleApp` :
une interface qui affiche un état et remonte des événements, des modèles de présentation qui détiennent cet
état, et des services portables qui font le travail. Il ne s’agit pas d’imposer un framework : XenoUI ne fournit
ni conteneur d’injection ni `ViewModel` ; il fournit des **points d’attache** (cycle de vie, dispatcher,
paramètres de navigation) sur lesquels cette architecture s’appuie. Pour l’architecture *interne* du framework,
voir [Architecture](architecture/index.md).

> Points d’extension autorisés du framework : [Décisions §6](architecture/decisions-v1.md#6-points-dextension-autorisés).

## Sommaire

1. [Les trois couches](#1-les-trois-couches)
2. [Flux de données](#2-flux-de-données)
3. [Couche services](#3-couche-services)
4. [Couche modèles de présentation](#4-couche-modèles-de-présentation)
5. [Couche interface](#5-couche-interface)
6. [Assemblage et injection](#6-assemblage-et-injection)
7. [Navigation et paramètres](#7-navigation-et-paramètres)
8. [Organisation des fichiers](#8-organisation-des-fichiers)
9. [Tester chaque couche](#9-tester-chaque-couche)
10. [Erreurs d’architecture courantes](#10-erreurs-darchitecture-courantes)
11. [Correspondance avec l’architecture Android](#11-correspondance-avec-larchitecture-android)

---

## 1. Les trois couches

```text
┌────────────────────────────────────────────────────────────┐
│ Interface           Pages · Composants · Design            │  XenoUI, Skia
│   affiche un état, appelle des Action                       │
├────────────────────────────────────────────────────────────┤
│ Modèles de présentation   RemoteFeed<T> · ArticleDetailsModel│  portable
│   détiennent l’état, orchestrent, publient des snapshots    │
├────────────────────────────────────────────────────────────┤
│ Services            ArticleService · dépôts · stockage      │  portable
│   HTTP, JSON, disque ; aucune notion d’interface            │
└────────────────────────────────────────────────────────────┘
```

| Couche | Connaît XenoUI ? | Connaît la plateforme ? | Où |
|---|---|---|---|
| Interface | Oui | Via `Native` seulement | `Pages/`, `Components/`, `Design/` |
| Modèles de présentation | **Non** (reçoivent un `Action<Action>` pour revenir sur le thread UI) | Non | `Services/` du sample |
| Services | Non | Non | `Services/` |

La frontière « un type de la couche basse ne référence jamais la couche haute » rend les deux couches du bas
testables par un simple projet `net11.0`.

## 2. Flux de données

```text
Utilisateur ─ toucher ─▶ Composant ─ Action ─▶ Page ─▶ Modèle ─▶ Service
                                                       │            │
                          Rebuild() / State.Value ◀────┴─ snapshot ◀─┘ (thread de fond → dispatcher → UI)
```

1. **Les événements remontent** : un composant appelle un `Action` ; la page le traduit en appel au modèle.
2. **Le modèle** appelle un service, sur un thread de fond, puis **publie un nouvel état** (snapshot immuable).
3. **L’état redescend** : la page reçoit la notification sur le thread UI et met à jour l’écran (`State.Value =` ou
   `Rebuild()`).

Une seule source de vérité par donnée ; l’interface n’en garde **que** l’état purement visuel (onglet ouvert,
position de défilement, texte en cours de saisie).

## 3. Couche services

Un service est une classe **sans dépendance d’interface** : elle prend des paramètres, retourne des valeurs
(ou des `Task`), accepte un `CancellationToken`.

```csharp
public interface IArticleService
{
    Task<Article[]> GetArticlesAsync(CancellationToken cancellationToken);
}

public sealed class ArticleService(HttpClient client) : IArticleService
{
    public async Task<Article[]> GetArticlesAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await client.GetAsync(Endpoint, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        /* … désérialisation source-generated, validation à la frontière … */
    }
}
```

Bonnes pratiques (toutes dans le sample) : **interface + implémentation** pour le test ; **un `HttpClient`
réutilisé** et possédé par l’application ; **JSON source-generated** (pas de réflexion à l’exécution) ; **délai
total** incluant la lecture du corps ; **validation** des données reçues (identifiants uniques pour des clés de
liste stables) ; erreurs **typées** converties en messages plus haut.

## 4. Couche modèles de présentation

Le modèle détient l’état d’un flux ou d’un écran et le **publie sous forme de snapshot immuable**. Équivalent
du `ViewModel` + `StateFlow` côté Android.

```csharp
public enum FeedStatus { Idle, Loading, Ready, Error }
public sealed record RemoteSnapshot<T>(FeedStatus Status, T[] Items, string? Error = null);

public class RemoteFeed<T>(Func<CancellationToken, Task<T[]>> load, Action<Action> post, Action changed)
    : IDisposable
{
    public RemoteSnapshot<T> Snapshot { get; }       // lu par la page
    public void Activate();                          // charge si nécessaire
    public void Refresh();                           // annule la précédente, relance
    public void Deactivate();                        // annule, garde les données
    public void Dispose();
}
```

Ce que ce modèle garantit, et que vous devriez garantir à votre tour :

- **Idempotence** de `Activate()` (« déjà chargé ? » : rien à faire) ;
- **annulation** de la requête précédente et **numéro de version** : un résultat périmé est ignoré ;
- **les anciennes données restent** pendant un rechargement ou après une erreur ;
- la **publication** passe par `post` (le dispatcher injecté) puis le callback `changed` ;
- **aucun type XenoUI** : seulement `Action<Action>` pour revenir sur le thread UI.

## 5. Couche interface

La page **observe** le modèle et **déclare** l’interface :

```csharp
public sealed class ApiPage : XenoPage, ITopBarPage
{
    private ArticleFeed _feed = null!;
    private volatile bool _destroyed;

    public override void OnCreate()
    {
        var dispatcher = Native.Require<IUiDispatcherCapability>();
        _feed = new ArticleFeed(new ArticleService(((App)Application).ApiClient), dispatcher.Post, () =>
        {
            if (_destroyed || !IsActive) return;
            Rebuild();
            RebuildTopBar();
        });
    }
    public override void OnActivated()   => _feed.Activate();
    public override void OnDeactivated() => _feed.Deactivate();
    public override void OnDestroy()     { _destroyed = true; _feed.Dispose(); }

    public override XenoComponent Body()
    {
        var state = _feed.Snapshot;                         // lecture seule
        return Column(StatusBar(state), LazyColumn(state.Items, ArticleCard, key: a => a.Id));
    }
}
```

La page :

- **ne contient aucune logique métier** (pas de HTTP, pas de JSON) ;
- **traduit** l’état du modèle en interface (`FeedStatus.Error` → texte et couleur) ;
- **traduit** les événements de l’interface en appels au modèle (`Refresh`) ;
- garde seulement l’état d’**affichage** (scroll, onglet interne, texte en saisie).

Les composants réutilisables, eux, ne connaissent **ni modèle ni page** : valeurs, `State<T>` et `Action`
([Créer un composant](components.md)).

## 6. Assemblage et injection

XenoUI n’impose pas de conteneur d’injection. Le motif du sample est une **composition racine** manuelle dans
`App` :

```csharp
public class App : XenoApp
{
    internal HttpClient ApiClient { get; } = new();                   // possédé par l’application
    internal ArticleDetailsModel ArticleDetails { get; private set; } = null!;

    public override void OnStart()
    {
        ArticleDetails = new ArticleDetailsModel(new ArticleService(ApiClient),
            Native.Require<IUiDispatcherCapability>().Post);          // dépendances explicites
        Page<HomePage>(); /* … */
        UseNavigationScaffold(_navigation);
    }

    public override void OnDispose() { ArticleDetails?.Dispose(); ApiClient.Dispose(); }
}
```

- **Une seule racine de composition** : `App.OnStart`. Les pages récupèrent ce qu’elles utilisent par `(App)Application`.
- **Dépendances par constructeur** pour modèles et services ; **interfaces** là où vous voulez substituer en test.
- **Durée de vie** : ce que possède `App` vit toute l’application ; ce que crée une page vit autant qu’elle
  (`OnCreate` → `OnDestroy`).
- Si l’application grandit, remplacez le cast `(App)Application` par une **interface de services** exposée
  par `App` ; un conteneur d’injection reste possible (rien dans XenoUI ne l’interdit), mais il doit être
  construit dans `OnStart`.

Services de la plateforme (`NativeTunnel`) : `Native.Require<T>()` (échoue s’il manque) et `Native.Get<T>()`
(`null` si absent), par exemple `IUiDispatcherCapability`, `IMotionPreferencesCapability`,
`IMediaPlaybackCapability`. Ils sont lus dans la couche interface et **injectés** plus bas sous forme de
simples délégués.

## 7. Navigation et paramètres

- **Navigation** : appelée depuis la page (`Navigate<T>()`), jamais depuis un modèle ou un composant. Le
  composant reçoit un `Action`.
- **Paramètres typés** : `XenoPage<TArgs>` + `Navigate<TPage, TArgs>(args)` ; la page transmet au modèle dans
  `OnNavigatedTo(args)`.
- **Intentions de l’application** : des méthodes de `App` (`SelectTab`, `OpenArticle`, `OpenFeedClip`) évitent
  que les pages connaissent le scaffold :

```csharp
internal void OpenArticle(Article article) =>
    Navigate<ArticleDetailsPage, ArticleDetailsArgs>(new(article), NavTransitions.SlideHorizontal(280));
```

- **Résultats** : XenoUI ne fournit pas de résultat typé de navigation ([Limites V1](navigation-scaffold.md#limites-v1)) ;
  passez par un modèle partagé ou un `Action` dans les paramètres.

## 8. Organisation des fichiers

```text
MyApp/
├─ App.cs                       composition racine, scaffold, cycle de vie
├─ Pages/                       une XenoPage par écran (+ args)
├─ Components/                  composants réutilisables, un fichier chacun
├─ Design/                      Theme, Ui (briques d’application), AppChrome
├─ Models/                      modèles de présentation (flux, sélection)
└─ Services/                    services portables (HTTP, stockage)
```

Le sample regroupe modèles et services dans `Services/` ; séparez-les dès que le projet grossit. Si vous voulez
tester modèles et services sans la plateforme, placez-les dans une **bibliothèque `net11.0`** référencée par
l’application.

## 9. Tester chaque couche

| Couche | Comment | Voir |
|---|---|---|
| Services | Test unitaire avec un `HttpMessageHandler` factice | `tests/SampleApp.Api.RegressionTests` |
| Modèles | Test unitaire : dispatcher factice (`a => a()`), snapshots, annulation, résultats hors ordre | idem |
| Composants / pages | Rendu headless en PNG (`PreviewRenderer.Render`) | [Tester](testing.md) |
| Intégration appareil | Manuel / outil `xeno` | [Dépannage](troubleshooting.md) |

## 10. Erreurs d’architecture courantes

| Erreur | Conséquence | Correction |
|---|---|---|
| HTTP et JSON dans la page | Intestable ; réseau déclenché par la composition | Service + modèle |
| Le modèle référence `XenoPage` ou `Rebuild` | Couplage ; pas de test sans UI | Callback `changed` injecté |
| État dupliqué (modèle et page) | Écrans incohérents | Le modèle publie, la page lit |
| Un composant qui navigue | Composant non réutilisable | Il reçoit un `Action` |
| Singleton statique global | Fuites entre tests ; durée de vie floue | Instance possédée par `App` |
| Travail dans `Body()` | Répété à chaque reconstruction | `OnActivated` / modèle |
| Mutation du snapshot | Courses entre threads | Snapshots immuables (`record`, tableaux) |

## 11. Correspondance avec l’architecture Android

| Android (guide d’architecture) | XenoUI |
|---|---|
| UI layer (Composables) | Pages et composants |
| `ViewModel` | Modèle de présentation (`RemoteFeed<T>`, `ArticleDetailsModel`) |
| `StateFlow<UiState>` | `Snapshot` immuable + callback `changed` |
| `collectAsStateWithLifecycle` | Abonnement `OnCreate`/`OnDestroy` + `IsActive` + `Rebuild()` |
| Data layer / Repository | Services (`ArticleService`) |
| Hilt / injection | Composition racine dans `App.OnStart` |
| `viewModelScope` | `CancellationTokenSource` du modèle, annulé dans `Deactivate` |
| `SavedStateHandle` | Non fourni (pas de restauration après mort du processus) |
| Unidirectional data flow | Idem : événements remontent, état redescend |
