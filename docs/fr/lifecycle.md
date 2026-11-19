# Cycle de vie

[Documentation](index.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [État](state.md) · [Effets de bord](side-effects.md) · [Navigation](navigation-scaffold.md)

Ce guide décrit **quand** le code de votre application s’exécute : au démarrage, quand une page apparaît ou
disparaît, quand l’application passe en arrière-plan, et quand `Body()` est appelée. Savoir où placer chaque
travail (créer un modèle, démarrer une requête, libérer un lecteur) évite la plupart des fuites et des
écritures tardives. Pour la vue interne du moteur, voir [Runtime §9–10](architecture/runtime.md#9-état-et-durée-de-vie) ;
la note [Cycle de vie historique](app-lifecycle.md) est conservée pour traçabilité.

## Sommaire

1. [Les trois niveaux](#1-les-trois-niveaux)
2. [Cycle de vie de l’application](#2-cycle-de-vie-de-lapplication)
3. [Cycle de vie d’une page](#3-cycle-de-vie-dune-page)
4. [Quand `Body()` est-elle appelée ?](#4-quand-body-est-elle-appelée-)
5. [Navigation et rétention](#5-navigation-et-rétention)
6. [Arrière-plan et premier plan](#6-arrière-plan-et-premier-plan)
7. [Où placer quoi](#7-où-placer-quoi)
8. [Composants : pas de cycle de vie](#8-composants--pas-de-cycle-de-vie)
9. [Frise d’exemple](#9-frise-dexemple)

---

## 1. Les trois niveaux

| Niveau | Type | Crochets |
|---|---|---|
| Application | `XenoApp` | `OnStart`, `OnForeground`, `OnBackground`, `OnLowMemory`, `OnDispose` (et `OnPreview` en aperçu) |
| Page | `XenoPage` | `OnCreate`, `OnActivated`, `OnDeactivated`, `OnDestroy` (et `OnNavigatedTo(args)` pour `XenoPage<T>`) |
| Composant | fonction | **aucun** : c’est une fonction appelée par `Body()` |

## 2. Cycle de vie de l’application

```text
démarrage de l’hôte
  └─ OnStart()               enregistrer les pages, créer modèles et services
       └─ Page<T>() × N      chaque page est instanciée une fois → page.OnCreate()
       └─ UseNavigationScaffold(...)
  └─ première composition (destination initiale : Body() puis OnActivated())
  …
OnBackground()  ⇄  OnForeground()      l’app quitte / retrouve le premier plan
  …
OnLowMemory()                          pression mémoire signalée par le système
OnDispose()                            fin : libérer lecteurs, clients, modèles ; page.OnDestroy() de chaque page
```

```csharp
public class App : XenoApp
{
    public override void OnStart()
    {
        ApiClient = new HttpClient();
        ArticleDetails = new ArticleDetailsModel(new ArticleService(ApiClient),
            Native.Require<IUiDispatcherCapability>().Post);
        Page<HomePage>();
        Page<SettingsPage>();
        UseNavigationScaffold(_navigation);
    }

    public override void OnBackground() { if (ActivePage is FeedPage feed) feed.Suspend(); }
    public override void OnForeground() { if (ActivePage is FeedPage feed) feed.Resume(); }
    public override void OnDispose()    { ApiClient.Dispose(); }
}
```

Points d’attention :

- **`OnStart` est le bon endroit** pour construire ce que plusieurs pages partagent (client HTTP, modèles).
  Pas de travail bloquant long : l’application n’affiche rien tant qu’il dure.
- **`Page<T>()` appelle `OnCreate()` immédiatement.** Les pages enregistrées dans `OnStart` sont donc toutes créées
  dès le démarrage, même celles que l’utilisateur n’ouvrira jamais : gardez `OnCreate` léger.
- **En aperçu** (`[Preview]`, VS / VS Code), l’application est *construite* mais pas *démarrée* : `OnStart` ne
  s’exécute pas, `OnPreview()` si. N’y mettez que ce que les pages lisent, sans réseau
  ([sample](sample-app.md#41-il-déclare-le-scaffold-de-navigation)).
- `OnLowMemory` : libérez les caches reconstructibles (images décodées, résultats en mémoire).

## 3. Cycle de vie d’une page

```csharp
public sealed class ApiPage : XenoPage, ITopBarPage
{
    public override void OnCreate()      { /* une fois : créer le flux, s’abonner */ }
    public override void OnActivated()   { /* à chaque fois que la page devient visible */ }
    public override void OnDeactivated() { /* quand elle cesse de l’être : annuler, suspendre */ }
    public override void OnDestroy()     { /* une fois : se désabonner, libérer */ }
}
```

| Crochet | Quand | À y faire | À ne pas y faire |
|---|---|---|---|
| `OnCreate` | Une fois, à `Page<T>()` | Créer des objets internes, s’abonner à un modèle | Charger des données, démarrer un lecteur |
| `OnNavigatedTo(args)` | À l’arrivée sur `XenoPage<T>` avec ses paramètres | Transmettre la sélection au modèle | Composer l’interface |
| `OnActivated` | À chaque affichage (après le commit de navigation) | Démarrer/reprendre le travail : requête, lecteur | Opérations longues sur le thread UI |
| `OnDeactivated` | Quand la page n’est plus l’active | Annuler requêtes, suspendre lecteurs | Libérer pour toujours (elle reviendra) |
| `OnDestroy` | À la fin de l’application | Se désabonner, libérer | Supposer qu’elle précède chaque navigation |

Deux précisions importantes :

- Les **pages sont des singletons** : instanciées une fois, réutilisées. `OnDestroy` n’est pas appelée à chaque
  retour en arrière. Ne l’utilisez pas pour « nettoyer à la sortie de l’écran » : c’est le rôle d’`OnDeactivated`.
- Les rappels de page ne changent qu’au **commit de la navigation** : une transition en cours n’est pas un état
  de cycle de vie.

### `IsActive`

`IsActive` est vrai si la page est celle du premier plan. Testez-le dans tout rappel qui peut arriver après coup
(réponse réseau, minuteur) avant de modifier l’interface ([Effets de bord](side-effects.md)).

## 4. Quand `Body()` est-elle appelée ?

| Situation | `Body()` appelée ? |
|---|---|
| Première apparition d’une page racine de destination (composition paresseuse) | Oui, une fois |
| Retour sur une destination déjà visitée (onglet) | **Non** : la scène est conservée |
| Push d’une page de détail | Oui (nouvelle scène montée au-dessus) |
| Pop | Non pour la page révélée ; la scène retirée est libérée |
| `Rebuild()` de la page | Oui, et sa scène est remplacée |
| Changement d’un `State` lié (`Text(state)`) | **Non** |
| Rotation, changement de taille | Non (mise en page seulement) |
| Aperçu | Oui, pour le rendu |

Conséquence : n’attendez pas que `Body()` s’exécute « à chaque fois que la page apparaît ». Pour un travail à
chaque apparition, utilisez **`OnActivated`**.

Les composants (`Chip.View(...)`) sont appelés **chaque fois que leur appelant** l’est.

## 5. Navigation et rétention

- **Onglets / destinations du scaffold** : conservent arbre ECS, scroll, fenêtre de liste et contrôleurs de
  texte. Quitter un onglet appelle `OnDeactivated`, pas `OnDestroy`.
- **Pile d’un onglet** : `Navigate<T>()` empile ; `GoBack()` dépile. `ReselectionBehavior = PopToRoot` revient à
  la racine d’un onglet quand on le retouche.
- **Retour système** : Android 7–12 via `OnBackPressed`, Android 13+ via `OnBackInvokedDispatcher` (enregistré
  seulement quand `CanGoBack`). `CanGoBack` tient compte des overlays modaux et du tiroir.
- **Paramètres** : `Navigate<TPage, TArgs>(args)` ; le payload est conservé par l’entrée de pile et restauré
  au retour.

Détails : [Navigation et NavigationScaffold](navigation-scaffold.md#cycle-de-vie).

## 6. Arrière-plan et premier plan

Quand l’utilisateur appuie sur Accueil :

1. `App.OnBackground()` est appelée ; suspendez ce qui consomme (lecteur, requête, minuteur, caméra) ;
2. la page active reçoit les appels que vous lui relayez (le sample appelle `Suspend()` sur la page courante) ;
3. au retour, `App.OnForeground()` reprend.

Le framework ne suspend pas vos tâches à votre place. **Tout travail qui continue en arrière-plan est un choix
de votre part.** Reportez-vous à la reprise graphique Android ([Reprise graphique](android-graphics-lifecycle.md)) pour la
perte de contexte GPU (verrouillage d’écran) : les scènes sont conservées sans recomposition.

## 7. Où placer quoi

| Besoin | Crochet |
|---|---|
| Créer un client, un modèle partagé | `App.OnStart` |
| S’abonner à un événement de modèle | `OnCreate` (se désabonner dans `OnDestroy`) |
| Charger la liste au premier affichage | `OnActivated` (idempotent : « déjà chargé ? ») |
| Transmettre les paramètres de navigation | `OnNavigatedTo` |
| Annuler une requête quand on quitte l’écran | `OnDeactivated` |
| Suspendre un lecteur en arrière-plan | `App.OnBackground` → méthode `Suspend()` de la page |
| Libérer un lecteur, un client | `OnDestroy` / `App.OnDispose` |
| Réagir à la mémoire | `App.OnLowMemory` |
| Reconstruire quand une donnée change | callback du modèle → `if (IsActive) Rebuild()` |

## 8. Composants : pas de cycle de vie

Un composant n’a ni création ni destruction observables : c’est un appel de fonction. Si un composant a besoin
d’une ressource (minuteur, abonnement), **la page qui le possède** la crée et la libère. Les composants à état
interne (classes) sont des objets que la page détient dans un champ ([État](state.md#2-où-garder-létat)).

## 9. Frise d’exemple

La page `ApiPage` du sample, de bout en bout :

```text
App.OnStart            Page<ApiPage>()  ──▶  ApiPage.OnCreate()     crée ArticleFeed (dispatcher injecté)
Utilisateur ouvre l’écran
  navigation commit    ──▶  ApiPage.OnActivated()  ──▶  _feed.Activate()  : requête HTTP
  Body() (1re fois)    affiche « Loading… »
Réponse reçue          (thread de fond) ──▶ dispatcher.Post ──▶ callback : if (IsActive) { Rebuild(); RebuildTopBar(); }
Utilisateur quitte
  navigation commit    ──▶  ApiPage.OnDeactivated() ──▶ _feed.Deactivate() : annule
Application en arrière-plan   App.OnBackground() ──▶ api.Suspend()
Retour                        App.OnForeground() ──▶ api.Resume()
Fin de l’application          ApiPage.OnDestroy() ──▶ _feed.Dispose()
```
