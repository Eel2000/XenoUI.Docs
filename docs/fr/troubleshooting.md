# Dépannage XenoUI par symptôme

[Index docs](index.md) · [Guide développeur](developer-guide.md) · [Architecture](architecture/index.md)

Les commandes partent du dossier de votre application (`MyApp/`). Commencez toujours par l’étape la moins destructive ; ne supprimez `bin/obj` qu’après avoir capturé l’erreur utile.

## Aucun appareil n’apparaît

### Le CLI ne voit rien

```powershell
adb devices -l
xeno devices --json
```

- Si `adb` est introuvable, configurez `ANDROID_SDK_ROOT`/`ANDROID_HOME` ou ajoutez `platform-tools` au `PATH`.
- Si `adb` ne liste rien, changez de câble/port, activez Débogage USB et choisissez le mode de connexion données.
- Si le téléphone est `unauthorized`, déverrouillez-le, révoquez si nécessaire les autorisations USB, reconnectez puis acceptez l’empreinte RSA.
- Si l’émulateur n’apparaît pas, démarrez-le complètement avant de rafraîchir.
- Si plusieurs installations de `adb` existent, vérifiez qu’IDE et CLI utilisent le même Android SDK.

### Le CLI voit la cible, pas Visual Studio

- sélectionnez votre projet XenoUI, le TFM `net10.0-android` et une configuration déployable ;
- rechargez la solution après installation du workload ;
- vérifiez que le projet évalué expose `AndroidApplication=true` ;
- confirmez que le projet est coché *Déployer* dans le Gestionnaire de configurations de la solution ;
- fermez les anciens serveurs `adb` seulement en dernier recours, puis laissez l’IDE le redémarrer.

### Le CLI voit la cible, pas VS Code

L’[extension VS Code](vscode.md) n’est pas encore publiée : lancez depuis le terminal intégré avec `xeno run --project MyApp.csproj --device <id>`. Une fois l’extension installée, vérifiez que `xenoui.cliPath` désigne le même outil `xeno` que le terminal.

## `xeno run` dit que l’appareil est offline ou unauthorized

Le CLI refuse volontairement une cible non `online`.

```powershell
adb reconnect
adb devices -l
```

Pour `unauthorized`, l’action nécessaire se fait sur le téléphone. Pour `offline`, attendez la fin du boot, reconnectez ou redémarrez l’émulateur. N’utilisez pas `--no-build` pour contourner cet état : il ne concerne que la compilation.

## Aucun APK signé n’est trouvé

Le CLI recherche l’APK sous `bin/<Configuration>/<TFM>/<RID>/` et préfère `<ApplicationId>-Signed.apk`.

```powershell
dotnet build MyApp.csproj `
  -f net10.0-android -c Debug `
  -p:RuntimeIdentifier=android-arm64
```

Vérifiez ensuite :

- que le RID correspond à l’ABI de l’appareil ;
- que `ApplicationId` est résolu ;
- que le projet est une application Android (`OutputType=Exe`, `AndroidApplication=true`) ;
- que `--no-build` n’est pas utilisé après un changement de RID/configuration.

## Le build ne trouve pas `XenoUI.BuildTasks.dll`

Avec `Xeno.SDK`, cette DLL est embarquée dans le package sous `tools/netstandard2.0`. Vérifiez la version résolue dans `global.json`, puis restaurez et reconstruisez :

```powershell
dotnet restore MyApp.csproj
dotnet build MyApp.csproj -f net10.0-android
```

Un chemin vers `bin/Debug` indique les anciens imports locaux d’avant `Xeno.SDK` : migrez le projet vers `<Project Sdk="Xeno.SDK">` ([Xeno.SDK](xeno-sdk.md)). Ne supprimez pas tout le cache NuGet.

## Le projet n’est pas reconnu comme application mobile

Contrôlez le SDK, sa version et le TFM :

```xml
<Project Sdk="Xeno.SDK/{{ xeno_version }}">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

Puis inspectez l’évaluation :

```powershell
dotnet msbuild path/to/App.csproj `
  -p:TargetFramework=net10.0-android `
  -getProperty:AndroidApplication `
  -getProperty:ApplicationId
```

## L’application affiche un écran vide

1. Capturez `adb logcat` et cherchez l’exception de démarrage.
2. Vérifiez que `OnStart()` appelle `Page<T>()` puis `Navigate<T>()`.
3. Vérifiez que `Body()` retourne un composant visible avec une taille/contrainte exploitable.
4. Ajoutez temporairement un fond opaque au composant racine.
5. Vérifiez les noms d’assets et les erreurs de décodage.
6. Reproduisez avec une page minimale `Column(Text("OK"))` pour isoler composition et contenu.

## Crash intermittent pendant Retour/navigation

- chaque page cible doit avoir été enregistrée une fois dans `OnStart()` ;
- ne gardez pas d’ID d’entité ou de `XenoComponent` dans un champ : ils appartiennent à un arbre transitoire ;
- conservez uniquement les modèles persistants (`State`, `ScrollState`, `TextEditController`), déclarés en champs avec `UseState`, `UseScroll`, `UseTextEdit` ;
- ne déclenchez pas manuellement plusieurs reconstructions imbriquées depuis `Body()` ;
- vérifiez les callbacks asynchrones : une callback d’un ancien arbre doit être annulée ou version-guarded ;
- répétez le parcours sur appareil avec les logs `adb logcat`.

Le moteur publie l’ancien ou le nouvel arbre complet sous le même verrou. Un crash qui observe un mélange pointe généralement vers une référence externe à une entité périmée ou une callback native tardive.

## Le scroll continue indéfiniment ou dépasse le contenu

- utilisez un seul `ScrollState` persistant par viewport ;
- ne recréez pas le `ScrollState` dans `Body()` : `UseScroll()` va dans l’initialiseur d’un champ, un appel dans `Body()` crée un nouvel état à chaque reconstruction ;
- imposez au viewport une hauteur/largeur finie (`Height` ou `FlexGrow` dans un parent contraint) ;
- vérifiez que le contenu mesuré n’est pas artificiellement infini ;
- testez contenu plus court que le viewport : l’offset attendu est `0` et l’animation doit s’arrêter ;
- après réduction du contenu, vérifiez que le rebuild entraîne un nouveau clamp.

Si le problème persiste, loggez offset, étendue du contenu, viewport, vitesse et `NeedsAnimationFrame` au moment où la borne est atteinte.

## Une `LazyColumn` consomme trop de mémoire ou construit tout

Vérifiez que vous utilisez la surcharge typée `LazyColumn(items, item => ...)`. L’ancienne surcharge `LazyColumn(scrollState, count, estimatedHeight, builder)` est conservée pour compatibilité et matérialise encore tous les items.

Avec la surcharge typée, le nombre d’entités doit rester proportionnel au viewport et à son overscan, pas à la collection. Ajoutez `key: item => item.Id` pour une source réordonnée ou remplacée. Un builder appelé à nouveau lors du déplacement de fenêtre est normal ; il ne doit pas déclencher d’I/O ni d’effet externe. Chargez les données en amont et gardez le builder pur.

## Une image ou une police n’apparaît pas

- placez le fichier sous `Assets/` ;
- vérifiez sa présence comme `XenoAsset` dans l’évaluation MSBuild ;
- essayez le chemin relatif complet plutôt que le nom court ;
- évitez deux fichiers ayant le même nom sans extension ;
- respectez la casse, surtout sur macOS/Linux ;
- vérifiez que la police est `.ttf` ou `.otf` et que `FontFamily` correspond au nom indexé.

Après ajout d’un asset, reconstruisez : il est embarqué à la compilation.

## Le clavier ne s’ouvre pas

- touchez la zone du `TextField`/`TextArea`, pas un overlay qui intercepte le hit-test ;
- donnez une hauteur visible au champ ;
- vérifiez que l’hôte a enregistré `ITextInputCapability` avant le premier build ;
- sur Android, vérifiez `AdjustResize` et cherchez une exception du hidden `EditText` ;
- vérifiez que la session active n’a pas été remplacée par un autre champ ;
- après rebuild, confirmez que le même `TextEditController` persiste.

## Le texte, curseur ou sélection se décale

- ne modifiez pas le texte en dehors de `TextEditController` ;
- utilisez des offsets UTF-16, pas un compteur de runes, pour `TextSelection` ;
- ne placez pas un caret au milieu d’une paire de substitution ;
- vérifiez densité, transformation de scroll et géométrie du viewport ;
- reproduisez avec emoji, accents combinés et texte RTL ;
- assurez-vous que layout, paragraph shape et sélection proviennent du même snapshot.

## Undo/redo ou composition se comporte mal

- regroupez les modifications corrélées dans `BeginBatchEdit()` ;
- n’exécutez pas Undo pendant un batch ouvert ;
- laissez l’IME terminer/annuler sa composition avant une réécriture externe agressive ;
- inspectez `TextEditOrigin` et `Version` dans l’événement `Changed` ;
- utilisez `SetValue`/`ApplyNativeSnapshot` selon l’origine, pas une mutation de chaîne parallèle.

## Le bouton Retour ferme l’app au lieu de revenir

- vérifiez que la pile contient plus d’une page (`CanGoBack`) ;
- n’appelez pas `GoBack()` deux fois pour un seul geste ;
- sur Android 13+, confirmez l’enregistrement du callback seulement quand `CanGoBack` ;
- à la racine, la fermeture/délégation à l’OS est le comportement attendu.

## Le build iOS échoue avec `CS8805`

Avec `Xeno.SDK` `{{ xeno_version }}`, la valeur par défaut `OutputType=Exe` n’est pas appliquée : `Microsoft.NET.Sdk` fixe `Library` avant elle. Android construit quand même une application (`AndroidApplication=true`), mais sur iOS le `Main.cs` généré est compilé comme une bibliothèque et le build échoue avec `CS8805`.

Contournement : déclarez la valeur dans le projet.

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
</PropertyGroup>
```

Corrigé sur la branche principale le 14 octobre 2026, livré avec la prochaine préversion ; la ligne peut alors être retirée.

## Le build iOS crée/modifie `Info.plist`

Lorsque le projet n’a pas de `Info.plist`, la cible SDK copie actuellement le fichier par défaut dans la racine du projet car le SDK iOS n’accepte pas simplement le fichier lié. Cette écriture dans le projet consommateur est connue et doit être remplacée avant V1. Ajoutez un `Info.plist` explicite si vous avez besoin d’un contrôle stable.

## Nettoyage en dernier recours

Après avoir sauvegardé les logs et vérifié qu’aucun travail utile ne réside dans les dossiers générés :

```powershell
dotnet clean MyApp.csproj -f net10.0-android
```

Préférez `dotnet clean` à une suppression manuelle large. Si vous devez supprimer `bin/obj`, ciblez uniquement le projet concerné.

## Informations à joindre à un bug

- version de `Xeno.SDK` (`global.json`) ;
- OS hôte, `dotnet --info`, `dotnet workload list` ;
- modèle/API/ABI de l’appareil ou image d’émulateur ;
- commande exacte et sortie complète pertinente ;
- `xeno devices --json` ;
- logs plateforme autour du crash ;
- reproduction minimale et fréquence ;
- capture vidéo pour scrolling, sélection ou navigation intermittente.
