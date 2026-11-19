# Tester une application XenoUI

[Documentation](index.md) · [Architecture d’une application](app-architecture.md) · [Créer un composant](components.md) · [Visual Studio](visual-studio.md) · [Dépannage](troubleshooting.md)

Ce guide présente ce que XenoUI permet de tester **aujourd’hui**, à quel niveau, et comment. Il n’existe pas
(encore) d’équivalent à `createComposeRule()` : pas de harnais d’interaction sur appareil ni d’arbre
sémantique interrogeable. Ce qui existe est solide et suffisant pour une bonne pyramide de tests : des tests
unitaires sur les couches portables, du **rendu headless en PNG** pour les composants et les pages, et un
dispositif manuel sur appareil.

> Parcours à valider à la main : [Guide développeur §16](developer-guide.md#16-valider-son-application). Tests attendus du moteur : [Runtime §12](architecture/runtime.md#12-tests-attendus).

## Sommaire

1. [La pyramide de tests](#1-la-pyramide-de-tests)
2. [Tester les services et les modèles](#2-tester-les-services-et-les-modèles)
3. [Rendre un composant en image](#3-rendre-un-composant-en-image)
4. [Tests « golden » (comparaison d’images)](#4-tests--golden--comparaison-dimages)
5. [Vérifier des pixels et des tailles](#5-vérifier-des-pixels-et-des-tailles)
6. [Tester les pages](#6-tester-les-pages)
7. [Prévisualiser comme tests visuels](#7-prévisualiser-comme-tests-visuels)
8. [Tests sur appareil](#8-tests-sur-appareil)
9. [Comment le framework se teste lui-même](#9-comment-le-framework-se-teste-lui-même)
10. [Intégration continue](#10-intégration-continue)
11. [Limites actuelles](#11-limites-actuelles)

---

## 1. La pyramide de tests

| Niveau | Quoi | Outil | Rapidité |
|---|---|---|---|
| 1 — Unité | Services, modèles de présentation, fonctions pures | Votre framework de test (xUnit, NUnit, MSTest) | Très rapide |
| 2 — Rendu headless | Un composant ou une page rendu en PNG ; pixels, tailles | `PreviewRenderer` | Rapide, sans appareil |
| 3 — Appareil / émulateur | Navigation, clavier, médias, arrière-plan | Manuel, outil `xeno` | Lent |

Le choix d’architecture ([Architecture d’une application](app-architecture.md)) qui paie le plus : des services et
modèles **sans type XenoUI**, donc testables au niveau 1 par un projet `net11.0` ordinaire.

## 2. Tester les services et les modèles

Services et modèles reçoivent leurs dépendances (HTTP, dispatcher) : on les remplace par des doublures.

```csharp
public sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        Task.FromResult(respond(request));
}

[Fact]
public async Task Articles_are_parsed_and_validated()
{
    var json = """[{"userId":1,"id":1,"title":"t","body":"b"}]""";
    var client = new HttpClient(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(json) }));
    var service = new ArticleService(client);

    var articles = await service.GetArticlesAsync(CancellationToken.None);

    Assert.Single(articles);
}

[Fact]
public async Task Stale_results_never_replace_the_latest()
{
    // Le dispatcher de test exécute tout de suite : `post = a => a()`.
    var feed = new RemoteFeed<int>(ct => Task.FromResult(new[] { 1 }), post: a => a(), changed: () => { });
    feed.Activate();
    feed.Refresh();
    /* attendre la publication, puis asserter sur feed.Snapshot */
}
```

À couvrir pour un flux distant (le dépôt le fait dans `tests/SampleApp.Api.RegressionTests`) : réponse vide,
erreur HTTP, JSON invalide, délai dépassé, annulation, résultats hors ordre, `Refresh` rapide, `Dispose`,
identifiants dupliqués.

## 3. Rendre un composant en image

`PreviewRenderer` — celui qui alimente les aperçus Visual Studio et VS Code — est **public** et utilisable dans un
test : il compose une interface dans un moteur privé, sans fenêtre ni appareil, et retourne un PNG.

```csharp
using XenoUI.Core.Preview;

PreviewImage image = PreviewRenderer.Render(
    () => Chip.View("Swift"),                            // Func<XenoComponent>
    new PreviewAttribute { Device = "android" },         // optionnel : appareil, largeur, hauteur, RTL
    assets: typeof(App).Assembly,                        // optionnel : assembly contenant Assets/
    scale: 2f);                                          // facteur de pixels

Assert.True(image.Succeeded, image.Error?.Message);
File.WriteAllBytes("chip.png", image.Png!);
```

`PreviewImage` fournit : `Png` (octets), `PixelWidth`/`PixelHeight`, `Width`/`Height` (dp), `Elapsed`, `Error`
et `Hint` (conseil quand une page échoue typiquement), et `Succeeded`.

| `PreviewAttribute` | Effet |
|---|---|
| `Device` | `android` (360×800), `pixel` (412×915), `iphone` (393×852), `iphone-se` (375×667), `foldable` (673×841), `tablet` (800×1280) |
| `Width`, `Height` | Forcer une taille en dp (0 = valeur de l’appareil ; pour un composant, la hauteur épouse le contenu) |
| `RightToLeft` | Mise en page droite-à-gauche |

Un composant est rendu **à la largeur de l’appareil, à la hauteur de son contenu**. Les polices et images
sont chargées depuis l’assembly passé en `assets`.

## 4. Tests « golden » (comparaison d’images)

Un test golden compare le rendu courant à une image de référence versionnée.

```csharp
static void AssertGolden(string name, Func<XenoComponent> content, PreviewAttribute? spec = null)
{
    var image = PreviewRenderer.Render(content, spec, scale: 1f);
    Assert.True(image.Succeeded, image.Error?.Message);

    string path = Path.Combine("Goldens", name + ".png");
    if (!File.Exists(path) || Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "1")
    {
        Directory.CreateDirectory("Goldens");
        File.WriteAllBytes(path, image.Png!);          // première exécution ou mise à jour volontaire
        return;
    }
    using var expected = SKBitmap.Decode(path);
    using var actual = SKBitmap.Decode(image.Png);
    Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
    int differing = 0;
    for (int y = 0; y < actual.Height; y++)
        for (int x = 0; x < actual.Width; x++)
            if (expected.GetPixel(x, y) != actual.GetPixel(x, y)) differing++;
    Assert.True(differing <= actual.Width * actual.Height / 1000, $"{differing} pixels differ in {name}");
}

[Fact] public void Chip_default()   => AssertGolden("chip", () => Chip.View("Swift"));
[Fact] public void Chip_rtl()       => AssertGolden("chip-rtl", () => Chip.View("Swift"), new() { RightToLeft = true });
[Fact] public void Chip_tablet()    => AssertGolden("chip-tablet", () => Chip.View("Swift"), new() { Device = "tablet" });
```

Conseils :

- **Un golden par variante** : défaut, long texte, RTL, grand écran, état erreur.
- Fixez l’échelle (`scale: 1f`) et les polices : la comparaison de pixels dépend du rendu de texte ; tolérez
  une petite marge (0,1 % ici) et **générez les goldens sur la même plateforme que la CI**.
- Versionnez les goldens et affichez le diff dans la CI quand un test échoue.
- Ne comparez pas des composants qui contiennent des animations en cours, des dates ou des données aléatoires.

## 5. Vérifier des pixels et des tailles

Sans image de référence, on peut asserter sur le rendu :

```csharp
var image = PreviewRenderer.Render(() => Badge.View("3", 0xFFDC2626), scale: 1f);
using var bitmap = SKBitmap.Decode(image.Png);

Assert.Equal(new SKColor(0xFFDC2626), bitmap.GetPixel(12, 8));         // couleur au centre du badge
Assert.InRange(image.Height, 20, 40);                                    // la hauteur épouse le contenu
Assert.Equal(360f, image.Width);                                         // largeur de l’appareil android (dp)
```

Utile pour les contrats de composants : « la pastille est rouge », « le texte long est tronqué à deux
lignes (hauteur bornée) », « le bouton fait au moins 44 dp ».

## 6. Tester les pages

Une page annotée `[Preview]` avec un constructeur sans paramètre est rendue **attachée à son application,
construite mais non démarrée** (`IsPreview`) : `OnStart` ne s’exécute pas, les services natifs sont des
doublures de design.

```csharp
var entry = PreviewRenderer.Discover(typeof(SettingsPage)).Single();
var image = PreviewRenderer.Render(entry, scale: 1);
Assert.True(image.Succeeded, image.Error?.Message);
```

`PreviewRenderer.Discover(typeof(T))` énumère les `[Preview]` d’un type (page ou classe de composants) ; un
`Render` en échec devient un **résultat** (`Error`, `Hint`), pas une exception : parfait pour un test qui vérifie
que **toutes les previews du projet se rendent**.

```csharp
[Fact]
public void Every_preview_renders()
{
    foreach (var type in typeof(App).Assembly.GetTypes())
        foreach (var entry in PreviewRenderer.Discover(type))
        {
            var image = PreviewRenderer.Render(entry, typeof(App).Assembly, scale: 1);
            Assert.True(image.Succeeded, $"{entry.DisplayName}: {image.Error?.Message} {image.Hint}");
        }
}
```

Une page qui exige `OnStart`, le réseau ou des services natifs absents échouera avec un `Hint` : concevez les
pages pour qu’elles se rendent avec des données d’exemple (`OnPreview()` de l’`App` prépare les modèles que lisent
les pages).

## 7. Prévisualiser comme tests visuels

Les `[Preview]` servent à la fois d’outil de conception et de **catalogue de tests visuels** : une preview par
variante (vide, plein, erreur, RTL, tablette). Le test « toutes les previews se rendent » ci-dessus transforme
ce catalogue en filet de sécurité de compilation et de rendu. Voir [Créer un composant §12](components.md#12-prévisualisation--preview).

## 8. Tests sur appareil

Ce qui ne se teste pas en headless : clavier et IME réels, geste de retour système, lecteurs audio/vidéo,
arrière-plan et reprise, perte de contexte GPU, performances. Marche à suivre :

1. `xeno devices` puis `xeno run --project … --device <id>` ([Installation](installation.md)) ;
2. suivre une **liste de scénarios** écrite : ouvrir chaque lab, changer d’onglet et revenir (état conservé),
   appuyer sur Accueil et revenir (lecteurs suspendus/repris), faire pivoter, couper le réseau (états d’erreur) ;
3. noter l’appareil, la version Android et le mode (Debug/Release) ;
4. pour les performances, mesurer en **Release, sans débogueur** ([Performance](performance.md)).

Un test d’interface automatisé sur appareil (UI Automator, Appium) peut piloter l’application comme n’importe
quelle application native ; XenoUI dessine ses propres pixels : **les éléments ne sont pas des vues natives**
(seuls les champs de texte portent un libellé d’accessibilité), il faut donc cibler par coordonnées ou par captures.

## 9. Comment le framework se teste lui-même

Utile comme modèle pour vos propres tests :

- `tests/XenoUI.Core.RegressionTests` : suite exécutable (`dotnet run -c Release`, pas `dotnet test`) avec un
  tableau `(nom, action)` et une fonction `Check(condition, message)`. Elle compose dans un `XenoEngine`,
  appelle `Refresh(engine, largeur, hauteur)`, puis lit les positions des entités
  (`engine.XenoUICacheMemory.Transforms`). Elle couvre layout (RTL, flex), navigation, overlays, texte,
  récupération GPU, **zéro allocation sur frames stables**, et les aperçus.
- `tests/SampleApp.Api.RegressionTests` : services et flux distants, sans projet plateforme.
- `tests/SampleApp.Overlays.RegressionTests` : couche overlay.
- `tests/XenoUI.Media.RegressionTests`, `Xeno.SDK.RegressionTests`, `XenoUI.Tooling.RegressionTests` : médias,
  SDK MSBuild, outil `xeno`.

Ces suites accèdent à des types internes (`InternalsVisibleTo`) ; **une application cliente doit s’en tenir à
l’API publique** (`PreviewRenderer`, vos propres services).

## 10. Intégration continue

Un job de CI type pour une application :

```yaml
- run: dotnet test MyApp.Tests/MyApp.Tests.csproj -c Release     # niveaux 1 et 2 (net11.0, sans workload mobile)
- run: dotnet build MyApp/MyApp.csproj -f net11.0-android -c Release   # compile l’application (workload Android requis)
```

Les tests de niveaux 1 et 2 ne demandent **ni appareil ni workload mobile** s’ils ciblent `net11.0` et référencent
les bibliothèques `XenoUI.Core` / votre bibliothèque de composants. Générez les goldens sur l’OS de la CI et
publiez les images en échec comme artefacts.

## 11. Limites actuelles

- Pas de harnais d’interaction (clic simulé, saisie, assertion sur un arbre sémantique) fourni pour une
  application cliente : les tests d’interaction passent par l’appareil.
- Les éléments dessinés par XenoUI ne sont pas des vues natives interrogeables (hors libellé des champs de texte) ; pas de `testTag` équivalent.
- Les comparaisons d’images dépendent de la plateforme de rendu des polices.
- Le framework est pré-V1 : traitez `PreviewRenderer` comme un outil d’outillage plutôt que comme une API
  figée.
