# Xeno.SDK — créer et distribuer une application

[Documentation](index.md) · [Guide développeur](developer-guide.md) · [Architecture du tooling](architecture/tooling-and-platforms.md)

`Xeno.SDK` est un SDK MSBuild distribué sous forme de package NuGet. Il importe `Microsoft.NET.Sdk`, référence les packages runtime correspondant à la plateforme, fournit les points d'entrée natifs et embarque les assets. Aucun import relatif au dépôt n'est nécessaire dans un projet consommateur.

## Statut et prérequis

Preview locale : `0.1.0-preview.5`. Aucun package n'est encore publié sur nuget.org. Il faut .NET 10 et le workload de la plateforme ciblée. Le SDK ne télécharge pas silencieusement de workload, Android SDK ou Xcode. Un build/signature/exécution d'application iOS exige toujours macOS et Xcode ; produire une bibliothèque iOS ou évaluer son projet sur Windows n'est pas une validation de l'application iOS.

## Produire le feed local

Depuis la racine du dépôt, avec PowerShell :

```powershell
./tools/Pack-XenoSdk.ps1
# Poste Android uniquement, sans workload iOS :
./tools/Pack-XenoSdk.ps1 -SkipIos
```

Le script produit dans `artifacts/packages` : `Xeno.SDK`, `XenoUI.Core`, `XenoUI.StandardLibrary`, `XenoUI.Platforms` et `XenoUI.Templates`. `XenoUI.Platforms` regroupe les hôtes natifs dans un seul package multi-cible (`lib/net10.0-android` et `lib/net10.0-ios`) ; NuGet choisit celui du TFM de l'application. Avec `-SkipIos`, il ne contient que la cible Android. Les runtimes, le SDK et les templates ont la même version. La tâche MSBuild est embarquée dans le package SDK, pas dans les dépendances de l'application. La licence existante du dépôt est conservée dans les packages.

Pour une autre version : `-Version 0.1.0-preview.6`. Ne réutilisez pas une version déjà distribuée : les caches NuGet considèrent les packages comme immuables. Changez la version consommée dans le projet ou dans `global.json`. Un cache NuGet global ne doit pas être supprimé pour mettre le SDK à jour.

## Consommer hors dépôt

Le plus simple est le template `dotnet new xenoui --feed <feed>`, qui génère `global.json` et `NuGet.config` : voir le [guide d’installation](installation.md#5-créer-une-application-et-utiliser-le-sdk). À la main :

Ajoutez le chemin absolu de votre feed local dans le `NuGet.Config` du nouveau projet, tout en conservant les sources nécessaires à SkiaSharp et aux autres dépendances :

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="xeno-local" value="D:/chemin/XenoUI/artifacts/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

Créez `MyApp.csproj` :

```xml
<Project Sdk="Xeno.SDK/0.1.0-preview.5">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
    <XenoAppId>com.example.myapp</XenoAppId>
    <XenoAppName>My App</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>
</Project>
```

Pour Android et iOS : `TargetFrameworks=net10.0-android;net10.0-ios`, avec les workloads/packages correspondants disponibles. La version SDK peut aussi être centralisée dans `global.json` :

```json
{ "msbuild-sdks": { "Xeno.SDK": "0.1.0-preview.5" } }
```

Dans ce cas utilisez `<Project Sdk="Xeno.SDK">`, sans répéter la version. Il ne faut pas ajouter `Xeno.SDK` comme une simple `PackageReference` : le SDK intervient dès l'évaluation du projet.

Ajoutez votre `XenoApp`, vos `XenoPage` et `Assets/`, puis `dotnet restore` et `dotnet build -f net10.0-android`. L'identifiant, les versions et les options de signature demeurent la responsabilité de l'application.

## Configuration et extensions

| Propriété | Défaut | Usage |
|---|---|---|
| `XenoAppId` | `com.xenoui.unnamed` | Identité, transmise à `ApplicationId` si non explicitement définie |
| `XenoAppName` | nom du projet | Nom affiché Android et plist iOS généré |
| `XenoMinAndroidVersion` | `24` | Minimum Android |
| `XenoMinIosVersion` | `13.0` | Minimum iOS |
| `XenoGenerateEntryPoint` | `true` | Mettre `false` pour fournir sa propre Activity ou son entrée iOS |
| `XenoEnableDefaultAssets` | `true` | Collecte automatique de `Assets/**/*` |
| `XenoEnableImplicitUsings` | `true` | Imports XenoUI et factories ; désactivables sans toucher aux références |
| `XenoEnableMedia` | `false` | Module média optionnel ; audio foreground et sortie vidéo native de référence Media3 sur Android. Voir les [capacités et limites](media-controller.md). |
| `XenoUseProjectReferences` | `false` | Mode contributeur explicite ; requiert `XenoSourceRoot` |

Les propriétés natives explicitement définies, notamment `ApplicationId`, `ApplicationTitle` et `SupportedOSPlatformVersion`, restent prioritaires. Dans un projet multi-cible, conditionnez les overrides par TFM. Vos autres `PackageReference`, composants C# et extensions NativeTunnel restent libres : le SDK n'impose pas une liste fermée de contrôles.

Les fichiers `Platforms/ios/**/*.cs` sont exclus d'Android et inversement. Avec `XenoGenerateEntryPoint=false`, vos fichiers de la plateforme active restent compilés. Les fichiers liés du SDK sont visibles à l'évaluation design-time sous `XenoGenerated`.

## Assets et permissions

Le chemin `Assets/Images/logo.png` devient la ressource `XenoAsset/Images/logo.png`. Désactivez la collecte automatique pour gérer vous-même les items `XenoAsset`, sans empêcher d'autres ressources natives.

```xml
<ItemGroup>
  <XenoPermission Include="CAMERA"
                  IosKey="NSCameraUsageDescription"
                  Reason="Photographier un document choisi par l'utilisateur" />
  <XenoPermission Include="android.permission.RECORD_AUDIO" />
</ItemGroup>
```

Android accepte un nom court (`CAMERA`) ou le nom qualifié et ajoute `uses-permission` à la racine du manifest, sans doublon. iOS utilise exclusivement la clé `IosKey` explicite et exige une `Reason` non vide ; il n'invente pas de justification utilisateur. Déclarer une permission n'effectue pas sa demande runtime : celle-ci relève du code natif/NativeTunnel.

`AndroidManifest.xml` et `Info.plist` fournis par l'application sont prioritaires, ou peuvent être sélectionnés via `AndroidManifest` et `AppBundleManifest`. Quand une fusion est nécessaire, elle produit un dérivé dans `obj` et ne modifie jamais le fichier source. Sans plist personnalisé, le SDK génère son défaut dans `obj`. Android reçoit des chemins relatifs au projet, conformément à son workload. Les tâches de génération ne s'exécutent pas en `DesignTimeBuild`.

Lorsque vous utilisez `XenoPermission`, déclarez les chemins de manifests personnalisés dans le `.csproj`, pas avec `-p:AndroidManifest=...` ou `-p:AppBundleManifest=...` : ces propriétés globales MSBuild verrouilleraient le chemin source et empêcheraient sa substitution par le dérivé fusionné. Le vérificateur couvre les propriétés du projet.

## Développer le framework avec le SampleApp

Le SampleApp utilise `<Project Sdk="Xeno.SDK">`. Le `Directory.Build.props` du dépôt active uniquement pour ce projet `XenoUseProjectReferences=true` et définit `XenoSourceRoot`. Ce mode permet de tester immédiatement les changements du runtime sans republier ses DLL. Il n'est pas emballé dans le SDK et ne s'applique jamais à un consommateur hors dépôt.

Pour vérifier le SampleApp comme consommateur NuGet : ajouter `-p:XenoUseProjectReferences=false` aux commandes restore/build et utiliser les packages alignés du feed. Après modification des fichiers SDK ou des entrées natives emballées, produire une nouvelle version SDK et actualiser `global.json` ; le mode source ne contourne pas le cache du SDK.

Les anciens `Sdk.props`/`Sdk.targets` à la racine sont provisoirement conservés pour les anciens consommateurs. Ils ne constituent plus le parcours officiel et ne sont pas inclus dans `Xeno.SDK`.

## Vérification reproductible

```powershell
./tools/Pack-XenoSdk.ps1
./tools/Test-XenoSdk.ps1
# Évaluation seule, sans compilation APK :
./tools/Test-XenoSdk.ps1 -SkipAndroidBuild
```

Le vérificateur crée une application dans le dossier temporaire système **hors checkout**, avec un cache NuGet isolé hors de ses sources. Il vérifie l'absence de `ProjectReference`, les versions, les propriétés design-time, le choix Android/iOS, la désactivation de l'entrée native, les assets et les manifests. Il compile un APK Android Release sans AOT/trimming pour isoler les risques de packaging. Les paramètres `-SkipIos`/`-SkipAndroidBuild` réduisent explicitement le périmètre.

Le consommateur est conservé pour inspection, et le résultat est écrit dans `artifacts/xeno-sdk-verification.json`. Ces artefacts ne sont pas versionnés. La présence d'un SDK MSBuild ne garantit pas à elle seule le sélecteur d'appareils ou le debugger d'un IDE : cette partie reste dans [le tooling](architecture/tooling-and-platforms.md).
