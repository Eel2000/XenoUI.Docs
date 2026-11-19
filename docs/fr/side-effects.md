# Effets de bord et travail asynchrone

[Documentation](index.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [État](state.md) · [Cycle de vie](lifecycle.md) · [Architecture](app-architecture.md)

Un **effet de bord** est tout ce qui sort du calcul pur d’une interface : un appel réseau, une lecture de
fichier, un minuteur, un abonnement à un événement, un lecteur audio. `Body()` et les composants ne doivent
**jamais** en produire. Ce guide montre où les placer, comment ramener leurs résultats sur le thread UI, et
comment éviter les écritures tardives sur une page qui n’est plus à l’écran.

Si vous venez de Jetpack Compose, la [table de correspondance](#1-correspondance-avec-les-effets-de-compose)
indique l’équivalent de `LaunchedEffect`, `DisposableEffect`, `rememberCoroutineScope`…

> Pour les règles de threading du moteur lui-même : [Runtime §11](architecture/runtime.md#11-threading-snapshots-et-concurrence).

## Sommaire

1. [Correspondance avec les effets de Compose](#1-correspondance-avec-les-effets-de-compose)
2. [Règle d’or : pas d’effet dans `Body()`](#2-règle-dor--pas-deffet-dans-body)
3. [Threads : qui peut faire quoi](#3-threads--qui-peut-faire-quoi)
4. [Démarrer et annuler du travail avec la page](#4-démarrer-et-annuler-du-travail-avec-la-page)
5. [S’abonner et se désabonner](#5-sabonner-et-se-désabonner)
6. [Résultats tardifs : `IsActive`, versions et numéros de série](#6-résultats-tardifs--isactive-versions-et-numéros-de-série)
7. [Minuteurs et « après N ms »](#7-minuteurs-et--après-n-ms-)
8. [Un flux distant complet](#8-un-flux-distant-complet)
9. [Arrière-plan](#9-arrière-plan)
10. [Pièges](#10-pièges)

---

## 1. Correspondance avec les effets de Compose

| Compose | XenoUI |
|---|---|
| `LaunchedEffect(key)` | Démarrer dans `OnActivated()` (ou `OnNavigatedTo`) ; annuler dans `OnDeactivated()` |
| `DisposableEffect` | S’abonner dans `OnCreate()`, se désabonner dans `OnDestroy()` ; ou paire `OnActivated`/`OnDeactivated` |
| `rememberCoroutineScope()` | Un `CancellationTokenSource` en champ de page |
| `produceState` / `collectAsState` | Un objet qui publie un snapshot + callback `changed` → `Rebuild()` ou `State.Value = …` ([§8](#8-un-flux-distant-complet)) |
| `snapshotFlow { state }` | `State<T>.OnChanged` (événement) |
| `rememberUpdatedState` | Lire un **champ** de page dans le rappel, au moment de son exécution |
| `SideEffect` | Pas d’équivalent : l’interface n’a pas d’effet à la composition |
| `rememberCoroutineScope` + `delay` | `Task.Delay(ms)` puis `Post` ([§7](#7-minuteurs-et--après-n-ms-)) |

## 2. Règle d’or : pas d’effet dans `Body()`

`Body()` est rappelée à chaque `Rebuild()` et dans les aperçus. Un effet dedans serait répété, et exécuté même en
design.

```csharp
// ✗ À ne pas faire
public override XenoComponent Body()
{
    _ = LoadAsync();                       // relancé à chaque reconstruction
    File.WriteAllText(path, text);         // effet de bord pendant la composition
    return Text(_status, 14);
}

// ✓ Le travail démarre au cycle de vie, la composition ne fait que lire
public override void OnActivated() => _feed.Activate();
public override XenoComponent Body() => Text(_status, 14);
```

## 3. Threads : qui peut faire quoi

| Opération | Thread |
|---|---|
| `Body()`, callbacks de `Clickable`, `OnActivated`… | Thread UI / de contrôle |
| Affecter un `State<T>`, un `TextEditController`, un `ScrollState` | **Thread UI uniquement** |
| `Rebuild()` | Tout thread : elle se **replanifie** sur le thread de contrôle |
| Réseau, JSON, disque, calcul lourd | **Thread de fond** (`Task.Run`) |

Pour revenir sur le thread UI depuis un thread de fond, injectez le **dispatcher** :

```csharp
var ui = Native.Require<IUiDispatcherCapability>();     // dans OnCreate()
…
_ = Task.Run(async () =>
{
    var data = await service.GetAsync(token);           // fond
    ui.Post(() =>                                       // retour thread UI
    {
        _items = data;
        Rebuild();
    });
});
```

Le sample passe `dispatcher.Post` à ses services : **le service ne connaît ni XenoUI ni la plateforme**, il
reçoit simplement un `Action<Action>`.

## 4. Démarrer et annuler du travail avec la page

Un champ `CancellationTokenSource` joue le rôle du scope :

```csharp
public sealed class SearchPage : XenoPage
{
    private CancellationTokenSource? _search;
    private readonly State<string> _status = UseState("");
    private IUiDispatcherCapability _ui = null!;

    public override void OnCreate() => _ui = Native.Require<IUiDispatcherCapability>();

    public override void OnDeactivated() => _search?.Cancel();    // quitter l’écran annule

    private void Search(string query)
    {
        _search?.Cancel();                                        // une seule recherche à la fois
        var source = _search = new CancellationTokenSource();
        _status.Value = "Recherche…";
        _ = Task.Run(async () =>
        {
            try
            {
                var results = await _service.SearchAsync(query, source.Token);
                _ui.Post(() => { if (!source.IsCancellationRequested) { _results = results; _status.Value = ""; Rebuild(); } });
            }
            catch (OperationCanceledException) { }
        });
    }
}
```

Points clés : annuler le précédent **avant** de démarrer ; ignorer le résultat si l’annulation a été demandée ;
ne jamais laisser une tâche écrire dans un `State` sans repasser par `Post`.

## 5. S’abonner et se désabonner

Tout abonnement à un événement d’un objet qui vit plus longtemps que la page doit être défait :

```csharp
public override void OnCreate()  { _model = ((App)Application).ArticleDetails; _model.Changed += OnChanged; }
public override void OnDestroy() { _destroyed = true; _model.Changed -= OnChanged; }

private void OnChanged() { if (!_destroyed && IsActive) { Rebuild(); RebuildTopBar(); } }
```

C’est le motif d’`ArticleDetailsPage`. Variantes :

| Source | Abonnement |
|---|---|
| Modèle de l’application (`Changed`) | `OnCreate` / `OnDestroy` |
| Lecteur média (`player.Changed`) | `OnActivated` (se réabonner sans doublon : `-=` puis `+=`) / `OnDeactivated` |
| `TextEditController.Changed` | Constructeur de la page (même durée de vie que le contrôleur) |
| `State<T>.OnChanged` | Idem : même durée de vie que les deux objets |

## 6. Résultats tardifs : `IsActive`, versions et numéros de série

Une réponse peut arriver après que l’utilisateur a quitté l’écran, relancé une recherche ou changé d’élément.
Trois gardes complémentaires :

1. **`IsActive`** : n’agir que si la page est au premier plan.
2. **Un drapeau `_destroyed`** (volatile) posé dans `OnDestroy`.
3. **Un numéro de version / de série** incrémenté à chaque nouvelle demande ; le résultat n’est publié que si
   son numéro est encore le courant :

```csharp
int serial = ++_toastSerial;
After(1600, () =>
{
    if (serial != _toastSerial) return;       // un toast plus récent a pris le relais
    _toast.Value = "";
});
```

`RemoteFeed<T>` (sample) applique les trois : il vérifie `_disposed`, `_active` et `version` **deux fois** (avant et
dans le `Post`), car l’état peut changer entre les deux.

## 7. Minuteurs et « après N ms »

Il n’y a pas de minuteur intégré à la composition. Le motif du sample :

```csharp
private Action<Action>? _post;                         // = Native.Require<IUiDispatcherCapability>().Post

private void After(int milliseconds, Action action)
{
    var post = _post;
    if (post == null) return;
    _ = Task.Delay(milliseconds).ContinueWith(_ => post(action), TaskScheduler.Default);
}
```

Associez toujours un numéro de série pour que deux minuteurs successifs ne se croisent pas. Pour des animations
par pas (interpolation de couleur, progression), voir [Animations §5](animations.md#5-animer-soi-même-pas-à-pas).

## 8. Un flux distant complet

Le sample sépare trois rôles ; reprenez-les tels quels :

```csharp
// 1. Service portable : aucune dépendance UI
public interface IArticleService { Task<Article[]> GetArticlesAsync(CancellationToken ct); }

// 2. Modèle de présentation : snapshot immuable + événement, dispatcher injecté
public sealed record RemoteSnapshot<T>(FeedStatus Status, T[] Items, string? Error = null);
public class RemoteFeed<T>(Func<CancellationToken, Task<T[]>> load, Action<Action> post, Action changed)
{
    public RemoteSnapshot<T> Snapshot { get; }
    public void Activate(); public void Deactivate(); public void Refresh(); // annulation + versions
}

// 3. Page : cycle de vie + rendu
public override void OnCreate()
{
    _feed = new ArticleFeed(service, Native.Require<IUiDispatcherCapability>().Post, () =>
    {
        if (_destroyed || !IsActive) return;
        Rebuild(); RebuildTopBar();
    });
}
public override void OnActivated()   => _feed.Activate();
public override void OnDeactivated() => _feed.Deactivate();
public override void OnDestroy()     { _destroyed = true; _feed.Dispose(); }
public override XenoComponent Body() => /* lit _feed.Snapshot et affiche Loading / Ready / Error */;
```

Le snapshot garde les **anciennes données** pendant un rafraîchissement ou après une erreur, ce qui évite de
vider l’écran. Détails : [Démonstration API](sample-api.md).

## 9. Arrière-plan

Le système peut suspendre l’application à tout moment. Le framework ne suspend rien à votre place :

```csharp
public override void OnBackground() { if (ActivePage is VideoLabPage v) v.Suspend(); }
public override void OnForeground() { if (ActivePage is VideoLabPage v) v.Resume(); }
```

`Suspend()` annule ou met en pause (lecteur, requêtes) ; `Resume()` reprend sans perdre la position. Voir
[Cycle de vie §6](lifecycle.md#6-arrière-plan-et-premier-plan).

## 10. Pièges

| Piège | Symptôme | Correction |
|---|---|---|
| Effet dans `Body()` | Requêtes répétées, aperçu qui plante | Cycle de vie de la page |
| Affecter un `State` depuis `Task.Run` | Corruption visuelle | `ui.Post(...)` |
| Pas d’annulation en quittant l’écran | Travail et batterie gaspillés, écritures tardives | `OnDeactivated` annule |
| Abonnement jamais retiré | Fuite, appels sur une page détruite | `OnDestroy` désabonne |
| Résultat appliqué sans garde | Ancien résultat qui écrase le nouveau | Version / série / `IsActive` |
| `async void` qui lève | Plantage du processus | `Task` + `try/catch` aux frontières |
| Travail long dans un callback `Clickable` | Interface qui fige | `Task.Run` puis `Post` |
