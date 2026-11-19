# Déboguer l’interface

[Documentation](index.md) · [FAQ](faq.md) · [Dépannage](troubleshooting.md) · [Tester](testing.md) · [Guide développeur](developer-guide.md)

Le [Dépannage](troubleshooting.md) est organisé par **symptôme** (appareil, build, écran vide…). Cette page est organisée par
**méthode** : comment découvrir ce que fait votre interface quand elle ne fait pas ce que vous attendez. XenoUI n’a pas encore
d’inspecteur visuel (ni inspecteur de layout, ni compteur de recompositions) : le débogage repose sur quelques techniques
simples qui fonctionnent bien parce que `Body()` est une fonction ordinaire.

## Sommaire

1. [Une méthode qui marche](#1-une-méthode-qui-marche)
2. [Voir ce qui a été composé : rendre en PNG](#2-voir-ce-qui-a-été-composé--rendre-en-png)
3. [L’écran est vide](#3-lécran-est-vide)
4. [Une valeur ne se met pas à jour](#4-une-valeur-ne-se-met-pas-à-jour)
5. [La mise en page est fausse](#5-la-mise-en-page-est-fausse)
6. [L’état est perdu ou réinitialisé](#6-létat-est-perdu-ou-réinitialisé)
7. [Plantages et exceptions](#7-plantages-et-exceptions)
8. [Journalisation](#8-journalisation)
9. [Problèmes de threads](#9-problèmes-de-threads)
10. [Lent ou saccadé](#10-lent-ou-saccadé)
11. [Attacher le débogueur](#11-attacher-le-débogueur)
12. [Que joindre à un rapport de bug](#12-que-joindre-à-un-rapport-de-bug)

---

## 1. Une méthode qui marche

1. **Reproduire** dans le plus petit écran : placez le composant fautif dans un `[Preview]` ou un test avec des données d’exemple.
2. **Isoler** : le problème vient-il des *données* (ce que vous passez), de la *structure* (ce que vous composez) ou de la
   *mise en page* (tailles) ?
3. **Regarder** : rendez-le ([§2](#2-voir-ce-qui-a-été-composé--rendre-en-png)) et journalisez les entrées ([§8](#8-journalisation)).
4. **Changer une seule chose** à la fois.

Les composants étant des fonctions de leurs entrées, un bug qui apparaît avec certaines entrées se reproduit **à chaque fois**
dans une preview. C’est votre principal avantage.

## 2. Voir ce qui a été composé : rendre en PNG

L’aperçu headless fonctionne depuis un petit projet de test ou console, sans appareil :

```csharp
var image = PreviewRenderer.Render(() => MonComposant.View(donnees), new PreviewAttribute { Device = "android" }, scale: 2f);
if (!image.Succeeded) Console.WriteLine($"{image.Error?.Message} {image.Hint}");
else File.WriteAllBytes("debug.png", image.Png!);
```

`Succeeded` est faux quand la composition lève une exception ; `Error` et `Hint` disent pourquoi. Ouvrez le PNG pour voir ce
qui est réellement dessiné. Essayez d’autres appareils (`Device = "iphone-se"`, `"tablet"`), `RightToLeft = true` et un texte
long. Voir [Tester](testing.md).

## 3. L’écran est vide

Vérifiez dans cet ordre (du plus probable au moins probable) :

| # | Vérification | Comment |
|---|---|---|
| 1 | Une **exception dans `Body()`** | Rendez la page en preview (§2) : `Error` la montre ; ou lisez logcat (§8) |
| 2 | La page n’est **pas enregistrée** | `Page<MaPage>()` dans `App.OnStart` ([Cycle de vie](lifecycle.md)) |
| 3 | Un **objet persistant créé dans `Body()`** | `UseState`/`UseScroll`/`UseTextEdit` doivent être des champs ([État](state.md)) |
| 4 | **Taille zéro** | Liste paresseuse sans taille bornée, `Box(0, 0)` sans parent qui étire ; donnez `FlexGrow(1)`, `Height` ou `Stretch` ([Mises en page](layouts.md)) |
| 5 | **Couleurs invisibles** | Alpha `00` (`0x00FFFFFF`) ou texte de la couleur du fond |
| 6 | **Contenu sous la barre** | `ContentOverlaysNavigationBar` ou marge de zone sûre |
| 7 | Un **rappel tardif** a écrit dans une page quittée | Protéger avec `IsActive` ([Effets de bord](side-effects.md)) |

## 4. Une valeur ne se met pas à jour

Demandez-vous : *est-elle liée ?*

- Une valeur qui change doit être un `State<T>` **passé à une liaison** : `Text(state)`, `Text($"{state}")`,
  `.Background(state)`, `.TextColor(state)`. Lire `state.Value` dans `Body()` ne s’abonne à rien.
- Si la **structure** a changé (un élément apparaît, une branche bascule), appelez `Rebuild()`.
- Une `List` modifiée sur place dans un `State<List<T>>` ne notifie pas : gardez la liste dans un champ et faites `Rebuild()`.
- Un `State` affecté **depuis un thread de fond** peut ne pas se repeindre : affectez sur le thread UI
  (`IUiDispatcherCapability.Post`).
- Un **composant gardé dans un champ** est mort après une reconstruction : gardez les données, recomposez.

Test rapide : journalisez le chemin du setter (`_count.OnChanged += () => Console.WriteLine("changed")`). Si cela s’affiche
mais que l’écran ne change pas, l’élément n’est pas lié ; si rien ne s’affiche, l’affectation n’a jamais eu lieu.

## 5. La mise en page est fausse

Utilisez des **fonds temporaires** pour rendre les boîtes visibles :

```csharp
Column(...).Background(0x33FF0000)     // voir les vraies bornes d’un conteneur
```

Vérifiez ensuite, dans l’ordre : les axes **principal** et **transversal** du parent (`MainAxis`, `CrossAxis`), si l’enfant a
une `Width`/`Height` explicite, `FlexGrow` ou `Stretch`, `Padding` ou `Margin`, et la règle propre au `ZStack` (la position
horizontale vient de la pile). Le tableau de [dépannage des mises en page](layouts.md) liste les causes usuelles. Prévisualisez
sur un appareil étroit (`iphone-se`) et avec un texte long pour révéler les débordements.

## 6. L’état est perdu ou réinitialisé

| Symptôme | Cause |
|---|---|
| Le compteur revient à sa valeur initiale après un changement sans rapport | `UseState` dans `Body()` |
| Le défilement saute en haut | `UseScroll()` créé dans `Body()` |
| Le texte saisi disparaît à la reconstruction | `UseTextEdit()` dans `Body()` |
| La liste perd sa position | `UseLazyList()` dans `Body()`, ou clés instables |
| L’état disparaît quand le processus est tué | Non restauré : persistez vous-même l’essentiel ([État](state.md)) |

## 7. Plantages et exceptions

- **La composition lève** (référence nulle, mauvais index) : la trace pointe dans votre `Body()` ou un composant. En preview
  cela devient `image.Error` ; sur appareil, c’est dans logcat.
- **Clé de liste dupliquée** → `InvalidOperationException` pendant la composition : rendez les clés uniques.
- **`XenoComponent.None` converti en `int`** → `InvalidOperationException` : testez `IsNone` avant.
- **Plantage pendant retour/navigation** : en général une page non enregistrée, un objet persistant créé dans `Body()` ou une
  entité périmée. Voir [Dépannage](troubleshooting.md).
- **Exceptions de tâches non observées** : interceptez à la frontière de chaque `Task.Run` ([Effets de bord](side-effects.md)).

## 8. Journalisation

Sur Android, `Console.WriteLine` et `System.Diagnostics.Debug.WriteLine` apparaissent dans logcat :

```powershell
adb shell pidof com.example.myapp
adb logcat --pid=<pid>
```

Journalisez les **entrées et les décisions**, pas chaque frame :

```csharp
public override XenoComponent Body()
{
    System.Diagnostics.Debug.WriteLine($"NotesPage.Body : {_notes.Count} notes");   // à chaque reconstruction
    …
}
```

Compter les appels de `Body()` sert aussi de **compteur de reconstructions** : s’il journalise plus que prévu, quelque chose
appelle `Rebuild()` trop souvent. Retirez les journaux temporaires avant de commiter ; ne journalisez jamais de secrets ni de
texte d’utilisateur.

## 9. Problèmes de threads

Symptômes : anomalies visuelles occasionnelles, `InvalidOperationException` côté UI, comportement qui change d’une exécution à
l’autre. Cause : un `State`, `TextEditController` ou `ScrollState` touché depuis un thread de fond. Correction : repasser par
`Native.Require<IUiDispatcherCapability>().Post(...)`. `Rebuild()` est le seul appel sûr depuis n’importe quel thread
([Effets de bord](side-effects.md)).

## 10. Lent ou saccadé

1. Mesurez en **Release**, sans débogueur, sur un **appareil réel** ([Performance](performance.md)).
2. Comptez les appels de `Body()` (§8) : trop de reconstructions est le coupable habituel ; utilisez des liaisons `State`.
3. Vérifiez les listes : `LazyColumn` avec clés stables, fonctions d’élément sans travail lourd.
4. Vérifiez le thread UI : ni réseau, ni disque, ni analyse dans les callbacks.
5. Regardez les grosses ombres, l’opacité de scène et les tailles animées ([Performance](performance.md)).

## 11. Attacher le débogueur

Lancez un build **Debug** sur l’appareil ou l’émulateur depuis Visual Studio, ou `xeno run … --configuration Debug`
([Guide développeur](developer-guide.md)), puis posez des points d’arrêt dans `Body()`, les callbacks et les modèles. Rappelez-vous
que `Body()` s’exécute à chaque reconstruction et que les previews l’exécutent aussi : un point d’arrêt dans `Body()` se déclenche souvent.

Pour inspecter le build plutôt que l’application, utilisez `dotnet build -v:diagnostic` et `dotnet msbuild -getProperty:…`.

## 12. Que joindre à un rapport de bug

- version des packages XenoUI, version du SDK .NET, niveau d’API Android ou version d’iOS, appareil et runtime (CoreCLR ou Mono) ;
- un **composant ou une page minimal** qui reproduit le problème, idéalement en `[Preview]` ;
- comportement attendu et obtenu, avec le **PNG** du §2 ;
- les lignes logcat pertinentes et la trace de pile ;
- si cela arrive en Debug et en Release, et sur un second appareil.

Voir aussi la liste du [Dépannage](troubleshooting.md).
